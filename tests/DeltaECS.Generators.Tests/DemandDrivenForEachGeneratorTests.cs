using System.Globalization;
using Delta.ECS.Generators.Consumer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
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

        AssertNoDiagnostics(run.Diagnostics.Where(static diagnostic => diagnostic.Id == "DECSGEN003"));
        Assert.That(generated, Does.Contain("ref global::Delta.ECS.SimpleFunctor functor"));
        Assert.That(generated, Does.Contain("GetGeneratedArray<global::Delta.ECS.T1>(rows, _route0)"));
        Assert.That(generated, Does.Not.Contain("IForEachEntity_W"));
    }

    [Test]
    public void RefReadonlyFunctorGeneratesConcreteOverload()
    {
        const string source = """
            namespace Delta.ECS;
            struct T1 { public int Value; }
            struct RefReadonlyFunctor : IForEach
            {
                public void Invoke(ref readonly T1 value) { _ = value.Value; }
            }
            static class Consumer
            {
                public static void Use(World world, Query query)
                {
                    var functor = new RefReadonlyFunctor();
                    world.ForEach(in query, ref functor);
                }
            }
            """;

        GeneratorDriverRunResult run = RunGenerator(source);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("ref global::Delta.ECS.RefReadonlyFunctor functor"));
        Assert.That(generated, Does.Contain("ref global::Delta.ECS.T1 component0"));
        Assert.That(generated, Does.Contain("var action = functor;"));
        Assert.That(generated, Does.Contain("action.Invoke(in component0)"));
        Assert.That(generated, Does.Contain("functor = action;"));
        Assert.That(generated, Does.Contain("component0 = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref component0, 1)"));
        Assert.That(generated.IndexOf("var action = functor;", StringComparison.Ordinal), Is.LessThan(generated.IndexOf("action.Invoke(in component0)", StringComparison.Ordinal)));
        Assert.That(generated.IndexOf("action.Invoke(in component0)", StringComparison.Ordinal), Is.LessThan(generated.IndexOf("functor = action;", StringComparison.Ordinal)));
        AssertCompiles(new[] { RuntimeStubSource, source }, run.GeneratedTrees);
    }

    [Test]
    public void ZeroArityFunctorIsRejectedWithoutGeneratedOverload()
    {
        const string source = """
            namespace Delta.ECS;
            struct EmptyFunctor : IForEach
            {
                public void Invoke() { }
            }
            static class Consumer
            {
                public static void Use(World world, Query query)
                {
                    var functor = new EmptyFunctor();
                    world.ForEach(in query, ref functor);
                }
            }
            """;

        GeneratorDriverRunResult run = RunGenerator(source);

        Assert.That(run.Diagnostics.Any(static diagnostic => diagnostic.Id == "DECSGEN001"), Is.True);
        Assert.That(GeneratedText(run), Does.Not.Contain("EmptyFunctor"));
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
    public void TupleContextElementNamesDoNotCreateDuplicateForEachShapes()
    {
        GeneratorDriverRunResult run = RunGenerator(TupleContextElementNamesSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(
            run.GeneratedTrees.Count(static tree => tree.FilePath.Contains("DemandForEach_", StringComparison.Ordinal)),
            Is.EqualTo(1));
        AssertCompiles(
            new[] { RuntimeStubSource, TupleContextElementNamesSource },
            run.GeneratedTrees);
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

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("ForEachAction_RWIV<T1, T2, T3, T4>"));
        Assert.That(generated, Does.Contain("ref readonly T1 component0"));
        Assert.That(generated, Does.Contain("in T3 component2"));
        Assert.That(generated, Does.Contain("T4 component3"));
    }

    [Test]
    public void StampIterationGeneratesTypedSelectorsAndAllCallbackForms()
    {
        GeneratorDriverRunResult run = RunGenerator(StampIterationSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("ForEachAction_I<Stamp>"));
        Assert.That(generated, Does.Contain("GetGeneratedStamp(access0, index)"));
        Assert.That(generated, Does.Contain("ExecuteEntityListParallel"));

        AssertCompiles(
            new[] { RuntimeStubSource, StampIterationSource },
            run.GeneratedTrees);
    }

    [Test]
    public void ParallelContextModesGenerateCompilableOverloads()
    {
        const string source = """
            namespace Delta.ECS;
            struct Position { public int Value; }
            struct State { public int Value; }
            struct IncrementFunctor : IForEach
            {
                public int Count;
                public void Invoke(ref Position position) { position.Value++; Count++; }
            }
            static class ParallelConsumer
            {
                public static void Use(World world, Query query)
                {
                    var state = new State();
                    var functor = new IncrementFunctor();
                    world.ForEachParallel(in query, ref functor, workerCount: 2);
                    ComponentId positionId = default;
                    world.ForEachParallel(in query, in state,
                        static (in State value, ref Position position) => position.Value += value.Value,
                        workerCount: 2);
                    world.ForEachParallel(in query, in state,
                        static (ref readonly State value, ref Position position) => position.Value += value.Value,
                        workerCount: 2);
                    world.ForEachEntityParallel(in query, state,
                        static (State value, Entity entity, ref Position position) => position.Value += value.Value + entity.Index,
                        workerCount: 2);
                    world.ForEachParallel<State, Position>(in query, positionId, in state,
                        static (in State value, ref Position position) => position.Value += value.Value,
                        workerCount: 2);
                }
            }
            """;

        GeneratorDriverRunResult run = RunGenerator(source);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("ForEachContextActionIn<TContext, T1>"));
        Assert.That(generated, Does.Contain("ForEachContextEntityActionValue<TContext, T1>"));
        Assert.That(generated, Does.Contain("public bool RequiresSingleThread => false;"));
        Assert.That(generated, Does.Contain("_functor.Invoke(ref row0);"));
        Assert.That(generated, Does.Contain("row0 = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref row0, 1)"));
        Assert.That(generated, Does.Not.Contain("Unsafe.Add(ref row0, index)"));
        Assert.That(generated, Does.Not.Contain("var action = _functor;"));
        Assert.That(generated, Does.Not.Contain("_functor = action;"));

        AssertCompiles(new[] { RuntimeStubSource, source }, run.GeneratedTrees);
    }

    [Test]
    public void ParallelRefStateIsRejected()
    {
        const string source = """
            namespace Delta.ECS;
            struct Position { public int Value; }
            struct State { public int Value; }
            static class ParallelConsumer
            {
                public static void Use(World world, Query query)
                {
                    var state = new State();
                    world.ForEachParallel(in query, ref state,
                        static (ref State value, ref Position position) => position.Value += value.Value);
                }
            }
            """;

        GeneratorDriverRunResult run = RunGenerator(source);

        Assert.That(run.Diagnostics.Any(static diagnostic => diagnostic.Id == "DECSGEN001"), Is.True);
        Assert.That(GeneratedText(run), Does.Not.Contain("ForEachContextAction<State"));
    }

    [Test]
    public void ParallelContextInterceptionKeepsReadOnlyContextModifiers()
    {
        const string source = """
            namespace Delta.ECS;
            struct Position { public int Value; }
            struct State { public int Value; }
            static class ParallelConsumer
            {
                public static void Use(World world, Query query)
                {
                    var state = new State();
                    world.ForEachParallel(in query, in state,
                        static (in State value, ref Position position) =>
                        {
                            if (position.Value < 0)
                            {
                                return;
                            }

                            position.Value += value.Value;
                        },
                        workerCount: 2);
                }
            }
            """;

        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(source);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics.Where(static diagnostic => diagnostic.Id == "DECSGEN005"));
        Assert.That(generated, Does.Contain("InterceptedParallelInvoker_"));
        Assert.That(generated, Does.Contain("in value"));

        AssertCompiles(new[] { RuntimeStubSource, source }, run.GeneratedTrees);
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

        GeneratorDriverRunResult run = RunGenerator(source);
        string generated = GeneratedText(run);

        Assert.That(generated, Does.Contain($"ForEachAction<{componentTypes}>"));
        Assert.That(generated, Does.Contain("SetWriteRoutes(new int[] { _route0,"));
        Assert.That(generated, Does.Contain("_route31 }"));
        Assert.That(generated, Does.Contain("_route31 = GeneratedForEachRuntime.GetPreparedWriteRoute<T32>(in query);"));
    }

    [Test]
    public void NoIdGenerationUsesCachedPrimaryRouteWithExtraQueryComponent()
    {
        string generated = GeneratedText(RunGenerator());

        Assert.That(generated, Does.Contain("GetPreparedReadRoute<T1>(in query)"));
        Assert.That(generated, Does.Contain("GetPreparedWriteRoute<T2>(in query)"));
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
        Assert.That(generated, Does.Contain("GeneratedForEachRuntime.OpenBoundDense<"));
        Assert.That(generated, Does.Contain("int chunkCount = execution.Rows.Length;"));
        Assert.That(generated, Does.Not.Contain("var batch = execution.Rows[chunkIndex]"));
        Assert.That(generated, Does.Contain("ref var batchCursor = ref"));
        Assert.That(generated, Does.Contain("for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)"));
        Assert.That(generated, Does.Contain("batchCursor = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref batchCursor, 1)"));
        Assert.That(generated, Does.Not.Contain("Unsafe.Add(ref firstBatch, chunkIndex)"));
        Assert.That(generated, Does.Contain("_route0 = GeneratedForEachRuntime.GetPreparedWriteRoute<T1>(in query);"));
        Assert.That(generated, Does.Contain("ref T1 component0 = ref global::System.Runtime.CompilerServices.Unsafe.NullRef<T1>()"));
        Assert.That(generated, Does.Contain("component0 = ref GeneratedForEachRuntime.GetGeneratedArrayReference(batch.Row0)"));
        Assert.That(generated, Does.Contain("for (int index = 0; index < count; index++)"));
        Assert.That(generated, Does.Contain("action(ref component0)"));
        Assert.That(generated, Does.Contain("component0 = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref component0, 1)"));
        Assert.That(generated, Does.Not.Contain("Unsafe.Add(ref component0, index)"));
        Assert.That(generated, Does.Contain(
            "SetWriteRoutes(new int[] { _route0 });"));
        Assert.That(generated, Does.Not.Contain("slots.MarkGeneratedWrite"));
        Assert.That(generated, Does.Not.Contain("Ref<T1>(index)"));
        Assert.That(generated, Does.Not.Contain("ExecuteGeneratedForEach"));
    }

    [Test]
    public void RefContextUsesAHotLoopLocalAndWritesBackAfterDenseExecution()
    {
        const string source = """
            namespace Delta.ECS;
            struct Position { public int Value; }
            struct State { public int Value; }
            static class Consumer
            {
                public static void Use(World world, Query query, ref State state)
                {
                    world.ForEach<State, Position>(in query, ref state,
                        static (ref State context, ref Position position) =>
                            context.Value += position.Value);
                }
            }
            """;

        GeneratorDriverRunResult run = RunGenerator(source);
        string generated = GeneratedText(run);

        Assert.That(generated, Does.Contain("TContext contextCopy = context;"));
        Assert.That(generated, Does.Contain("action(ref contextCopy, ref component0)"));
        Assert.That(generated, Does.Contain("context = contextCopy;"));
        Assert.That(
            generated.IndexOf("action(ref contextCopy, ref component0)", StringComparison.Ordinal),
            Is.LessThan(generated.IndexOf("context = contextCopy;", StringComparison.Ordinal)));
        AssertCompiles(new[] { RuntimeStubSource, source }, run.GeneratedTrees);
    }

    [Test]
    public void ExplicitIdEntityInterceptionBindsRowsFromChunkArrays()
    {
        const string source = """
            namespace Delta.ECS;
            struct Position { public int Value; }
            static class Consumer
            {
                public static void Use(World world, Query query, ComponentId positionId)
                {
                    world.ForEachEntity<Position>(in query, positionId,
                        static (Entity entity, ref Position position) => position.Value += entity.Index);
                }
            }
            """;

        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(source);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("GetGeneratedArray<global::Delta.ECS.Position>(access0)"));
        Assert.That(generated, Does.Contain("Unsafe.Add(ref __deltaEcs_row_"));
        AssertCompiles(new[] { RuntimeStubSource, source }, run.GeneratedTrees);
    }

    [Test]
    public void ReadOnlyDenseGenerationUsesReadExecutionWithoutWriteState()
    {
        const string source = """
            namespace Delta.ECS;
            struct Position { public int Value; }
            static class Consumer
            {
                public static void Use(World world, Query query)
                {
                    world.ForEach<Position>(in query,
                        static (in Position position) => _ = position.Value);
                }
            }
            """;

        string generated = GeneratedText(RunGenerator(source));

        Assert.That(generated, Does.Contain("GeneratedForEachRuntime.OpenBoundDenseRead<"));
        Assert.That(generated, Does.Contain("for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)"));
        Assert.That(generated, Does.Not.Contain("GeneratedForEachRuntime.OpenBoundDense<"));
        Assert.That(generated, Does.Not.Contain("OpenWriteDense(world, in query)"));
        Assert.That(generated, Does.Not.Contain("MarkGeneratedWrite(access0)"));
    }

    [Test]
    public void MultipleWriteRowsUseOneArchetypePlanTraversal()
    {
        const string source = """
            namespace Delta.ECS;
            struct Position { public int Value; }
            struct Velocity { public int Value; }
            static class Consumer
            {
                public static void Use(World world, Query query)
                {
                    world.ForEach<Position, Velocity>(in query,
                        static (ref Position position, ref Velocity velocity) =>
                        {
                            position.Value += velocity.Value;
                        });
                }
            }
            """;

        string generated = GeneratedText(RunGenerator(source));

        Assert.That(generated, Does.Contain("SetWriteRoutes(new int[] { _route0, _route1 });"));
        Assert.That(generated, Does.Not.Contain("execution.MarkArchetypeWrite(access0)"));
        Assert.That(generated, Does.Not.Contain("execution.MarkArchetypeWrite(access1)"));
    }

    [Test]
    public void GeneratedConsumerSourcesCompile()
    {
        GeneratorDriverRunResult run = RunGenerator();
        var generated = run.GeneratedTrees.Select(static tree => tree.GetText().ToString());
        AssertCompiles(CreateCompilation(new[] { RuntimeStubSource, ConsumerSource }.Concat(generated)));
    }

    [Test]
    public void EnabledStaticLambdaGeneratesAClosedKernelWithoutCallbackDispatch()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(InterceptionSource);
        string generated = GeneratedText(run);
        string intercepted = string.Join(
            "\n",
            run.GeneratedTrees
                .Where(static tree => tree.FilePath.Contains("DemandForEachInterceptor_", StringComparison.Ordinal))
                .Select(static tree => tree.GetText().ToString()));

        AssertNoDiagnostics(run.Diagnostics.Where(static diagnostic => diagnostic.Id == "DECSGEN005"));
        Assert.That(generated, Does.Not.Contain("InvokeInterceptedCallback_"));
        Assert.That(generated, Does.Contain("InterceptsLocationAttribute"));
        Assert.That(generated, Does.Contain("ExecuteInterceptedClosed_"));
        Assert.That(generated, Does.Contain("value.Value++"));
        Assert.That(generated, Does.Contain("ForEachAction<global::Delta.ECS.T1> _"));
        Assert.That(generated, Does.Not.Contain("InterceptedFunctor_"));
        Assert.That(generated, Does.Not.Contain("ref functor"));
        Assert.That(intercepted, Does.Contain("var batch = batchCursor;"));
        Assert.That(intercepted, Does.Not.Contain("= execution.Rows[chunk"));
        Assert.That(intercepted, Does.Contain("ref var batchCursor = ref"));
        Assert.That(intercepted, Does.Contain("MemoryMarshal.GetReference(execution.Rows)"));
        Assert.That(intercepted, Does.Contain("ref global::Delta.ECS.T1 row"));
        Assert.That(intercepted, Does.Contain("Unsafe.NullRef<global::Delta.ECS.T1>()"));
        Assert.That(intercepted, Does.Contain("= ref GeneratedForEachRuntime.GetGeneratedArrayReference("));

        AssertCompiles(new[] { RuntimeStubSource, InterceptionSource }, run.GeneratedTrees);
    }

    [Test]
    public void NestedComponentNamespacesAreImportedIntoInterceptedKernels()
    {
        const string source = """
            namespace Delta.ECS
            {
                namespace NestedComponents
                {
                    struct NestedPosition { public int Value; }
                }

                static class NestedConsumer
                {
                    public static void Use(World world, Query query)
                    {
                        world.ForEach(
                            in query,
                            static (ref NestedComponents.NestedPosition position) => position.Value++);
                    }
                }
            }
            """;

        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(source);
        string intercepted = string.Join(
            "\n",
            run.GeneratedTrees
                .Where(static tree => tree.FilePath.Contains("DemandForEachInterceptor_", StringComparison.Ordinal))
                .Select(static tree => tree.GetText().ToString()));

        AssertNoDiagnostics(run.Diagnostics.Where(static diagnostic => diagnostic.Id == "DECSGEN005"));
        Assert.That(intercepted, Does.Contain("using global::Delta.ECS.NestedComponents;"));
        Assert.That(intercepted, Does.Contain("NestedPosition"));
        AssertCompiles(new[] { RuntimeStubSource, source }, run.GeneratedTrees);
    }

    [Test]
    public void StaticEntityListLambdasUseTheInterceptedEntityListKernel()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(EntityListInterceptionSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics.Where(static diagnostic => diagnostic.Id == "DECSGEN005"));
        Assert.That(generated, Does.Contain("ExecuteEntityList(world, in query, entities, ref invoker"));
        Assert.That(generated, Does.Contain("ExecuteEntityListParallel(world, in query, entities, ref invoker"));
        Assert.That(generated, Does.Contain("value.Value++"));
        Assert.That(generated, Does.Contain("entity.Index"));

        AssertCompiles(
            new[] { RuntimeStubSource, EntityListInterceptionSource },
            run.GeneratedTrees);
    }

    [Test]
    public void InterceptedLambdaWithReturnKeepsCallbackBoundary()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(ReturningInterceptionSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics.Where(static diagnostic => diagnostic.Id == "DECSGEN005"));
        Assert.That(generated, Does.Contain("InvokeInterceptedCallback_"));
        Assert.That(generated, Does.Contain("return;"));
    }

    [Test]
    public void CSharp9FallsBackToOrdinaryGeneratedForEach()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(
            InterceptionSource,
            LanguageVersion.CSharp9);
        string generated = GeneratedText(run);

        Assert.That(generated, Does.Not.Contain("DemandForEachInterceptor_"));
        Assert.That(generated, Does.Not.Contain("InterceptsLocationAttribute"));
        AssertNoDiagnostics(run.Diagnostics);

        AssertNoDiagnostics(run.GeneratedTrees
            .Select(tree => CSharpSyntaxTree.ParseText(tree.GetText(), new CSharpParseOptions(LanguageVersion.CSharp9), tree.FilePath))
            .SelectMany(static tree => tree.GetDiagnostics())
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Test]
    public void CSharp9ForEachInContextOmitsRefReadonlyDelegateContracts()
    {
        const string source = """
            namespace Delta.ECS
            {
            struct Position { public int Value; }
            struct Range { public int Value; }
            struct Target { public int Value; }
            struct State { public int Value; }
            static class Consumer
            {
                public static void Use(World world, Query query)
                {
                    var state = new State();
                    System.ReadOnlySpan<Entity> entities = default;
                    world.ForEachEntity(
                        entities,
                        in query,
                        static (Entity entity) => _ = entity);
                    world.ForEachEntityParallel(
                        entities,
                        in query,
                        static (Entity entity) => _ = entity,
                        workerCount: 2);
                    world.ForEachParallel<State, Position, Range, Target>(
                        in query,
                        in state,
                        static (in State value, in Position position, in Range range, ref Target target) =>
                            target.Value += value.Value + position.Value + range.Value,
                        workerCount: 2);
                }
            }
            }
            """;

        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(source, LanguageVersion.CSharp9);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Not.Contain("ForEachContextActionRefReadonly"));

        AssertCompiles(
            new[] { RuntimeStubFor(LanguageVersion.CSharp9), source },
            run.GeneratedTrees,
            LanguageVersion.CSharp9);
    }

    [Test]
    public void InterceptedCallSitesKeepTheirUsingAliasesIsolated()
    {
        string[] consumerSources =
        {
            InterceptionAliasSharedSource,
            InterceptionAliasFirstSource,
            InterceptionAliasSecondSource
        };
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(consumerSources);

        AssertNoDiagnostics(run.Diagnostics.Where(static diagnostic => diagnostic.Id == "DECSGEN005"));
        Assert.That(
            run.GeneratedTrees.Count(static tree => tree.FilePath.Contains("DemandForEachInterceptor_", StringComparison.Ordinal)),
            Is.EqualTo(2));

        AssertCompiles(new[] { RuntimeStubSource }.Concat(consumerSources), run.GeneratedTrees);
    }

    [Test]
    public void EnabledStaticMethodGroupsGenerateDirectFunctorCalls()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(StaticMethodGroupInterceptionSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics.Where(static diagnostic => diagnostic.Id == "DECSGEN005"));
        Assert.That(generated, Does.Contain("global::Delta.ECS.Consumer.Update(ref component0)"));
        Assert.That(generated, Does.Contain("global::Delta.ECS.Callbacks.Update(ref component0)"));
        Assert.That(generated, Does.Contain("global::Delta.ECS.Consumer.UpdateWithContext(ref context, in component0)"));
        Assert.That(generated, Does.Contain("global::Delta.ECS.Consumer.UpdateParallel(in context, ref component0)"));
        Assert.That(generated, Does.Contain("global::Delta.ECS.Consumer.UpdateEntity(entity, ref component0)"));
        Assert.That(generated, Does.Contain("global::Delta.ECS.Consumer.UpdateEntityParallel(in context, entity, ref component0)"));
        Assert.That(generated, Does.Contain("ExecuteInterceptedClosed_"));
        Assert.That(generated, Does.Not.Contain("InterceptedFunctor_"));

        AssertCompiles(
            new[] { RuntimeStubSource, StaticMethodGroupInterceptionSource },
            run.GeneratedTrees);
    }

    [Test]
    public void StampMethodGroupsGenerateDirectReadOnlyCallbacks()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(StampMethodGroupSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics.Where(static diagnostic => diagnostic.Id == "DECSGEN005"));
        Assert.That(generated, Does.Contain("global::Delta.ECS.StampCallbacks.Update(in component0)"));

        AssertCompiles(
            new[] { RuntimeStubSource, StampMethodGroupSource },
            run.GeneratedTrees);
    }

    [Test]
    public void StaticMethodGroupsCanInferComponentTypes()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(ImplicitStaticMethodGroupInterceptionSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics.Where(static diagnostic => diagnostic.Id == "DECSGEN005"));
        Assert.That(generated, Does.Contain("global::Delta.ECS.Consumer.Update(ref component0)"));

        AssertCompiles(
            new[] { RuntimeStubSource, ImplicitStaticMethodGroupInterceptionSource },
            run.GeneratedTrees);
    }

    [Test]
    public void InstanceMethodGroupsReportFallbackReason()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(InstanceMethodGroupInterceptionSource);
        Diagnostic diagnostic = run.Diagnostics.Single(static value => value.Id == "DECSGEN005");

        Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture), Does.Contain("instance method"));
        Assert.That(GeneratedText(run), Does.Contain("ForEachAction<T1>"));
        Assert.That(GeneratedText(run), Does.Not.Contain("InterceptedFunctor_"));

        AssertCompiles(
            new[] { RuntimeStubSource, InstanceMethodGroupInterceptionSource },
            run.GeneratedTrees);
    }

    [Test]
    public void EnabledUnsupportedLambdaReportsFallbackReason()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(UnsupportedInterceptionSource);
        Diagnostic diagnostic = run.Diagnostics.Single(static value => value.Id == "DECSGEN005");

        Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Info));
        Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture), Does.Contain("not a static lambda"));
        Assert.That(GeneratedText(run), Does.Contain("ForEachAction<T1>"));
    }

    [Test]
    public void EnabledPrivateLambdaReferenceReportsFallbackReason()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(PrivateReferenceInterceptionSource);
        Diagnostic diagnostic = run.Diagnostics.Single(static value => value.Id == "DECSGEN005");

        Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Info));
        Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture), Does.Contain("private or protected"));
        Assert.That(GeneratedText(run), Does.Contain("ForEachAction<T1>"));
        Assert.That(GeneratedText(run), Does.Not.Contain("InterceptedFunctor_"));
    }

    [Test]
    public void RealConsumerProjectExecutesDenseGeneratedPaths()
    {
        int checksum = ConsumerProof.Run();

        Assert.That(checksum, Is.GreaterThan(0));
    }

    [Test]
    public void RealConsumerProjectExecutesGeneratedStructuralPaths()
    {
        Assert.That(ConsumerProof.RunStructural(), Is.EqualTo(21));
    }

    [Test]
    public void RealConsumerProjectExecutesGeneratedQueryPaths()
    {
        Assert.That(ConsumerProof.RunGenericQueries(), Is.EqualTo(1));
    }

    [Test]
    public void RealConsumerProjectExecutesGeneratedWherePaths()
    {
        Assert.That(ConsumerProof.RunGeneratedWhere(), Is.EqualTo(1));
    }

    [Test]
    public void RealConsumerProjectExecutesGeneratedQueryComposition()
    {
        Assert.That(ConsumerProof.RunGenericQueries(), Is.EqualTo(1));
    }

    [Test]
    public void StructuralGeneratorEmitsOnlyUsedGenericShapes()
    {
        GeneratorDriverRunResult run = RunGenerator(StructuralSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Not.Contain("public static Entity Create<T1, T2>(this World target)"));
        Assert.That(generated, Does.Contain("public static int Create<T1, T2>(this World target, int count, global::System.Span<Entity> output)"));
        Assert.That(generated, Does.Contain("public static bool Add<T1, T2>(this World target, Entity entity)"));
        Assert.That(generated, Does.Contain("public static bool Add<T1, T2>(this World target, Entity entity, in T1 value0, in T2 value1)"));
        Assert.That(generated, Does.Contain("ExecuteGeneratedAdd"));
        Assert.That(generated, Does.Contain("public static bool Set<T1, T2>(this World target, Entity entity, in T1 value0, in T2 value1)"));
        Assert.That(generated, Does.Contain("ExecuteGeneratedSet"));
        Assert.That(generated, Does.Contain("SetUnsafe(component0, in value0)"));
        Assert.That(generated, Does.Contain("public static int Add<T1, T2>(this World target"));
        Assert.That(generated, Does.Contain("public static int Remove<T1, T2>(this World target"));
        Assert.That(generated, Does.Contain("public static int Create<T1, T2>(this World target"));
        Assert.That(generated, Does.Contain("GetGeneratedPrimaryComponentIds<global::Delta.ECS.GeneratedPrimaryComponentSetKey<T1, T2>>"));

        AssertCompiles(new[] { RuntimeStubSource, StructuralSource }, run.GeneratedTrees);
    }

    [Test]
    public void StructuralGeneratorEmitsPositionalComponentIdCreateShapes()
    {
        GeneratorDriverRunResult run = RunGenerator(ExplicitCreateSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("public static int Create(this World target, ComponentId component0, ComponentId component1, int count)"));
        Assert.That(generated, Does.Contain("public static int Create(this World target, ComponentId component0, int count, global::System.Span<Entity> output)"));
        Assert.That(generated, Does.Contain("components[0] = component0;"));

        AssertCompiles(new[] { RuntimeStubSource, ExplicitCreateSource }, run.GeneratedTrees);
    }

    [Test]
    public void GeneratedGrammarMatrixCompilesTypedAndExplicitSelectors()
    {
        GeneratorDriverRunResult run = RunGenerator(GrammarMatrixSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("public static void ForEach<T1, T2>(this World world"));
        Assert.That(generated, Does.Contain("public static void ForEachParallel<T1, T2>(this World world"));
        Assert.That(generated, Does.Contain("public static int Add<T1, T2>(this World target"));
        Assert.That(generated, Does.Contain("public static int Remove<T1, T2>(this World target"));
        Assert.That(generated, Does.Contain("public static int Create<T1, T2>(this World target"));

        AssertCompiles(new[] { RuntimeStubSource, GrammarMatrixSource }, run.GeneratedTrees);
    }

    [Test]
    public void QueryGeneratorEmitsTypedWorldFactories()
    {
        GeneratorDriverRunResult run = RunGenerator(GenericQuerySource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("public static Query WhereAll<T1, T2>(this World world)"));
        Assert.That(generated, Does.Contain("public static Query WhereAny<T1, T2, T3>(this World world)"));
        Assert.That(generated, Does.Contain("public static Query WhereNone<T1>(this World world)"));
        Assert.That(generated, Does.Contain("GetGeneratedPrimaryComponentIds<global::Delta.ECS.GeneratedPrimaryComponentSetKey<T1, T2>>"));
        Assert.That(generated, Does.Contain("QuerySpec.WhereAny(components)"));

        AssertCompiles(new[] { RuntimeStubSource, GenericQuerySource }, run.GeneratedTrees);
    }

    [Test]
    public void QueryGeneratorSupportsPrivateNestedComponentTypes()
    {
        GeneratorDriverRunResult run = RunGenerator(PrivateNestedQueryComponentSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("public static Query WhereAll<T1>(this World world)"));
        AssertCompiles(new[] { RuntimeStubSource, PrivateNestedQueryComponentSource }, run.GeneratedTrees);
    }

    [Test]
    public void StructuralDocumentationUsesTheOperationName()
    {
        GeneratorDriverRunResult run = RunGenerator(StructuralSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("Executes the generated Add operation."));
        Assert.That(generated, Does.Contain("Executes the generated Create operation."));
        Assert.That(generated, Does.Not.Contain("Executes the generated Add|"));
        AssertCompiles(new[] { RuntimeStubSource, StructuralSource }, run.GeneratedTrees);
    }

    [Test]
    public void QueryGeneratorEmitsComposableQueryFactories()
    {
        GeneratorDriverRunResult run = RunGenerator(GenericQueryChainSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("public static Query WhereAll<T1, T2, T3>(this Query query)"));
        Assert.That(generated, Does.Contain("public static Query WhereNone<T1, T2>(this Query query)"));
        Assert.That(generated, Does.Contain("public static Query WhereAny<T1, T2>(this Query query)"));
        Assert.That(generated, Does.Contain("ComposeGeneratedQuery(in query, additions)"));

        AssertCompiles(new[] { RuntimeStubSource, GenericQueryChainSource }, run.GeneratedTrees);
    }

    [Test]
    public void QueryGeneratorSharesFactoriesWithTheSameSignatureAcrossSystems()
    {
        GeneratorDriverRunResult run = RunGenerator(QueryFactoriesWithDuplicateSignaturesSource);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(
            run.GeneratedTrees.Count(static tree => tree.FilePath.Contains("GeneratedQuery_", StringComparison.Ordinal)),
            Is.EqualTo(1));

        AssertCompiles(
            new[] { RuntimeStubSource, QueryFactoriesWithDuplicateSignaturesSource },
            run.GeneratedTrees);
    }

    [Test]
    public void DemandDrivenGeneratorSupportsPositionalComponentIdSelectors()
    {
        GeneratorDriverRunResult run = RunGenerator(ExplicitSelectorSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("ComponentId component0, ComponentId component1"));
        Assert.That(generated, Does.Contain("GetPreparedWriteAccess(in query, componentId0"));
        Assert.That(generated, Does.Contain("GetPreparedReadAccess(in query, componentId1"));

        AssertCompiles(new[] { RuntimeStubSource, ExplicitSelectorSource }, run.GeneratedTrees);
    }

    [Test]
    public void WhereGeneratorEmitsPredicateViewsAndMutationTerminals()
    {
        GeneratorDriverRunResult run = RunGenerator(WhereMutationSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(
            run.GeneratedTrees.Count(static tree => tree.FilePath.Contains("GeneratedWhere_", StringComparison.Ordinal)),
            Is.EqualTo(2));
        Assert.That(generated, Does.Contain("GeneratedWhereQuery_"));
        Assert.That(generated, Does.Contain("ExecuteGeneratedWhereDestroy"));
        Assert.That(generated, Does.Contain("ExecuteGeneratedWhereAdd"));
        Assert.That(generated, Does.Contain("ExecuteGeneratedWhereRemove"));
        Assert.That(generated, Does.Contain("ExecuteGeneratedWhereForEach"));
        Assert.That(generated, Does.Contain("public int Add<U1>(in U1 value0)"));
        Assert.That(generated, Does.Contain("public int Add<U1>(ComponentId component0, in U1 value0)"));
        Assert.That(generated, Does.Contain("public int Add<U1>(ComponentId component0)"));
        Assert.That(generated, Does.Contain("public int Remove<U1>(ComponentId component0)"));
        Assert.That(generated, Does.Contain("public int Remove<U1, U2>(ComponentId component0, ComponentId component1)"));
        Assert.That(generated, Does.Contain("public int Add<U1, U2>(in U1 value0, in U2 value1)"));
        Assert.That(generated, Does.Contain("public int Add<U1, U2>(ComponentId component0, ComponentId component1, in U1 value0, in U2 value1)"));
        Assert.That(generated, Does.Contain("public void Execute(ref GeneratedQuerySlots slots, ref GeneratedWhereStructuralContext context)"));
        Assert.That(generated, Does.Contain("public void Invoke(ref GeneratedQuerySlots slots)"));
        Assert.That(generated, Does.Not.Contain("Invoke(ref GeneratedQuerySlots slots, int index)"));

        AssertCompiles(new[] { RuntimeStubSource, WhereMutationSource }, run.GeneratedTrees);
    }

    [Test]
    public void WhereZeroArityEntityTerminalGeneratesValidCode()
    {
        GeneratorDriverRunResult run = RunGenerator(WhereZeroArityTerminalSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("ExecuteGeneratedWhereForEach"));
        Assert.That(generated, Does.Contain("GeneratedWhereAction_"));
        Assert.That(generated, Does.Contain("(Entity entity)"));
        Assert.That(generated, Does.Not.Contain("(entity, )"));
        AssertCompiles(new[] { RuntimeStubSource, WhereZeroArityTerminalSource }, run.GeneratedTrees);
    }

    [Test]
    public void CSharp9WhereGenerationCompilesStructuralTerminals()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(WhereMutationSource, LanguageVersion.CSharp9);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Not.Contain("scoped ref GeneratedQuerySlots"));
        Assert.That(generated, Does.Not.Contain("private ref "));

        AssertCompiles(
            new[] { RuntimeStubFor(LanguageVersion.CSharp9), WhereMutationSource },
            run.GeneratedTrees,
            LanguageVersion.CSharp9);
    }

    [Test]
    public void WhereGeneratorDeduplicatesGenericMutationTerminalsAcrossComponentTypes()
    {
        GeneratorDriverRunResult run = RunGenerator(WhereDuplicateMutationSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated.Split("public int Add<U1>()", StringSplitOptions.None).Length - 1, Is.EqualTo(1));

        AssertCompiles(new[] { RuntimeStubSource, WhereDuplicateMutationSource }, run.GeneratedTrees);
    }

    [Test]
    public void GenericCallSitesShareThePublicShapeWhileKeepingConcreteContexts()
    {
        GeneratorDriverRunResult run = RunGenerator(GenericCallSitesSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(
            run.GeneratedTrees.Count(static tree => tree.FilePath.Contains("DemandForEach_", StringComparison.Ordinal)),
            Is.EqualTo(1));
        Assert.That(
            run.GeneratedTrees.Count(static tree => tree.FilePath.Contains("GeneratedWhere_", StringComparison.Ordinal)
                && !tree.FilePath.Contains("Interceptor", StringComparison.Ordinal)),
            Is.EqualTo(1));
        Assert.That(generated, Does.Contain("ForEachContextAction<TContext, T1>"));
        AssertCompiles(new[] { RuntimeStubSource, GenericCallSitesSource }, run.GeneratedTrees);
    }

    [Test]
    public void GenericInterceptionSitesKeepTheirConcreteComponentAndContextTypes()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(GenericCallSitesSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics.Where(static diagnostic => diagnostic.Id != "DECSGEN005"));
        Assert.That(
            run.GeneratedTrees.Count(static tree => tree.FilePath.Contains("DemandForEach_", StringComparison.Ordinal)
                && !tree.FilePath.Contains("Interceptor", StringComparison.Ordinal)),
            Is.EqualTo(1));
        Assert.That(
            run.GeneratedTrees.Count(static tree => tree.FilePath.Contains("DemandForEachInterceptor_", StringComparison.Ordinal)),
            Is.EqualTo(2));
        Assert.That(generated, Does.Contain("PositionContext"));
        Assert.That(generated, Does.Contain("VelocityContext"));
        AssertCompiles(new[] { RuntimeStubSource, GenericCallSitesSource }, run.GeneratedTrees);
    }

    [Test]
    public void WhereStructuralTerminalsSeparateExplicitIdsFromValues()
    {
        GeneratorDriverRunResult run = RunGenerator(WhereStructuralParameterMatrixSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("public int Add<U1>()"));
        Assert.That(generated, Does.Contain("public int Add<U1>(ComponentId component0)"));
        Assert.That(generated, Does.Contain("public int Add<U1>(in U1 value0)"));
        Assert.That(generated, Does.Contain("public int Add<U1>(ComponentId component0, in U1 value0)"));
        Assert.That(generated, Does.Contain("public int Remove<U1>()"));
        Assert.That(generated, Does.Contain("public int Remove<U1>(ComponentId component0)"));
        Assert.That(generated, Does.Contain("public int Remove<U1, U2>()"));
        Assert.That(generated, Does.Contain("public int Remove<U1, U2>(ComponentId component0, ComponentId component1)"));
        AssertCompiles(new[] { RuntimeStubSource, WhereStructuralParameterMatrixSource }, run.GeneratedTrees);

        GeneratorDriverRunResult intercepted = RunGeneratorWithInterceptors(WhereStructuralParameterMatrixSource);
        AssertNoDiagnostics(intercepted.Diagnostics.Where(static diagnostic => diagnostic.Id != "DECSGEN005"));
        string interceptedText = GeneratedText(intercepted);
        Assert.That(interceptedText, Does.Contain("ValueStructuralInvoker_"));
        Assert.That(interceptedText, Does.Not.Contain("return view.Add"));
        AssertCompiles(new[] { RuntimeStubSource, WhereStructuralParameterMatrixSource }, intercepted.GeneratedTrees);
    }

    [Test]
    public void WherePredicateSupportsInAndRefReadonlyComponents()
    {
        GeneratorDriverRunResult run = RunGenerator(WhereReadonlyPredicateSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("Entity entity, in T1 component0"));
        Assert.That(generated, Does.Contain("Entity entity, ref readonly T1 component0, in T2 component1"));
        Assert.That(generated, Does.Contain("ref readonly T1 component0"));

        AssertCompiles(new[] { RuntimeStubSource, WhereReadonlyPredicateSource }, run.GeneratedTrees);
    }

    [Test]
    public void WhereSupportsPredicatesWithoutEntityAndWhereEntityKeepsEntityForm()
    {
        GeneratorDriverRunResult run = RunGenerator(WhereWithoutEntitySource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain(" Where<T1>(this World world, in Query query"));
        Assert.That(generated, Does.Contain(" WhereEntity<T1>(this World world, in Query query"));
        Assert.That(generated, Does.Contain("GeneratedWherePredicate_"));
        Assert.That(generated, Does.Contain("bool GeneratedWherePredicate_"));
        Assert.That(generated, Does.Contain("firstEntity = ref slots.GetGeneratedEntityReference()"));
        Assert.That(generated, Does.Not.Contain("slots.EntityAt(index)"));

        AssertCompiles(new[] { RuntimeStubSource, WhereWithoutEntitySource }, run.GeneratedTrees);
    }

    [Test]
    public void CSharp9WhereWithoutEntityStructuralTerminalCompiles()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(WhereWithoutEntitySource, LanguageVersion.CSharp9);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Not.Contain("scoped ref GeneratedQuerySlots"));
        Assert.That(generated, Does.Not.Contain("private ref "));

        AssertCompiles(
            new[] { RuntimeStubFor(LanguageVersion.CSharp9), WhereWithoutEntitySource },
            run.GeneratedTrees,
            LanguageVersion.CSharp9);
    }

    [Test]
    public void WhereRejectsEntityParameterAndRequiresWhereEntity()
    {
        GeneratorDriverRunResult run = RunGenerator(WhereEntityParameterSource);

        Diagnostic diagnostic = run.Diagnostics.Single(static value => value.Id == "DECSGEN007");
        Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture), Does.Contain("WhereEntity"));
        Assert.That(run.GeneratedTrees, Is.Empty);
    }

    [Test]
    public void WherePredicateRejectsWritableComponents()
    {
        GeneratorDriverRunResult run = RunGenerator(WhereWritablePredicateSource);

        Diagnostic diagnostic = run.Diagnostics.Single(static value => value.Id == "DECSGEN006");
        Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture), Does.Contain("cannot write components"));
        Assert.That(run.GeneratedTrees, Is.Empty);
    }

    [Test]
    public void WhereGeneratorSupportsFunctorPredicatesAndContextTerminals()
    {
        GeneratorDriverRunResult run = RunGenerator(WhereFunctorSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(run.GeneratedTrees.Count, Is.GreaterThan(0));
        Assert.That(generated, Does.Contain("_predicate.Invoke(ref _predicateContext, entity"));
        Assert.That(generated, Does.Contain("_action.Invoke(ref _context, entity"));
        Assert.That(generated, Does.Not.Contain("var predicate = _predicate;"));
        Assert.That(generated, Does.Not.Contain("var action = _action;"));

        AssertCompiles(new[] { RuntimeStubSource, WhereFunctorSource }, run.GeneratedTrees);
    }

    [Test]
    public void CSharp9WhereFunctorContextUsesSafeByrefLikeStorage()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(WhereFunctorSource, LanguageVersion.CSharp9);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("Span<global::Delta.ECS.PredicateState> _predicateContext"));
        Assert.That(generated, Does.Contain("Span<global::Delta.ECS.HealthPredicate> _predicate"));
        Assert.That(generated, Does.Not.Contain("private ref PredicateState _predicateContext"));
        Assert.That(generated, Does.Not.Contain("private ref HealthPredicate _predicate"));

        AssertCompiles(
            new[] { RuntimeStubFor(LanguageVersion.CSharp9), WhereFunctorSource },
            run.GeneratedTrees,
            LanguageVersion.CSharp9);
    }

    [Test]
    public void WhereGeneratorEmitsInterceptionForStaticTerminalCallbacks()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(WhereInterceptionSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("GeneratedWhereInterceptor_"));
        Assert.That(generated, Does.Contain("InterceptsLocationAttribute"));
        Assert.That(generated, Does.Contain("Predicate_"));
        Assert.That(generated, Does.Contain("execution.MarkArchetypeWrites"));
        Assert.That(generated, Does.Contain("ref global::Delta.ECS.InterceptedWhereSystem.Mutation action"));
        Assert.That(generated, Does.Contain("action.Invoke"));
        Assert.That(generated, Does.Contain("private struct StructuralInvoker_"));
        Assert.That(generated, Does.Contain("IGeneratedWhereStructuralInvoker"));
        Assert.That(generated, Does.Contain("context.ProcessRun"));
        Assert.That(generated, Does.Contain("ref global::Delta.ECS.Health row0 = ref global::System.Runtime.CompilerServices.Unsafe.NullRef<global::Delta.ECS.Health>()"));
        Assert.That(generated, Does.Contain("row0 = ref global::Delta.ECS.GeneratedForEachRuntime.GetGeneratedArrayReference(slots.GetGeneratedArray<global::Delta.ECS.Health>(access0))"));
        Assert.That(generated, Does.Contain("row0 = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref row0, 1)"));

        AssertCompiles(new[] { RuntimeStubSource, WhereInterceptionSource }, run.GeneratedTrees);
    }

    [Test]
    public void WhereInterceptionSupportsStaticMethodGroups()
    {
        GeneratorDriverRunResult run = RunGeneratorWithInterceptors(WhereMethodGroupInterceptionSource);
        string generated = GeneratedText(run);

        AssertNoDiagnostics(run.Diagnostics);
        Assert.That(generated, Does.Contain("InterceptedWhereSystem.IsDead(in component0)"));
        Assert.That(generated, Does.Contain("InterceptedWhereSystem.Reset(ref component0)"));
        Assert.That(generated, Does.Contain("InterceptedWhereSystem.IsDeadEntity(entity, in component0)"));
        Assert.That(generated, Does.Contain("InterceptedWhereSystem.ResetEntity(entity, ref component0)"));

        AssertCompiles(
            new[] { RuntimeStubSource, WhereMethodGroupInterceptionSource },
            run.GeneratedTrees);
    }

    [Test]
    public void WhereStaticMethodGroupsKeepConcreteContextBindingsAcrossDiscoveryOrders()
    {
        foreach (string source in new[] { WhereContextBindingSource, WhereContextBindingReversedSource })
        {
            GeneratorDriverRunResult run = RunGeneratorWithInterceptors(source);
            string generated = GeneratedText(run);

            AssertNoDiagnostics(run.Diagnostics);
            Assert.That(generated, Does.Contain("GeneratedWhereQuery_"));
            Assert.That(generated, Does.Contain("global::Delta.ECS.FirstContext, global::Delta.ECS.Health"));
            Assert.That(generated, Does.Contain("global::Delta.ECS.SecondContext, global::Delta.ECS.Health"));
            Assert.That(generated, Does.Contain("return Where<global::Delta.ECS.FirstContext, global::Delta.ECS.Health>"));
            Assert.That(generated, Does.Contain("return Where<global::Delta.ECS.SecondContext, global::Delta.ECS.Health>"));

            AssertCompiles(new[] { RuntimeStubSource, source }, run.GeneratedTrees);
        }
    }

    private static GeneratorDriverRunResult RunGenerator()
        => RunGenerator(ConsumerSource);

    private static GeneratorDriverRunResult RunGenerator(string consumerSource)
    {
        CSharpCompilation compilation = CreateCompilation(new[] { RuntimeStubSource, consumerSource });
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[]
            {
                new DemandDrivenForEachGenerator().AsSourceGenerator(),
                new GeneratedStructuralGenerator().AsSourceGenerator(),
                new GeneratedQueryGenerator().AsSourceGenerator(),
                new GeneratedWhereGenerator().AsSourceGenerator()
            });
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private static GeneratorDriverRunResult RunGeneratorWithInterceptors(string consumerSource)
        => RunGeneratorWithInterceptors(new[] { consumerSource });

    private static GeneratorDriverRunResult RunGeneratorWithInterceptors(
        string consumerSource,
        LanguageVersion languageVersion)
        => RunGeneratorWithInterceptors(new[] { consumerSource }, languageVersion);

    private static GeneratorDriverRunResult RunGeneratorWithInterceptors(IEnumerable<string> consumerSources)
        => RunGeneratorWithInterceptors(consumerSources, LanguageVersion.Latest);

    private static GeneratorDriverRunResult RunGeneratorWithInterceptors(
        IEnumerable<string> consumerSources,
        LanguageVersion languageVersion)
    {
        CSharpCompilation compilation = CreateCompilation(
            new[] { RuntimeStubSource }.Concat(consumerSources),
            languageVersion);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[]
            {
                new DemandDrivenForEachGenerator().AsSourceGenerator(),
                new GeneratedStructuralGenerator().AsSourceGenerator(),
                new GeneratedQueryGenerator().AsSourceGenerator(),
                new GeneratedWhereGenerator().AsSourceGenerator()
            },
            Array.Empty<AdditionalText>(),
            new CSharpParseOptions(languageVersion),
            new FixedAnalyzerConfigOptionsProvider("Delta.ECS.Generated"),
            default);
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private static string GeneratedText(GeneratorDriverRunResult run)
        => string.Join(
            Environment.NewLine,
            run.GeneratedTrees
                .OrderBy(static tree => tree.FilePath, StringComparer.Ordinal)
                .Select(static tree => tree.GetText().ToString()));

    private static void AssertNoDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        Diagnostic[] values = diagnostics.ToArray();
        Assert.That(values, Is.Empty, string.Join(Environment.NewLine, values.Select(static value => value.ToString())));
    }

    private static void AssertCompiles(CSharpCompilation compilation)
        => AssertNoDiagnostics(compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

    private static void AssertCompiles(
        IEnumerable<string> sources,
        IEnumerable<SyntaxTree> generatedTrees,
        LanguageVersion languageVersion = LanguageVersion.Latest)
        => AssertCompiles(CreateCompilationWithGeneratedTrees(sources, generatedTrees, languageVersion));

    private static CSharpCompilation CreateCompilation(
        IEnumerable<string> sources,
        LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create(
            "DeltaEcsGeneratorHarness",
            sources.Select(source => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(languageVersion))),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private static CSharpCompilation CreateCompilationWithGeneratedTrees(
        IEnumerable<string> sources,
        IEnumerable<SyntaxTree> generatedTrees,
        LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path));
        var parseOptions = new CSharpParseOptions(languageVersion);
        if (languageVersion >= LanguageVersion.CSharp11)
        {
            parseOptions = parseOptions.WithFeatures(new[]
            {
                new KeyValuePair<string, string>("InterceptorsNamespaces", "Delta.ECS.Generated")
            });
        }
        var syntaxTrees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source, parseOptions))
            .Concat(generatedTrees.Select(tree => CSharpSyntaxTree.ParseText(tree.GetText().ToString(), parseOptions, tree.FilePath)));
        return CSharpCompilation.Create(
            "DeltaEcsGeneratorHarness",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private sealed class FixedAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _options;

        public FixedAnalyzerConfigOptionsProvider(string interceptorsNamespace)
        {
            _options = new FixedAnalyzerConfigOptions(interceptorsNamespace);
        }

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }

    private sealed class FixedAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly string _interceptorsNamespace;

        public FixedAnalyzerConfigOptions(string interceptorsNamespace)
        {
            _interceptorsNamespace = interceptorsNamespace;
        }

        public override bool TryGetValue(string key, out string value)
        {
            if (key == "build_property.InterceptorsNamespaces")
            {
                value = _interceptorsNamespace;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }

    private static string RuntimeStubFor(LanguageVersion languageVersion)
        => languageVersion < LanguageVersion.CSharp11
            ? RuntimeStubSource
                .Replace("scoped ReadOnlySpan<int>", "ReadOnlySpan<int>", StringComparison.Ordinal)
            : RuntimeStubSource;

    private const string RuntimeStubSource = """
        namespace Delta.ECS
        {
        using System;
        public readonly struct Entity { public int Index { get; } }
        public readonly struct ComponentId { }
        public readonly struct Stamp { }
        public readonly struct QuerySpec
        {
            public static QuerySpec WhereAll(ReadOnlySpan<ComponentId> components) => default;
            public static QuerySpec WhereAny(ReadOnlySpan<ComponentId> components) => default;
            public static QuerySpec WhereNone(ReadOnlySpan<ComponentId> components) => default;
        }
        public readonly struct Query { }
        public readonly struct ReadAccess { }
        public readonly struct WriteAccess { }
        public delegate void ForEachAction();
        public delegate void ForEachEntityAction(Entity entity);
        public delegate void ForEachContextAction<TContext>(ref TContext context);
        public delegate void ForEachContextEntityAction<TContext>(ref TContext context, Entity entity);
        public delegate void ForEachContextActionIn<TContext>(in TContext context);
        public delegate void ForEachContextEntityActionIn<TContext>(in TContext context, Entity entity);
        public delegate void ForEachContextActionValue<TContext>(TContext context);
        public delegate void ForEachContextEntityActionValue<TContext>(TContext context, Entity entity);
        public interface IForEach { }
        public interface IForEachEntity { }
        public interface IForEachContext<TContext> { }
        public interface IForEachContextEntity<TContext> { }
        public interface IWherePredicate { }
        public struct GeneratedWhereStructuralContext
        {
            public void ProcessRun(int sourceSlot, int count, bool selected) { }
            public void ProcessRun<TInitializer>(int sourceSlot, int count, bool selected, ref TInitializer initializer)
                where TInitializer : struct, IGeneratedComponentValueInitializer { }
        }
        public interface IGeneratedWhereInvoker
        {
            void Invoke(ref GeneratedQuerySlots slots);
        }
        public interface IGeneratedWhereStructuralInvoker
        {
            void Execute(ref GeneratedQuerySlots slots, ref GeneratedWhereStructuralContext context);
        }
        public interface IGeneratedParallelInvoker
        {
            bool RequiresSingleThread { get; }
            void Invoke(ref GeneratedQuerySlots slots);
        }
        public sealed class ComponentLayoutRegistry
        {
            public ComponentId GetPrimary(Type type) => default;
            public ComponentId GetPrimary<T>() => default;
        }
        public ref struct GeneratedQuerySlots
        {
            public int ChunkId => 0;
            public int Count => 0;
            public Entity EntityAt(int index) => default;
            public ref Entity GetGeneratedEntityReference() => throw new NotImplementedException();
            public T[] GetGeneratedArray<T>(int queryComponentIndex) => Array.Empty<T>();
            public T[] GetGeneratedArray<T>(ReadAccess access) => Array.Empty<T>();
            public T[] GetGeneratedArray<T>(WriteAccess access) => Array.Empty<T>();
            public ref T GetGeneratedReadReference<T>(int queryComponentIndex) => throw new NotImplementedException();
            public ref T GetGeneratedReadReference<T>(ReadAccess access) => throw new NotImplementedException();
            public ref T GetGeneratedWriteReference<T>(int queryComponentIndex) => throw new NotImplementedException();
            public ref T GetGeneratedWriteReference<T>(WriteAccess access) => throw new NotImplementedException();
            public Stamp GetGeneratedStamp(ReadAccess access, int index) => default;
            public Stamp GetGeneratedStamp(int queryComponentIndex, int index) => default;
        }
        public ref struct GeneratedReadQuerySlots
        {
            public int Count => 0;
            public Entity EntityAt(int index) => default;
            public ref readonly Entity GetGeneratedEntityReference() => throw new NotImplementedException();
            public ref T GetGeneratedReadReference<T>(int queryComponentIndex) => throw new NotImplementedException();
            public ref T GetGeneratedReadReference<T>(ReadAccess access) => throw new NotImplementedException();
            public Stamp GetGeneratedStamp(ReadAccess access, int index) => default;
            public Stamp GetGeneratedStamp(int queryComponentIndex, int index) => default;
        }
        public readonly struct GeneratedBoundChunk
        {
            public int Count => 0;
            public ref Entity GetEntityReference() => throw new NotImplementedException();
        }
        public abstract class GeneratedDenseBinding<TRows> where TRows : struct
        {
            protected abstract void Prepare(in Query query);
            protected abstract TRows BindRows(Array[] rows, GeneratedBoundChunk chunk);
            protected void SetWriteRoutes(ReadOnlySpan<int> routes) { }
        }
        public ref struct GeneratedBoundExecution<TRows> where TRows : struct
        {
            public ReadOnlySpan<TRows> Rows => default;
            public void Dispose() { }
        }
        public ref struct GeneratedDenseExecution
        {
            public bool MoveNextTrusted(out GeneratedQuerySlots slots) { slots = default; return false; }
            public bool MoveNextTrusted(out Array[] componentRows, out int count)
            {
                componentRows = Array.Empty<Array>();
                count = 0;
                return false;
            }
            public void MarkArchetypeWrite(int queryComponentIndex) { }
            public void MarkArchetypeWrites(scoped ReadOnlySpan<int> queryComponentIndices) { }
            public void MarkArchetypeWrites<TWriter>(ref TWriter writer)
                where TWriter : struct, IGeneratedArchetypeStampWriter { }
            public void Dispose() { }
        }
        public ref struct GeneratedReadDenseExecution
        {
            public bool MoveNextTrusted(out GeneratedReadQuerySlots slots) { slots = default; return false; }
            public bool MoveNextTrusted(out Array[] componentRows, out int count)
            {
                componentRows = Array.Empty<Array>();
                count = 0;
                return false;
            }
            public void Dispose() { }
        }
        public interface IGeneratedArchetypeStampWriter { void Write(Stamp[] stamps); }
        public interface IGeneratedComponentValueInitializer
        {
            void Initialize(ref GeneratedComponentValueWriter writer);
        }
        public ref struct GeneratedComponentValueWriter
        {
            public void Set<T>(ComponentId component, in T value) { }
            public void SetUnsafe<T>(ComponentId component, in T value) { }
        }
        public static class GeneratedForEachRuntime
        {
            public static GeneratedBoundExecution<TRows> OpenBoundDense<TBinding, TRows>(World world, in Query query)
                where TBinding : GeneratedDenseBinding<TRows>, new() where TRows : struct => default;
            public static GeneratedBoundExecution<TRows> OpenBoundDenseRead<TBinding, TRows>(World world, in Query query)
                where TBinding : GeneratedDenseBinding<TRows>, new() where TRows : struct => default;
            public static T[] GetGeneratedArray<T>(Array[] rows, int route) => throw new NotImplementedException();
            public static ref T GetGeneratedArrayReference<T>(T[] row) => throw new NotImplementedException();
            public static void ThrowIfNull(object? value, string parameterName) { }
            public static bool ExecuteGeneratedAdd<TInitializer>(World world, Entity entity, ReadOnlySpan<ComponentId> components, ref TInitializer initializer)
                where TInitializer : struct, IGeneratedComponentValueInitializer => true;
            public static bool ExecuteGeneratedSet<TInitializer>(World world, Entity entity, ReadOnlySpan<ComponentId> components, ref TInitializer initializer)
                where TInitializer : struct, IGeneratedComponentValueInitializer => true;
            public static ref T GetGeneratedRow<T>(Array[] componentRows, int queryComponentIndex) => throw new NotImplementedException();
            public static GeneratedDenseExecution OpenDense(World world, in Query query) => default;
            public static GeneratedDenseExecution OpenWriteDense(World world, in Query query) => default;
            public static GeneratedReadDenseExecution OpenReadDense(World world, in Query query) => default;
            public static ReadAccess GetPreparedReadAccess<T>(in Query query) => default;
            public static int GetPreparedReadRoute<T>(in Query query) => default;
            public static int GetPreparedReadRoute<T>(in Query query, ComponentId component) => default;
            public static ReadAccess GetPreparedStampAccess<T>(in Query query) => default;
            public static ReadAccess GetPreparedStampAccess(in Query query, ComponentId component) => default;
            public static ReadAccess GetPreparedStampAccess<T>(in Query query, ComponentId component) => default;
            public static WriteAccess GetPreparedWriteAccess<T>(in Query query) => default;
            public static int GetPreparedWriteRoute<T>(in Query query) => default;
            public static int GetPreparedWriteRoute<T>(in Query query, ComponentId component) => default;
            public static void ValidateGeneratedWhere(World world, in Query query) { }
            public static Query ComposeGeneratedQuery(in Query query, QuerySpec additions) => default;
            public static ReadOnlySpan<ComponentId> GetGeneratedPrimaryComponentIds<TKey>(World world, Func<World, ComponentId[]> resolver) => resolver(world);
            public static ReadOnlySpan<ComponentId> GetGeneratedPrimaryComponentIds<TKey>(in Query query, Func<World, ComponentId[]> resolver) => default;
            public static int ExecuteGeneratedWhereDestroy<TInvoker>(World world, in Query query, ref TInvoker invoker, ReadOnlySpan<int> writeComponentIndices)
                where TInvoker : struct, IGeneratedWhereStructuralInvoker => 0;
            public static int ExecuteGeneratedWhereAdd<TInvoker>(World world, in Query query, ref TInvoker invoker, ReadOnlySpan<int> writeComponentIndices, ReadOnlySpan<ComponentId> componentIds)
                where TInvoker : struct, IGeneratedWhereStructuralInvoker => 0;
            public static int ExecuteGeneratedWhereRemove<TInvoker>(World world, in Query query, ref TInvoker invoker, ReadOnlySpan<int> writeComponentIndices, ReadOnlySpan<ComponentId> componentIds)
                where TInvoker : struct, IGeneratedWhereStructuralInvoker => 0;
            public static void ExecuteGeneratedWhereForEach<TInvoker>(World world, in Query query, ref TInvoker invoker, ReadOnlySpan<int> writeComponentIndices)
                where TInvoker : struct, IGeneratedWhereInvoker { }
            public static void ExecuteParallelDense<TInvoker>(World world, in Query query, ref TInvoker invoker, ReadOnlySpan<int> writeComponentIndices, int workerCount = 0)
                where TInvoker : struct, IGeneratedParallelInvoker { }
            public static void ExecuteEntityList<TInvoker>(World world, in Query query, ReadOnlySpan<Entity> entities, ref TInvoker invoker, ReadOnlySpan<int> writeComponentIndices)
                where TInvoker : struct, IGeneratedParallelInvoker { }
            public static void ExecuteEntityListParallel<TInvoker>(World world, in Query query, ReadOnlySpan<Entity> entities, ref TInvoker invoker, ReadOnlySpan<int> writeComponentIndices, int workerCount = 0)
                where TInvoker : struct, IGeneratedParallelInvoker { }
            public static ReadAccess GetPreparedReadAccess(in Query query, ComponentId component, Type runtimeType) => default;
            public static WriteAccess GetPreparedWriteAccess(in Query query, ComponentId component, Type runtimeType) => default;
            public static int GetWriteQueryComponentIndex(WriteAccess access) => default;
            public static int GetReadQueryComponentIndex(ReadAccess access) => default;
            public static void IncrementArchetypeStamp(Stamp[] stamps, int componentIndex) { }
        }
        public sealed partial class World
        {
            public ComponentLayoutRegistry Layouts { get; } = new();
            public Query WhereAll(ReadOnlySpan<ComponentId> components) => default;
            public Entity Create(ReadOnlySpan<ComponentId> components) => default;
            public int Create(ReadOnlySpan<ComponentId> components, int count) => count;
            public int Create(ReadOnlySpan<ComponentId> components, int count, Span<Entity> output) => count;
            public Query CreateQuery(in QuerySpec spec) => default;
            public bool Add(Entity entity, ReadOnlySpan<ComponentId> components) => true;
            public int Add(ReadOnlySpan<Entity> entities, ReadOnlySpan<ComponentId> components) => 0;
            public bool Remove(Entity entity, ReadOnlySpan<ComponentId> components) => true;
            public int Remove(ReadOnlySpan<Entity> entities, ReadOnlySpan<ComponentId> components) => 0;
            public bool Set<T>(Entity entity, in T value) => true;
            public int Add(in Query query, ReadOnlySpan<ComponentId> components) => 0;
            public int Remove(in Query query, ReadOnlySpan<ComponentId> components) => 0;
            public void ForEach(in Query query, ForEachAction action) { }
            public void ForEachEntity(in Query query, ForEachEntityAction action) { }
            public void ForEach<TContext>(in Query query, ref TContext context, ForEachContextAction<TContext> action) { }
            public void ForEachEntity<TContext>(in Query query, ref TContext context, ForEachContextEntityAction<TContext> action) { }
        }
        }
        """;

    private const string StructuralSource = """
        namespace Delta.ECS;
        using System;
        struct Position { }
        struct Velocity { }
        static class StructuralConsumer
        {
            public static void Use(
                World world,
                Query query,
                ReadOnlySpan<Entity> entities,
                ComponentId position,
                ComponentId velocity)
            {
                world.Create<Position, Velocity>(2);
                Span<Entity> output = stackalloc Entity[2];
                world.Create<Position, Velocity>(2, output);
                world.Create<Position, Velocity>(position, velocity, 2);
                world.Create<Position, Velocity>(position, velocity, 2, output);
                Entity entity = default;
                world.Add<Position, Velocity>(entity);
                world.Add<Position, Velocity>(entity, position, velocity);
                world.Add<Position, Velocity>(entities);
                world.Add<Position, Velocity>(entities, position, velocity);
                world.Remove<Position, Velocity>(entity);
                world.Remove<Position, Velocity>(entity, position, velocity);
                world.Remove<Position, Velocity>(entities);
                world.Remove<Position, Velocity>(entities, position, velocity);
                world.Add<Position, Velocity>(in query);
                world.Add<Position, Velocity>(in query, position, velocity);
                world.Remove<Position, Velocity>(in query);
                world.Remove<Position, Velocity>(in query, position, velocity);
                world.Add(entity, new Position(), new Velocity());
                world.Add<Position, Velocity>(entity, new Position(), new Velocity());
                world.Set(entity, new Position(), new Velocity());
                world.Set<Position, Velocity>(entity, new Position(), new Velocity());
            }
        }
        """;

    private const string ExplicitCreateSource = """
        namespace Delta.ECS;
        using System;
        static class ExplicitStructuralConsumer
        {
            public static void Use(World world, ComponentId position, ComponentId velocity, Span<Entity> output)
            {
                _ = world.Create(position, velocity, 2);
                _ = world.Create(position, 2, output);
            }
        }
        """;

    private const string GenericQuerySource = """
        namespace Delta.ECS;
        struct Position { }
        struct Velocity { }
        struct Acceleration { }
        static class QueryConsumer
        {
            public static void Use(World world)
            {
                Query all = world.WhereAll<Position, Velocity>();
                Query any = world.WhereAny<Position, Velocity, Acceleration>();
                Query none = world.WhereNone<Acceleration>();
                _ = all;
                _ = any;
                _ = none;
            }
        }
        """;

    private const string PrivateNestedQueryComponentSource = """
        namespace Delta.ECS;
        static class PrivateQueryConsumer
        {
            private struct Position { }

            public static Query Build(World world)
                => world.WhereAll<Position>();
        }
        """;

    private const string GenericQueryChainSource = """
        namespace Delta.ECS;
        struct Position { }
        struct Health { }
        struct Human { }
        struct Dead { }
        struct Escaped { }
        struct Armed { }
        struct Berserk { }
        static class QueryConsumer
        {
            public static Query Build(World world)
                => world
                    .WhereAll<Position, Health, Human>()
                    .WhereNone<Dead, Escaped>()
                    .WhereAny<Armed, Berserk>();

            public static Query BuildWithLocal(World world)
            {
                Query query = world.WhereAll<Position, Health, Human>();
                return query.WhereNone<Dead, Escaped>().WhereAny<Armed, Berserk>();
            }

            public static Query BuildWithVar(World world)
            {
                var query = world.WhereAll<Position, Health>();
                return query.WhereNone<Dead, Escaped, Human>().WhereAny<Armed>();
            }
        }
        """;

    private const string ExplicitSelectorSource = """
        namespace Delta.ECS;
        struct Position { public int Value; }
        struct Velocity { public int Value; }
        static class ExplicitSelectorConsumer
        {
            public static void Use(World world, Query query, ComponentId position, ComponentId velocity)
            {
                world.ForEach(in query, position, velocity,
                    static (ref Position value, in Velocity source) => value.Value += source.Value);
            }
        }
        """;

    private const string GrammarMatrixSource = """
        namespace Delta.ECS;
        using System;
        struct T1 { public int Value; }
        struct T2 { public int Value; }
        struct Context { public int Value; }
        static class GrammarMatrixConsumer
        {
            public static void ApplyEntity(Entity entity, ref T1 first, in T2 second) => first.Value += entity.Index + second.Value;

            public static void Use(
                World world,
                in Query query,
                ReadOnlySpan<Entity> entities,
                ComponentId first,
                ComponentId second,
                ref Context context)
            {
                world.ForEach<T1, T2>(in query, first, second, static (ref T1 a, in T2 b) => a.Value += b.Value);
                world.ForEachEntity<T1, T2>(entities, first, second, ApplyEntity);
                world.ForEachParallel<T1, T2>(in query, first, second, static (ref T1 a, in T2 b) => a.Value += b.Value, workerCount: 2);
                world.ForEachEntityParallel<T1, T2>(entities, in query, first, second, ApplyEntity, workerCount: 2);

                world.ForEach<Context, T1, T2>(in query, first, second, ref context,
                    static (ref Context state, ref T1 a, in T2 b) =>
                    {
                        state.Value++;
                        a.Value += b.Value;
                    });

                Entity entity = default;
                world.Add<T1, T2>(entity, first, second);
                world.Remove<T1, T2>(entity, first, second);
                world.Add<T1, T2>(entities, first, second);
                world.Remove<T1, T2>(entities, first, second);
                world.Add<T1, T2>(in query, first, second);
                world.Remove<T1, T2>(in query, first, second);
                world.Create<T1, T2>(first, second, 2);
            }
        }
        """;

    private const string QueryFactoriesWithDuplicateSignaturesSource = """
        namespace Delta.ECS;
        struct Position { }
        struct Velocity { }
        struct Acceleration { }
        struct Lifetime { }
        static class MovementSystem
        {
            public static Query Build(World world) => world.WhereAll<Position, Velocity>();
        }
        static class LifetimeSystem
        {
            public static Query Build(World world) => world.WhereAll<Acceleration, Lifetime>();
        }
        """;

    private const string TupleContextElementNamesSource = """
        namespace Delta.ECS;
        struct SquadMember { public Entity Squad; }
        static class EcsEntryPoint
        {
            public static void Use(World world, Query query, Entity squad)
            {
                var state = (Squad: squad, AliveMembers: 0);
                world.ForEach(
                    in query,
                    ref state,
                    static (ref (Entity Squad, int AliveMembers) state, in SquadMember member) =>
                    {
                        if (member.Squad.Index == state.Squad.Index)
                        {
                            state.AliveMembers++;
                        }
                    });
            }
        }
        static class SurvivorJoinSystem
        {
            public static void Tick(World world, Query query, Entity squad)
            {
                var memberState = (Squad: squad, Count: 0);
                world.ForEach(
                    in query,
                    ref memberState,
                    static (ref (Entity Squad, int Count) state, in SquadMember member) =>
                    {
                        if (member.Squad.Index == state.Squad.Index)
                        {
                            state.Count++;
                        }
                    });
            }
        }
        """;

    private const string WhereMutationSource = """
        namespace Delta.ECS
        {
        struct Health { public int Value; }
        struct Team { public int Id; public int DefaultHealth; }
        struct Dead { }
        struct Alive { }
        static class MutationSystems
        {
            public static void Run(World world, in Query query, ComponentId deadId, ComponentId aliveId)
            {
                world.WhereEntity(in query, static (Entity entity, in Health health) => health.Value <= 0).Destroy();
                world.WhereEntity(in query, static (Entity entity, in Health health) => health.Value <= 0).Add<Dead>();
                world.WhereEntity(in query, static (Entity entity, in Health health) => health.Value <= 0).Add<Dead>(deadId);
                world.WhereEntity(in query, static (Entity entity, in Health health) => health.Value <= 0)
                    .Add<Dead>(new Dead());
                world.WhereEntity(in query, static (Entity entity, in Health health) => health.Value <= 0)
                    .Add<Dead>(world.Layouts.GetPrimary<Dead>(), new Dead());
                world.WhereEntity(in query, static (Entity entity, in Health health) => health.Value <= 0).Remove<Alive>();
                world.WhereEntity(in query, static (Entity entity, in Health health) => health.Value <= 0).Remove<Alive>(aliveId);
                world.WhereEntity(in query, static (Entity entity, in Health health) => health.Value <= 0)
                    .ForEach(static (ref Health health) => health.Value = 0);
                world.WhereEntity(in query, static (Entity entity, in Health health, in Team team) =>
                    health.Value <= 0 && team.Id == 1)
                    .Destroy();
                world.WhereEntity(in query, static (Entity entity, in Health health, in Team team) =>
                    health.Value <= 0 && team.Id == 1)
                    .Add<Dead>();
                world.WhereEntity(in query, static (Entity entity, in Health health, in Team team) =>
                    health.Value <= 0 && team.Id == 1)
                    .Remove<Alive>();
                world.WhereEntity(in query, static (Entity entity, in Health health, in Team team) =>
                    health.Value <= 0 && team.Id == 1)
                    .ForEachEntity(static (Entity current, ref Health health, in Team team) => _ = current.Index + team.Id);
                world.WhereEntity(in query, static (Entity entity, in Health health, in Team team) =>
                    health.Value <= 0 && team.Id == 1)
                    .ForEach(static (ref Health health, in Team team) => health.Value = team.DefaultHealth);
                world.WhereEntity(in query, static (Entity entity, in Health health, in Team team) =>
                    health.Value <= 0 && team.Id == 1)
                    .Add<Dead, Alive>(new Dead(), new Alive());
                world.WhereEntity(in query, static (Entity entity, in Health health, in Team team) =>
                    health.Value <= 0 && team.Id == 1)
                    .Add<Dead, Alive>(deadId, aliveId, new Dead(), new Alive());
                world.WhereEntity(in query, static (Entity entity, in Health health, in Team team) =>
                    health.Value <= 0 && team.Id == 1)
                    .Remove<Dead, Alive>(deadId, aliveId);
            }
        }
        }
        """;

    private const string WhereZeroArityTerminalSource = """
        namespace Delta.ECS;
        struct Health { public int Value; }
        struct Dead { }
        static class ZeroArityWhereSystem
        {
            public static void Run(World world, in Query query)
            {
                world.WhereEntity(in query, static (Entity entity, in Health health) => health.Value <= entity.Index)
                    .ForEachEntity(static (Entity entity) => _ = entity);
            }
        }
        """;

    private const string WhereDuplicateMutationSource = """
        namespace Delta.ECS;
        struct Health { public int Value; }
        struct Position { public int Value; }
        struct Dead { }
        struct Selected { }
        static class DuplicateMutationSystem
        {
            public static void Run(World world, in Query query)
            {
                world.Where(in query, static (in Health health) => health.Value <= 0).Add<Dead>();
                world.Where(in query, static (in Position position) => position.Value < 0).Add<Selected>();
            }
        }
        """;

    private const string GenericCallSitesSource = """
        namespace Delta.ECS;
        struct Position { public int Value; }
        struct Velocity { public int Value; }
        struct PositionContext { public int Value; }
        struct VelocityContext { public int Value; }
        struct Health { public int Value; }
        static class GenericCallSites
        {
            public static void Use(
                World world,
                in Query query,
                ref PositionContext positionContext,
                ref VelocityContext velocityContext)
            {
                world.ForEach<PositionContext, Position>(in query, ref positionContext,
                    static (ref PositionContext context, ref Position position) =>
                    {
                        context.Value += position.Value;
                    });
                world.ForEach<VelocityContext, Velocity>(in query, ref velocityContext,
                    static (ref VelocityContext context, ref Velocity velocity) =>
                    {
                        context.Value += velocity.Value;
                    });
                world.Where(in query, ref positionContext,
                    static (ref PositionContext context, in Health health) =>
                    {
                        context.Value += health.Value;
                        return health.Value >= 0;
                    });
                world.Where(in query, ref velocityContext,
                    static (ref VelocityContext context, in Health health) =>
                    {
                        context.Value += health.Value;
                        return health.Value >= 0;
                    });
            }
        }
        """;

    private const string WhereStructuralParameterMatrixSource = """
        namespace Delta.ECS;
        struct Health { public int Value; }
        struct Dead { }
        struct Alive { }
        static class WhereStructuralParameterMatrix
        {
            public static void Use(World world, in Query query, ComponentId deadId, ComponentId aliveId)
            {
                world.Where(in query, static (in Health health) => health.Value <= 0).Add<Dead>();
                world.Where(in query, static (in Health health) => health.Value <= 0).Add<Dead>(deadId);
                world.Where(in query, static (in Health health) => health.Value <= 0).Add<Dead>(new Dead());
                world.Where(in query, static (in Health health) => health.Value <= 0).Add<Dead>(deadId, new Dead());
                world.Where(in query, static (in Health health) => health.Value <= 0).Remove<Dead>();
                world.Where(in query, static (in Health health) => health.Value <= 0).Remove<Dead>(deadId);
                world.Where(in query, static (in Health health) => health.Value <= 0).Remove<Dead, Alive>();
                world.Where(in query, static (in Health health) => health.Value <= 0).Remove<Dead, Alive>(deadId, aliveId);
            }
        }
        """;

    private const string WhereEntityParameterSource = """
        namespace Delta.ECS;
        struct Health { public int Value; }
        static class InvalidWhereSystem
        {
            public static void Run(World world, in Query query)
            {
                world.Where(in query, static (Entity entity, in Health health) => health.Value <= entity.Index);
            }
        }
        """;

    private const string WhereReadonlyPredicateSource = """
        namespace Delta.ECS;
        struct Health { public int Value; }
        struct Team { public int Id; }
        struct Dead { }
        static class ReadonlyPredicateSystem
        {
            public static void Run(World world, in Query query)
            {
                world.WhereEntity(
                        in query,
                        static (Entity entity, in Health health) => health.Value <= 0)
                    .Destroy();
                world.WhereEntity(
                        in query,
                        static (Entity entity, ref readonly Health health, in Team team) =>
                            health.Value <= 0 && team.Id == 1)
                    .Add<Dead>();
            }
        }
        """;

    private const string WhereWithoutEntitySource = """
        namespace Delta.ECS
        {
        struct Health { public int Value; }
        struct Dead { }
        struct PredicateState { public int Seen; }
        struct HealthPredicate : IWherePredicate
        {
            public bool Invoke(in Health health) => health.Value <= 0;
        }
        struct ContextPredicate : IWherePredicate
        {
            public bool Invoke(ref PredicateState state, in Health health)
            {
                state.Seen++;
                return health.Value <= 0;
            }
        }
        static class ReadonlyPredicateSystem
        {
            public static void Run(World world, in Query query)
            {
                world.Where(in query, static (in Health health) => health.Value <= 0).Destroy();
                world.WhereEntity(in query, static (Entity entity, in Health health) => health.Value <= entity.Index).Destroy();
                var state = new PredicateState();
                world.Where(
                        in query,
                        ref state,
                        static (ref PredicateState state, in Health health) =>
                        {
                            state.Seen++;
                            return health.Value <= 0;
                        })
                    .Add<Dead>();
                var predicate = new HealthPredicate();
                world.Where(in query, ref predicate).Remove<Dead>();
                var contextPredicate = new ContextPredicate();
                world.Where(in query, ref state, ref contextPredicate).Destroy();
            }
        }
        }
        """;

    private const string WhereWritablePredicateSource = """
        namespace Delta.ECS;
        struct Health { public int Value; }
        static class WritablePredicateSystem
        {
            public static void Run(World world, in Query query)
            {
                world.WhereEntity(
                        in query,
                        static (Entity entity, ref Health health) => health.Value <= 0)
                    .Destroy();
            }
        }
        """;

    private const string StampIterationSource = """
        namespace Delta.ECS;
        using System;
        struct Health { public int Value; }
        struct StampContext { public int Value; }
        struct StampFunctor : IForEach
        {
            public void Invoke(in Stamp stamp) { }
        }
        struct StampContextFunctor : IForEachContext<StampContext>
        {
            public void Invoke(ref StampContext context, in Stamp stamp) { context.Value += stamp.GetHashCode(); }
        }
        static class StampConsumer
        {
            public static void Use(World world, in Query query, ComponentId healthId, ReadOnlySpan<Entity> entities)
            {
                var context = new StampContext();
                world.ForEachStamp<Health>(in query, static (in Stamp stamp) => { _ = stamp; });
                world.ForEachEntityStamp<Health>(in query, static (Entity entity, ref readonly Stamp stamp) => { _ = entity; _ = stamp; });
                world.ForEachStamp<StampContext, Health>(in query, ref context, static (ref StampContext state, in Stamp stamp) => state.Value += stamp.GetHashCode());
                world.ForEachStamp(in query, healthId, static (in Stamp stamp) => { _ = stamp; });
                world.ForEachStampParallel<Health>(in query, static (in Stamp stamp) => { _ = stamp; }, workerCount: 2);
                world.ForEachStamp(entities, in query, healthId, static (in Stamp stamp) => { _ = stamp; });
                world.ForEachEntityStampParallel(entities, in query, healthId, static (Entity entity, in Stamp stamp) => { _ = entity; _ = stamp; }, workerCount: 2);
                world.ForEachStampParallel(in query, healthId, static (in Stamp stamp) => { _ = stamp; }, workerCount: 2);
                world.ForEachEntityStampParallel<StampContext, Health>(in query, in context, static (in StampContext state, Entity entity, in Stamp stamp) => { _ = state; _ = entity; _ = stamp; }, workerCount: 2);
                var functor = new StampFunctor();
                world.ForEachStamp<Health>(in query, ref functor);
                var contextFunctor = new StampContextFunctor();
                world.ForEachStamp<Health>(in query, ref context, ref contextFunctor);
            }
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
        struct Functor : IForEachContextEntity<Context>
        {
            public void Invoke(ref Context context, Entity entity, in T1 a, ref T2 b, in T3 c, ref T4 d) { context.Value += entity.Index + a.Value + c.Value; b.Value++; d.Value++; }
        }
        struct AllModesFunctor : IForEach
        {
            public void Invoke(ref readonly T1 a, ref T2 b, in T3 c, T4 d) { b.Value += a.Value + c.Value + d.Value; }
        }
        struct EntityOnlyFunctor : IForEachEntity
        {
            public void Invoke(Entity entity) { _ = entity; }
        }
        static class Consumer
        {
            public static void Use(World world, Query query, ComponentId c1, ComponentId c2, ComponentId c3, ComponentId c4, ComponentId c5, ComponentId c6, ComponentId c7, ComponentId c8)
            {
                world.ForEach(in query, static () => { });
                world.ForEachEntity(in query, static (Entity entity) => { _ = entity; });
                ReadOnlySpan<Entity> entities = default;
                world.ForEachEntity(entities, in query, static (Entity entity) => { _ = entity; });
                world.ForEachEntity(entities, static (Entity entity) => { _ = entity; });
                world.ForEachEntityParallel(in query, static (Entity entity) => { _ = entity; }, workerCount: 2);
                world.ForEachEntityParallel(entities, in query, static (Entity entity) => { _ = entity; }, workerCount: 2);
                world.ForEachEntityParallel(entities, static (Entity entity) => { _ = entity; }, workerCount: 2);
                var entityOnlyFunctor = new EntityOnlyFunctor();
                world.ForEachEntity(in query, ref entityOnlyFunctor);
                world.ForEachEntity(entities, in query, ref entityOnlyFunctor);
                world.ForEachEntity(entities, ref entityOnlyFunctor);
                world.ForEachEntityParallel(in query, ref entityOnlyFunctor, workerCount: 2);
                world.ForEachEntityParallel(entities, in query, ref entityOnlyFunctor, workerCount: 2);
                world.ForEachEntityParallel(entities, ref entityOnlyFunctor, workerCount: 2);
                var context = new Context();
                world.ForEachEntity(
                    in query,
                    ref context,
                    static (ref Context state, Entity entity) => state.Value += entity.Index);
                world.ForEachEntityParallel(
                    in query,
                    context,
                    static (Context state, Entity entity) => _ = state.Value + entity.Index,
                    workerCount: 2);
                world.ForEachEntity(
                    entities,
                    in query,
                    ref context,
                    static (ref Context state, Entity entity) => state.Value += entity.Index);
                world.ForEachEntityParallel(
                    entities,
                    in query,
                    context,
                    static (Context state, Entity entity) => _ = state.Value + entity.Index,
                    workerCount: 2);
                world.ForEach<Context>(in query, ref context, static (ref Context value) => value.Value++);
                var allModesFunctor = new AllModesFunctor();
                world.ForEach(in query, ref allModesFunctor);
                world.ForEach<T1>(in query, static (ref T1 value) => value.Value++);
                world.ForEach<T1, T2, T3, T4>(in query, static (in T1 a, ref T2 b, in T3 c, ref T4 d) => { b.Value += a.Value; d.Value += c.Value; });
                world.ForEach<T1, T2, T3, T4, T5>(in query, c1, c2, c3, c4, c5, static (in T1 a, ref T2 b, in T3 c, ref T4 d, ref T5 e) => { b.Value += a.Value; d.Value += c.Value; e.Value++; });
                world.ForEach<T1, T2, T3, T4, T5, T6, T7, T8>(in query, static (ref T1 a, in T2 b, ref T3 c, in T4 d, ref T5 e, in T6 f, ref T7 g, in T8 h) => { a.Value += b.Value; c.Value += d.Value; e.Value += f.Value; g.Value += h.Value; });
                var functor = new Functor();
                world.ForEachEntity(in query, ref context, ref functor);
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

    private const string InterceptionSource = """
        namespace Delta.ECS;
        struct T1 { public int Value; }
        static class Consumer
        {
            public static void Use(World world, Query query)
            {
                world.ForEach<T1>(in query, static (ref T1 value) => value.Value++);
            }
        }
        """;

    private const string EntityListInterceptionSource = """
        namespace Delta.ECS;
        using System;
        struct T1 { public int Value; }
        static class Consumer
        {
            public static void Update(ref T1 value) => value.Value++;
            public static void UpdateEntity(Entity entity, ref T1 value) => value.Value += entity.Index;

            public static void Use(World world, Query query, ReadOnlySpan<Entity> entities)
            {
                world.ForEach<T1>(entities, in query, static (ref T1 value) => value.Value++);
                world.ForEachEntity<T1>(entities, in query, static (Entity entity, ref T1 value) => value.Value += entity.Index);
                world.ForEachParallel<T1>(entities, in query, static (ref T1 value) => value.Value++, workerCount: 2);
                world.ForEachEntityParallel<T1>(entities, in query, UpdateEntity, workerCount: 2);
                world.ForEach<T1>(entities, Update);
            }
        }
        """;

    private const string ReturningInterceptionSource = """
        namespace Delta.ECS;
        struct T1 { public int Value; }
        static class Consumer
        {
            public static void Use(World world, Query query)
            {
                world.ForEach<T1>(in query, static (ref T1 value) =>
                {
                    if (value.Value > 0)
                    {
                        return;
                    }

                    value.Value++;
                });
            }
        }
        """;

    private const string InterceptionAliasSharedSource = """
        namespace Delta.ECS;
        struct AliasComponent { public int Value; }
        """;

    private const string InterceptionAliasFirstSource = """
        using Callback = Delta.ECS.FirstCallback;
        namespace Delta.ECS;
        static class FirstCallback
        {
            internal static void Apply(ref AliasComponent value) => value.Value++;
        }
        static class FirstAliasConsumer
        {
            internal static void Use(World world, Query query)
                => world.ForEach(in query, static (ref AliasComponent value) => Callback.Apply(ref value));
        }
        """;

    private const string InterceptionAliasSecondSource = """
        using Callback = Delta.ECS.SecondCallback;
        namespace Delta.ECS;
        static class SecondCallback
        {
            internal static void Apply(ref AliasComponent value) => value.Value--;
        }
        static class SecondAliasConsumer
        {
            internal static void Use(World world, Query query)
                => world.ForEach(in query, static (ref AliasComponent value) => Callback.Apply(ref value));
        }
        """;

    private const string StaticMethodGroupInterceptionSource = """
        namespace Delta.ECS;
        struct T1 { public int Value; }
        struct T2 { public int Value; }
        struct Context { public int Value; }
        static class Callbacks
        {
            public static void Update(ref T1 value) => value.Value++;
        }
        static class Consumer
        {
            public static void Update(ref T1 value) => value.Value++;
            public static void Update(ref T2 value) => value.Value += 2;
            public static void UpdateWithContext(ref Context context, in T1 value) => context.Value += value.Value;
            public static void UpdateParallel(in Context context, ref T1 value) => value.Value += context.Value;
            public static void UpdateEntity(Entity entity, ref T1 value) => value.Value += entity.Index;
            public static void UpdateEntityParallel(in Context context, Entity entity, ref T1 value) => value.Value += context.Value + entity.Index;

            public static void Use(World world, Query query)
            {
                var context = new Context();
                world.ForEach<T1>(in query, Update);
                world.ForEach<T2>(in query, Update);
                world.ForEach<T1>(in query, Callbacks.Update);
                world.ForEach<Context, T1>(in query, ref context, UpdateWithContext);
                world.ForEachParallel<Context, T1>(in query, in context, UpdateParallel, workerCount: 2);
                world.ForEachEntity<T1>(in query, UpdateEntity);
                world.ForEachEntityParallel<Context, T1>(in query, in context, UpdateEntityParallel, workerCount: 2);
            }
        }
        """;

    private const string StampMethodGroupSource = """
        namespace Delta.ECS;
        struct Health { public int Value; }
        static class StampCallbacks
        {
            public static void Update(in Stamp stamp) => _ = stamp;
            public static void UpdateReadonly(ref readonly Stamp stamp) => _ = stamp;
            public static void UpdateEntity(Entity entity, in Stamp stamp) => _ = entity;
        }
        static class StampConsumer
        {
            public static void Use(World world, Query query, ComponentId healthId)
            {
                world.ForEachStamp<Health>(in query, StampCallbacks.Update);
                world.ForEachStamp<Health>(in query, StampCallbacks.UpdateReadonly);
                world.ForEachEntityStamp<Health>(in query, StampCallbacks.UpdateEntity);
                world.ForEachStamp(in query, healthId, StampCallbacks.Update);
                world.ForEachStampParallel<Health>(in query, StampCallbacks.Update, workerCount: 2);
                world.ForEachEntityStampParallel<Health>(in query, StampCallbacks.UpdateEntity, workerCount: 2);
            }
        }
        """;

    private const string InstanceMethodGroupInterceptionSource = """
        namespace Delta.ECS;
        struct T1 { public int Value; }
        class Consumer
        {
            private void Update(ref T1 value) => value.Value++;

            public void Use(World world, Query query)
            {
                world.ForEach<T1>(in query, Update);
            }
        }
        """;

    private const string ImplicitStaticMethodGroupInterceptionSource = """
        namespace Delta.ECS;
        struct T1 { public int Value; }
        static class Consumer
        {
            public static void Update(ref T1 value) => value.Value++;

            public static void Use(World world, Query query)
            {
                world.ForEach(in query, Update);
            }
        }
        """;

    private const string UnsupportedInterceptionSource = """
        namespace Delta.ECS;
        struct T1 { public int Value; }
        static class Consumer
        {
            public static void Use(World world, Query query, int delta)
            {
                world.ForEach<T1>(in query, (ref T1 value) => value.Value += delta);
            }
        }
        """;

    private const string PrivateReferenceInterceptionSource = """
        namespace Delta.ECS;
        struct T1 { public int Value; }
        static class Consumer
        {
            private static int Delta => 1;

            public static void Use(World world, Query query)
            {
                world.ForEach<T1>(in query, static (ref T1 value) => value.Value += Delta);
            }
        }
        """;

    private const string WhereFunctorSource = """
        namespace Delta.ECS
        {
        struct Health { public int Value; }
        struct Dead { }
        struct PredicateState { public int Seen; }
        struct ActionState { public int Count; }
        struct HealthPredicate : IWherePredicate
        {
            public bool Invoke(ref PredicateState state, Entity entity, in Health health)
            {
                state.Seen += entity.Index;
                return health.Value <= 0;
            }
        }
        struct HealthAction : IForEachContextEntity<ActionState>
        {
            public void Invoke(ref ActionState state, Entity entity, ref Health health)
            {
                state.Count += entity.Index;
                health.Value = 0;
            }
        }
        static class FunctorWhereSystem
        {
            public static void Run(World world, in Query query)
            {
                var predicateState = new PredicateState();
                var predicate = new HealthPredicate();
                var actionState = new ActionState();
                var action = new HealthAction();
                world.WhereEntity(in query, ref predicateState, ref predicate)
                    .ForEachEntity(ref actionState, ref action);
            }
        }
        }
        """;

    private const string WhereInterceptionSource = """
        namespace Delta.ECS;
        struct Health { public int Value; }
        struct Dead { }
        static class InterceptedWhereSystem
        {
            internal struct Mutation : IForEach
            {
                public void Invoke(ref Health health) => health.Value++;
            }

            internal struct Context
            {
                public int Count;
            }

            internal struct EntityMutation : IForEachContextEntity<Context>
            {
                public void Invoke(ref Context context, Entity entity, ref Health health)
                {
                    context.Count += entity.Index;
                    health.Value++;
                }
            }

            public static void Run(World world, in Query query)
            {
                world.WhereEntity(
                        in query,
                        static (Entity entity, in Health health) => health.Value <= entity.Index)
                    .ForEach(static (ref Health health) =>
                    {
                        health.Value = 0;
                    });
                world.Where(
                        in query,
                        static (in Health health) => health.Value > 0)
                    .ForEach(static (ref Health health) => health.Value++);
                world.WhereEntity(
                        in query,
                        static (Entity entity, in Health health) => health.Value <= entity.Index)
                    .Destroy();
                world.Where(
                        in query,
                        static (in Health health) => health.Value > 0)
                    .Add<Dead>();
                var action = new Mutation();
                world.WhereEntity(
                        in query,
                        static (Entity entity, in Health health) => health.Value <= entity.Index)
                    .ForEach(ref action);
                var context = new Context();
                var entityAction = new EntityMutation();
                world.WhereEntity(
                        in query,
                        static (Entity entity, in Health health) => health.Value <= entity.Index)
                    .ForEachEntity(ref context, ref entityAction);
            }
        }
        """;

    private const string WhereMethodGroupInterceptionSource = """
        namespace Delta.ECS;
        struct Health { public int Value; }
        static class InterceptedWhereSystem
        {
            public static bool IsDead(in Health health) => health.Value <= 0;
            public static bool IsDeadEntity(Entity entity, in Health health) => health.Value <= entity.Index;
            public static void Reset(ref Health health) => health.Value = 0;
            public static void ResetEntity(Entity entity, ref Health health) => health.Value += entity.Index;

            public static void Run(World world, in Query query)
            {
                world.Where(in query, IsDead).ForEach(Reset);
                world.WhereEntity(in query, IsDeadEntity).ForEachEntity(ResetEntity);
            }
        }
        """;

    private const string WhereContextBindingSource = """
        namespace Delta.ECS;
        struct Health { public int Value; }
        struct FirstContext { public int Value; }
        struct SecondContext { public int Value; }
        static class ContextBindingSystem
        {
            public static bool IsFirstDead(ref FirstContext context, in Health health)
            {
                context.Value++;
                return health.Value <= 0;
            }

            public static bool IsSecondDead(ref SecondContext context, in Health health)
            {
                context.Value++;
                return health.Value <= 0;
            }

            public static void Run(World world, in Query query, ref FirstContext first, ref SecondContext second)
            {
                world.Where(in query, ref first, IsFirstDead).Destroy();
                world.Where(in query, ref second, IsSecondDead).Destroy();
            }
        }
        """;

    private const string WhereContextBindingReversedSource = """
        namespace Delta.ECS;
        struct Health { public int Value; }
        struct FirstContext { public int Value; }
        struct SecondContext { public int Value; }
        static class ContextBindingSystem
        {
            public static bool IsFirstDead(ref FirstContext context, in Health health)
            {
                context.Value++;
                return health.Value <= 0;
            }

            public static bool IsSecondDead(ref SecondContext context, in Health health)
            {
                context.Value++;
                return health.Value <= 0;
            }

            public static void Run(World world, in Query query, ref FirstContext first, ref SecondContext second)
            {
                world.Where(in query, ref second, IsSecondDead).Destroy();
                world.Where(in query, ref first, IsFirstDead).Destroy();
            }
        }
        """;
}
