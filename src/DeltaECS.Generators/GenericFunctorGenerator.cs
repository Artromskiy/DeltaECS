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

            if (!discovered.IsEmpty)
            {
                output.AddSource("GenericFunctorDynamicArguments.g.cs", GenericFunctorTemplates.RenderDynamicOverloads());
            }
        });
        context.RegisterSourceOutput(models, static (output, discovered) => GeneratorPipeline.EmitShapes(
            discovered, output, "GenericFunctor_", static model => model.TypeName,
            static model => GenericFunctorTemplates.Render(model)));
    }

    private static GenericFunctorModel? ReadModel(GeneratorSyntaxContext syntax)
        => syntax.SemanticModel.GetDeclaredSymbol(syntax.Node) is INamedTypeSymbol functor
            ? CreateModel(functor)
            : null;

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
            .Where(method => CallbackReader.HasValidPrefix(method, hasContext, hasEntity, contextType, requireRefContext: false, entityRef: true))
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
        bool supportsTypeTokenDispatch = GenericTypeConstraintSupport.TryGetSupported(functor, out ImmutableArray<GenericTypeParameterConstraint> dispatchTypeParameters);
        return new GenericFunctorModel(
            GeneratorSupport.DisplayType(functor),
            GeneratorSupport.DisplayType(functor.ConstructUnboundGenericType()),
            hasEntity,
            hasContext,
            hasContext ? GeneratorSupport.DisplayType(contextType!) : null,
            contextMode,
            string.Join(", ", parameters.Select(static parameter => parameter.Name)),
            string.Join("\n", parameters.Select(Constraint).Where(static clause => clause.Length != 0)),
            supportsTypeTokenDispatch,
            dispatchTypeParameters,
            rows);
    }

    private static IEnumerable<ITypeParameterSymbol> TypeParameters(INamedTypeSymbol type)
        => (type.ContainingType is null ? Enumerable.Empty<ITypeParameterSymbol>() : TypeParameters(type.ContainingType))
            .Concat(type.TypeParameters);

    private static string Selector(ITypeSymbol type, ITypeParameterSymbol[] parameters)
        => type switch
        {
            ITypeParameterSymbol parameter => $"genericArguments[{Array.FindIndex(parameters, candidate => SymbolEqualityComparer.Default.Equals(candidate, parameter))}]",
            INamedTypeSymbol { IsGenericType: true } named when GeneratorSupport.ContainsTypeParameter(named)
                => GenericComponentSelector(named, parameters),
            _ => $"world.Layouts.GetPrimary<{GeneratorSupport.DisplayType(type)}>()"
        };

    private static string GenericComponentSelector(INamedTypeSymbol type, ITypeParameterSymbol[] parameters)
    {
        string arguments = string.Join(", ", type.TypeArguments.Select(argument => Selector(argument, parameters)));
        return $$"""global::Delta.ECS.GeneratedForEachRuntime.GetGenericFunctorComponent<{{GeneratorSupport.DisplayType(type)}}>(world, stackalloc global::Delta.ECS.ComponentId[] { {{arguments}} })""";
    }

    private static string Constraint(ITypeParameterSymbol parameter)
    {
        string primaryConstraint = parameter.HasUnmanagedTypeConstraint
            ? "unmanaged"
            : parameter.HasValueTypeConstraint
                ? "struct"
                : parameter.HasReferenceTypeConstraint
                    ? parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class"
                    : parameter.HasNotNullConstraint ? "notnull" : string.Empty;
        string[] constraints = new[] { primaryConstraint }
            .Where(static constraint => constraint.Length != 0)
            .Concat(parameter.ConstraintTypes.Select(GeneratorSupport.DisplayType))
            .Concat(parameter.HasConstructorConstraint ? new[] { "new()" } : Array.Empty<string>())
            .ToArray();
        return constraints.Length == 0 ? string.Empty : $"where {parameter.Name} : {string.Join(", ", constraints)}";
    }
}

internal sealed record GenericFunctorModel(
    string TypeName,
    string OpenTypeName,
    bool HasEntity,
    bool HasContext,
    string? ContextType,
    ContextModeKind ContextMode,
    string TypeParameters,
    string Constraints,
    bool SupportsTypeTokenDispatch,
    ImmutableArray<GenericTypeParameterConstraint> DispatchTypeParameters,
    ImmutableArray<GenericFunctorRow> Rows)
{
    internal int Arity => TypeParameters.Split(',').Length;
}

internal readonly record struct GenericFunctorRow(string TypeName, char Pattern, string Selector);
