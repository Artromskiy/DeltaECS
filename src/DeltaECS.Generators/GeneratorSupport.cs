using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Delta.ECS.Generators;

internal static class GeneratorSupport
{
    private const int FirstInterceptorLanguageVersion = 1100;
    private const string InterceptorNamespace = "Delta.ECS.Generated";

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

    internal static StringBuilder CreateSource(int capacity, string header)
    {
        var source = new StringBuilder(capacity);
        source.Append(header);
        return source;
    }

    internal static string FinishSource(StringBuilder source)
        => GeneratedSourceFormatter.Format(source.ToString());

    internal static IncrementalValuesProvider<InvocationCandidate> InvocationProvider(
        IncrementalGeneratorInitializationContext context)
        => context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is InvocationExpressionSyntax invocation
                && invocation.Expression is MemberAccessExpressionSyntax member
                && IsGeneratedApiName(member.Name.Identifier.ValueText),
            static (syntaxContext, _) => new InvocationCandidate((InvocationExpressionSyntax)syntaxContext.Node));

    internal static bool IsGeneratedApiName(string name)
        => ApiKind(name) != GeneratedApiKind.Unknown;

    internal static bool IsGeneratedSourcePath(string path)
        => path.EndsWith("ForEach.g.cs", StringComparison.Ordinal)
            || path.Contains("DemandForEach_", StringComparison.Ordinal)
            || path.Contains("GeneratedQuery_", StringComparison.Ordinal)
            || path.Contains("GeneratedStructural_", StringComparison.Ordinal)
            || path.Contains("GeneratedWhere_", StringComparison.Ordinal)
            || path.Contains("GeneratedWhereInterceptor_", StringComparison.Ordinal);

    internal static GeneratedApiKind ApiKind(string name)
        => name switch
        {
            "Add" or "Create" or "Destroy" or "Remove" or "Set" => GeneratedApiKind.Structural,
            "WhereAll" or "WhereAny" or "WhereNone" => GeneratedApiKind.QueryFactory,
            "Where" or "WhereEntity" => GeneratedApiKind.Where,
            "ForEach" or "ForEachEntity"
                or "ForEachParallel" or "ForEachEntityParallel"
                or "ForEachStamp" or "ForEachEntityStamp"
                or "ForEachStampParallel" or "ForEachEntityStampParallel" => GeneratedApiKind.Iteration,
            _ => GeneratedApiKind.Unknown
        };

    internal static bool IsIterationName(string name)
        => ApiKind(name) == GeneratedApiKind.Iteration;

    internal static bool IsParallelIterationName(string name)
        => name is "ForEachParallel" or "ForEachEntityParallel"
            or "ForEachStampParallel" or "ForEachEntityStampParallel";

    internal static bool IsEntityIterationName(string name)
        => name is "ForEachEntity" or "ForEachEntityParallel"
            or "ForEachEntityStamp" or "ForEachEntityStampParallel";

    internal static bool IsStampIterationName(string name)
        => name is "ForEachStamp" or "ForEachEntityStamp"
            or "ForEachStampParallel" or "ForEachEntityStampParallel";

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
            && named.ContainingNamespace.ToDisplayString() == "Delta.ECS";

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
            && named.ContainingNamespace.ToDisplayString() == "System";
    }

    internal static bool IsEntityOutput(ITypeSymbol? type)
        => type is INamedTypeSymbol named
            && named.Name == "Span"
            && named.TypeArguments.Length == 1
            && IsEntityType(named.TypeArguments[0])
            && named.ContainingNamespace.ToDisplayString() == "System";

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

    internal static string GenericTypes(int arity, string prefix = "T")
    {
        var values = new string[arity];
        for (int index = 0; index < arity; index++)
        {
            values[index] = prefix + (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        return string.Join(", ", values);
    }

    internal static string GenericList(int arity, string prefix = "T")
        => GenericTypes(arity, prefix);

    internal static ImmutableArray<ComponentSlot> ComponentSlots(
        int arity,
        SelectorKind selector,
        AccessKind access,
        string prefix = "T")
    {
        var slots = ImmutableArray.CreateBuilder<ComponentSlot>(arity);
        for (int index = 0; index < arity; index++)
        {
            slots.Add(new ComponentSlot(
                index,
                prefix + (index + 1).ToString(CultureInfo.InvariantCulture),
                selector,
                access));
        }

        return slots.ToImmutable();
    }

    internal static AccessKind AccessKindFrom(char pattern)
        => pattern switch
        {
            'W' => AccessKind.RowWrite,
            'R' => AccessKind.RefReadonly,
            'S' => AccessKind.StampRead,
            _ => AccessKind.Value
        };

    internal static char PatternLetter(RefKind refKind)
        => refKind switch
        {
            RefKind.In => 'I',
            RefKind.Ref => 'W',
            (RefKind)4 or (RefKind)5 => 'R',
            _ => 'V'
        };

    internal static char PatternLetter(ParameterSyntax parameter)
    {
        bool hasRef = parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.RefKeyword));
        bool hasReadonly = parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword));
        return hasRef
            ? hasReadonly ? 'R' : 'W'
            : parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.InKeyword)) ? 'I' : 'V';
    }

    internal static bool IsWrite(char mode) => mode == 'W';

    internal static string ParameterPrefix(char mode)
        => mode switch
        {
            'R' => "ref readonly ",
            'W' => "ref ",
            'I' => "in ",
            _ => string.Empty
        };

    internal static string InvocationPrefix(char mode)
        => mode is 'R' or 'I' ? "in " : mode == 'W' ? "ref " : string.Empty;

    internal static string GenericParameters(int arity, string prefix = "T")
        => arity == 0 ? string.Empty : "<" + GenericTypes(arity, prefix) + ">";

    internal static string GenericParameters(string generic)
        => string.IsNullOrEmpty(generic) ? string.Empty : "<" + generic + ">";

    internal static string JoinIndexed(int count, Func<int, string> format)
    {
        var values = new string[count];
        for (int index = 0; index < count; index++)
        {
            values[index] = format(index);
        }

        return string.Join(", ", values);
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

    internal static ApiShape CreateIterationShape(
        string receiver,
        bool isStamp,
        bool parallel,
        bool hasEntity,
        bool hasEntityTarget,
        bool hasQuery,
        bool explicitIds,
        bool genericSelectors,
        bool isFunctor,
        bool hasContext,
        bool implicitComponents,
        ContextModeKind contextMode,
        string pattern,
        string[] components,
        string? functorType,
        string? contextType,
        string methodName)
    {
        TargetKind target = hasEntityTarget
            ? TargetKind.Entity
            : receiver == "World" ? TargetKind.World : TargetKind.EntityList;
        SelectorKind selectorKind = explicitIds
            ? SelectorKind.ComponentIds
            : genericSelectors ? SelectorKind.Generic : SelectorKind.Inferred;
        var slots = ImmutableArray.CreateBuilder<ComponentSlot>(components.Length);
        for (int index = 0; index < components.Length; index++)
        {
            slots.Add(new ComponentSlot(
                index,
                components[index],
                selectorKind,
                isStamp
                    ? AccessKind.StampRead
                    : AccessKindFrom(index < pattern.Length ? pattern[index] : 'I')));
        }

        ContextModeKind mode = hasContext ? contextMode : ContextModeKind.None;
        var callback = new CallbackSpec(
            isFunctor ? CallbackSource.Functor : CallbackSource.Lambda,
            hasEntity,
            functorType);
        return new ApiShape(
            isStamp ? OperationKind.StampIteration : OperationKind.Iteration,
            target,
            hasQuery ? (target == TargetKind.EntityList ? QueryMode.Optional : QueryMode.Required) : QueryMode.None,
            new SelectorSpec(selectorKind, slots.ToImmutable()),
            new ContextSpec(hasContext ? mode : ContextModeKind.None, contextType),
            callback,
            new ExecutionSpec(
                parallel ? ExecutionKind.Parallel : target == TargetKind.EntityList ? ExecutionKind.EntityList : ExecutionKind.Dense,
                isStamp ? ValueKind.Stamp : ValueKind.Component),
            name: methodName + "|" + (!implicitComponents ? "Explicit" : "Implicit"),
            pattern: pattern);
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
