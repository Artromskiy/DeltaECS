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
                "Delta.ECS.DeltaEcsComponentAttribute",
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

            var registration = new ComponentRegistration(type, schemaId, IsTagType(type));
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

    private static string Render(List<ComponentRegistration> registrations)
    {
        var factories = new List<string>();
        var registrationCalls = new List<string>();
        foreach (ComponentRegistration registration in registrations)
        {
            string typeName = registration.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string factoryName = "ComponentRegistration_" + GeneratorSupport.StableName(typeName);
            string schemaId = registration.SchemaId.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
            registrationCalls.Add($"global::Delta.ECS.GeneratedComponentRegistrationRegistry.Register(new global::Delta.ECS.Generated.{factoryName}());");
            factories.Add($$"""
                internal sealed class {{factoryName}} : global::Delta.ECS.IGeneratedComponentRegistration
                {
                    public {{factoryName}}() { }

                    public global::System.Type ComponentType => typeof({{typeName}});

                    public global::Delta.ECS.SchemaId SchemaId => new(0x{{schemaId}}UL);

                    public bool IsTag => {{registration.IsTag.ToString().ToLowerInvariant()}};

                    global::Delta.ECS.ComponentId global::Delta.ECS.IGeneratedComponentRegistration.Register(
                        global::Delta.ECS.ComponentLayoutRegistry layouts)
                        => global::Delta.ECS.ComponentLayoutRegistryRegistrationExtensions.Register<{{typeName}}>(
                            layouts,
                            SchemaId);
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

    private sealed record ComponentRegistration(INamedTypeSymbol Type, ulong SchemaId, bool IsTag);
}
