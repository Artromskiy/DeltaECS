using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Delta.ECS.Generators
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ComponentSchemaIdAnalyzer : DiagnosticAnalyzer
    {
        public const string PinSchemaIdDiagnosticId = "DECS0001";
        public const string InvalidSchemaIdDiagnosticId = "DECS0002";

        private const string SchemaIdPropertyName = "SchemaId";

        private static readonly DiagnosticDescriptor PinSchemaIdRule = new DiagnosticDescriptor(
            PinSchemaIdDiagnosticId,
            "Pin the ECS component schema ID",
            "Pin the current name-derived schema ID 0x{0} as an explicit SchemaId",
            "DeltaECS.Schema",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "An explicit schema ID keeps this component's persisted identity stable if its name changes.");

        private static readonly DiagnosticDescriptor InvalidSchemaIdRule = new DiagnosticDescriptor(
            InvalidSchemaIdDiagnosticId,
            "ECS component schema ID cannot be zero",
            "SchemaId 0 is invalid; use a non-zero explicit ID or the name-derived ID 0x{0}",
            "DeltaECS.Schema",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Zero is reserved and cannot identify an ECS component schema.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(PinSchemaIdRule, InvalidSchemaIdRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.StructDeclaration, SyntaxKind.RecordDeclaration);
        }

        private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not TypeDeclarationSyntax declaration ||
                context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol type ||
                type.TypeKind != TypeKind.Struct ||
                HasOpenGenericContainingType(type))
            {
                return;
            }

            foreach (AttributeData attributeData in type.GetAttributes())
            {
                if (!IsComponentAttribute(attributeData.AttributeClass))
                {
                    continue;
                }

                AttributeSyntax? attributeSyntax = attributeData.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) as AttributeSyntax;
                if (attributeSyntax == null || attributeSyntax.Parent?.Parent != declaration)
                {
                    continue;
                }

                bool hasExplicitSchemaId = false;
                bool hasInvalidZeroSchemaId = false;
                foreach (var namedArgument in attributeData.NamedArguments)
                {
                    if (!string.Equals(namedArgument.Key, SchemaIdPropertyName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    hasExplicitSchemaId = true;
                    hasInvalidZeroSchemaId = namedArgument.Value.Value is ulong value && value == 0UL;
                    break;
                }

                if (hasExplicitSchemaId)
                {
                    if (hasInvalidZeroSchemaId)
                    {
                        ulong suggestedSchemaId = ComponentSchemaIdHash.Compute(ComponentSchemaIdHash.GetMetadataFullName(type));
                        ReportInvalidSchemaId(context, attributeSyntax, suggestedSchemaId);
                    }

                    continue;
                }

                ulong schemaId = ComponentSchemaIdHash.Compute(ComponentSchemaIdHash.GetMetadataFullName(type));
                if (schemaId == 0UL)
                {
                    ReportInvalidSchemaId(context, attributeSyntax, schemaId);
                    continue;
                }

                ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                    .Add("SchemaId", schemaId.ToString("X16", CultureInfo.InvariantCulture));
                context.ReportDiagnostic(Diagnostic.Create(
                    PinSchemaIdRule,
                    attributeSyntax.Name.GetLocation(),
                    properties,
                    schemaId.ToString("X16", CultureInfo.InvariantCulture)));
            }
        }

        private static void ReportInvalidSchemaId(SyntaxNodeAnalysisContext context, AttributeSyntax attributeSyntax, ulong schemaId)
        {
            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                .Add("SchemaId", schemaId.ToString("X16", CultureInfo.InvariantCulture));
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSchemaIdRule,
                attributeSyntax.GetLocation(),
                properties,
                schemaId.ToString("X16", CultureInfo.InvariantCulture)));
        }

        private static bool IsComponentAttribute(INamedTypeSymbol? attributeType)
        {
            if (attributeType == null || !string.Equals(attributeType.MetadataName, "DeltaEcsComponentAttribute", StringComparison.Ordinal))
            {
                return false;
            }

            return string.Equals(attributeType.ContainingNamespace?.ToDisplayString(), "Delta.ECS", StringComparison.Ordinal);
        }

        private static bool HasOpenGenericContainingType(INamedTypeSymbol type)
        {
            for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
            {
                if (current.Arity != 0)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
