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
    internal static ITypeSymbol[] GenericArgumentTypes(SemanticModel model, GenericNameSyntax? genericName)
        => genericName?.TypeArgumentList.Arguments
            .Select(argument => model.GetTypeInfo(argument).Type)
            .OfType<ITypeSymbol>()
            .ToArray()
            ?? Array.Empty<ITypeSymbol>();

    private const int FirstInterceptorLanguageVersion = 1100;
    private const int FirstScopedLanguageVersion = 1100;
    private const string InterceptorNamespace = "Delta.ECS.Generated";
    // RefKind.RefReadOnlyParameter is not available in the oldest Roslyn API
    // referenced by the generator, so keep the host enum value here.
    private const int RefReadOnlyParameterValue = 4;
    internal const string EcsNamespace = "Delta.ECS";
    internal const string SystemNamespace = "System";

    internal static string ContainingNamespace(SemanticModel model, SyntaxNode node)
        => model.GetEnclosingSymbol(node.SpanStart)?.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
            ? containingNamespace.ToDisplayString()
            : string.Empty;

    internal static ImmutableArray<string> EcsNamespaceUsings(string namespaceName)
        => string.Equals(namespaceName, EcsNamespace, StringComparison.Ordinal)
            ? ImmutableArray<string>.Empty
            : ImmutableArray.Create("using global::Delta.ECS;");

    internal static string QualifiedName(string namespaceName, string typeName) => namespaceName.Length == 0 ? "global::" + typeName : "global::" + namespaceName + "." + typeName;

    internal static bool IsInterceptionEnabled(AnalyzerConfigOptions options)
        => options.TryGetValue("build_property.InterceptorsNamespaces", out string? namespaces)
            && namespaces
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(static value => string.Equals(value.Trim(), InterceptorNamespace, StringComparison.Ordinal));

    internal static bool SupportsInterceptors(Compilation compilation)
        => compilation.SyntaxTrees.FirstOrDefault()?.Options is CSharpParseOptions options
            && (int)options.LanguageVersion >= FirstInterceptorLanguageVersion;

    internal static bool SupportsScoped(Compilation compilation)
        => compilation.SyntaxTrees.FirstOrDefault()?.Options is CSharpParseOptions options
            && (int)options.LanguageVersion >= FirstScopedLanguageVersion;

    internal static bool TryGetInterceptionLocation(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        out string data,
        out string attributeSyntax)
    {
        data = string.Empty;
        attributeSyntax = string.Empty;
        MethodInfo? getLocation = FindExtensionMethod(
            "GetInterceptableLocation",
            static parameters => parameters.Length == 3
                && parameters[0].ParameterType == typeof(SemanticModel)
                && parameters[1].ParameterType == typeof(InvocationExpressionSyntax));
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

        MethodInfo? getAttribute = FindExtensionMethod(
            "GetInterceptsLocationAttributeSyntax",
            static parameters => parameters.Length == 1);
        if (getAttribute is null)
        {
            return false;
        }

        attributeSyntax = getAttribute.Invoke(null, new[] { location })?.ToString() ?? string.Empty;
        return attributeSyntax.Length > 0;
    }

    private static MethodInfo? FindExtensionMethod(string name, Func<ParameterInfo[], bool> matches)
        => typeof(Microsoft.CodeAnalysis.CSharp.CSharpExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method => method.Name == name && matches(method.GetParameters()));

    internal static IncrementalValuesProvider<InvocationCandidate> InvocationProvider(
        IncrementalGeneratorInitializationContext context,
        Func<SyntaxNode, bool>? candidateFilter = null)
        => context.SyntaxProvider.CreateSyntaxProvider(
            (node, _) => node is InvocationExpressionSyntax invocation
                && invocation.Expression is MemberAccessExpressionSyntax member
                && IsGeneratedApiName(member.Name.Identifier.ValueText)
                && (candidateFilter is null || candidateFilter(node)),
            static (syntaxContext, _) => new InvocationCandidate((InvocationExpressionSyntax)syntaxContext.Node));

    internal static bool IsGeneratedApiName(string name) => ApiDescriptor.TryGet(name, out _);

    internal static bool IsGeneratedSourcePath(string path) => path.EndsWith("ForEach.g.cs", StringComparison.Ordinal)
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
        string genericPrefix,
        AccessKind? accessOverride = null)
        => Enumerable.Range(0, pattern.Length)
            .Select(index =>
            {
                string genericType = genericPrefix + (index + 1).ToString(CultureInfo.InvariantCulture);
                string typeName = isFunctor ? components[index] : genericType;
                return new ComponentModel(
                    typeName,
                    accessOverride ?? AccessKindFrom(pattern[index]),
                    ResolvedTypeName: typeName);
            })
            .ToImmutableArray();

    internal static ImmutableArray<InvocationCandidate> ExcludeGenerated(ImmutableArray<InvocationCandidate> invocations)
        => invocations.IsDefaultOrEmpty
            ? ImmutableArray<InvocationCandidate>.Empty
            : invocations
                .Where(static candidate => !IsGeneratedSourcePath(candidate.Invocation.SyntaxTree.FilePath))
                .ToImmutableArray();

    internal static bool IsNamedType(ITypeSymbol? type, string name) => type is INamedTypeSymbol named
            && named.Name == name
            && named.ContainingNamespace.ToDisplayString() == EcsNamespace;

    internal static bool IsWorldReceiver(SemanticModel model, ExpressionSyntax expression) => IsReceiver(model, expression, "World");

    internal static bool IsQueryReceiver(SemanticModel model, ExpressionSyntax expression) => IsReceiver(model, expression, "Query", allowWorld: true);

    internal static bool IsQuerySpecReceiver(SemanticModel model, ExpressionSyntax expression) => IsReceiver(model, expression, "QuerySpec");

    private static bool IsReceiver(SemanticModel model, ExpressionSyntax expression, string typeName, bool allowWorld = false)
    {
        if (IsNamedType(model.GetTypeInfo(expression).Type, typeName))
        {
            return true;
        }

        if (expression is InvocationExpressionSyntax invocation
            && invocation.Expression is MemberAccessExpressionSyntax member
            && member.Name.Identifier.ValueText is "WhereAll" or "WhereAny" or "WhereNone")
        {
            return allowWorld && IsWorldReceiver(model, member.Expression)
                || IsReceiver(model, member.Expression, typeName, allowWorld);
        }

        if (expression is IdentifierNameSyntax identifier
            && model.GetSymbolInfo(identifier).Symbol is ILocalSymbol local
            && local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is VariableDeclaratorSyntax declarator
            && declarator.Initializer?.Value is ExpressionSyntax initializer)
        {
            return IsReceiver(model, initializer, typeName, allowWorld);
        }

        return false;
    }

    internal static bool IsEcsType(ITypeSymbol? type, string name) => IsNamedType(type, name);

    internal static bool IsEntityType(ITypeSymbol? type) => IsNamedType(type, "Entity");

    internal static bool IsEntityRefType(ITypeSymbol? type) => IsNamedType(type, "EntityRef");

    internal static bool IsComponentId(ITypeSymbol? type) => IsNamedType(type, "ComponentId");

    internal static bool HasGenericTypeInChain(INamedTypeSymbol type, bool includeSelf)
    {
        for (INamedTypeSymbol? current = includeSelf ? type : type.ContainingType;
             current is not null;
             current = current.ContainingType)
        {
            if (current.Arity != 0)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsInt32(ITypeSymbol? type) => type?.SpecialType == SpecialType.System_Int32;

    internal static bool IsStampType(ITypeSymbol? type) => IsNamedType(type, "Stamp");

    internal static bool IsEntityBatch(ITypeSymbol? type) => IsBatch(type, "Entity", allowArray: true, allowReadOnlySpan: true);

    internal static bool IsEntityOutput(ITypeSymbol? type) => IsBatch(type, "Entity", allowArray: false, allowReadOnlySpan: false);

    internal static bool IsComponentIdBatch(ITypeSymbol? type) => IsBatch(type, "ComponentId", allowArray: true, allowReadOnlySpan: true);

    private static bool IsBatch(ITypeSymbol? type, string elementName, bool allowArray, bool allowReadOnlySpan)
    {
        if (allowArray && type is IArrayTypeSymbol array)
        {
            return IsEcsType(array.ElementType, elementName);
        }

        return type is INamedTypeSymbol named
            && named.TypeArguments.Length == 1
            && IsEcsType(named.TypeArguments[0], elementName)
            && (named.Name == "Span" || allowReadOnlySpan && named.Name == "ReadOnlySpan")
            && named.ContainingNamespace.ToDisplayString() == SystemNamespace;
    }

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

    internal static ImmutableArray<ComponentModel> ComponentModels(int arity, AccessKind access, string prefix = "T") => ComponentModels(new string('V', arity), Array.Empty<string>(), false, prefix, access);

    internal static AccessKind AccessKindFrom(char pattern) => pattern switch
    {
        'W' => AccessKind.RowWrite,
        'R' => AccessKind.RefReadonly,
        'S' => AccessKind.StampRead,
        'I' => AccessKind.RowRead,
        _ => AccessKind.Value
    };

    internal static char PatternLetter(RefKind refKind) => refKind switch
    {
        RefKind.In => 'I',
        RefKind.Ref => 'W',
        _ when IsRefReadonly(refKind) => 'R',
        _ => 'V'
    };

    internal static bool IsRefReadonly(RefKind refKind) => (int)refKind == RefReadOnlyParameterValue;

    internal static char PatternLetter(ParameterSyntax parameter)
    {
        bool hasRef = parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.RefKeyword));
        bool hasReadonly = parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword));
        return hasRef
            ? hasReadonly ? 'R' : 'W'
            : parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.InKeyword)) ? 'I' : 'V';
    }

    internal static bool IsAccessibleType(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => IsAccessibleType(array.ElementType),
        IPointerTypeSymbol pointer => IsAccessibleType(pointer.PointedAtType),
        INamedTypeSymbol named => named.DeclaredAccessibility is (Accessibility.Public or Accessibility.Internal)
            && (named.ContainingType is null || IsAccessibleType(named.ContainingType))
            && named.TypeArguments.All(IsAccessibleType),
        _ => true
    };

    internal static bool ContainsTypeParameter(ITypeSymbol type) => type is ITypeParameterSymbol
            || type is INamedTypeSymbol named && named.TypeArguments.Any(ContainsTypeParameter)
            || type is IArrayTypeSymbol array && ContainsTypeParameter(array.ElementType);

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
        return result.Concat(types
                .OfType<INamedTypeSymbol>()
                .Where(static type => !type.ContainingNamespace.IsGlobalNamespace)
                .Select(static type => "using global::" + type.ContainingNamespace.ToDisplayString() + ";")
                .Where(directive => seen.Add(directive.TrimEnd(';').Trim())))
            .ToArray();
    }

    internal static bool TryGetInterceptionUsings(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        IEnumerable<LambdaExpressionSyntax?> callbacks,
        IEnumerable<IMethodSymbol?> methods,
        bool includeSourceUsings,
        bool includeEnclosingUsings,
        bool rejectInaccessibleContainingType,
        out string[] usings)
    {
        var result = includeSourceUsings
            ? invocation.SyntaxTree.GetRoot().DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Select(static directive => directive.ToString())
                .ToList()
            : new List<string>();
        var closedTypes = new List<ITypeSymbol?>();
        ISymbol? enclosing = model.GetEnclosingSymbol(invocation.SpanStart);
        if (includeEnclosingUsings && enclosing is not null)
        {
            string namespaceName = enclosing.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (namespaceName.Length != 0)
            {
                result.Add("using global::" + namespaceName + ";");
            }

            if (enclosing.ContainingType is { Arity: 0 } containingType)
            {
                if (!IsAccessibleSymbol(containingType) && rejectInaccessibleContainingType)
                {
                    usings = Array.Empty<string>();
                    return false;
                }

                if (IsAccessibleSymbol(containingType))
                {
                    result.Add("using static " + containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ";");
                }
            }
        }

        closedTypes.AddRange(callbacks.OfType<LambdaExpressionSyntax>()
            .SelectMany(callback => ClosedTypesFromLambda(model, callback)));
        closedTypes.AddRange(methods.OfType<IMethodSymbol>()
            .SelectMany(static method => new[] { method.ContainingType }
                .Concat(method.Parameters.Select(static parameter => parameter.Type))));
        closedTypes.AddRange(invocation.ArgumentList.Arguments
            .Select(argument => model.GetTypeInfo(argument.Expression).Type));
        if ((invocation.Expression as MemberAccessExpressionSyntax)?.Name is GenericNameSyntax genericName)
        {
            closedTypes.AddRange(genericName.TypeArgumentList.Arguments
                .Select(argument => model.GetTypeInfo(argument).Type));
        }

        usings = AppendNamespaceUsings(result.Distinct(StringComparer.Ordinal), closedTypes);
        return true;
    }

    internal static IEnumerable<ITypeSymbol?> ClosedTypesFromLambda(SemanticModel model, LambdaExpressionSyntax lambda)
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
        string methodName,
        ContextModeKind functorPassMode = ContextModeKind.Ref)
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
            functorType,
            isFunctor ? functorPassMode : ContextModeKind.None);
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
            Name: methodName);
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
