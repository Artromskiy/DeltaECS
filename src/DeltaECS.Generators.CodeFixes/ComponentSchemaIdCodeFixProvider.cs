using System.Collections.Immutable;
using System.Composition;
using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace Delta.ECS.Generators;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ComponentSchemaIdCodeFixProvider)), Shared]
public sealed class ComponentSchemaIdCodeFixProvider : CodeFixProvider
{
    private const string DiagnosticId = "DECS0001";
    private const string SchemaIdProperty = "SchemaId";
    private const string Title = "Pin component SchemaId";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => FixAllProvider.Create(PinDocumentAsync);

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(SchemaIdProperty, out string? schemaIdText) ||
                !ulong.TryParse(schemaIdText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong schemaId))
            {
                continue;
            }

            AttributeSyntax? attribute = root.FindNode(diagnostic.Location.SourceSpan)
                .FirstAncestorOrSelf<AttributeSyntax>();
            if (attribute is null || attribute.ArgumentList is { Arguments.Count: > 0 })
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    cancellationToken => AddSchemaIdAsync(context.Document, diagnostic.Location.SourceSpan, schemaId, cancellationToken),
                    equivalenceKey: Title),
                diagnostic);
        }
    }

    private static async Task<Document?> PinDocumentAsync(FixAllContext context, Document document, ImmutableArray<Diagnostic> diagnostics)
    {
        // Apply from the end so earlier diagnostic spans remain valid.
        foreach (Diagnostic diagnostic in diagnostics.OrderByDescending(static diagnostic => diagnostic.Location.SourceSpan.Start))
        {
            if (diagnostic.Properties.TryGetValue(SchemaIdProperty, out string? schemaIdText)
                && ulong.TryParse(schemaIdText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong schemaId))
            {
                document = await AddSchemaIdAsync(document, diagnostic.Location.SourceSpan, schemaId, context.CancellationToken).ConfigureAwait(false);
            }
        }

        return document;
    }

    private static async Task<Document> AddSchemaIdAsync(
        Document document,
        TextSpan diagnosticSpan,
        ulong schemaId,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        AttributeSyntax? attribute = root?.FindNode(diagnosticSpan).FirstAncestorOrSelf<AttributeSyntax>();
        if (root is null || attribute is null)
        {
            return document;
        }

        string literalText = $"0x{schemaId:X16}UL";
        AttributeArgumentSyntax argument = SyntaxFactory.AttributeArgument(
            SyntaxFactory.LiteralExpression(
                SyntaxKind.NumericLiteralExpression,
                SyntaxFactory.Literal(literalText, schemaId)));
        AttributeSyntax updatedAttribute = attribute
            .WithArgumentList(SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(argument)))
            .WithAdditionalAnnotations(Formatter.Annotation);

        return document.WithSyntaxRoot(root.ReplaceNode(attribute, updatedAttribute));
    }
}
