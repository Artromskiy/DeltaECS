using System.Globalization;
using Delta.ECS.Generators.Consumer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Delta.ECS.Generators.Tests;

[TestFixture]
public sealed class DemandDrivenForEachGeneratorTests
{
    [Test]
    public void DemandDrivenOutputIsDeterministic()
    {
        string first = GeneratedText(RunGenerator());
        string second = GeneratedText(RunGenerator());

        Assert.That(second, Is.EqualTo(first));
        Assert.That(first, Does.Contain("DemandForEachExtensions_"));
        Assert.That(first, Does.Contain("ForEachAction<T1>"));
        Assert.That(first, Does.Contain("ForEachAction_IWIW<T1, T2, T3, T4>"));
        Assert.That(first, Does.Contain("ForEachAction_IWIWW<T1, T2, T3, T4, T5>"));
        Assert.That(first, Does.Contain("ForEachAction_WIWIWIWI<T1, T2, T3, T4, T5, T6, T7, T8>"));
        Assert.That(first, Does.Contain("ForEachEntityAction<T1>"));
        Assert.That(first, Does.Not.Contain("interface IForEach_"));
        Assert.That(first, Does.Not.Contain("interface IForEachEntity_"));
        Assert.That(first, Does.Not.Contain("dynamic"));
    }

    [Test]
    public void DemandDrivenDiagnosticsAreDeterministic()
    {
        var first = RunGenerator().Diagnostics
            .Select(static diagnostic => (diagnostic.Id, diagnostic.Severity, Message: diagnostic.GetMessage(CultureInfo.InvariantCulture)))
            .ToArray();
        var second = RunGenerator().Diagnostics
            .Select(static diagnostic => (diagnostic.Id, diagnostic.Severity, Message: diagnostic.GetMessage(CultureInfo.InvariantCulture)))
            .ToArray();

        Assert.That(second, Is.EqualTo(first));
        Assert.That(first, Is.Empty);
    }

    [Test]
    public void AmbiguousFunctorInvokePatternsReportDiagnostic()
    {
        GeneratorDriverRunResult run = RunGenerator(AmbiguousFunctorSource);

        var diagnostics = run.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "DECSGEN003")
            .ToArray();

        Assert.That(diagnostics, Has.Length.EqualTo(1));
        Assert.That(
            diagnostics[0].GetMessage(CultureInfo.InvariantCulture),
            Does.Contain("I")
                .And.Contain("W"));
    }

    [Test]
    public void MarkerFunctorGeneratesConcreteOverloadWithoutSpecializedInterface()
    {
        GeneratorDriverRunResult run = RunGenerator(SingleFunctorSource);
        string generated = GeneratedText(run);

        Assert.That(run.Diagnostics.Where(static diagnostic => diagnostic.Id == "DECSGEN003"), Is.Empty);
        Assert.That(generated, Does.Contain("ref global::Delta.ECS.SimpleFunctor functor"));
        Assert.That(generated, Does.Contain("GetGeneratedWriteReference<global::Delta.ECS.T1>(access0)"));
        Assert.That(generated, Does.Not.Contain("IForEachEntity_W"));
    }

    [Test]
    public void PrivateFunctorReportsGeneratorDiagnostic()
    {
        GeneratorDriverRunResult run = RunGenerator(PrivateFunctorSource);

        var diagnostics = run.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "DECSGEN004")
            .ToArray();

        Assert.That(diagnostics, Has.Length.EqualTo(1));
        Assert.That(
            diagnostics[0].GetMessage(CultureInfo.InvariantCulture),
            Does.Contain("PrivateFunctor")
                .And.Contain("at least internal"));
    }

    [Test]
    public void DemandDrivenGenerationCoversArityOneFourFiveAndEight()
    {
        string generated = GeneratedText(RunGenerator());

        Assert.That(generated, Does.Contain("ForEachAction<T1>"));
        Assert.That(generated, Does.Contain("ForEachAction_IWIW<T1, T2, T3, T4>"));
        Assert.That(generated, Does.Contain("ForEachAction_IWIWW<T1, T2, T3, T4, T5>"));
        Assert.That(generated, Does.Contain("ForEachAction_WIWIWIWI<T1, T2, T3, T4, T5, T6, T7, T8>"));
        Assert.That(generated, Does.Not.Contain("ForEachAction<T1, T2, T3, T4, T5, T6, T7, T8, T9>"));
    }

    [Test]
    public void ImplicitLambdaComponentTypesGenerateTheSameShape()
    {
        const string source = """
            namespace Delta.ECS;
            struct Position { public int Value; }
            struct Velocity { public int Value; }
            static class Consumer
            {
                public static void Use(FilteredEntitySequence sequence)
                {
                    sequence.ForEach(static (ref Position position, in Velocity velocity) =>
                        position.Value += velocity.Value);
                }
            }
            """;

        GeneratorDriverRunResult run = RunGenerator(source);
        string generated = GeneratedText(run);

        Assert.That(run.Diagnostics, Is.Empty);
        Assert.That(generated, Does.Contain("ForEachAction_WI<global::Delta.ECS.Position, global::Delta.ECS.Velocity>"));
    }

    [Test]
    public void RefReadonlyInAndValueParametersGenerateDistinctModes()
    {
        const string source = """
            namespace Delta.ECS;
            struct Position { public int Value; }
            struct Velocity { public int Value; }
            struct Acceleration { public int Value; }
            struct Scale { public int Value; }
            static class Consumer
            {
                public static void Use(World world, Query query)
                {
                    world.ForEach<Position, Velocity, Acceleration, Scale>(in query,
                        static (ref readonly Position position, ref Velocity velocity, in Acceleration acceleration, Scale scale) =>
                        {
                            velocity.Value += position.Value + acceleration.Value + scale.Value;
                        });
                }
            }
            """;

        GeneratorDriverRunResult run = RunGenerator(source);
        string generated = GeneratedText(run);

        Assert.That(run.Diagnostics, Is.Empty);
        Assert.That(generated, Does.Contain("ForEachAction_RWIV<T1, T2, T3, T4>"));
        Assert.That(generated, Does.Contain("ref readonly T1 component0"));
        Assert.That(generated, Does.Contain("in T3 component2"));
        Assert.That(generated, Does.Contain("T4 component3"));
    }

    [Test]
    public void DemandDrivenGenerationIsNotLimitedToLegacySixteenComponentMatrix()
    {
        const int arity = 32;
        string componentTypes = string.Join(", ", Enumerable.Range(1, arity).Select(static index => $"T{index}"));
        string parameters = string.Join(", ", Enumerable.Range(1, arity).Select(static index => $"ref T{index} value{index}"));
        string declarations = string.Join(Environment.NewLine, Enumerable.Range(1, arity).Select(static index => $"struct T{index} {{ }}"));
        string source = $$"""
            namespace Delta.ECS;
            {{declarations}}
            static class WideConsumer
            {
                public static void Use(World world, Query query)
                {
                    world.ForEach<{{componentTypes}}>(in query, static ({{parameters}}) => { });
                }
            }
            """;

        string generated = GeneratedText(RunGenerator(source));

        Assert.That(generated, Does.Contain($"ForEachAction<{componentTypes}>"));
    }

    [Test]
    public void NoIdGenerationUsesCachedPrimaryRouteWithExtraQueryComponent()
    {
        string generated = GeneratedText(RunGenerator());

        Assert.That(generated, Does.Contain("CreateReadAccess(world, in query, typeof(T1)"));
        Assert.That(generated, Does.Contain("CreateWriteAccess(world, in query, typeof(T2)"));
        Assert.That(generated, Does.Not.Contain("AccessRead(world, in query, world.Layouts.GetPrimary"));
        Assert.That(generated, Does.Not.Contain("AccessWrite(world, in query, world.Layouts.GetPrimary"));
        Assert.That(generated, Does.Not.Contain("ResolveComponentIds"));
        Assert.That(generated, Does.Not.Contain("AllMask.Count != destination.Length"));
    }

    [Test]
    public void DenseGenerationUsesClosedExecutionMethod()
    {
        const string source = """
            namespace Delta.ECS;
            struct Position { public int Value; }
            static class Consumer
            {
                public static void Use(World world, Query query)
                {
                    world.ForEach<Position>(in query,
                        static (ref Position position) => position.Value++);
                }
            }
            """;

        string generated = GeneratedText(RunGenerator(source));

        Assert.That(generated, Does.Contain("ExecuteClosed_"));
        Assert.That(generated, Does.Contain("GeneratedForEachRuntime.OpenDense(world, in query"));
        Assert.That(generated, Does.Contain("while (execution.MoveNext(out var slots))"));
        Assert.That(generated, Does.Contain("ref T1 row0 = ref slots.GetGeneratedWriteReference<T1>(access0)"));
        Assert.That(generated, Does.Contain("for (int index = 0; index < count; index++)"));
        Assert.That(generated, Does.Contain("ref T1 component0 = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref row0, index)"));
        Assert.That(generated, Does.Contain("action(ref component0)"));
        Assert.That(generated, Does.Not.Contain("Ref<T1>(index)"));
        Assert.That(generated, Does.Not.Contain("ExecuteGeneratedForEach"));
    }

    [Test]
    public void GeneratedConsumerSourcesCompile()
    {
        GeneratorDriverRunResult run = RunGenerator();
        var generated = run.GeneratedTrees.Select(static tree => tree.GetText().ToString());
        CSharpCompilation compilation = CreateCompilation(new[] { RuntimeStubSource, ConsumerSource }.Concat(generated));

        var errors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors.Select(static error => error.ToString())));
    }

    [Test]
    public void RealConsumerProjectExecutesDenseAndSequenceGeneratedPaths()
    {
        int checksum = ConsumerProof.Run();

        Assert.That(checksum, Is.GreaterThan(0));
    }

    private static GeneratorDriverRunResult RunGenerator()
        => RunGenerator(ConsumerSource);

    private static GeneratorDriverRunResult RunGenerator(string consumerSource)
    {
        CSharpCompilation compilation = CreateCompilation(new[] { RuntimeStubSource, consumerSource });
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new DemandDrivenForEachGenerator().AsSourceGenerator());
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private static string GeneratedText(GeneratorDriverRunResult run)
        => string.Join(
            Environment.NewLine,
            run.GeneratedTrees
                .OrderBy(static tree => tree.FilePath, StringComparer.Ordinal)
                .Select(static tree => tree.GetText().ToString()));

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
        public readonly struct QuerySpec
        {
            public static QuerySpec WhereAll(ReadOnlySpan<ComponentId> components) => default;
        }
        public readonly struct Query { }
        public readonly struct ReadAccess { }
        public readonly struct WriteAccess { }
        public delegate void ForEachAction();
        public delegate void ForEachEntityAction(Entity entity);
        public delegate void ForEachContextAction<TContext>(ref TContext context);
        public delegate void ForEachContextEntityAction<TContext>(ref TContext context, Entity entity);
        public interface IForEach { }
        public interface IForEachEntity { }
        public interface IForEachContext<TContext> { }
        public interface IForEachContextEntity<TContext> { }
        public sealed class ComponentLayoutRegistry
        {
            public ComponentId GetPrimary(Type type) => default;
        }
        public ref struct ReadRow
        {
            public ref T Ref<T>(QuerySlots slots) => throw new NotImplementedException();
            public ref T Ref<T>(int index) => throw new NotImplementedException();
        }
        public ref struct QuerySlots
        {
            public Entity CurrentEntity => default;
            public bool MoveNext() => false;
            public ReadRow GetRow(ReadAccess access) => default;
            public ReadRow GetRow(WriteAccess access) => default;
        }
        public ref struct GeneratedQuerySlots
        {
            public Entity CurrentEntity => default;
            public int CurrentIndex => 0;
            public int Count => 0;
            public Entity EntityAt(int index) => default;
            public bool MoveNext() => false;
            public ref T GetGeneratedReadReference<T>(int queryComponentIndex) => throw new NotImplementedException();
            public ref T GetGeneratedReadReference<T>(ReadAccess access) => throw new NotImplementedException();
            public ref T GetGeneratedWriteReference<T>(int queryComponentIndex) => throw new NotImplementedException();
            public ref T GetGeneratedWriteReference<T>(WriteAccess access) => throw new NotImplementedException();
        }
        public ref struct GeneratedDenseExecution
        {
            public bool MoveNext(out GeneratedQuerySlots slots) { slots = default; return false; }
            public void Dispose() { }
        }
        public ref struct GeneratedSequenceCursor
        {
            public Entity Entity => default;
            public int Slot => 0;
            public ReadRow GetReadRow(int queryComponentIndex) => default;
            public ReadRow GetWriteRow(int queryComponentIndex) => default;
        }
        public interface IGeneratedForEachInvoker { void Invoke(ref GeneratedQuerySlots slots); }
        public interface IGeneratedSequenceInvoker { void Invoke(ref GeneratedSequenceCursor cursor); }
        public static class GeneratedForEachRuntime
        {
            public static GeneratedDenseExecution OpenDense(World world, in Query query, bool hasWrites) => default;
            public static ReadAccess CreateReadAccess(World world, in Query query, Type runtimeType) => default;
            public static WriteAccess CreateWriteAccess(World world, in Query query, Type runtimeType) => default;
            public static ReadAccess CreateReadAccess(World world, in Query query, ComponentId component, Type runtimeType) => default;
            public static WriteAccess CreateWriteAccess(World world, in Query query, ComponentId component, Type runtimeType) => default;
            public static int AccessRead(World world, in Query query, ComponentId component, Type runtimeType) => default;
            public static int AccessWrite(World world, in Query query, ComponentId component, Type runtimeType) => default;
            public static int AccessRead(World world, in Query query, Type runtimeType) => default;
            public static int AccessWrite(World world, in Query query, Type runtimeType) => default;
        }
        public sealed partial class World
        {
            public ComponentLayoutRegistry Layouts { get; } = new();
            public Query CreateQuery(in QuerySpec spec) => default;
            public QueryScope BeginScope(in Query query) => default;
            public void ForEach(in Query query, ForEachAction action) { }
            public void ForEachEntity(in Query query, ForEachEntityAction action) { }
            public void ForEach<TContext>(in Query query, ref TContext context, ForEachContextAction<TContext> action) { }
            public void ForEachEntity<TContext>(in Query query, ref TContext context, ForEachContextEntityAction<TContext> action) { }
            public void ExecuteGeneratedForEach<TInvoker>(in Query query, ref TInvoker invoker, bool hasWrites)
                where TInvoker : struct, IGeneratedForEachInvoker { }
            public void ExecuteGeneratedSequence<TInvoker>(ReadOnlySpan<Entity> entities, in Query query, ref TInvoker invoker, bool hasWrites)
                where TInvoker : struct, IGeneratedSequenceInvoker { }
        }
        public ref struct QueryScope
        {
            public QueryArchetypes Archetypes => default;
            public void Dispose() { }
        }
        public ref struct QueryArchetypes
        {
            public QueryArchetype Current => default;
            public bool MoveNext() => false;
        }
        public readonly ref struct QueryArchetype
        {
            public QueryArchetypeChunks Chunks => default;
        }
        public ref struct QueryArchetypeChunks
        {
            public QueryChunk Current => default;
            public bool MoveNext() => false;
        }
        public readonly ref struct QueryChunk
        {
            public QuerySlots Slots => default;
        }
        public readonly ref partial struct EntitySequence
        {
            public World GeneratedWorld => new();
            public ReadOnlySpan<Entity> GeneratedEntities => default;
        }
        public readonly ref partial struct FilteredEntitySequence
        {
            public World GeneratedWorld => new();
            public ReadOnlySpan<Entity> GeneratedEntities => default;
            public Query GeneratedQuery => default;
        }
        """;

    private const string ConsumerSource = """
        namespace Delta.ECS;
        using System;
        struct T1 { public int Value; }
        struct T2 { public int Value; }
        struct T3 { public int Value; }
        struct T4 { public int Value; }
        struct T5 { public int Value; }
        struct T6 { public int Value; }
        struct T7 { public int Value; }
        struct T8 { public int Value; }
        struct Context { public int Value; }
        struct EmptyFunctor : IForEach
        {
            public void Invoke() { }
        }
        struct Functor : IForEachContextEntity<Context>
        {
            public void Invoke(ref Context context, Entity entity, in T1 a, ref T2 b, in T3 c, ref T4 d) { context.Value += entity.Index + a.Value + c.Value; b.Value++; d.Value++; }
        }
        struct AllModesFunctor : IForEach
        {
            public void Invoke(ref readonly T1 a, ref T2 b, in T3 c, T4 d) { b.Value += a.Value + c.Value + d.Value; }
        }
        static class Consumer
        {
            public static void Use(World world, Query query, ComponentId c1, ComponentId c2, ComponentId c3, ComponentId c4, ComponentId c5, ComponentId c6, ComponentId c7, ComponentId c8)
            {
                world.ForEach(in query, static () => { });
                world.ForEachEntity(in query, static (Entity entity) => { _ = entity; });
                var context = new Context();
                world.ForEach<Context>(in query, ref context, static (ref Context value) => value.Value++);
                var emptyFunctor = new EmptyFunctor();
                world.ForEach(in query, ref emptyFunctor);
                var allModesFunctor = new AllModesFunctor();
                world.ForEach(in query, ref allModesFunctor);
                world.ForEach<T1>(in query, static (ref T1 value) => value.Value++);
                world.ForEach<T1, T2, T3, T4>(in query, static (in T1 a, ref T2 b, in T3 c, ref T4 d) => { b.Value += a.Value; d.Value += c.Value; });
                world.ForEach<T1, T2, T3, T4, T5>(in query, c1, c2, c3, c4, c5, static (in T1 a, ref T2 b, in T3 c, ref T4 d, ref T5 e) => { b.Value += a.Value; d.Value += c.Value; e.Value++; });
                world.ForEach<T1, T2, T3, T4, T5, T6, T7, T8>(in query, static (ref T1 a, in T2 b, ref T3 c, in T4 d, ref T5 e, in T6 f, ref T7 g, in T8 h) => { a.Value += b.Value; c.Value += d.Value; e.Value += f.Value; g.Value += h.Value; });
                var functor = new Functor();
                world.ForEachEntity(in query, ref context, ref functor);
                EntitySequence sequence = new();
                sequence.ForEachEntity<T1>(static (Entity entity, ref T1 value) => value.Value += entity.Index);
                FilteredEntitySequence filtered = new();
                filtered.ForEachEntity<T1, T2>(static (Entity entity, in T1 a, ref T2 b) => b.Value += a.Value + entity.Index);
            }
        }
        """;

    private const string AmbiguousFunctorSource = """
        namespace Delta.ECS;
        struct T1 { public int Value; }
        struct AmbiguousFunctor : IForEachEntity
        {
            public void Invoke(Entity entity, in T1 value) { }
            public void Invoke(Entity entity, ref T1 value) { }
        }
        static class Consumer
        {
            public static void Use(World world, Query query)
            {
                var functor = new AmbiguousFunctor();
                world.ForEachEntity(in query, ref functor);
            }
        }
        """;

    private const string SingleFunctorSource = """
        namespace Delta.ECS;
        struct T1 { public int Value; }
        struct SimpleFunctor : IForEachEntity
        {
            public void Invoke(Entity entity, ref T1 first) { }
        }
        static class Consumer
        {
            public static void Use(World world, Query query)
            {
                var functor = new SimpleFunctor();
                world.ForEachEntity(in query, ref functor);
            }
        }
        """;

    private const string PrivateFunctorSource = """
        namespace Delta.ECS;
        struct T1 { public int Value; }
        static class Consumer
        {
            private struct PrivateFunctor : IForEach
            {
                public void Invoke(ref T1 value) { }
            }

            public static void Use(World world, Query query)
            {
                var functor = new PrivateFunctor();
                world.ForEach(in query, ref functor);
            }
        }
        """;
}
