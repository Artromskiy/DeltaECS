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
        => GeneratorPipeline.RegisterShapeOutput<QueryModel>(
            context,
            static syntaxContext => TryReadShape(
                syntaxContext.SemanticModel,
                (InvocationExpressionSyntax)syntaxContext.Node,
                out QueryModel? shape)
                ? shape
                : null,
            "GeneratedQuery_",
            static shape => shape.Key,
            static shape => GeneratedQueryTemplates.Render(shape));

    private static bool TryReadShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        out QueryModel? shape)
    {
        shape = null;
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || !TryFactoryName(member.Name, out string name, out int genericArity)
            || !ApiDescriptor.TryGet(name, out ApiDescriptor descriptor)
            || descriptor.Family != GeneratedApiKind.QueryFactory
            || (!GeneratorSupport.IsWorldReceiver(model, member.Expression)
                && !GeneratorSupport.IsQueryReceiver(model, member.Expression)
                && !GeneratorSupport.IsQuerySpecReceiver(model, member.Expression)))
        {
            return false;
        }

        bool querySpecReceiver = GeneratorSupport.IsQuerySpecReceiver(model, member.Expression);

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
            if (querySpecReceiver)
            {
                return false;
            }
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
            registrationBinding = selection.RegistrationBinding;
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
            registrationBinding,
            querySpecReceiver);
        return true;
    }

    private static bool TryFactoryName(NameSyntax nameSyntax, out string name, out int arity)
    {
        (name, arity) = nameSyntax switch
        {
            GenericNameSyntax genericName => (
                genericName.Identifier.ValueText,
                genericName.TypeArgumentList.Arguments.Count),
            IdentifierNameSyntax identifierName => (identifierName.Identifier.ValueText, 0),
            _ => (string.Empty, 0)
        };
        return nameSyntax is GenericNameSyntax or IdentifierNameSyntax;
    }

}
