using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Delta.ECS.Generators;

/// <summary>Suppresses implicit-lambda style hints when they erase ForEach ECS types.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ImplicitLambdaDiagnosticSuppressor : DiagnosticSuppressor
{
    private const string ImplicitLambdaDiagnosticId = "IDE0350";
    private static readonly SuppressionDescriptor ExplicitComponentTypesRequired = new(
        "DECSECSUP001",
        ImplicitLambdaDiagnosticId,
        "DeltaECS needs explicit ECS parameter types to generate this ForEach callback.");
    private static readonly ImmutableArray<SuppressionDescriptor> Suppressions =
        ImmutableArray.Create(ExplicitComponentTypesRequired);

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions
        => Suppressions;

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Location.SourceTree is not { } tree
                || !RequiresExplicitTypes(context.GetSemanticModel(tree), diagnostic))
            {
                continue;
            }

            context.ReportSuppression(Suppression.Create(ExplicitComponentTypesRequired, diagnostic));
        }
    }

    internal static bool RequiresExplicitTypes(SemanticModel model, Diagnostic diagnostic)
    {
        SyntaxTree tree = model.SyntaxTree;
        SyntaxNode root = tree.GetRoot();
        SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (node.AncestorsAndSelf().OfType<LambdaExpressionSyntax>().FirstOrDefault() is not { } lambda
            || lambda.Parent is not ArgumentSyntax argument
            || argument.Expression != lambda
            || argument.Parent is not ArgumentListSyntax argumentList
            || argumentList.Parent is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax member
            || member.Name is not IdentifierNameSyntax name
            || !ApiDescriptor.TryGet(name.Identifier.ValueText, out ApiDescriptor descriptor)
            || descriptor.Family != GeneratedApiKind.Iteration)
        {
            return false;
        }

        bool orderedQueryReceiver = OrderedQueryInvocationGrammar.TryReadOrderedWhereSource(
                member.Expression,
                model,
                out _)
            || GeneratorSupport.IsEcsType(model.GetTypeInfo(member.Expression).Type, "OrderedQuery");
        if (!GeneratorSupport.IsWorldReceiver(model, member.Expression) && !orderedQueryReceiver)
        {
            return false;
        }

        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        int callbackArgumentIndex = arguments.IndexOf(argument);
        if (callbackArgumentIndex < 0
            || !DemandDrivenForEachGenerator.TryReadPrefix(
                model,
                invocation,
                descriptor,
                orderedQueryReceiver,
                callbackArgumentIndex,
                out InvocationCursorResult prefix))
        {
            return false;
        }

        ParameterSyntax[] parameters = CallbackReader.LambdaParameters(lambda);
        int contextParameterCount = prefix.HasContext ? 1 : 0;
        int componentParameterStart = contextParameterCount + (descriptor.HasEntity ? 1 : 0);
        int componentCount = parameters.Length - componentParameterStart;
        if (!ArityEvidence.TryBind(
                descriptor.MinimumArity,
                out int resolvedComponentCount,
                second: prefix.ComponentIdCount == 0 ? null : prefix.ComponentIdCount,
                third: componentCount))
        {
            return false;
        }

        if (componentCount == 0)
        {
            return descriptor.HasEntity
                && CallbackReader.IsEntityRefParameter(model, parameters[contextParameterCount]);
        }

        return parameters
            .Skip(componentParameterStart)
            .Take(resolvedComponentCount)
            .All(static parameter => parameter.Type is not null);
    }
}
