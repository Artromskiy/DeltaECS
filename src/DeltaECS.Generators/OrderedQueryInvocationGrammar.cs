using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

internal static class OrderedQueryInvocationGrammar
{
    internal static bool IsCandidate(SyntaxNode node)
        => node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member }
            && member.Name.Identifier.ValueText is "OrderBy" or "ThenBy";

    internal static bool TryReadMethod(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        out MemberAccessExpressionSyntax member,
        out ApiDescriptor descriptor,
        out PredicateModel? whereSource)
    {
        member = null!;
        descriptor = default;
        whereSource = null;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        string name = memberAccess.Name.Identifier.ValueText;
        if (!ApiDescriptor.TryGet(name, out ApiDescriptor candidate)
            || candidate.Family != GeneratedApiKind.Ordering)
        {
            return false;
        }

        bool isQueryOrder = name == "OrderBy"
            && GeneratorSupport.IsEcsType(model.GetTypeInfo(memberAccess.Expression).Type, "Query");
        bool isWhereOrder = name == "OrderBy"
            && TryReadWhereSource(memberAccess.Expression, model, out whereSource);
        bool isOrderedWhereThen = name == "ThenBy"
            && TryReadOrderedWhereSource(memberAccess.Expression, model, out whereSource);
        bool isOrderedQueryOrder = name == "ThenBy"
            && (isOrderedWhereThen || IsOrderedQueryExpression(memberAccess.Expression, model));
        if (!isQueryOrder && !isWhereOrder && !isOrderedQueryOrder)
        {
            return false;
        }

        member = memberAccess;
        descriptor = candidate;
        return true;
    }

    private static bool IsOrderedQueryExpression(ExpressionSyntax expression, SemanticModel model)
        => GeneratorSupport.IsEcsType(model.GetTypeInfo(expression).Type, "OrderedQuery")
            || expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member }
                && member.Name.Identifier.ValueText == "OrderBy"
                && GeneratorSupport.IsEcsType(model.GetTypeInfo(member.Expression).Type, "Query");

    internal static bool TryReadWhereSource(ExpressionSyntax expression, SemanticModel model, out PredicateModel? shape)
    {
        shape = null;
        if (expression is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        return GeneratedWhereGenerator.TryReadPredicate(model, invocation, out shape);
    }

    internal static bool TryReadOrderedWhereSource(ExpressionSyntax expression, SemanticModel model, out PredicateModel? shape)
    {
        shape = null;
        if (expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member })
        {
            return false;
        }

        if (member.Name.Identifier.ValueText == "OrderBy")
        {
            return TryReadWhereSource(member.Expression, model, out shape);
        }

        return member.Name.Identifier.ValueText == "ThenBy"
            && TryReadOrderedWhereSource(member.Expression, model, out shape);
    }

    internal static bool TryReadArguments(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        ApiDescriptor descriptor,
        int componentArity,
        bool contextArgumentPresent,
        out InvocationCursorResult result,
        out RegistrationBindingKind registrationBinding)
    {
        result = default;
        registrationBinding = RegistrationBindingKind.Primary;
        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return false;
        }

        var cursor = new InvocationCursor(model, arguments, descriptor);
        if (!cursor.TryRead(arguments.Count - 1, out result, contextArgumentPresent: contextArgumentPresent))
        {
            return false;
        }

        registrationBinding = result.RegistrationBinding;
        return result.HasComponentIdSpan || result.ComponentIdCount is 0 || result.ComponentIdCount == componentArity;
    }
}
