using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

internal sealed class ShapeRegistry<TShape>
    where TShape : class
{
    private readonly Func<TShape, string> _key;
    private readonly Dictionary<string, TShape> _items = new(StringComparer.Ordinal);

    internal ShapeRegistry(Func<TShape, string> key) => _key = key;

    internal TShape GetOrAdd(TShape candidate, Action<TShape, TShape>? merge = null)
    {
        string shapeKey = _key(candidate);
        if (_items.TryGetValue(shapeKey, out TShape? existing))
        {
            merge?.Invoke(existing, candidate);
            return existing;
        }

        _items.Add(shapeKey, candidate);
        return candidate;
    }

    internal IEnumerable<TShape> Ordered() => _items.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => pair.Value);
}

internal static class GeneratorPipeline
{
    internal static void RegisterShapeOutput<TShape>(
        IncrementalGeneratorInitializationContext context,
        Func<GeneratorSyntaxContext, TShape?> readShape,
        string sourcePrefix,
        Func<TShape, string> key,
        Func<TShape, string> render)
        where TShape : class
    {
        IncrementalValuesProvider<TShape> shapes = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is InvocationExpressionSyntax invocation
                && invocation.Expression is MemberAccessExpressionSyntax member
                && GeneratorSupport.IsGeneratedApiName(member.Name.Identifier.ValueText)
                && !GeneratorSupport.IsGeneratedSourcePath(node.SyntaxTree.FilePath),
            (syntaxContext, _) => readShape(syntaxContext))
            .Where(static shape => shape is not null)
            .Select(static (shape, _) => shape!);
        context.RegisterSourceOutput(shapes.Collect(), (productionContext, discovered) =>
            EmitShapes(discovered, productionContext, sourcePrefix, key, render));
    }

    internal static void RegisterInterceptorInput(
        IncrementalGeneratorInitializationContext context,
        Action<Compilation, ImmutableArray<InvocationCandidate>, SourceProductionContext, bool> execute,
        Func<SyntaxNode, bool>? candidateFilter = null)
    {
        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(
                GeneratorSupport.InvocationProvider(context, candidateFilter).Collect())
                .Combine(context.AnalyzerConfigOptionsProvider.Select(
                    static (provider, _) => GeneratorSupport.IsInterceptionEnabled(provider.GlobalOptions))),
            (productionContext, input) => execute(
                input.Left.Left,
                input.Left.Right,
                productionContext,
                input.Right));
    }

    internal static void EmitShapes<TShape>(
        ImmutableArray<TShape> discoveredShapes,
        SourceProductionContext context,
        string sourcePrefix,
        Func<TShape, string> key,
        Func<TShape, string> render)
        where TShape : class
    {
        var shapes = new ShapeRegistry<TShape>(key);
        foreach (TShape shape in discoveredShapes)
        {
            shapes.GetOrAdd(shape);
        }

        foreach (TShape shape in shapes.Ordered())
        {
            string shapeKey = key(shape);
            context.AddSource(
                sourcePrefix + GeneratorSupport.StableName(shapeKey) + ".g.cs",
                render(shape));
        }
    }
}
