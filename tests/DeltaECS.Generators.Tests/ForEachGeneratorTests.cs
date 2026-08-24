using Delta.ECS.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Delta.ECS.Generators.Tests;

[TestFixture]
public sealed class ForEachGeneratorTests
{
    [Test]
    public void OutputIsDeterministic()
    {
        var first = RunGenerator().GeneratedTrees.Single().GetText().ToString();
        var second = RunGenerator().GeneratedTrees.Single().GetText().ToString();

        Assert.That(second, Is.EqualTo(first));
        Assert.That(first, Does.Contain("ForEach<T1, T2, T3, T4>"));
        Assert.That(first, Does.Contain("ForEach(in Query query, ForEachAction action)"));
        Assert.That(first, Does.Contain("ForEach(in Query query, ForEachEntityAction action)"));
        Assert.That(first, Does.Contain("ForEachEntityTag"));
        Assert.That(first, Does.Not.Contain("dynamic"));
    }

    [Test]
    public void GeneratedMatrixCompiles()
    {
        GeneratorDriverRunResult run = RunGenerator();
        var generated = run.GeneratedTrees.Select(static tree => tree.GetText().ToString());
        var compilation = CreateCompilation(new[] { RuntimeStubSource, ConsumerSource }.Concat(generated));
        var result = compilation.GetDiagnostics();

        Assert.That(result.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
    }

    [Test]
    public void GeneratedDelegateForEachExecutesDenseRows()
    {
        using var world = new Delta.ECS.World(chunkCapacity: 2);
        var positionId = world.Layouts.Register<RuntimePosition>(new Delta.ECS.SchemaId(1));
        var first = world.Create(positionId);
        var second = world.Create(positionId);
        var query = world.CreateQuery(Delta.ECS.QuerySpec.ForComponents(positionId));

        world.ForEach<RuntimePosition>(in query, positionId, static (ref RuntimePosition value) => value.Value++);

        Assert.That(world.TryGetComponent(first, positionId, out RuntimePosition firstValue), Is.True);
        Assert.That(world.TryGetComponent(second, positionId, out RuntimePosition secondValue), Is.True);
        Assert.That(firstValue.Value, Is.EqualTo(1));
        Assert.That(secondValue.Value, Is.EqualTo(1));
    }

    private struct RuntimePosition
    {
        public int Value;
    }

    private static GeneratorDriverRunResult RunGenerator()
    {
        var compilation = CreateCompilation(new[] { RuntimeStubSource });
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ForEachGenerator().AsSourceGenerator());
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private static CSharpCompilation CreateCompilation(IEnumerable<string> sources)
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create(
            "DeltaEcsGeneratorHarness",
            sources.Select(static source => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private const string RuntimeStubSource = """
        namespace Delta.ECS;
        using System;
        public readonly struct Entity { public int Index { get; } }
        public readonly struct ComponentId { }
        public readonly struct Query { }
        public readonly struct WriteAccess { }
        public ref struct ReadValues { public ref T Ref<T>(QueryChunkCursor cursor) => throw new NotImplementedException(); }
        public ref struct QueryChunkCursor
        {
            public ReadOnlySpan<Entity> Entities => default;
            public int CurrentIndex => 0;
            public bool MoveNext() => false;
            public ReadValues GetWrite(WriteAccess access) => default;
        }
        internal interface IForEachInvoker { void Invoke(ref QueryChunkCursor cursor); }
        internal static class ForEachRuntime
        {
            internal static WriteAccess AccessWrite<T>(World world, in Query query, ComponentId component) => default;
            internal static void ResolveComponentIds(in Query query, Span<ComponentId> destination) { }
            internal static void Execute<TInvoker>(World world, in Query query, ref TInvoker invoker)
                where TInvoker : struct, IForEachInvoker { }
        }
        public sealed partial class World { }
        """;

    private const string ConsumerSource = """
        namespace Delta.ECS;
        struct Position { public int Value; }
        struct Velocity { public int Value; }
        struct EntityFunctor : IForEachEntity<Position>
        {
            public void Invoke(Entity entity, ref Position position) { position.Value += entity.Index; }
        }
        struct Functor : IForEach<Position, Velocity>
        {
            public void Invoke(ref Position position, ref Velocity velocity) { velocity.Value += position.Value; }
        }
        struct ZeroFunctor : IForEach
        {
            public void Invoke() { }
        }
        struct ZeroContextFunctor : IForEachContext<int>
        {
            public void Invoke(ref int context) { context++; }
        }
        static class Consumer
        {
            public static void Use(World world, Query query, ComponentId positionId, ComponentId velocityId)
            {
                world.ForEach<Position>(in query, positionId, (ref Position position) => position.Value++);
                world.ForEach<Position, Velocity>(in query, positionId, velocityId, static (ref Position p, ref Velocity v) => v.Value += p.Value);
                var context = 0;
                world.ForEach<int, Position>(in query, ref context, positionId, static (ref int c, ref Position p) => c += p.Value);
                var functor = new Functor();
                world.ForEach<Functor, Position, Velocity>(in query, positionId, velocityId, ref functor);
                var entityFunctor = new EntityFunctor();
                world.ForEach<EntityFunctor, Position>(in query, positionId, ref entityFunctor, ForEachEntityTag.Instance);
                world.ForEach(in query, static () => { });
                world.ForEach(in query, ref context, static (ref int value) => value++);
                var zeroFunctor = new ZeroFunctor();
                world.ForEach(in query, ref zeroFunctor);
                var zeroContextFunctor = new ZeroContextFunctor();
                world.ForEach(in query, ref context, ref zeroContextFunctor);
                world.ForEach(in query, static (Entity entity) => _ = entity);
            }
        }
        """;
}
