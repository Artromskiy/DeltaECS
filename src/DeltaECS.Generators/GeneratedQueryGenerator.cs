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
            || !TryFactoryName(member.Name, out string name, out int genericArity)
            || !IsFactoryName(name)
            || !ApiDescriptor.TryGet(name, out ApiDescriptor descriptor)
            || descriptor.Family != GeneratedApiKind.QueryFactory
            || (!IsWorldReceiver(model, member.Expression)
                && !IsQueryReceiver(model, member.Expression)))
        {
            return false;
        }

        var cursor = new InvocationCursor(model, invocation.ArgumentList.Arguments, descriptor);
        if (!cursor.TryRead(-1, out InvocationCursorResult selection))
        {
            return false;
        }

        TypeBindingKind typeBinding;
        RegistrationBindingKind registrationBinding;
        int arity;
        if (genericArity != 0)
        {
            if (selection.ComponentIdCount != 0 && selection.ComponentIdCount != genericArity)
            {
                return false;
            }
            if (!selection.HasComponentIds && invocation.ArgumentList.Arguments.Count != 0)
            {
                return false;
            }

            arity = genericArity;
            typeBinding = TypeBindingKind.Generic;
            registrationBinding = selection.HasComponentIdSpan
                ? RegistrationBindingKind.Dynamic
                : selection.ComponentIdCount != 0
                    ? RegistrationBindingKind.Explicit
                    : RegistrationBindingKind.Primary;
        }
        else
        {
            if (selection.ComponentIdCount == 0 || selection.HasComponentIdSpan)
            {
                return false;
            }

            arity = selection.ComponentIdCount;
            typeBinding = TypeBindingKind.None;
            registrationBinding = RegistrationBindingKind.Explicit;
        }

        if (arity < descriptor.MinimumArity)
        {
            return false;
        }

        shape = new QueryModel(
            name,
            arity,
            GeneratorSupport.ContainingNamespace(model, invocation),
            typeBinding,
            registrationBinding);
        return true;
    }

    private static bool TryFactoryName(NameSyntax nameSyntax, out string name, out int arity)
    {
        switch (nameSyntax)
        {
            case GenericNameSyntax genericName:
                name = genericName.Identifier.ValueText;
                arity = genericName.TypeArgumentList.Arguments.Count;
                return true;
            case IdentifierNameSyntax identifierName:
                name = identifierName.Identifier.ValueText;
                arity = 0;
                return true;
            default:
                name = string.Empty;
                arity = 0;
                return false;
        }
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
            && invocation.Expression is MemberAccessExpressionSyntax member
            && TryFactoryName(member.Name, out string name, out _)
            && IsFactoryName(name))
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
