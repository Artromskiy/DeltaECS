using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Delta.ECS.Generators;

internal static class GeneratorSupport
{
    private const int FirstInterceptorLanguageVersion = 1100;
    private const string InterceptorNamespace = "Delta.ECS.Generated";
    // RefKind.RefReadOnlyParameter is not available in the oldest Roslyn API
    // referenced by the generator, so keep the host enum value here.
    private const int RefReadOnlyParameterValue = 4;
    internal const string EcsNamespace = "Delta.ECS";
    internal const string SystemNamespace = "System";

    internal static bool IsInterceptionEnabled(AnalyzerConfigOptions options)
        => options.TryGetValue("build_property.InterceptorsNamespaces", out string? namespaces)
            && namespaces
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(static value => string.Equals(value.Trim(), InterceptorNamespace, StringComparison.Ordinal));

    internal static bool SupportsInterceptors(Compilation compilation)
        => compilation.SyntaxTrees.FirstOrDefault()?.Options is CSharpParseOptions options
            && (int)options.LanguageVersion >= FirstInterceptorLanguageVersion;

    internal static bool TryGetInterceptionLocation(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        out string data,
        out string attributeSyntax)
    {
        data = string.Empty;
        attributeSyntax = string.Empty;
        MethodInfo? getLocation = typeof(Microsoft.CodeAnalysis.CSharp.CSharpExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(static method =>
            {
                if (method.Name != "GetInterceptableLocation")
                {
                    return false;
                }

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 3
                    && parameters[0].ParameterType == typeof(SemanticModel)
                    && parameters[1].ParameterType == typeof(InvocationExpressionSyntax);
            });
        if (getLocation is null)
        {
            return false;
        }

        object? location = getLocation.Invoke(null, new object?[] { model, invocation, default });
        if (location is null)
        {
            return false;
        }

        data = location.GetType().GetProperty("Data")?.GetValue(location) as string ?? string.Empty;
        if (data.Length == 0)
        {
            return false;
        }

        MethodInfo? getAttribute = typeof(Microsoft.CodeAnalysis.CSharp.CSharpExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(static method => method.Name == "GetInterceptsLocationAttributeSyntax"
                && method.GetParameters().Length == 1);
        if (getAttribute is null)
        {
            return false;
        }

        attributeSyntax = getAttribute.Invoke(null, new[] { location })?.ToString() ?? string.Empty;
        return attributeSyntax.Length > 0;
    }

    internal static IncrementalValuesProvider<InvocationCandidate> InvocationProvider(
        IncrementalGeneratorInitializationContext context)
        => context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is InvocationExpressionSyntax invocation
                && invocation.Expression is MemberAccessExpressionSyntax member
                && IsGeneratedApiName(member.Name.Identifier.ValueText),
            static (syntaxContext, _) => new InvocationCandidate((InvocationExpressionSyntax)syntaxContext.Node));

    internal static bool IsGeneratedApiName(string name)
        => ApiDescriptor.TryGet(name, out _);

    internal static bool IsGeneratedSourcePath(string path)
        => path.EndsWith("ForEach.g.cs", StringComparison.Ordinal)
            || path.Contains("DemandForEach_", StringComparison.Ordinal)
            || path.Contains("GeneratedQuery_", StringComparison.Ordinal)
            || path.Contains("GeneratedStructural_", StringComparison.Ordinal)
            || path.Contains("GeneratedWhere_", StringComparison.Ordinal)
            || path.Contains("GeneratedWhereInterceptor_", StringComparison.Ordinal)
            || path.Contains("GeneratedSystemAccess_", StringComparison.Ordinal);

    internal static ImmutableArray<ComponentModel> ComponentModels(
        string pattern,
        string[] components,
        bool isFunctor,
        string genericPrefix)
        => Enumerable.Range(0, pattern.Length)
            .Select(index =>
            {
                string genericType = genericPrefix + (index + 1).ToString(CultureInfo.InvariantCulture);
                string typeName = isFunctor ? components[index] : genericType;
                return new ComponentModel(
                    typeName,
                    AccessKindFrom(pattern[index]),
                    resolvedTypeName: typeName);
            })
            .ToImmutableArray();

    internal static ImmutableArray<InvocationCandidate> ExcludeGenerated(
        ImmutableArray<InvocationCandidate> invocations)
    {
        if (invocations.IsDefaultOrEmpty)
        {
            return ImmutableArray<InvocationCandidate>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<InvocationCandidate>(invocations.Length);
        foreach (InvocationCandidate candidate in invocations)
        {
            string path = candidate.Invocation.SyntaxTree.FilePath;
            if (IsGeneratedSourcePath(path))
            {
                continue;
            }

            builder.Add(candidate);
        }

        return builder.ToImmutable();
    }

    internal static bool IsNamedType(ITypeSymbol? type, string name)
        => type is INamedTypeSymbol named
            && named.Name == name
            && named.ContainingNamespace.ToDisplayString() == EcsNamespace;

    internal static bool IsEcsType(ITypeSymbol? type, string name)
        => IsNamedType(type, name);

    internal static bool IsEntityType(ITypeSymbol? type)
        => IsNamedType(type, "Entity");

    internal static bool IsComponentId(ITypeSymbol? type)
        => IsNamedType(type, "ComponentId");

    internal static bool IsInt32(ITypeSymbol? type)
        => type?.SpecialType == SpecialType.System_Int32;

    internal static bool IsStampType(ITypeSymbol? type)
        => IsNamedType(type, "Stamp");

    internal static bool IsEntityBatch(ITypeSymbol? type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return IsEntityType(array.ElementType);
        }

        return type is INamedTypeSymbol named
            && named.TypeArguments.Length == 1
            && IsEntityType(named.TypeArguments[0])
            && named.Name is "Span" or "ReadOnlySpan"
            && named.ContainingNamespace.ToDisplayString() == SystemNamespace;
    }

    internal static bool IsEntityOutput(ITypeSymbol? type)
        => type is INamedTypeSymbol named
            && named.Name == "Span"
            && named.TypeArguments.Length == 1
            && IsEntityType(named.TypeArguments[0])
            && named.ContainingNamespace.ToDisplayString() == SystemNamespace;

    internal static string DisplayType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsTupleType: true } tuple)
        {
            return "(" + string.Join(
                ", ",
                tuple.TupleElements.Select(static element => DisplayType(element.Type))) + ")";
        }

        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    internal static ImmutableArray<ComponentModel> ComponentModels(
        int arity,
        AccessKind access,
        string prefix = "T")
    {
        var slots = ImmutableArray.CreateBuilder<ComponentModel>(arity);
        for (int index = 0; index < arity; index++)
        {
            slots.Add(new ComponentModel(
                prefix + (index + 1).ToString(CultureInfo.InvariantCulture),
                access,
                resolvedTypeName: prefix + (index + 1).ToString(CultureInfo.InvariantCulture)));
        }

        return slots.ToImmutable();
    }

    internal static AccessKind AccessKindFrom(char pattern)
        => pattern switch
        {
            'W' => AccessKind.RowWrite,
            'R' => AccessKind.RefReadonly,
            'S' => AccessKind.StampRead,
            'I' => AccessKind.RowRead,
            _ => AccessKind.Value
        };

    internal static char PatternLetter(RefKind refKind)
        => refKind switch
        {
            RefKind.In => 'I',
            RefKind.Ref => 'W',
            _ when IsRefReadonly(refKind) => 'R',
            _ => 'V'
        };

    internal static bool IsRefReadonly(RefKind refKind)
        => (int)refKind == RefReadOnlyParameterValue;

    internal static char PatternLetter(ParameterSyntax parameter)
    {
        bool hasRef = parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.RefKeyword));
        bool hasReadonly = parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword));
        return hasRef
            ? hasReadonly ? 'R' : 'W'
            : parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.InKeyword)) ? 'I' : 'V';
    }

    internal static bool IsAccessibleType(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return IsAccessibleType(array.ElementType);
            case IPointerTypeSymbol pointer:
                return IsAccessibleType(pointer.PointedAtType);
            case INamedTypeSymbol named:
                if (named.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)
                    || (named.ContainingType is not null && !IsAccessibleType(named.ContainingType)))
                {
                    return false;
                }

                return named.TypeArguments.All(IsAccessibleType);
            default:
                return true;
        }
    }

    internal static bool IsAccessibleSymbol(ISymbol symbol)
    {
        if (symbol.DeclaredAccessibility is Accessibility.Private
            or Accessibility.Protected
            or Accessibility.ProtectedAndInternal)
        {
            return false;
        }

        for (INamedTypeSymbol? type = symbol.ContainingType; type is not null; type = type.ContainingType)
        {
            if (type.DeclaredAccessibility is Accessibility.Private
                or Accessibility.Protected
                or Accessibility.ProtectedAndInternal)
            {
                return false;
            }
        }

        return symbol is not ITypeSymbol typeSymbol || IsAccessibleType(typeSymbol);
    }

    /// <summary>
    /// Adds <c>using global::…;</c> directives for the containing namespaces of closed types.
    /// Nested component namespaces are not imported by a parent-namespace using alone.
    /// </summary>
    internal static string[] AppendNamespaceUsings(IEnumerable<string> usings, IEnumerable<ITypeSymbol?> types)
    {
        var result = new List<string>(usings);
        var seen = new HashSet<string>(result.Select(static value => value.TrimEnd(';').Trim()), StringComparer.Ordinal);
        foreach (ITypeSymbol? type in types)
        {
            if (type is not INamedTypeSymbol { ContainingNamespace: { IsGlobalNamespace: false } ns })
            {
                continue;
            }

            string directive = "using global::" + ns.ToDisplayString() + ";";
            string key = directive.TrimEnd(';').Trim();
            if (seen.Add(key))
            {
                result.Add(directive);
            }
        }

        return result.ToArray();
    }

    internal static IEnumerable<ITypeSymbol?> ClosedTypesFromLambda(
        SemanticModel model,
        LambdaExpressionSyntax lambda)
    {
        foreach (ParameterSyntax parameter in CallbackReader.LambdaParameters(lambda))
        {
            if (model.GetDeclaredSymbol(parameter) is IParameterSymbol symbol)
            {
                yield return symbol.Type;
            }
            else if (parameter.Type is not null)
            {
                yield return model.GetTypeInfo(parameter.Type).Type;
            }
        }
    }

    internal static ApiModel CreateIterationShape(
        bool isStamp,
        bool parallel,
        bool hasEntity,
        bool hasEntityTarget,
        bool hasQuery,
        RegistrationBindingKind registrationBinding,
        TypeBindingKind typeBinding,
        bool isFunctor,
        bool hasContext,
        ContextModeKind contextMode,
        string pattern,
        string[] components,
        string? functorType,
        string? contextType,
        string methodName)
    {
        TargetKind target = hasEntityTarget
            ? TargetKind.EntityList
            : TargetKind.World;
        var slots = ImmutableArray.CreateBuilder<ComponentModel>(components.Length);
        for (int index = 0; index < components.Length; index++)
        {
            slots.Add(new ComponentModel(
                typeBinding == TypeBindingKind.Generic
                    ? "T" + (index + 1).ToString(CultureInfo.InvariantCulture)
                    : components[index],
                isStamp
                    ? AccessKind.StampRead
                    : AccessKindFrom(index < pattern.Length ? pattern[index] : 'I'),
                components[index]));
        }

        ContextModeKind mode = hasContext ? contextMode : ContextModeKind.None;
        var callback = new CallbackModel(
            isFunctor ? CallbackSource.Functor : CallbackSource.Lambda,
            hasEntity,
            functorType);
        return new ApiModel(
            OperationKind.Iteration,
            target,
            hasQuery ? (target == TargetKind.EntityList ? QueryMode.Optional : QueryMode.Required) : QueryMode.None,
            new SelectorModel(typeBinding, registrationBinding, slots.ToImmutable()),
            new ContextModel(hasContext ? mode : ContextModeKind.None, contextType),
            callback,
            new ExecutionModel(
                target == TargetKind.EntityList ? Scope.EntityList : Scope.QueryWide,
                isStamp ? ValueDomain.Stamp : ValueDomain.Component,
                parallel ? Schedule.Parallel : Schedule.Sequential),
            name: methodName);
    }

    internal static string StableName(string value)
    {
        unchecked
        {
            // Keep generated identifiers stable while making collisions
            // effectively negligible for independently generated shapes.
            ulong hash = 14695981039346656037UL;
            foreach (char character in value)
            {
                hash = (hash ^ character) * 1099511628211UL;
            }

            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }
    }
}
