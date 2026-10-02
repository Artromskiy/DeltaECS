using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>Emits direct closed-type factories for runtime-selected generic API calls.</summary>
[Generator]
public sealed class GeneratedGenericBindingsGenerator : IIncrementalGenerator
{
    private static readonly HashSet<string> FunctorMethods = new(StringComparer.Ordinal)
    {
        "ForEach", "ForEachEntity", "ForEachParallel", "ForEachEntityParallel",
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var discoveries = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax,
                static (syntax, _) => ReadDiscovery(syntax))
            .Where(static discovery => discovery is not null)
            .Select(static (discovery, _) => discovery!)
            .Collect();

        context.RegisterSourceOutput(discoveries.Combine(context.CompilationProvider), static (output, input) =>
        {
            ImmutableArray<GenericBindingDiscovery> discovered = input.Left;
            if (discovered.IsDefaultOrEmpty)
            {
                return;
            }

            var componentTypes = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);
            var componentDefinitions = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
            var componentTuples = new Dictionary<string, Dictionary<string, ImmutableArray<ITypeSymbol>>>(StringComparer.Ordinal);
            var unresolvedComponentDefinitions = new HashSet<string>(StringComparer.Ordinal);
            var functorDefinitions = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
            var functorTuples = new Dictionary<string, Dictionary<string, ImmutableArray<ITypeSymbol>>>(StringComparer.Ordinal);
            var unresolvedFunctors = new HashSet<string>(StringComparer.Ordinal);

            foreach (GenericBindingDiscovery discovery in discovered)
            {
                if (discovery.RegisteredComponentType is ITypeSymbol registeredType && IsClosedType(registeredType)
                    && GeneratorSupport.IsAccessibleSymbol(registeredType))
                {
                    AddIfMissing(componentTypes, GeneratorSupport.DisplayType(registeredType), registeredType);
                }

                if (discovery.GenericComponentDefinition is INamedTypeSymbol componentDefinition
                    && GeneratorSupport.IsAccessibleSymbol(componentDefinition))
                {
                    string definitionKey = GeneratorSupport.DisplayType(componentDefinition.ConstructUnboundGenericType());
                    AddIfMissing(componentDefinitions, definitionKey, componentDefinition);
                    if (!discovery.GenericArguments.IsDefaultOrEmpty)
                    {
                        ImmutableArray<ITypeSymbol> tuple = discovery.GenericArguments;
                        AddTypes(componentTypes, tuple);
                        AddTuple(componentTuples, definitionKey, tuple);
                    }
                    else
                    {
                        unresolvedComponentDefinitions.Add(definitionKey);
                    }
                }

                if (discovery.GenericFunctorDefinition is INamedTypeSymbol functorDefinition)
                {
                    string definitionKey = GeneratorSupport.DisplayType(functorDefinition.ConstructUnboundGenericType());
                    AddIfMissing(functorDefinitions, definitionKey, functorDefinition);
                    if (!discovery.GenericArguments.IsDefaultOrEmpty)
                    {
                        ImmutableArray<ITypeSymbol> tuple = discovery.GenericArguments;
                        AddTypes(componentTypes, tuple);
                        AddTuple(functorTuples, definitionKey, tuple);
                    }
                    else
                    {
                        unresolvedFunctors.Add(definitionKey);
                    }
                }
            }

            var componentBindings = new List<GenericComponentFactoryBinding>();
            foreach (KeyValuePair<string, INamedTypeSymbol> definitionEntry in componentDefinitions)
            {
                string definitionKey = definitionEntry.Key;
                INamedTypeSymbol definition = definitionEntry.Value;
                IEnumerable<ImmutableArray<ITypeSymbol>> tuples = definition.Arity == 1 && unresolvedComponentDefinitions.Contains(definitionKey)
                    ? componentTypes.Values.Select(static type => ImmutableArray.Create(type))
                        .Concat(GetTuples(componentTuples, definitionKey))
                    : GetTuples(componentTuples, definitionKey);
                foreach (ImmutableArray<ITypeSymbol> tuple in DistinctTuples(tuples))
                {
                    if (!TryCloseType(input.Right, definition, tuple, out INamedTypeSymbol? closedType)
                        || !GeneratorSupport.IsAccessibleSymbol(closedType!))
                    {
                        continue;
                    }

                    componentBindings.Add(new GenericComponentFactoryBinding(
                        GeneratorSupport.DisplayType(definition.ConstructUnboundGenericType()),
                        tuple.Select(GeneratorSupport.DisplayType).ToImmutableArray(),
                        GeneratorSupport.DisplayType(closedType!)));
                }
            }

            var functorBindings = new List<GenericFunctorFactoryBinding>();
            foreach (KeyValuePair<string, INamedTypeSymbol> definitionEntry in functorDefinitions)
            {
                string definitionKey = definitionEntry.Key;
                INamedTypeSymbol definition = definitionEntry.Value;
                GenericFunctorModel? model = GenericFunctorGenerator.CreateModel(definition);
                if (model is null)
                {
                    continue;
                }

                IEnumerable<ImmutableArray<ITypeSymbol>> tuples = model.Arity == 1 && unresolvedFunctors.Contains(definitionKey)
                    ? componentTypes.Values.Select(static type => ImmutableArray.Create(type))
                        .Concat(GetTuples(functorTuples, definitionKey))
                    : GetTuples(functorTuples, definitionKey);
                foreach (ImmutableArray<ITypeSymbol> tuple in DistinctTuples(tuples))
                {
                    if (!TryCloseType(input.Right, definition, tuple, out _))
                    {
                        continue;
                    }

                    string executorName = "global::Delta.ECS.Generated.GenericFunctorExecutor_" + GeneratorSupport.StableName(model.TypeName);
                    functorBindings.Add(new GenericFunctorFactoryBinding(
                        GeneratorSupport.DisplayType(definition.ConstructUnboundGenericType()),
                        tuple.Select(GeneratorSupport.DisplayType).ToImmutableArray(),
                        executorName + "<" + string.Join(", ", tuple.Select(GeneratorSupport.DisplayType)) + ">"));
                }
            }

            if (componentBindings.Count == 0 && functorBindings.Count == 0)
            {
                return;
            }

            bool needsModuleInitializerAttribute = input.Right.GetTypeByMetadataName("System.Runtime.CompilerServices.ModuleInitializerAttribute") is null;
            output.AddSource("GeneratedGenericBindings.g.cs", GenericBindingTemplates.Render(
                componentBindings,
                functorBindings,
                needsModuleInitializerAttribute));
        });
    }

    private static GenericBindingDiscovery? ReadDiscovery(GeneratorSyntaxContext syntax)
    {
        var invocation = (InvocationExpressionSyntax)syntax.Node;
        IMethodSymbol? method = syntax.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (method is null)
        {
            return ReadFunctorDiscovery(invocation, syntax.SemanticModel);
        }

        if (IsLayoutRegistry(method.ContainingType))
        {
            if (method.IsGenericMethod && method.Name == "Register" && method.TypeArguments.Length == 1)
            {
                return new GenericBindingDiscovery(method.TypeArguments[0], null, default, null);
            }

            if (method.Name == "Register" && !method.IsGenericMethod && invocation.ArgumentList.Arguments.Count >= 3
                && TryGetOpenGenericType(invocation.ArgumentList.Arguments[0].Expression, syntax.SemanticModel, out INamedTypeSymbol? definition))
            {
                int arity = definition.Arity;
                if (invocation.ArgumentList.Arguments.Count - 2 != arity)
                {
                    return new GenericBindingDiscovery(null, definition, default, null);
                }

                var arguments = ImmutableArray.CreateBuilder<ITypeSymbol>(arity);
                for (int index = 0; index < arity; index++)
                {
                    ITypeSymbol? type = ResolveComponentType(
                        invocation.ArgumentList.Arguments[index + 2].Expression,
                        syntax.SemanticModel,
                        new HashSet<ISymbol>(SymbolEqualityComparer.Default));
                    if (type is null || !IsClosedType(type))
                    {
                        arguments.Clear();
                        break;
                    }

                    arguments.Add(type);
                }

                ImmutableArray<ITypeSymbol> tuple = arguments.ToImmutable();
                ITypeSymbol? closedType = TryCloseType(syntax.SemanticModel.Compilation, definition, tuple, out INamedTypeSymbol? closed)
                    ? closed
                    : null;
                return new GenericBindingDiscovery(closedType, definition, tuple, null);
            }
        }

        return ReadFunctorDiscovery(invocation, syntax.SemanticModel);
    }

    private static GenericBindingDiscovery? ReadFunctorDiscovery(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (!FunctorMethods.Contains(GetMethodName(invocation.Expression)))
        {
            return null;
        }

        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        for (int typeIndex = 0; typeIndex < arguments.Count; typeIndex++)
        {
            if (!TryGetOpenGenericType(arguments[typeIndex].Expression, semanticModel, out INamedTypeSymbol? definition))
            {
                continue;
            }

            GenericFunctorModel? model = GenericFunctorGenerator.CreateModel(definition);
            if (model is null || typeIndex < model.Arity)
            {
                continue;
            }

            var tuple = ImmutableArray.CreateBuilder<ITypeSymbol>(model.Arity);
            for (int index = typeIndex - model.Arity; index < typeIndex; index++)
            {
                ITypeSymbol? type = ResolveComponentType(
                    arguments[index].Expression,
                    semanticModel,
                    new HashSet<ISymbol>(SymbolEqualityComparer.Default));
                if (type is null || !IsClosedType(type))
                {
                    tuple.Clear();
                    break;
                }

                tuple.Add(type);
            }

            return new GenericBindingDiscovery(null, null, default, definition, tuple.ToImmutable());
        }

        return null;
    }

    private static ITypeSymbol? ResolveComponentType(ExpressionSyntax expression, SemanticModel semanticModel, HashSet<ISymbol> resolving)
    {
        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            return ResolveComponentType(parenthesized.Expression, semanticModel, resolving);
        }

        if (expression is CastExpressionSyntax cast)
        {
            return ResolveComponentType(cast.Expression, semanticModel, resolving);
        }

        if (expression is InvocationExpressionSyntax invocation)
        {
            return ResolveRegisteredType(invocation, semanticModel, resolving);
        }

        ISymbol? symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        if (symbol is ILocalSymbol or IFieldSymbol)
        {
            if (!resolving.Add(symbol))
            {
                return null;
            }

            foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
            {
                SyntaxNode declaration = reference.GetSyntax();
                ExpressionSyntax? initializer = declaration switch
                {
                    VariableDeclaratorSyntax variable => variable.Initializer?.Value,
                    PropertyDeclarationSyntax property => property.Initializer?.Value,
                    _ => null,
                };
                if (initializer is not null)
                {
                    return ResolveComponentType(initializer, semanticModel.Compilation.GetSemanticModel(initializer.SyntaxTree), resolving);
                }
            }
        }

        return null;
    }

    private static ITypeSymbol? ResolveRegisteredType(InvocationExpressionSyntax invocation, SemanticModel semanticModel, HashSet<ISymbol> resolving)
    {
        if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method || !IsLayoutRegistry(method.ContainingType))
        {
            return null;
        }

        if (method.Name == "Register" && method.IsGenericMethod && method.TypeArguments.Length == 1)
        {
            return method.TypeArguments[0];
        }

        if (method.Name == "GetPrimary" && method.IsGenericMethod && method.TypeArguments.Length == 1)
        {
            return method.TypeArguments[0];
        }

        if (method.Name != "Register" || method.IsGenericMethod || invocation.ArgumentList.Arguments.Count < 3
            || !TryGetOpenGenericType(invocation.ArgumentList.Arguments[0].Expression, semanticModel, out INamedTypeSymbol? definition)
            || invocation.ArgumentList.Arguments.Count - 2 != definition.Arity)
        {
            return null;
        }

        var arguments = ImmutableArray.CreateBuilder<ITypeSymbol>(definition.Arity);
        for (int index = 0; index < definition.Arity; index++)
        {
            ITypeSymbol? type = ResolveComponentType(invocation.ArgumentList.Arguments[index + 2].Expression, semanticModel, resolving);
            if (type is null || !IsClosedType(type))
            {
                return null;
            }

            arguments.Add(type);
        }

        return TryCloseType(semanticModel.Compilation, definition, arguments.ToImmutable(), out INamedTypeSymbol? closedType)
            ? closedType
            : null;
    }

    private static bool TryGetOpenGenericType(ExpressionSyntax expression, SemanticModel semanticModel, out INamedTypeSymbol definition)
    {
        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            return TryGetOpenGenericType(parenthesized.Expression, semanticModel, out definition);
        }

        if (expression is TypeOfExpressionSyntax typeOf
            && semanticModel.GetTypeInfo(typeOf.Type).Type is INamedTypeSymbol { IsUnboundGenericType: true } type)
        {
            definition = type.OriginalDefinition;
            return true;
        }

        ISymbol? symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        if (symbol is ILocalSymbol or IFieldSymbol)
        {
            foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
            {
                SyntaxNode declaration = reference.GetSyntax();
                ExpressionSyntax? initializer = declaration switch
                {
                    VariableDeclaratorSyntax variable => variable.Initializer?.Value,
                    PropertyDeclarationSyntax property => property.Initializer?.Value,
                    _ => null,
                };
                if (initializer is not null)
                {
                    return TryGetOpenGenericType(initializer, semanticModel.Compilation.GetSemanticModel(initializer.SyntaxTree), out definition);
                }
            }
        }

        definition = null!;
        return false;
    }

    private static bool TryCloseType(Compilation compilation, INamedTypeSymbol definition, ImmutableArray<ITypeSymbol> arguments, out INamedTypeSymbol? closedType)
    {
        if (definition.Arity != arguments.Length || arguments.Any(static type => !IsClosedType(type))
            || !SatisfiesConstraints(compilation, definition, arguments))
        {
            closedType = null;
            return false;
        }

        try
        {
            closedType = definition.Construct(arguments.ToArray());
            return !ContainsTypeParameter(closedType) && closedType.TypeKind != TypeKind.Error;
        }
        catch (ArgumentException)
        {
            closedType = null;
            return false;
        }
    }

    private static bool SatisfiesConstraints(Compilation compilation, INamedTypeSymbol definition, ImmutableArray<ITypeSymbol> arguments)
    {
        for (int index = 0; index < definition.TypeParameters.Length; index++)
        {
            ITypeParameterSymbol parameter = definition.TypeParameters[index];
            ITypeSymbol argument = arguments[index];
            bool nullableValueType = argument is INamedTypeSymbol named
                && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

            if (parameter.HasUnmanagedTypeConstraint && !argument.IsUnmanagedType
                || parameter.HasValueTypeConstraint && (!argument.IsValueType || nullableValueType)
                || parameter.HasReferenceTypeConstraint && (!argument.IsReferenceType
                    || parameter.ReferenceTypeConstraintNullableAnnotation != NullableAnnotation.Annotated
                        && argument.NullableAnnotation == NullableAnnotation.Annotated)
                || parameter.HasNotNullConstraint && argument.NullableAnnotation == NullableAnnotation.Annotated)
            {
                return false;
            }

            if (parameter.HasConstructorConstraint && !argument.IsValueType
                && !(argument is INamedTypeSymbol argumentType && !argumentType.IsAbstract
                    && argumentType.InstanceConstructors.Any(static constructor => constructor.Parameters.Length == 0
                        && constructor.DeclaredAccessibility == Accessibility.Public)))
            {
                return false;
            }

            foreach (ITypeSymbol constraint in parameter.ConstraintTypes)
            {
                if (constraint is ITypeParameterSymbol otherParameter)
                {
                    int constraintIndex = Array.FindIndex(definition.TypeParameters.ToArray(), candidate => SymbolEqualityComparer.Default.Equals(candidate, otherParameter));
                    if (constraintIndex >= 0 && !ClassifiesAsImplicit(compilation, argument, arguments[constraintIndex]))
                    {
                        return false;
                    }

                    continue;
                }

                if (ContainsTypeParameter(constraint))
                {
                    continue;
                }

                if (!ClassifiesAsImplicit(compilation, argument, constraint))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ClassifiesAsImplicit(Compilation compilation, ITypeSymbol source, ITypeSymbol destination)
        => compilation is not CSharpCompilation csharpCompilation
            || csharpCompilation.ClassifyConversion(source, destination).IsImplicit;

    private static bool ContainsTypeParameter(ITypeSymbol type)
        => type is ITypeParameterSymbol || type is INamedTypeSymbol named && named.TypeArguments.Any(ContainsTypeParameter);

    private static bool IsClosedType(ITypeSymbol type)
        => !ContainsTypeParameter(type) && type.TypeKind != TypeKind.Error && type.TypeKind != TypeKind.Pointer
            && type is not ITypeParameterSymbol && !(type is INamedTypeSymbol named && named.IsUnboundGenericType);

    private static void AddIfMissing<TValue>(Dictionary<string, TValue> dictionary, string key, TValue value)
    {
        if (!dictionary.ContainsKey(key))
        {
            dictionary.Add(key, value);
        }
    }

    private static bool IsLayoutRegistry(INamedTypeSymbol? type)
        => type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Delta.ECS.ComponentLayoutRegistry";

    private static string GetMethodName(ExpressionSyntax expression)
        => expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => string.Empty,
        };

    private static void AddTypes(Dictionary<string, ITypeSymbol> types, ImmutableArray<ITypeSymbol> tuple)
    {
        foreach (ITypeSymbol type in tuple)
        {
            if (IsClosedType(type) && GeneratorSupport.IsAccessibleSymbol(type))
            {
                AddIfMissing(types, GeneratorSupport.DisplayType(type), type);
            }
        }
    }

    private static void AddTuple(Dictionary<string, Dictionary<string, ImmutableArray<ITypeSymbol>>> tuples, string key, ImmutableArray<ITypeSymbol> tuple)
    {
        if (!tuples.TryGetValue(key, out Dictionary<string, ImmutableArray<ITypeSymbol>>? values))
        {
            values = new Dictionary<string, ImmutableArray<ITypeSymbol>>(StringComparer.Ordinal);
            tuples.Add(key, values);
        }

        string tupleKey = string.Join("\u001f", tuple.Select(GeneratorSupport.DisplayType));
        if (!values.ContainsKey(tupleKey))
        {
            values.Add(tupleKey, tuple);
        }
    }

    private static IEnumerable<ImmutableArray<ITypeSymbol>> GetTuples(
        Dictionary<string, Dictionary<string, ImmutableArray<ITypeSymbol>>> tuples,
        string key)
        => tuples.TryGetValue(key, out Dictionary<string, ImmutableArray<ITypeSymbol>>? values)
            ? values.Values
            : Enumerable.Empty<ImmutableArray<ITypeSymbol>>();

    private static IEnumerable<ImmutableArray<ITypeSymbol>> DistinctTuples(IEnumerable<ImmutableArray<ITypeSymbol>> tuples)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (ImmutableArray<ITypeSymbol> tuple in tuples)
        {
            if (tuple.IsDefaultOrEmpty || tuple.Any(static type => !IsClosedType(type)))
            {
                continue;
            }

            if (seen.Add(string.Join("\u001f", tuple.Select(GeneratorSupport.DisplayType))))
            {
                yield return tuple;
            }
        }
    }

    private sealed class GenericBindingDiscovery(
        ITypeSymbol? registeredComponentType,
        INamedTypeSymbol? genericComponentDefinition,
        ImmutableArray<ITypeSymbol> genericComponentArguments,
        INamedTypeSymbol? genericFunctorDefinition = null,
        ImmutableArray<ITypeSymbol> genericFunctorArguments = default)
    {
        internal ITypeSymbol? RegisteredComponentType { get; } = registeredComponentType;
        internal INamedTypeSymbol? GenericComponentDefinition { get; } = genericComponentDefinition;
        internal ImmutableArray<ITypeSymbol> GenericArguments { get; } = genericComponentDefinition is not null
            ? genericComponentArguments
            : genericFunctorArguments;
        internal INamedTypeSymbol? GenericFunctorDefinition { get; } = genericFunctorDefinition;
    }
}

internal sealed class GenericComponentFactoryBinding(
    string genericDefinition,
    ImmutableArray<string> genericArguments,
    string componentType)
{
    internal string GenericDefinition { get; } = genericDefinition;
    internal ImmutableArray<string> GenericArguments { get; } = genericArguments;
    internal string ComponentType { get; } = componentType;
}

internal sealed class GenericFunctorFactoryBinding(
    string genericDefinition,
    ImmutableArray<string> genericArguments,
    string executorType)
{
    internal string GenericDefinition { get; } = genericDefinition;
    internal ImmutableArray<string> GenericArguments { get; } = genericArguments;
    internal string ExecutorType { get; } = executorType;
}
