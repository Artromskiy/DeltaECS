using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

internal delegate bool TryReadGeneratedShape<TShape>(
    SemanticModel model,
    InvocationExpressionSyntax invocation,
    out TShape? shape)
    where TShape : class;

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

    internal IEnumerable<TShape> Ordered()
        => _items.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => pair.Value);
}

internal static class GeneratorPipeline
{
    internal static IncrementalValuesProvider<TShape?> ShapeProvider<TShape>(
        IncrementalGeneratorInitializationContext context,
        Func<GeneratorSyntaxContext, TShape?> readShape)
        where TShape : class
        => context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is InvocationExpressionSyntax invocation
                && invocation.Expression is MemberAccessExpressionSyntax member
                && GeneratorSupport.IsGeneratedApiName(member.Name.Identifier.ValueText)
                && !GeneratorSupport.IsGeneratedSourcePath(node.SyntaxTree.FilePath),
            (syntaxContext, _) => readShape(syntaxContext));

    internal static IncrementalValueProvider<
        (Compilation Compilation, ImmutableArray<InvocationCandidate> Invocations)> Input(
        IncrementalGeneratorInitializationContext context)
        => context.CompilationProvider.Combine(
            GeneratorSupport.InvocationProvider(context).Collect());

    internal static void EmitDistinct<TShape>(
        Compilation compilation,
        ImmutableArray<InvocationCandidate> discoveredInvocations,
        SourceProductionContext context,
        string sourcePrefix,
        TryReadGeneratedShape<TShape> readShape,
        Func<TShape, string> key,
        Func<TShape, string> render)
        where TShape : class
    {
        var shapes = new ShapeRegistry<TShape>(key);
        foreach (InvocationCandidate candidate in GeneratorSupport.ExcludeGenerated(discoveredInvocations))
        {
            InvocationExpressionSyntax invocation = candidate.Invocation;
            SemanticModel model = compilation.GetSemanticModel(invocation.SyntaxTree);
            if (!readShape(model, invocation, out TShape? shape) || shape is null)
            {
                continue;
            }

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
