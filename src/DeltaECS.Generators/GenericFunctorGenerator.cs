using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>Generates typed executors for runtime-closed generic struct functors.</summary>
[Generator]
public sealed class GenericFunctorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is StructDeclarationSyntax { TypeParameterList: not null, BaseList: not null },
                static (syntax, _) => ReadModel(syntax))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .Collect();
        context.RegisterSourceOutput(models, static (output, discovered) =>
        {
            foreach (int arity in discovered.Select(static model => model.Arity).Distinct())
            {
                output.AddSource($"GenericFunctorArity_{arity}.g.cs", GenericFunctorTemplates.RenderOverloads(arity));
            }
        });
        context.RegisterSourceOutput(models, static (output, discovered) => GeneratorPipeline.EmitShapes(
            discovered, output, "GenericFunctor_", static model => model.TypeName,
            static model => GenericFunctorTemplates.Render(model)));
    }

    private static GenericFunctorModel? ReadModel(GeneratorSyntaxContext syntax)
    {
        return syntax.SemanticModel.GetDeclaredSymbol(syntax.Node) is INamedTypeSymbol functor
            ? CreateModel(functor)
            : null;
    }

    internal static GenericFunctorModel? CreateModel(INamedTypeSymbol functor)
    {
        if (!GeneratorSupport.IsAccessibleSymbol(functor)
            || !CallbackReader.TryGetForEachMarker(functor, out bool hasContext, out bool hasEntity, out ITypeSymbol? contextType))
        {
            return null;
        }

        var parameters = TypeParameters(functor).ToArray();
        if (parameters.Select(static parameter => parameter.Name).Distinct(StringComparer.Ordinal).Count() != parameters.Length)
        {
            return null;
        }

        int prefixLength = (hasContext ? 1 : 0) + (hasEntity ? 1 : 0);
        IMethodSymbol[] methods = functor.GetMembers("Invoke").OfType<IMethodSymbol>()
            .Where(static method => !method.IsStatic && !method.IsGenericMethod && method.ReturnsVoid
                && method.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
                && method.Parameters.All(static parameter => CallbackReader.IsSupportedRefKind(parameter.RefKind)))
            .Where(method => CallbackReader.HasValidPrefix(method, hasContext, hasEntity, contextType, requireRefContext: false))
            .ToArray();
        if (methods.Length != 1)
        {
            return null;
        }

        IParameterSymbol[] parametersAfterPrefix = methods[0].Parameters.Skip(prefixLength).ToArray();
        if (parametersAfterPrefix.Length == 0 && !hasEntity)
        {
            return null;
        }

        ContextModeKind contextMode = hasContext
            ? CallbackReader.ContextMode(methods[0].Parameters[0].RefKind)
            : ContextModeKind.None;
        if (contextMode == ContextModeKind.RefReadonly)
        {
            contextMode = ContextModeKind.In;
        }

        IParameterSymbol[] componentParameters = parametersAfterPrefix;
        var rows = componentParameters.Select(parameter => new GenericFunctorRow(
            GeneratorSupport.DisplayType(parameter.Type),
            GeneratorSupport.PatternLetter(parameter.RefKind),
            Selector(parameter.Type, parameters))).ToImmutableArray();
        return new GenericFunctorModel(
            GeneratorSupport.DisplayType(functor),
            GeneratorSupport.DisplayType(functor.ConstructUnboundGenericType()),
            hasEntity,
            hasContext,
            hasContext ? GeneratorSupport.DisplayType(contextType!) : null,
            contextMode,
            string.Join(", ", parameters.Select(static parameter => parameter.Name)),
            string.Join("\n", parameters.Select(Constraint).Where(static clause => clause.Length != 0)),
            rows);
    }

    private static IEnumerable<ITypeParameterSymbol> TypeParameters(INamedTypeSymbol type)
        => (type.ContainingType is null ? Enumerable.Empty<ITypeParameterSymbol>() : TypeParameters(type.ContainingType))
            .Concat(type.TypeParameters);

    private static string Selector(ITypeSymbol type, ITypeParameterSymbol[] parameters)
    {
        if (type is ITypeParameterSymbol parameter)
        {
            int index = Array.FindIndex(parameters, candidate => SymbolEqualityComparer.Default.Equals(candidate, parameter));
            return $"genericArguments[{index}]";
        }

        if (type is INamedTypeSymbol named && named.IsGenericType && ContainsParameter(named))
        {
            string arguments = string.Join(", ", named.TypeArguments.Select(argument => Selector(argument, parameters)));
            return $$"""global::Delta.ECS.GeneratedForEachRuntime.GetGenericFunctorComponent<{{GeneratorSupport.DisplayType(named)}}>(world, stackalloc global::Delta.ECS.ComponentId[] { {{arguments}} })""";
        }

        return $"world.Layouts.GetPrimary<{GeneratorSupport.DisplayType(type)}>()";
    }

    private static bool ContainsParameter(ITypeSymbol type)
        => type is ITypeParameterSymbol || type is INamedTypeSymbol named && named.TypeArguments.Any(ContainsParameter);

    private static string Constraint(ITypeParameterSymbol parameter)
    {
        var constraints = new List<string>();
        if (parameter.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }
        else if (parameter.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }
        else if (parameter.HasReferenceTypeConstraint)
        {
            constraints.Add(parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
        }
        else if (parameter.HasNotNullConstraint)
        {
            constraints.Add("notnull");
        }

        constraints.AddRange(parameter.ConstraintTypes.Select(GeneratorSupport.DisplayType));
        if (parameter.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        return constraints.Count == 0 ? string.Empty : $"where {parameter.Name} : {string.Join(", ", constraints)}";
    }
}

internal sealed class GenericFunctorModel
{
    internal GenericFunctorModel(
        string typeName,
        string openTypeName,
        bool hasEntity,
        bool hasContext,
        string? contextType,
        ContextModeKind contextMode,
        string typeParameters,
        string constraints,
        ImmutableArray<GenericFunctorRow> rows)
    {
        TypeName = typeName;
        OpenTypeName = openTypeName;
        HasEntity = hasEntity;
        HasContext = hasContext;
        ContextType = contextType;
        ContextMode = contextMode;
        TypeParameters = typeParameters;
        Constraints = constraints;
        Rows = rows;
    }

    internal string TypeName { get; }
    internal string OpenTypeName { get; }
    internal bool HasEntity { get; }
    internal bool HasContext { get; }
    internal string? ContextType { get; }
    internal ContextModeKind ContextMode { get; }
    internal int Arity => TypeParameters.Split(',').Length;
    internal string TypeParameters { get; }
    internal string Constraints { get; }
    internal ImmutableArray<GenericFunctorRow> Rows { get; }
}

internal sealed class GenericFunctorRow
{
    internal GenericFunctorRow(string typeName, char pattern, string selector)
    {
        TypeName = typeName;
        Pattern = pattern;
        Selector = selector;
    }

    internal string TypeName { get; }
    internal char Pattern { get; }
    internal string Selector { get; }
}
