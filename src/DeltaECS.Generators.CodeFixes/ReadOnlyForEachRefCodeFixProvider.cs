using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace Delta.ECS.Generators;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ReadOnlyForEachRefCodeFixProvider)), Shared]
public sealed class ReadOnlyForEachRefCodeFixProvider : CodeFixProvider
{
    private const string DiagnosticId = "DECSGEN012";
    private const string ModifierProperty = "Modifier";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(ModifierProperty, out string? modifier)
                || modifier is not ("in" or "ref readonly"))
            {
                continue;
            }

            string title = $"Use {modifier}";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => ReplaceModifierAsync(context.Document, diagnostic.Location.SourceSpan, modifier, cancellationToken),
                    equivalenceKey: title),
                diagnostic);
        }

    }

    private static async Task<Document> ReplaceModifierAsync(
        Document document,
        TextSpan diagnosticSpan,
        string modifier,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        ParameterSyntax? parameter = root?.FindNode(diagnosticSpan).FirstAncestorOrSelf<ParameterSyntax>();
        if (root is null || parameter is null)
        {
            return document;
        }

        SyntaxTokenList modifiers = modifier == "in"
            ? ReplaceWithIn(parameter.Modifiers)
            : ReplaceWithRefReadonly(parameter.Modifiers);
        ParameterSyntax updatedParameter = parameter
            .WithModifiers(modifiers)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return document.WithSyntaxRoot(root.ReplaceNode(parameter, updatedParameter));
    }

    private static SyntaxTokenList ReplaceWithIn(SyntaxTokenList modifiers)
    {
        SyntaxTokenList updated = default;
        foreach (SyntaxToken modifier in modifiers)
        {
            updated = updated.Add(modifier.IsKind(SyntaxKind.RefKeyword)
                ? SyntaxFactory.Token(modifier.LeadingTrivia, SyntaxKind.InKeyword, modifier.TrailingTrivia)
                : modifier);
        }

        return updated;
    }

    private static SyntaxTokenList ReplaceWithRefReadonly(SyntaxTokenList modifiers)
    {
        SyntaxTokenList updated = default;
        foreach (SyntaxToken modifier in modifiers)
        {
            if (!modifier.IsKind(SyntaxKind.RefKeyword))
            {
                updated = updated.Add(modifier);
                continue;
            }

            updated = updated
                .Add(modifier.WithTrailingTrivia(SyntaxFactory.Space))
                .Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword).WithTrailingTrivia(modifier.TrailingTrivia));
        }

        return updated;
    }
}
