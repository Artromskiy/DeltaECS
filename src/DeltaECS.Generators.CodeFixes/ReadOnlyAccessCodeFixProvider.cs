using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace Delta.ECS.Generators;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ReadOnlyAccessCodeFixProvider)), Shared]
public sealed class ReadOnlyAccessCodeFixProvider : CodeFixProvider
{
    private const string DiagnosticId = "DECSGEN013";
    private const string ModifierProperty = "Modifier";
    private const string RefLocalProperty = "RefLocal";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            string? title = diagnostic.Properties.TryGetValue(ModifierProperty, out string? modifier)
                ? $"Use {modifier}"
                : diagnostic.Properties.ContainsKey(RefLocalProperty)
                    ? "Use GetReadRef"
                    : null;
            if (title is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => FixAsync(context.Document, diagnostic, cancellationToken),
                    equivalenceKey: title),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    private static Task<Document> FixAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        => diagnostic.Properties.TryGetValue(ModifierProperty, out string? modifier)
            ? ReplaceModifierAsync(document, diagnostic.Location.SourceSpan, modifier!, cancellationToken)
            : ReplaceGetRefAsync(document, diagnostic, cancellationToken);

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

    private static async Task<Document> ReplaceGetRefAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var name = root?.FindNode(diagnosticSpan).FirstAncestorOrSelf<SimpleNameSyntax>();
        if (root is null || name is null)
        {
            return document;
        }

        SyntaxNode updatedRoot = root.ReplaceNode(name, name.WithIdentifier(
            SyntaxFactory.Identifier(name.Identifier.LeadingTrivia, "GetReadRef", name.Identifier.TrailingTrivia)));
        if (diagnostic.Properties.TryGetValue(RefLocalProperty, out string? isRefLocal)
            && bool.TryParse(isRefLocal, out bool updateRefLocal)
            && updateRefLocal)
        {
            VariableDeclaratorSyntax? variable = updatedRoot.FindNode(diagnosticSpan).FirstAncestorOrSelf<VariableDeclaratorSyntax>();
            if (variable?.Parent is VariableDeclarationSyntax declaration
                && declaration.Type is RefTypeSyntax refType
                && !refType.ReadOnlyKeyword.IsKind(SyntaxKind.ReadOnlyKeyword))
            {
                RefTypeSyntax updatedType = refType.WithReadOnlyKeyword(
                    SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword).WithTrailingTrivia(SyntaxFactory.Space));
                updatedRoot = updatedRoot.ReplaceNode(refType, updatedType);
            }
        }

        return document.WithSyntaxRoot(updatedRoot.WithAdditionalAnnotations(Formatter.Annotation));
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
