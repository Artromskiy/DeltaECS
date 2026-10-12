namespace Delta.ECS.Generators;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Creates strongly typed registration factories for marked components.</summary>
[Generator]
public sealed class ComponentRegistryGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor UnsupportedComponent = GeneratorDiagnostics.Error(
        "DECS0003",
        "Component type cannot be registered automatically",
        "Marked component '{0}' must be a non-generic struct accessible to generated code",
        "Component registration");
    private static readonly DiagnosticDescriptor SchemaCollision = GeneratorDiagnostics.Error(
        "DECS0004",
        "Component schema ID collision",
        "Components '{0}' and '{1}' have the same schema ID 0x{2:X16}; assign an explicit unique SchemaId",
        "Component registration");
    private static readonly DiagnosticDescriptor InvalidSchemaId = GeneratorDiagnostics.Error(
        "DECS0005",
        "Component schema ID cannot be zero",
        "Component '{0}' resolved to schema ID 0; assign a non-zero explicit SchemaId",
        "Component registration");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var components = context.SyntaxProvider.ForAttributeWithMetadataName(
                "Delta.ECS.ComponentSchemaAttribute",
                static (node, _) => node is StructDeclarationSyntax or RecordDeclarationSyntax,
                static (syntax, _) => new MarkedComponent(
                    (INamedTypeSymbol)syntax.TargetSymbol,
                    syntax.TargetNode.GetLocation()))
            .Collect();

        context.RegisterSourceOutput(components, static (output, markedComponents) =>
            Generate(output, markedComponents));
    }

    private static void Generate(SourceProductionContext context, ImmutableArray<MarkedComponent> markedComponents)
    {
        var registrations = new List<ComponentRegistration>();
        var schemas = new Dictionary<ulong, ComponentRegistration>();
        foreach (MarkedComponent marked in markedComponents)
        {
            INamedTypeSymbol type = marked.Type;
            if (type.TypeKind != TypeKind.Struct
                || type.IsRefLikeType
                || type.IsGenericType
                || GeneratorSupport.HasGenericTypeInChain(type, includeSelf: false)
                || !GeneratorSupport.IsAccessibleSymbol(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(UnsupportedComponent, marked.Location, type.ToDisplayString()));
                continue;
            }

            AttributeData? attribute = ComponentSchemaIdHash.ComponentAttribute(type);
            (bool hasExplicitSchemaId, ulong explicitSchemaId) = attribute is null
                ? default
                : ComponentSchemaIdHash.ExplicitSchemaId(attribute);
            if (hasExplicitSchemaId && explicitSchemaId == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidSchemaId, marked.Location, type.ToDisplayString()));
                continue;
            }

            ulong schemaId = hasExplicitSchemaId
                ? explicitSchemaId
                : ComponentSchemaIdHash.Compute(ComponentSchemaIdHash.GetMetadataFullName(type));
            if (schemaId == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidSchemaId, marked.Location, type.ToDisplayString()));
                continue;
            }

            var registration = new ComponentRegistration(
                type,
                schemaId,
                IsTagType(type),
                ReadComponentAliases(type),
                ReadComponentGroups(type));
            if (schemas.TryGetValue(schemaId, out ComponentRegistration previous))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    SchemaCollision,
                    marked.Location,
                    type.ToDisplayString(),
                    previous.Type.ToDisplayString(),
                    schemaId));
                continue;
            }

            schemas.Add(schemaId, registration);
            registrations.Add(registration);
        }

        registrations.Sort(static (left, right) => StringComparer.Ordinal.Compare(
            left.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            right.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        if (registrations.Count == 0)
        {
            return;
        }

        context.AddSource("GeneratedComponentCatalog.g.cs", Render(registrations));
    }

    private static bool IsTagType(INamedTypeSymbol type) => type.TypeKind == TypeKind.Struct
            && !type.GetMembers().OfType<IFieldSymbol>().Any(static field => !field.IsStatic && !field.IsConst)
            && !type.GetAttributes()
                .Where(static attribute => attribute.AttributeClass?.ToDisplayString()
                    == "System.Runtime.InteropServices.StructLayoutAttribute")
                .SelectMany(static attribute => attribute.NamedArguments)
                .Any(static argument => argument.Key == "Size"
                    && argument.Value.Value is int size
                    && size > 1);

    private static List<ComponentGroupRegistration> ReadComponentGroups(INamedTypeSymbol componentType)
    {
        var groups = new List<ComponentGroupRegistration>();
        var seen = new HashSet<(string EnumType, ulong Value)>();
        foreach (AttributeData attribute in componentType.GetAttributes())
        {
            INamedTypeSymbol? attributeType = attribute.AttributeClass;
            if (attributeType is null
                || attributeType.OriginalDefinition.MetadataName != "ComponentGroupAttribute`1"
                || attributeType.ContainingNamespace.ToDisplayString() != "Delta.ECS"
                || attributeType.TypeArguments[0] is not INamedTypeSymbol enumType
                || enumType.TypeKind != TypeKind.Enum)
            {
                continue;
            }

            bool isFlags = enumType.GetAttributes().Any(static enumAttribute =>
                enumAttribute.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");
            foreach (TypedConstant constructorArgument in attribute.ConstructorArguments)
            {
                if (constructorArgument.Kind == TypedConstantKind.Array)
                {
                    foreach (TypedConstant groupId in constructorArgument.Values)
                    {
                        AddGroup(enumType, groupId, isFlags, groups, seen);
                    }

                    continue;
                }

                AddGroup(enumType, constructorArgument, isFlags, groups, seen);
            }
        }

        return groups;
    }

    private static List<string> ReadComponentAliases(INamedTypeSymbol componentType)
    {
        var aliases = new List<string>();
        foreach (AttributeData attribute in componentType.GetAttributes())
        {
            INamedTypeSymbol? attributeType = attribute.AttributeClass;
            if (attributeType?.MetadataName != "ComponentAliasAttribute"
                || attributeType.ContainingNamespace.ToDisplayString() != "Delta.ECS"
                || attribute.ConstructorArguments.Length != 1
                || attribute.ConstructorArguments[0].Value is not string alias)
            {
                continue;
            }

            aliases.Add(alias);
        }

        return aliases;
    }

    private static void AddGroup(
        INamedTypeSymbol enumType,
        TypedConstant constant,
        bool isFlags,
        List<ComponentGroupRegistration> groups,
        HashSet<(string EnumType, ulong Value)> seen)
    {
        if (constant.Value is null || enumType.EnumUnderlyingType is null)
        {
            return;
        }

        ulong value = ToEnumBits(constant.Value, enumType.EnumUnderlyingType.SpecialType);
        if (!isFlags || value == 0)
        {
            AddGroup(enumType, value, groups, seen);
            return;
        }

        int bitIndex = 0;
        while (value != 0)
        {
            if ((value & 1) != 0)
            {
                AddGroup(enumType, 1UL << bitIndex, groups, seen);
            }

            value >>= 1;
            bitIndex++;
        }
    }

    private static void AddGroup(
        INamedTypeSymbol enumType,
        ulong value,
        List<ComponentGroupRegistration> groups,
        HashSet<(string EnumType, ulong Value)> seen)
    {
        string enumTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (seen.Add((enumTypeName, value)))
        {
            groups.Add(new ComponentGroupRegistration(enumType, value));
        }
    }

    private static ulong ToEnumBits(object value, SpecialType underlyingType) => underlyingType switch
    {
        SpecialType.System_SByte => (byte)(sbyte)value,
        SpecialType.System_Byte => (byte)value,
        SpecialType.System_Int16 => (ushort)(short)value,
        SpecialType.System_UInt16 => (ushort)value,
        SpecialType.System_Int32 => (uint)(int)value,
        SpecialType.System_UInt32 => (uint)value,
        SpecialType.System_Int64 => (ulong)(long)value,
        SpecialType.System_UInt64 => (ulong)value,
        _ => 0,
    };

    private static string Render(List<ComponentRegistration> registrations)
    {
        var factories = new List<string>();
        var registrationCalls = new List<string>();
        foreach (ComponentRegistration registration in registrations)
        {
            string typeName = registration.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string factoryName = "ComponentRegistration_" + GeneratorSupport.StableName(typeName);
            string schemaId = registration.SchemaId.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
            string aliasCalls = string.Join("\n", registration.Aliases.Select(alias =>
                $"layouts.AddAlias({Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(alias, quote: true)}, componentId);"));
            registrationCalls.Add($"global::Delta.ECS.GeneratedComponentRegistrationRegistry.Register(new global::Delta.ECS.Generated.{factoryName}());");
            factories.Add($$"""
                internal sealed class {{factoryName}} : global::Delta.ECS.IGeneratedComponentRegistration
                {
                    public {{factoryName}}() { }

                    public global::System.Type ComponentType => typeof({{typeName}});

                    public global::Delta.ECS.SchemaId SchemaId => new(0x{{schemaId}}UL);

                    public bool IsTag => {{registration.IsTag.ToString().ToLowerInvariant()}};

                    global::Delta.ECS.ComponentId global::Delta.ECS.IGeneratedComponentRegistration.Register(
                        global::Delta.ECS.ComponentRegistry layouts)
                    {
                        global::Delta.ECS.ComponentId componentId = global::Delta.ECS.ComponentRegistryRegistrationExtensions.Register<{{typeName}}>(
                            layouts,
                            SchemaId);
                {{GeneratorTemplates.Indent(string.Join("\n", registration.Groups.Select(group =>
                    $"layouts.RegisterGeneratedComponentGroup(typeof({group.EnumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}).TypeHandle, 0x{group.Value:X16}UL, componentId);")), "        ")}}
                {{GeneratorTemplates.Indent(aliasCalls, "        ")}}
                        return componentId;
                    }
                }
                """);
        }

        return $$"""
            // <auto-generated />
            namespace Delta.ECS.Generated
            {
            {{GeneratorTemplates.Indent(string.Join("\n\n", factories), "    ")}}
            }

            namespace Delta.ECS.Generated
            {
                internal static partial class GenericBindingModuleInitializer
                {
                    static partial void RegisterComponentCatalog()
                    {
            {{GeneratorTemplates.Indent(string.Join("\n", registrationCalls), "            ")}}
                    }
                }
            }
            """;
    }

    private sealed record MarkedComponent(INamedTypeSymbol Type, Location Location);

    private sealed record ComponentRegistration(
        INamedTypeSymbol Type,
        ulong SchemaId,
        bool IsTag,
        List<string> Aliases,
        List<ComponentGroupRegistration> Groups);

    private sealed record ComponentGroupRegistration(INamedTypeSymbol EnumType, ulong Value);
}
