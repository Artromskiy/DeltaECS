using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>Syntax-free callback data consumed by raw-string interception templates.</summary>
internal sealed class CallSiteBinding
{
    internal CallSiteBinding(
        LambdaExpressionSyntax? lambda,
        IMethodSymbol? method,
        bool preserveValueSemantics)
    {
        LambdaParameterNames = lambda is null
            ? Array.Empty<string>()
            : CallbackReader.LambdaParameters(lambda).Select(static parameter => parameter.Identifier.ValueText).ToArray();
        LambdaBody = lambda switch
        {
            { Body: BlockSyntax block } => block.ToString(),
            { Body: ExpressionSyntax expression } => expression.ToString(),
            _ => null
        };
        LambdaBodyIsBlock = lambda?.Body is BlockSyntax;
        CanInline = lambda is not null
            && !preserveValueSemantics
            && !lambda.Body.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>().Any();
        MethodGroupTarget = method is null ? null : CallbackReader.MethodGroupTarget(method);
        var identifiers = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        if (lambda is not null)
        {
            foreach (string identifier in lambda.DescendantTokens()
                .Where(static token => token.IsKind(SyntaxKind.IdentifierToken))
                .Select(static token => token.ValueText))
            {
                identifiers.Add(identifier);
            }
        }

        LambdaIdentifiers = identifiers.ToImmutable();
    }

    internal CallSiteBinding(
        string[] parameterNames,
        string? body,
        bool bodyIsBlock,
        bool canInline,
        string? methodGroupTarget)
    {
        LambdaParameterNames = parameterNames;
        LambdaBody = body;
        LambdaBodyIsBlock = bodyIsBlock;
        CanInline = canInline;
        MethodGroupTarget = methodGroupTarget;
        LambdaIdentifiers = ImmutableHashSet<string>.Empty;
    }

    internal string[] LambdaParameterNames { get; }
    internal string? LambdaBody { get; }
    internal bool LambdaBodyIsBlock { get; }
    internal bool CanInline { get; }
    internal string? MethodGroupTarget { get; }
    internal ImmutableHashSet<string> LambdaIdentifiers { get; }
}

/// <summary>Shared semantic rules for generated callback arguments.</summary>
internal static class CallbackReader
{
    internal static ParameterSyntax[] LambdaParameters(LambdaExpressionSyntax? lambda)
        => lambda switch
        {
            SimpleLambdaExpressionSyntax simple => new[] { simple.Parameter },
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.ToArray(),
            _ => Array.Empty<ParameterSyntax>()
        };

    internal static bool IsEntityParameter(SemanticModel model, ParameterSyntax parameter, bool allowImplicit = false)
        => parameter.Modifiers.Count == 0
            && ((parameter.Type is not null && GeneratorSupport.IsEntityType(model.GetTypeInfo(parameter.Type).Type))
                || (allowImplicit && parameter.Type is null));

    internal static bool IsSupportedRefKind(RefKind refKind)
        => refKind is RefKind.None or RefKind.In or RefKind.Ref
            || GeneratorSupport.IsRefReadonly(refKind);

    internal static bool IsSupportedReadRefKind(RefKind refKind)
        => refKind is RefKind.None or RefKind.In
            || GeneratorSupport.IsRefReadonly(refKind);

    internal static RefKind ArgumentRefKind(ArgumentSyntax argument)
    {
        SyntaxKind kind = argument.RefKindKeyword.Kind();
        if (kind == SyntaxKind.InKeyword)
        {
            return Microsoft.CodeAnalysis.RefKind.In;
        }

        if (kind == SyntaxKind.RefKeyword)
        {
            foreach (SyntaxToken token in argument.ChildTokens())
            {
                if (token.IsKind(SyntaxKind.ReadOnlyKeyword))
                {
                    return Microsoft.CodeAnalysis.RefKind.RefReadOnly;
                }
            }

            return Microsoft.CodeAnalysis.RefKind.Ref;
        }

        return Microsoft.CodeAnalysis.RefKind.None;
    }

    internal static RefKind ParameterRefKind(ParameterSyntax parameter)
    {
        bool hasRef = parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.RefKeyword));
        return hasRef
            ? parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword))
                ? Microsoft.CodeAnalysis.RefKind.RefReadOnly
                : Microsoft.CodeAnalysis.RefKind.Ref
            : parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.InKeyword))
                ? Microsoft.CodeAnalysis.RefKind.In
                : Microsoft.CodeAnalysis.RefKind.None;
    }

    internal static ContextModeKind ContextMode(Microsoft.CodeAnalysis.RefKind refKind)
        => refKind == Microsoft.CodeAnalysis.RefKind.Ref
            ? ContextModeKind.Ref
            : refKind == Microsoft.CodeAnalysis.RefKind.In
                ? ContextModeKind.In
                : GeneratorSupport.IsRefReadonly(refKind)
                    ? ContextModeKind.RefReadonly
                    : ContextModeKind.Value;

    internal static bool AreCompatibleContextModes(ContextModeKind argumentMode, ContextModeKind callbackMode)
        => argumentMode == callbackMode
            || (argumentMode == ContextModeKind.In && callbackMode == ContextModeKind.RefReadonly);

    internal static bool IsStaticMethodGroupExpression(SemanticModel model, ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax or CastExpressionSyntax)
        {
            expression = expression switch
            {
                ParenthesizedExpressionSyntax parenthesizedExpression => parenthesizedExpression.Expression,
                CastExpressionSyntax cast => cast.Expression,
                _ => expression
            };
        }

        if (expression is IdentifierNameSyntax)
        {
            return true;
        }

        return expression is MemberAccessExpressionSyntax member
            && model.GetSymbolInfo(member.Expression).Symbol is INamedTypeSymbol or INamespaceSymbol or IAliasSymbol;
    }

    internal static bool TryGetMethodGroupTarget(
        SemanticModel model,
        ExpressionSyntax expression,
        out IMethodSymbol? method)
    {
        ExpressionSyntax originalExpression = expression;
        while (true)
        {
            expression = expression switch
            {
                ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
                CastExpressionSyntax cast => cast.Expression,
                _ => expression
            };
            if (expression is not (ParenthesizedExpressionSyntax or CastExpressionSyntax))
            {
                break;
            }
        }

        ImmutableArray<ISymbol> members = model.GetMemberGroup(expression);
        if (members.Length == 1 && members[0] is IMethodSymbol resolvedMethod)
        {
            method = resolvedMethod;
            return true;
        }

        if (members.Length > 1
            && model.GetTypeInfo(originalExpression).ConvertedType is INamedTypeSymbol delegateType
            && delegateType.DelegateInvokeMethod is { } invoke)
        {
            IMethodSymbol[] matches = members
                .OfType<IMethodSymbol>()
                .Where(candidate => SameCallbackSignature(candidate, invoke))
                .ToArray();
            if (matches.Length == 1)
            {
                method = matches[0];
                return true;
            }
        }

        method = null;
        return false;
    }

    internal static bool TryGetMethodGroupTarget(
        SemanticModel model,
        ExpressionSyntax expression,
        int expectedParameterCount,
        ImmutableArray<ITypeSymbol?> expectedTypes,
        bool hasEntity,
        out IMethodSymbol? method)
    {
        ExpressionSyntax normalized = expression;
        while (normalized is ParenthesizedExpressionSyntax or CastExpressionSyntax)
        {
            normalized = normalized switch
            {
                ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
                CastExpressionSyntax cast => cast.Expression,
                _ => normalized
            };
        }

        IMethodSymbol[] matches = model.GetMemberGroup(normalized)
            .OfType<IMethodSymbol>()
            .Where(candidate => candidate.Parameters.Length == expectedParameterCount)
            .Where(candidate => expectedTypes.IsDefaultOrEmpty
                || MatchesGenericCallbackTypes(candidate, expectedTypes, hasEntity))
            .ToArray();
        method = matches.Length == 1 ? matches[0] : null;
        return method is not null;
    }

    private static bool MatchesGenericCallbackTypes(
        IMethodSymbol method,
        ImmutableArray<ITypeSymbol?> expectedTypes,
        bool hasEntity)
    {
        if (expectedTypes.Any(static type => type is null))
        {
            return false;
        }

        IParameterSymbol[] parameters = method.Parameters.ToArray();
        int entityOffset = hasEntity ? 1 : 0;
        for (int contextOffset = 0; contextOffset <= 1; contextOffset++)
        {
            if (expectedTypes.Length < contextOffset
                || parameters.Length != expectedTypes.Length + entityOffset)
            {
                continue;
            }

            int componentStart = contextOffset + entityOffset;
            int componentCount = expectedTypes.Length - contextOffset;
            if (hasEntity
                && (parameters[contextOffset].RefKind != RefKind.None
                    || !GeneratorSupport.IsEntityType(parameters[contextOffset].Type)))
            {
                continue;
            }

            if (contextOffset != 0
                && (parameters[0].RefKind is not (RefKind.Ref or RefKind.In)
                    || !SymbolEqualityComparer.Default.Equals(parameters[0].Type, expectedTypes[0])))
            {
                continue;
            }

            if (parameters.Skip(componentStart).Take(componentCount)
                .Zip(expectedTypes.Skip(contextOffset), static (parameter, expected) =>
                    SymbolEqualityComparer.Default.Equals(parameter.Type, expected))
                .All(static match => match))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameCallbackSignature(IMethodSymbol method, IMethodSymbol callback)
        => SymbolEqualityComparer.Default.Equals(method.ReturnType, callback.ReturnType)
            && method.Parameters.Length == callback.Parameters.Length
            && method.Parameters.Zip(callback.Parameters, static (candidate, expected) =>
                candidate.RefKind == expected.RefKind
                && SymbolEqualityComparer.Default.Equals(candidate.Type, expected.Type))
                .All(static match => match);

    internal static bool TryGetStaticMethodGroupTarget(
        SemanticModel model,
        ExpressionSyntax expression,
        out IMethodSymbol? method)
    {
        if (TryGetMethodGroupTarget(model, expression, out IMethodSymbol? candidate)
            && candidate is { IsStatic: true, MethodKind: MethodKind.Ordinary, Arity: 0, ContainingType: not null }
            && GeneratorSupport.IsAccessibleSymbol(candidate))
        {
            method = candidate;
            return true;
        }

        method = null;
        return false;
    }

    internal static string MethodGroupTarget(IMethodSymbol method)
        => method.ContainingType is { } containingType
            ? containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + method.Name
            : ThrowHelper.ThrowMethodGroupTargetMissing(method);

    internal static bool TryGetMarker(
        INamedTypeSymbol type,
        Func<string, bool> name,
        out INamedTypeSymbol? marker)
    {
        INamedTypeSymbol[] markers = type.AllInterfaces
            .Where(static candidate => candidate.ContainingNamespace.ToDisplayString() == GeneratorSupport.EcsNamespace)
            .Where(candidate => name(candidate.Name))
            .ToArray();
        marker = markers.Length == 1 ? markers[0] : null;
        return marker is not null;
    }

    internal static bool TryGetForEachMarker(
        INamedTypeSymbol type,
        out bool hasContext,
        out bool hasEntity,
        out ITypeSymbol? contextType)
    {
        bool result = TryGetMarker(type, static name => name is
            "IForEach" or "IForEachEntity" or "IForEachContext" or "IForEachContextEntity", out INamedTypeSymbol? marker);
        hasContext = marker?.Name is "IForEachContext" or "IForEachContextEntity";
        hasEntity = marker?.Name is "IForEachEntity" or "IForEachContextEntity";
        contextType = hasContext ? marker!.TypeArguments[0] : null;
        return result;
    }

    internal static bool HasWherePredicateMarker(INamedTypeSymbol type)
        => TryGetMarker(type, static name => name == "IWherePredicate", out _);

    internal static bool HasValidPrefix(
        IMethodSymbol method,
        bool hasContext,
        bool hasEntity,
        ITypeSymbol? contextType,
        bool requireRefContext)
    {
        int index = 0;
        if (hasContext)
        {
            if (method.Parameters.Length == 0
                || (requireRefContext
                    ? method.Parameters[0].RefKind != Microsoft.CodeAnalysis.RefKind.Ref
                    : !IsSupportedRefKind(method.Parameters[0].RefKind))
                || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contextType))
            {
                return false;
            }

            index++;
        }

        if (hasEntity)
        {
            if (method.Parameters.Length <= index
                || method.Parameters[index].RefKind != Microsoft.CodeAnalysis.RefKind.None
                || !GeneratorSupport.IsEntityType(method.Parameters[index].Type))
            {
                return false;
            }

            index++;
        }

        return method.Parameters.Length >= index;
    }

    internal static bool HasAccessibleLambdaReferences(
        SemanticModel model,
        LambdaExpressionSyntax lambda,
        out string? reason)
    {
        foreach (ParameterSyntax parameter in LambdaParameters(lambda))
        {
            if (model.GetDeclaredSymbol(parameter) is IParameterSymbol symbol
                && !GeneratorSupport.IsAccessibleSymbol(symbol.Type))
            {
                reason = "a lambda parameter type is not accessible to generated callback code";
                return false;
            }
        }

        foreach (SyntaxNode node in lambda.Body.DescendantNodesAndSelf())
        {
            ISymbol? symbol = model.GetSymbolInfo(node).Symbol;
            if (symbol is null or IParameterSymbol or IRangeVariableSymbol)
            {
                continue;
            }

            if (symbol is ILocalSymbol)
            {
                reason = "the lambda body references a local or local constant";
                return false;
            }

            reason = symbol is ITypeParameterSymbol
                ? "the lambda body references a type parameter"
                : !GeneratorSupport.IsAccessibleSymbol(symbol)
                    ? "the lambda body references a private or protected symbol"
                    : null;
            if (reason is not null)
            {
                return false;
            }
        }

        for (INamedTypeSymbol? type = model.GetEnclosingSymbol(lambda.SpanStart)?.ContainingType;
             type is not null;
             type = type.ContainingType)
        {
            if (type.Arity != 0)
            {
                reason = "generic containing types are not supported by the generated callback";
                return false;
            }
        }

        if (model.GetEnclosingSymbol(lambda.SpanStart) is IMethodSymbol { IsGenericMethod: true })
        {
            reason = "generic containing methods are not supported by the generated callback";
            return false;
        }

        reason = null;
        return true;
    }

    internal static bool HasTypedAccessibleParameter(SemanticModel model, ParameterSyntax parameter)
    {
        ITypeSymbol? type = parameter.Type is null ? null : model.GetTypeInfo(parameter.Type).Type;
        return type is not null
            && type.TypeKind != TypeKind.Error
            && type is not ITypeParameterSymbol
            && GeneratorSupport.IsAccessibleSymbol(type)
            && (!parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.InKeyword))
                || !parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.RefKeyword)
                    || modifier.IsKind(SyntaxKind.ReadOnlyKeyword)));
    }
}
