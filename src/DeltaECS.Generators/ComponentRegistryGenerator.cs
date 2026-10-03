namespace Delta.ECS.Generators;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Creates strongly typed registration factories for marked components.</summary>
[Generator]
public sealed class ComponentRegistryGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor UnsupportedComponent = new(
        "DECS0003",
        "Component type cannot be registered automatically",
        "Marked component '{0}' must be a non-generic struct accessible to generated code",
        "Component registration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SchemaCollision = new(
        "DECS0004",
        "Component schema ID collision",
        "Components '{0}' and '{1}' have the same schema ID 0x{2:X16}; assign an explicit unique SchemaId",
        "Component registration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSchemaId = new(
        "DECS0005",
        "Component schema ID cannot be zero",
        "Component '{0}' resolved to schema ID 0; assign a non-zero explicit SchemaId",
        "Component registration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

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
                || HasGenericContainingType(type)
                || !GeneratorSupport.IsAccessibleSymbol(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(UnsupportedComponent, marked.Location, type.ToDisplayString()));
                continue;
            }

            AttributeData? attribute = type.GetAttributes().FirstOrDefault(static item =>
                item.AttributeClass?.ToDisplayString() == "Delta.ECS.DeltaEcsComponentAttribute");
            if (TryGetExplicitSchemaId(attribute, out ulong explicitSchemaId) && explicitSchemaId == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidSchemaId, marked.Location, type.ToDisplayString()));
                continue;
            }

            ulong schemaId = TryGetExplicitSchemaId(attribute, out explicitSchemaId)
                ? explicitSchemaId
                : ComponentSchemaIdHash.Compute(ComponentSchemaIdHash.GetMetadataFullName(type));
            if (schemaId == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidSchemaId, marked.Location, type.ToDisplayString()));
                continue;
            }

            var registration = new ComponentRegistration(type, schemaId);
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

    private static bool TryGetExplicitSchemaId(AttributeData? attribute, out ulong schemaId)
    {
        schemaId = default;
        if (attribute is null)
        {
            return false;
        }

        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (argument.Key == "SchemaId" && argument.Value.Value is ulong value)
            {
                schemaId = value;
                return true;
            }
        }

        return false;
    }

    private static bool HasGenericContainingType(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type.ContainingType; current is not null; current = current.ContainingType)
        {
            if (current.Arity != 0)
            {
                return true;
            }
        }

        return false;
    }

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

                    public global::Delta.ECS.ComponentId Register(global::Delta.ECS.ComponentLayoutRegistry layouts)
                        => layouts.Register<{{typeName}}>(SchemaId);
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

    private sealed class MarkedComponent
    {
        internal MarkedComponent(INamedTypeSymbol type, Location location)
        {
            Type = type;
            Location = location;
        }

        internal INamedTypeSymbol Type { get; }

        internal Location Location { get; }
    }

    private sealed class ComponentRegistration
    {
        internal ComponentRegistration(INamedTypeSymbol type, ulong schemaId)
        {
            Type = type;
            SchemaId = schemaId;
        }

        internal INamedTypeSymbol Type { get; }

        internal ulong SchemaId { get; }

    }
}
