using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Arch.Core;
using Arch.Core.Utils;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Delta.ECS;

namespace Delta.ECS.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class DeltaEcsVsArchBenchmarks
{
    [Params(10_000, 100_000)]
    public int Amount { get; set; }

    [Params(1, 2, 4, 8)]
    public int ComponentCount { get; set; }

    private World _deltaWorld = null!;
    private ComponentId[] _deltaComponents = Array.Empty<ComponentId>();
    private Query _deltaQuery;
    private WriteAccess[] _deltaBindings = Array.Empty<WriteAccess>();
    private Entity[] _deltaCreated = Array.Empty<Entity>();
    private World _arrayWorld = null!;
    private ComponentId[] _arrayComponents = Array.Empty<ComponentId>();
    private Query _arrayQuery;
    private WriteAccess[] _arrayBindings = Array.Empty<WriteAccess>();
    private Entity[] _arrayCreated = Array.Empty<Entity>();

    private Arch.Core.World _archWorld = null!;
    private ComponentType[] _archComponents = Array.Empty<ComponentType>();
    private Arch.Core.QueryDescription _archQuery;

    private struct Value
    {
        public float X;
        public float Y;
    }

    private struct ArchValue0 { public float X; public float Y; }
    private struct ArchValue1 { public float X; public float Y; }
    private struct ArchValue2 { public float X; public float Y; }
    private struct ArchValue3 { public float X; public float Y; }
    private struct ArchValue4 { public float X; public float Y; }
    private struct ArchValue5 { public float X; public float Y; }
    private struct ArchValue6 { public float X; public float Y; }
    private struct ArchValue7 { public float X; public float Y; }

    private static readonly ComponentType[] s_allArchComponents =
    {
        typeof(ArchValue0), typeof(ArchValue1), typeof(ArchValue2), typeof(ArchValue3),
        typeof(ArchValue4), typeof(ArchValue5), typeof(ArchValue6), typeof(ArchValue7)
    };

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaComponents = new ComponentId[ComponentCount];
        for (var i = 0; i < ComponentCount; i++)
        {
            _deltaComponents[i] = layouts.Register(typeof(Value), new SchemaId((ulong)(10_001 + i)));
        }

        _deltaWorld = new World(layouts, initialEntityCapacity: Amount);
        var spec = QuerySpec.ForComponents(_deltaComponents);
        _deltaQuery = _deltaWorld.CreateQuery(in spec);
        _deltaBindings = new WriteAccess[ComponentCount];
        for (var i = 0; i < ComponentCount; i++)
            _deltaBindings[i] = _deltaQuery.AccessWrite(_deltaComponents[i]);
        _deltaCreated = new Entity[Amount];
        _deltaWorld.Create(_deltaComponents, _deltaCreated);
        for (var i = 0; i < _deltaCreated.Length; i++)
        {
            for (var componentIndex = 0; componentIndex < _deltaComponents.Length; componentIndex++)
            {
                _deltaWorld.Set(_deltaCreated[i], _deltaComponents[componentIndex], new Value { X = 1f, Y = 2f });
            }
        }

        var arrayLayouts = new ComponentLayoutRegistry();
        _arrayComponents = new ComponentId[ComponentCount];
        for (var i = 0; i < ComponentCount; i++)
        {
            _arrayComponents[i] = arrayLayouts.Register(typeof(Value), new SchemaId((ulong)(11_001 + i)));
        }

        _arrayWorld = new World(
            arrayLayouts,
            initialEntityCapacity: Amount);
        var arrayDescription = QuerySpec.ForComponents(_arrayComponents);
        _arrayQuery = _arrayWorld.CreateQuery(in arrayDescription);
        _arrayBindings = new WriteAccess[ComponentCount];
        for (var i = 0; i < ComponentCount; i++)
            _arrayBindings[i] = _arrayQuery.AccessWrite(_arrayComponents[i]);
        _arrayCreated = new Entity[Amount];
        _arrayWorld.Create(_arrayComponents, _arrayCreated);
        for (var i = 0; i < _arrayCreated.Length; i++)
        {
            for (var componentIndex = 0; componentIndex < _arrayComponents.Length; componentIndex++)
            {
                _arrayWorld.Set(_arrayCreated[i], _arrayComponents[componentIndex], new Value { X = 1f, Y = 2f });
            }
        }

        _archWorld = Arch.Core.World.Create();
        _archComponents = new ComponentType[ComponentCount];
        Array.Copy(s_allArchComponents, _archComponents, ComponentCount);
        _archQuery = new Arch.Core.QueryDescription { All = _archComponents };
        _archWorld.Reserve(_archComponents, Amount);
        for (var i = 0; i < Amount; i++)
        {
            var entity = _archWorld.Create(_archComponents);
            SetArchValues(entity);
        }
    }

    [Benchmark]
    public void DeltaECS_DenseIteration()
    {
        IterateDelta(_deltaWorld, in _deltaQuery, _deltaBindings, ComponentCount);
    }

    [Benchmark(Baseline = true)]
    public void DeltaECS_Array_DenseIteration()
    {
        IterateDelta(_arrayWorld, in _arrayQuery, _arrayBindings, ComponentCount);
    }

    [Benchmark]
    public void Arch_DenseIteration()
    {
        switch (ComponentCount)
        {
            case 1:
                _archWorld.Query(_archQuery, static (ref ArchValue0 c0) => Update(ref c0));
                break;
            case 2:
                _archWorld.Query(_archQuery, static (ref ArchValue0 c0, ref ArchValue1 c1) =>
                {
                    Update(ref c0);
                    Update(ref c1);
                });
                break;
            case 4:
                _archWorld.Query(_archQuery, static (ref ArchValue0 c0, ref ArchValue1 c1, ref ArchValue2 c2, ref ArchValue3 c3) =>
                {
                    Update(ref c0);
                    Update(ref c1);
                    Update(ref c2);
                    Update(ref c3);
                });
                break;
            case 8:
                _archWorld.Query(_archQuery, static (
                    ref ArchValue0 c0,
                    ref ArchValue1 c1,
                    ref ArchValue2 c2,
                    ref ArchValue3 c3,
                    ref ArchValue4 c4,
                    ref ArchValue5 c5,
                    ref ArchValue6 c6,
                    ref ArchValue7 c7) =>
                {
                    Update(ref c0);
                    Update(ref c1);
                    Update(ref c2);
                    Update(ref c3);
                    Update(ref c4);
                    Update(ref c5);
                    Update(ref c6);
                    Update(ref c7);
                });
                break;
            default:
                throw new InvalidOperationException();
        }
    }

    private void SetArchValues(Arch.Core.Entity entity)
    {
        if (ComponentCount >= 1) _archWorld.Set(entity, new ArchValue0 { X = 1f, Y = 2f });
        if (ComponentCount >= 2) _archWorld.Set(entity, new ArchValue1 { X = 1f, Y = 2f });
        if (ComponentCount >= 4)
        {
            _archWorld.Set(entity, new ArchValue2 { X = 1f, Y = 2f });
            _archWorld.Set(entity, new ArchValue3 { X = 1f, Y = 2f });
        }

        if (ComponentCount >= 8)
        {
            _archWorld.Set(entity, new ArchValue4 { X = 1f, Y = 2f });
            _archWorld.Set(entity, new ArchValue5 { X = 1f, Y = 2f });
            _archWorld.Set(entity, new ArchValue6 { X = 1f, Y = 2f });
            _archWorld.Set(entity, new ArchValue7 { X = 1f, Y = 2f });
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void IterateDelta(World world, in Query query, WriteAccess[] bindings, int componentCount)
    {
        using var scope = world.OpenQuery(in query);
        var b0 = scope.Bind(bindings[0]);
        var b1 = componentCount >= 2 ? scope.Bind(bindings[1]) : default;
        var b2 = componentCount >= 4 ? scope.Bind(bindings[2]) : default;
        var b3 = componentCount >= 4 ? scope.Bind(bindings[3]) : default;
        var b4 = componentCount >= 8 ? scope.Bind(bindings[4]) : default;
        var b5 = componentCount >= 8 ? scope.Bind(bindings[5]) : default;
        var b6 = componentCount >= 8 ? scope.Bind(bindings[6]) : default;
        var b7 = componentCount >= 8 ? scope.Bind(bindings[7]) : default;
        var archetypes = scope.Archetypes;

        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                switch (componentCount)
                {
                    case 1:
                    {
                        var c0 = slots.GetRow(b0);
                        while (slots.MoveNext())
                        {
                            Update(ref c0.Ref<Value>(slots));
                        }

                        break;
                    }
                    case 2:
                    {
                        var c0 = slots.GetRow(b0);
                        var c1 = slots.GetRow(b1);
                        while (slots.MoveNext())
                        {
                            Update(ref c0.Ref<Value>(slots));
                            Update(ref c1.Ref<Value>(slots));
                        }

                        break;
                    }
                    case 4:
                    {
                        var c0 = slots.GetRow(b0);
                        var c1 = slots.GetRow(b1);
                        var c2 = slots.GetRow(b2);
                        var c3 = slots.GetRow(b3);
                        while (slots.MoveNext())
                        {
                            Update(ref c0.Ref<Value>(slots));
                            Update(ref c1.Ref<Value>(slots));
                            Update(ref c2.Ref<Value>(slots));
                            Update(ref c3.Ref<Value>(slots));
                        }

                        break;
                    }
                    case 8:
                    {
                        var c0 = slots.GetRow(b0);
                        var c1 = slots.GetRow(b1);
                        var c2 = slots.GetRow(b2);
                        var c3 = slots.GetRow(b3);
                        var c4 = slots.GetRow(b4);
                        var c5 = slots.GetRow(b5);
                        var c6 = slots.GetRow(b6);
                        var c7 = slots.GetRow(b7);
                        while (slots.MoveNext())
                        {
                            Update(ref c0.Ref<Value>(slots));
                            Update(ref c1.Ref<Value>(slots));
                            Update(ref c2.Ref<Value>(slots));
                            Update(ref c3.Ref<Value>(slots));
                            Update(ref c4.Ref<Value>(slots));
                            Update(ref c5.Ref<Value>(slots));
                            Update(ref c6.Ref<Value>(slots));
                            Update(ref c7.Ref<Value>(slots));
                        }

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(componentCount));
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Update(ref Value value) { value.X += value.Y; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Update(ref ArchValue0 value) { value.X += value.Y; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Update(ref ArchValue1 value) { value.X += value.Y; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Update(ref ArchValue2 value) { value.X += value.Y; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Update(ref ArchValue3 value) { value.X += value.Y; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Update(ref ArchValue4 value) { value.X += value.Y; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Update(ref ArchValue5 value) { value.X += value.Y; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Update(ref ArchValue6 value) { value.X += value.Y; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Update(ref ArchValue7 value) { value.X += value.Y; }
}

[MemoryDiagnoser]
[SimpleJob]
public class DeltaEcsBatchBenchmarks
{
    [Params(1_000, 100_000)]
    public int Amount { get; set; }

    private World _world = null!;
    private ComponentId[] _createComponents = Array.Empty<ComponentId>();
    private Entity[] _entities = Array.Empty<Entity>();

    private struct BatchValue
    {
        public long A { get; set; }
        public long B { get; set; }
    }

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        var first = layouts.Register(typeof(BatchValue), new SchemaId(30_001));
        var second = layouts.Register(typeof(BatchValue), new SchemaId(30_002));
        _createComponents = new[] { first, second };
        _world = new World(layouts, initialEntityCapacity: Amount);
        _entities = new Entity[Amount];
    }

    [Benchmark]
    public void BatchCreateDestroy()
    {
        _world.Create(_createComponents, _entities);
        _world.Destroy(_entities);
    }

}

[MemoryDiagnoser]
[SimpleJob]
public class DeltaEcsTransitionBenchmarks
{
    [Params(1_000, 100_000)]
    public int Amount { get; set; }

    private World _world = null!;
    private ComponentId[] _transitionComponents = Array.Empty<ComponentId>();
    private Entity[] _entities = Array.Empty<Entity>();

    private struct TransitionValue
    {
        public long A { get; set; }
        public long B { get; set; }
    }

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        var first = layouts.Register(typeof(TransitionValue), new SchemaId(30_101));
        var second = layouts.Register(typeof(TransitionValue), new SchemaId(30_102));
        _transitionComponents = new[] { second };
        _world = new World(layouts, initialEntityCapacity: Amount);
        _entities = new Entity[Amount];
        _world.Create(new[] { first }, _entities);
    }

    [Benchmark]
    public void BatchAddRemoveTransition()
    {
        _world.AddComponents(_transitionComponents, _entities);
        _world.RemoveComponents(_transitionComponents, _entities);
    }
}

[MemoryDiagnoser]
[SimpleJob]
public class DeltaEcsManagedArrayBenchmarks
{
    [Params(10_000, 100_000)]
    public int Amount { get; set; }

    private World _world = null!;
    private ComponentId _component;
    private Query _query;
    private ReadAccess _binding;
    private Entity[] _entities = Array.Empty<Entity>();

    private struct ManagedValue
    {
        public string? Name;
        public int Value;
    }

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _component = layouts.Register(typeof(ManagedValue), new SchemaId(31_001));
        _world = new World(layouts, initialEntityCapacity: Amount);
        var spec = QuerySpec.ForComponents(_component);
        _query = _world.CreateQuery(in spec);
        _binding = _query.AccessRead(_component);
        _entities = new Entity[Amount];
        _world.Create(new[] { _component }, _entities);
        for (var i = 0; i < _entities.Length; i++)
        {
            _world.Set(_entities[i], _component, new ManagedValue { Name = "managed", Value = i });
        }
    }

    [Benchmark]
    public void ManagedArrayDenseIteration()
    {
        var sum = 0;
        using var scope = _world.OpenQuery(in _query);
        var binding = scope.Bind(_binding);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                var values = slots.GetRow(binding);
                while (slots.MoveNext())
                {
                    sum += values.Ref<ManagedValue>(slots).Value;
                }
            }
        }

        GC.KeepAlive(sum);
    }
}

[MemoryDiagnoser]
[SimpleJob]
public class DeltaEcsHotPathProfileBenchmarks
{
    private World _world = null!;
    private ComponentId _first;
    private ComponentId _second;
    private Query _query;
    private ReadAccess _firstBinding;
    private ReadAccess _secondReadBinding;
    private WriteAccess _firstWriteBinding;
    private WriteAccess _secondBinding;
    private Entity[] _entities = Array.Empty<Entity>();

    private struct Value
    {
        public float X;
        public float Y;
    }

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _first = layouts.Register(typeof(Value), new SchemaId(20_001));
        _second = layouts.Register(typeof(Value), new SchemaId(20_002));
        _world = new World(layouts, initialEntityCapacity: 100_000);
        var spec = QuerySpec.ForComponents(_first, _second);
        _query = _world.CreateQuery(in spec);
        _firstBinding = _query.AccessRead(_first);
        _secondReadBinding = _query.AccessRead(_second);
        _firstWriteBinding = _query.AccessWrite(_first);
        _secondBinding = _query.AccessWrite(_second);
        _entities = new Entity[100_000];
        _world.Create(new[] { _first, _second }, _entities);
        for (var i = 0; i < _entities.Length; i++)
        {
            _world.Set(_entities[i], _first, new Value { X = 1, Y = 2 });
            _world.Set(_entities[i], _second, new Value { X = 1, Y = 2 });
        }
    }

    [Benchmark]
    public void QueryPlanDispatchOnly()
    {
        var chunksCount = 0;
        using var scope = _world.OpenQuery(in _query);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                chunksCount++;
            }
        }

        GC.KeepAlive(chunksCount);
    }

    [Benchmark]
    public void QueryPlanComponentRowLookup()
    {
        var chunksCount = 0;
        using var scope = _world.OpenQuery(in _query);
        var first = scope.Bind(_firstBinding);
        var second = scope.Bind(_secondReadBinding);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                _ = slots.GetRow(first);
                _ = slots.GetRow(second);
                chunksCount++;
            }
        }

        GC.KeepAlive(chunksCount);
    }

    [Benchmark]
    public void QueryPlanSlotLoop()
    {
        var chunksCount = 0;
        using var scope = _world.OpenQuery(in _query);
        var firstBinding = scope.Bind(_firstWriteBinding);
        var secondBinding = scope.Bind(_secondBinding);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                var first = slots.GetRow(firstBinding);
                var second = slots.GetRow(secondBinding);
                while (slots.MoveNext())
                {
                    first.Ref<Value>(slots).X += first.Ref<Value>(slots).Y;
                    second.Ref<Value>(slots).X += second.Ref<Value>(slots).Y;
                }

                chunksCount++;
            }
        }

        GC.KeepAlive(chunksCount);
    }
}

public static class Program
{
    private static readonly Type[] s_fullComparisonSuite = ComparativeBenchmarkCatalog.FullComparison;

    private static void RunComparative(string route, string[] args)
    {
        var reportPath = ExtractOption(args, "--combined-report", out var benchmarkArgs);
        RunTimed($"comparative/{route}", () =>
        {
            BenchmarkSwitcher.FromTypes(ComparativeBenchmarkCatalog.ForRoute(route)).Run(benchmarkArgs);
        });
        if (reportPath is not null)
        {
            ComparativeReportBuilder.WriteManifest(reportPath);
        }
    }

    private static void RunTimed(string name, Action benchmarkRun)
    {
        var startedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"[{startedAt:yyyy-MM-dd HH:mm:ss zzz}] Benchmark started: {name}");

        using var heartbeat = new Timer(
            _ => Console.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] Benchmark still running: {name}, elapsed {stopwatch.Elapsed:hh\\:mm\\:ss}"),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));

        try
        {
            benchmarkRun();
        }
        finally
        {
            stopwatch.Stop();
            Console.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] Benchmark finished: {name}, elapsed {stopwatch.Elapsed:hh\\:mm\\:ss}");
        }
    }

    private static string? ExtractOption(string[] args, string option, out string[] remaining)
    {
        var values = new List<string>(args.Length);
        string? path = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], option, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                path = args[++i];
                continue;
            }

            values.Add(args[i]);
        }

        remaining = values.ToArray();
        return path;
    }

    public static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "contract-smoke", StringComparison.OrdinalIgnoreCase))
        {
            ComparativeBenchmarkCatalog.Validate();
            ComparativeBenchmarkExecutionSmoke.RunAmount100();
            Console.WriteLine($"Comparative contract smoke passed: {ComparativeBenchmarkCatalog.FullComparison.Length} unified classes, {ComparativeCapabilityManifest.Rows.Count} capability rows, Amount=100 execution.");
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "combined-report", StringComparison.OrdinalIgnoreCase))
        {
            var directory = args.Length > 1 ? args[1] : "artifacts/comparative";
            ComparativeBenchmarkCatalog.Validate();
            ComparativeReportBuilder.WriteManifest(directory);
            Console.WriteLine($"Wrote comparative report to {directory}.");
            return;
        }

        if (args.Length > 0 && (string.Equals(args[0], "iteration", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args[0], "openquery", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args[0], "rawaccess", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args[0], "structural-list", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args[0], "structural-query", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args[0], "structural-atomic", StringComparison.OrdinalIgnoreCase)))
        {
            var benchmarkArgs = args[1..];
            RunComparative(args[0], benchmarkArgs);
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "full-comparison", StringComparison.OrdinalIgnoreCase))
        {
            RunComparative("full-comparison", args[1..]);
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "distinct", StringComparison.OrdinalIgnoreCase))
        {
            var benchmarkArgs = new string[args.Length - 1];
            Array.Copy(args, 1, benchmarkArgs, 0, benchmarkArgs.Length);
            RunTimed("distinct", () =>
            {
                BenchmarkSwitcher.FromTypes(new[] { typeof(DistinctDenseComparisonBenchmarks) }).Run(benchmarkArgs);
            });
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "capacity", StringComparison.OrdinalIgnoreCase))
        {
            var benchmarkArgs = new string[args.Length - 1];
            Array.Copy(args, 1, benchmarkArgs, 0, benchmarkArgs.Length);
            RunTimed("capacity", () =>
            {
                BenchmarkSwitcher.FromTypes(new[] { typeof(DenseCapacitySweepBenchmarks) }).Run(benchmarkArgs);
            });
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "defaultecs", StringComparison.OrdinalIgnoreCase))
        {
            var benchmarkArgs = new string[args.Length - 1];
            Array.Copy(args, 1, benchmarkArgs, 0, benchmarkArgs.Length);
            RunTimed("default-ecs", () =>
            {
                BenchmarkSwitcher.FromTypes(new[] { typeof(DefaultEcsComparisonBenchmarks) }).Run(benchmarkArgs);
            });
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "ecslite", StringComparison.OrdinalIgnoreCase))
        {
            var benchmarkArgs = new string[args.Length - 1];
            Array.Copy(args, 1, benchmarkArgs, 0, benchmarkArgs.Length);
            RunTimed("ecs-lite", () =>
            {
                BenchmarkSwitcher.FromTypes(new[] { typeof(EcsLiteComparisonBenchmarks) }).Run(benchmarkArgs);
            });
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "scenario-small", StringComparison.OrdinalIgnoreCase))
        {
            var benchmarkArgs = new string[args.Length - 1];
            Array.Copy(args, 1, benchmarkArgs, 0, benchmarkArgs.Length);
            RunTimed("scenario-small", () =>
            {
                BenchmarkSwitcher.FromTypes(new[] { typeof(SmallDenseScenarioBenchmarks) }).Run(benchmarkArgs);
            });
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "scenario-wide", StringComparison.OrdinalIgnoreCase))
        {
            var benchmarkArgs = new string[args.Length - 1];
            Array.Copy(args, 1, benchmarkArgs, 0, benchmarkArgs.Length);
            RunTimed("scenario-wide", () =>
            {
                BenchmarkSwitcher.FromTypes(new[] { typeof(WideArchetypeNarrowAccessBenchmarks) }).Run(benchmarkArgs);
            });
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "scenario-wide-comparison", StringComparison.OrdinalIgnoreCase))
        {
            var benchmarkArgs = new string[args.Length - 1];
            Array.Copy(args, 1, benchmarkArgs, 0, benchmarkArgs.Length);
            RunTimed("scenario-wide-comparison", () =>
            {
                BenchmarkSwitcher.FromTypes(new[] { typeof(WideArchetypeNarrowAccessComparisonBenchmarks) }).Run(benchmarkArgs);
            });
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "scenario-fragmented", StringComparison.OrdinalIgnoreCase))
        {
            var benchmarkArgs = new string[args.Length - 1];
            Array.Copy(args, 1, benchmarkArgs, 0, benchmarkArgs.Length);
            RunTimed("scenario-fragmented", () =>
            {
                BenchmarkSwitcher.FromTypes(new[] { typeof(DeltaOnlyFragmentedQueryBenchmarks) }).Run(benchmarkArgs);
            });
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "scenario-sparse", StringComparison.OrdinalIgnoreCase))
        {
            var benchmarkArgs = new string[args.Length - 1];
            Array.Copy(args, 1, benchmarkArgs, 0, benchmarkArgs.Length);
            RunTimed("scenario-sparse", () =>
            {
                BenchmarkSwitcher.FromTypes(new[] { typeof(SparseHeterogeneousQueryBenchmarks) }).Run(benchmarkArgs);
            });
            return;
        }

        if (args.Length > 0)
        {
            RunTimed("assembly", () =>
            {
                BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
            });
        }
        else
        {
            RunTimed("default", () =>
            {
                BenchmarkRunner.Run<DeltaEcsVsArchBenchmarks>();
            });
        }
    }
}
