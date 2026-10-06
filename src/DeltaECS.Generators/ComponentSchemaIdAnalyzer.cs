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

        private static readonly DiagnosticDescriptor PinSchemaIdRule = GeneratorDiagnostics.Info(
            PinSchemaIdDiagnosticId,
            "Pin the ECS component schema ID",
            "Pin the current name-derived schema ID 0x{0} as an explicit SchemaId",
            "DeltaECS.Schema",
            "An explicit schema ID keeps this component's persisted identity stable if its name changes.");

        private static readonly DiagnosticDescriptor InvalidSchemaIdRule = GeneratorDiagnostics.Error(
            InvalidSchemaIdDiagnosticId,
            "ECS component schema ID cannot be zero",
            "SchemaId 0 is invalid; use a non-zero explicit ID or the name-derived ID 0x{0}",
            "DeltaECS.Schema",
            "Zero is reserved and cannot identify an ECS component schema.");

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
                GeneratorSupport.HasGenericTypeInChain(type, includeSelf: true))
            {
                return;
            }

            AttributeData? attributeData = ComponentSchemaIdHash.ComponentAttribute(type);
            if (attributeData is null)
            {
                return;
            }

            AttributeSyntax? attributeSyntax = attributeData.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) as AttributeSyntax;
            if (attributeSyntax == null || attributeSyntax.Parent?.Parent != declaration)
            {
                return;
            }

            (bool hasExplicitSchemaId, ulong explicitSchemaId) = ComponentSchemaIdHash.ExplicitSchemaId(attributeData);
            if (hasExplicitSchemaId)
            {
                if (explicitSchemaId == 0UL)
                {
                    ulong suggestedSchemaId = ComponentSchemaIdHash.Compute(ComponentSchemaIdHash.GetMetadataFullName(type));
                    ReportSchemaId(context, InvalidSchemaIdRule, attributeSyntax, suggestedSchemaId);
                }

                return;
            }

            ulong schemaId = ComponentSchemaIdHash.Compute(ComponentSchemaIdHash.GetMetadataFullName(type));
            if (schemaId == 0UL)
            {
                ReportSchemaId(context, InvalidSchemaIdRule, attributeSyntax, schemaId);
                return;
            }

            ReportSchemaId(context, PinSchemaIdRule, attributeSyntax.Name, schemaId);
        }

        private static void ReportSchemaId(
            SyntaxNodeAnalysisContext context,
            DiagnosticDescriptor rule,
            SyntaxNode location,
            ulong schemaId)
        {
            string formattedSchemaId = schemaId.ToString("X16", CultureInfo.InvariantCulture);
            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                .Add("SchemaId", formattedSchemaId);
            context.ReportDiagnostic(Diagnostic.Create(
                rule,
                location.GetLocation(),
                properties,
                formattedSchemaId));
        }

    }
}
