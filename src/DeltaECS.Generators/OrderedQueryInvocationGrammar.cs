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
        out ApiDescriptor descriptor)
    {
        member = null!;
        descriptor = default;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        string name = memberAccess.Name.Identifier.ValueText;
        if (!ApiDescriptor.TryGet(name, out ApiDescriptor candidate)
            || candidate.Family != GeneratedApiKind.Ordering
            || !(name == "OrderBy"
                ? GeneratorSupport.IsEcsType(model.GetTypeInfo(memberAccess.Expression).Type, "Query")
                : IsOrderedQueryExpression(memberAccess.Expression, model)))
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
                && (member.Name.Identifier.ValueText == "OrderBy"
                    ? GeneratorSupport.IsEcsType(model.GetTypeInfo(member.Expression).Type, "Query")
                    : member.Name.Identifier.ValueText == "ThenBy"
                        && IsOrderedQueryExpression(member.Expression, model));

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
