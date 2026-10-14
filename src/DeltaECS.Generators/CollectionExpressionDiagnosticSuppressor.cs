using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Delta.ECS.Generators;

/// <summary>Suppresses collection-expression hints that change DeltaECS overload resolution.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollectionExpressionDiagnosticSuppressor : DiagnosticSuppressor
{
    private const string CollectionExpressionDiagnosticId = "IDE0300";
    private static readonly SuppressionDescriptor ExplicitArrayRequired = new(
        "DECSECSUP002",
        CollectionExpressionDiagnosticId,
        "DeltaECS requires an explicit array here to preserve overload resolution.");
    private static readonly ImmutableArray<SuppressionDescriptor> Suppressions =
        ImmutableArray.Create(ExplicitArrayRequired);

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => Suppressions;

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Location.SourceTree is not { } tree
                || !RequiresExplicitArray(context.GetSemanticModel(tree), diagnostic))
            {
                continue;
            }

            context.ReportSuppression(Suppression.Create(ExplicitArrayRequired, diagnostic));
        }
    }

    internal static bool RequiresExplicitArray(SemanticModel model, Diagnostic diagnostic)
    {
        SyntaxNode root = model.SyntaxTree.GetRoot();
        SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (node.AncestorsAndSelf().OfType<ExpressionSyntax>().FirstOrDefault(static expression =>
                expression is ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax) is not { } array
            || array.Parent is not ArgumentSyntax argument
            || argument.Parent is not ArgumentListSyntax arguments
            || arguments.Parent is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax member
            || !GeneratorSupport.IsWorldReceiver(model, member.Expression)
            || arguments.Arguments.Count == 0
            || arguments.Arguments[0] != argument
            || model.GetOperation(invocation) is not IInvocationOperation operation
            || !IsDeltaEcsMethod(operation.TargetMethod))
        {
            return false;
        }

        IArgumentOperation? firstArgument = operation.Arguments.FirstOrDefault(candidate => candidate.Syntax == argument);
        if (firstArgument is null || !IsReadOnlySpan(firstArgument.Parameter?.Type, out ITypeSymbol? elementType))
        {
            return false;
        }

        return operation.TargetMethod.Name switch
        {
            "Create" => IsComponentId(elementType) && HasComponentValues(operation, firstArgument),
            "ForEach" or "ForEachParallel" or "ForEachEntity" or "ForEachEntityParallel" =>
                IsEntity(elementType) && HasFollowingQuery(operation, firstArgument),
            _ => false
        };
    }

    private static bool HasComponentValues(IInvocationOperation operation, IArgumentOperation firstArgument)
        => operation.Arguments
            .Where(argument => argument.Syntax != firstArgument.Syntax && argument.ArgumentKind == ArgumentKind.Explicit)
            .Any(argument => argument.Parameter is { } parameter
                && parameter.Type.SpecialType != SpecialType.System_Int32
                && !IsEntitySpan(parameter.Type));

    private static bool HasFollowingQuery(IInvocationOperation operation, IArgumentOperation firstArgument)
        => operation.Arguments
            .Where(argument => argument.Syntax != firstArgument.Syntax && argument.ArgumentKind == ArgumentKind.Explicit)
            .Any(argument => GeneratorSupport.IsEcsType(argument.Parameter?.Type, "Query"));

    private static bool IsDeltaEcsMethod(IMethodSymbol method)
    {
        string namespaceName = method.ContainingNamespace.ToDisplayString();
        return namespaceName == GeneratorSupport.EcsNamespace
            || namespaceName.StartsWith(GeneratorSupport.EcsNamespace + ".Generated.", StringComparison.Ordinal)
            || method.ContainingType.Name.StartsWith("GeneratedStructuralExtensions_", StringComparison.Ordinal)
            || method.ContainingType.Name.StartsWith("DemandForEachExtensions_", StringComparison.Ordinal);
    }

    private static bool IsReadOnlySpan(ITypeSymbol? type, out ITypeSymbol? elementType)
    {
        elementType = null;
        if (type is not INamedTypeSymbol named
            || named.Name != "ReadOnlySpan"
            || named.ContainingNamespace.ToDisplayString() != GeneratorSupport.SystemNamespace
            || named.TypeArguments.Length != 1)
        {
            return false;
        }

        elementType = named.TypeArguments[0];
        return true;
    }

    private static bool IsEntitySpan(ITypeSymbol type)
        => type is INamedTypeSymbol named
            && named.Name is "Span" or "ReadOnlySpan"
            && named.ContainingNamespace.ToDisplayString() == GeneratorSupport.SystemNamespace
            && named.TypeArguments.Length == 1
            && IsEntity(named.TypeArguments[0]);

    private static bool IsComponentId(ITypeSymbol? type) => GeneratorSupport.IsEcsType(type, "ComponentId");

    private static bool IsEntity(ITypeSymbol? type) => GeneratorSupport.IsEntityType(type);
}
