using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>
/// Generates typed World factories and composable Query extensions only for
/// the WhereAll, WhereAny and WhereNone shapes used by a consumer compilation.
/// </summary>
[Generator]
public sealed class GeneratedQueryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            GeneratorPipeline.ShapeProvider<QueryModel>(
                    context,
                    static syntaxContext => TryReadShape(
                        syntaxContext.SemanticModel,
                        (InvocationExpressionSyntax)syntaxContext.Node,
                        out QueryModel? shape)
                        ? shape
                        : null)
                .Where(static shape => shape is not null)
                .Select(static (shape, _) => shape!)
                .Collect(),
            static (productionContext, shapes) => GeneratorPipeline.EmitShapes<QueryModel>(
                shapes,
                productionContext,
                "GeneratedQuery_",
                static shape => shape.Key,
                static shape => GeneratedQueryTemplates.Render(shape)));
    }

    private static bool TryReadShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        out QueryModel? shape)
    {
        shape = null;
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || member.Name is not GenericNameSyntax genericName
            || !IsFactoryName(genericName.Identifier.ValueText)
            || !ApiDescriptor.TryGet(genericName.Identifier.ValueText, out ApiDescriptor descriptor)
            || descriptor.Family != GeneratedApiKind.QueryFactory
            || invocation.ArgumentList.Arguments.Count != 0
            || (!IsWorldReceiver(model, member.Expression)
                && !IsQueryReceiver(model, member.Expression)))
        {
            return false;
        }

        int arity = genericName.TypeArgumentList.Arguments.Count;
        if (arity < descriptor.MinimumArity)
        {
            return false;
        }

        shape = new QueryModel(genericName.Identifier.ValueText, arity);
        return true;
    }

    private static bool IsFactoryName(string name)
        => name is "WhereAll" or "WhereAny" or "WhereNone";

    private static bool IsWorldReceiver(SemanticModel model, ExpressionSyntax expression)
        => GeneratorSupport.IsNamedType(model.GetTypeInfo(expression).Type, "World");

    private static bool IsQueryReceiver(SemanticModel model, ExpressionSyntax expression)
    {
        if (GeneratorSupport.IsNamedType(model.GetTypeInfo(expression).Type, "Query"))
        {
            return true;
        }

        // Earlier generated extension calls are not part of the input
        // compilation's semantic model, so recognize a fluent factory chain
        // syntactically as well as a resolved Query receiver.
        if (expression is InvocationExpressionSyntax invocation
            && invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax member
            && member.Name is GenericNameSyntax genericName
            && IsFactoryName(genericName.Identifier.ValueText))
        {
            return IsWorldReceiver(model, member.Expression)
                || IsQueryReceiver(model, member.Expression);
        }

        if (expression is IdentifierNameSyntax identifier
            && model.GetSymbolInfo(identifier).Symbol is ILocalSymbol local
            && local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is VariableDeclaratorSyntax declarator
            && declarator.Initializer?.Value is ExpressionSyntax initializer)
        {
            return IsWorldReceiver(model, initializer)
                || IsQueryReceiver(model, initializer);
        }

        return false;
    }

}
