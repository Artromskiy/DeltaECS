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
    private WriteRequest<Value>[] _deltaBindings = Array.Empty<WriteRequest<Value>>();
    private Entity[] _deltaCreated = Array.Empty<Entity>();
    private World _arrayWorld = null!;
    private ComponentId[] _arrayComponents = Array.Empty<ComponentId>();
    private Query _arrayQuery;
    private WriteRequest<Value>[] _arrayBindings = Array.Empty<WriteRequest<Value>>();
    private Entity[] _arrayCreated = Array.Empty<Entity>();

    private Arch.Core.World _archWorld = null!;
    private ComponentType[] _archComponents = Array.Empty<ComponentType>();
    private Arch.Core.QueryDescription _archQuery;

    private struct Value
    {
        public float X;
        public float Y;
    }

    private struct DeltaState
    {
        public WriteRequest<Value>[] Bindings;
        public int ComponentCount;
    }

    private struct ArchValue0 { public float X; public float Y; }
    private struct ArchValue1 { public float X; public float Y; }
    private struct ArchValue2 { public float X; public float Y; }
    private struct ArchValue3 { public float X; public float Y; }
    private struct ArchValue4 { public float X; public float Y; }
    private struct ArchValue5 { public float X; public float Y; }
    private struct ArchValue6 { public float X; public float Y; }
    private struct ArchValue7 { public float X; public float Y; }

    private static readonly QueryAction<DeltaState> s_deltaIteration = IterateDelta;
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
            _deltaComponents[i] = layouts.Register<Value>(new SchemaId((ulong)(10_001 + i)));
        }

        _deltaWorld = new World(layouts, initialEntityCapacity: Amount);
        var description = QuerySpec.ForComponents(_deltaComponents);
        _deltaQuery = _deltaWorld.CreateQuery(in description);
        _deltaBindings = new WriteRequest<Value>[ComponentCount];
        for (var i = 0; i < ComponentCount; i++)
            _deltaBindings[i] = _deltaQuery.Access<Value>(_deltaComponents[i], AccessMode.Write);
        _deltaCreated = new Entity[Amount];
        _deltaWorld.CreateBatch(_deltaComponents, _deltaCreated);
        for (var i = 0; i < _deltaCreated.Length; i++)
        {
            for (var componentIndex = 0; componentIndex < _deltaComponents.Length; componentIndex++)
            {
                _deltaWorld.SetComponent(_deltaCreated[i], _deltaComponents[componentIndex], new Value { X = 1f, Y = 2f });
            }
        }

        var arrayLayouts = new ComponentLayoutRegistry();
        _arrayComponents = new ComponentId[ComponentCount];
        for (var i = 0; i < ComponentCount; i++)
        {
            _arrayComponents[i] = arrayLayouts.Register<Value>(new SchemaId((ulong)(11_001 + i)));
        }

        _arrayWorld = new World(
            arrayLayouts,
            initialEntityCapacity: Amount);
        var arrayDescription = QuerySpec.ForComponents(_arrayComponents);
        _arrayQuery = _arrayWorld.CreateQuery(in arrayDescription);
        _arrayBindings = new WriteRequest<Value>[ComponentCount];
        for (var i = 0; i < ComponentCount; i++)
            _arrayBindings[i] = _arrayQuery.Access<Value>(_arrayComponents[i], AccessMode.Write);
        _arrayCreated = new Entity[Amount];
        _arrayWorld.CreateBatch(_arrayComponents, _arrayCreated);
        for (var i = 0; i < _arrayCreated.Length; i++)
        {
            for (var componentIndex = 0; componentIndex < _arrayComponents.Length; componentIndex++)
            {
                _arrayWorld.SetComponent(_arrayCreated[i], _arrayComponents[componentIndex], new Value { X = 1f, Y = 2f });
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
        var state = new DeltaState
        {
            Bindings = _deltaBindings,
            ComponentCount = ComponentCount
        };
        _deltaWorld.Query(in _deltaQuery, ref state, s_deltaIteration);
    }

    [Benchmark(Baseline = true)]
    public void DeltaECS_Array_DenseIteration()
    {
        var state = new DeltaState
        {
            Bindings = _arrayBindings,
            ComponentCount = ComponentCount
        };
        _arrayWorld.Query(in _arrayQuery, ref state, s_deltaIteration);
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
    private static void IterateDelta(ref DeltaState state, ref QueryChunkCursor lease)
    {
        switch (state.ComponentCount)
        {
            case 1: IterateOne(ref state, ref lease); break;
            case 2: IterateTwo(ref state, ref lease); break;
            case 4: IterateFour(ref state, ref lease); break;
            case 8: IterateEight(ref state, ref lease); break;
            default: throw new ArgumentOutOfRangeException(nameof(state.ComponentCount));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void IterateOne(ref DeltaState state, ref QueryChunkCursor lease)
    {
        var c0 = lease.Get(state.Bindings[0]);
        var slotCount = lease.SlotCount;
        while (lease.MoveNext())
        {
            Update(ref c0[lease]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void IterateTwo(ref DeltaState state, ref QueryChunkCursor lease)
    {
        var c0 = lease.Get(state.Bindings[0]);
        var c1 = lease.Get(state.Bindings[1]);
        var slotCount = lease.SlotCount;
        while (lease.MoveNext())
        {
            Update(ref c0[lease]);
            Update(ref c1[lease]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void IterateFour(ref DeltaState state, ref QueryChunkCursor lease)
    {
        var c0 = lease.Get(state.Bindings[0]);
        var c1 = lease.Get(state.Bindings[1]);
        var c2 = lease.Get(state.Bindings[2]);
        var c3 = lease.Get(state.Bindings[3]);
        var slotCount = lease.SlotCount;
        while (lease.MoveNext())
        {
            Update(ref c0[lease]);
            Update(ref c1[lease]);
            Update(ref c2[lease]);
            Update(ref c3[lease]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void IterateEight(ref DeltaState state, ref QueryChunkCursor lease)
    {
        var c0 = lease.Get(state.Bindings[0]);
        var c1 = lease.Get(state.Bindings[1]);
        var c2 = lease.Get(state.Bindings[2]);
        var c3 = lease.Get(state.Bindings[3]);
        var c4 = lease.Get(state.Bindings[4]);
        var c5 = lease.Get(state.Bindings[5]);
        var c6 = lease.Get(state.Bindings[6]);
        var c7 = lease.Get(state.Bindings[7]);
        var slotCount = lease.SlotCount;
        while (lease.MoveNext())
        {
            Update(ref c0[lease]);
            Update(ref c1[lease]);
            Update(ref c2[lease]);
            Update(ref c3[lease]);
            Update(ref c4[lease]);
            Update(ref c5[lease]);
            Update(ref c6[lease]);
            Update(ref c7[lease]);
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
        var first = layouts.Register<BatchValue>(new SchemaId(30_001));
        var second = layouts.Register<BatchValue>(new SchemaId(30_002));
        _createComponents = new[] { first, second };
        _world = new World(layouts, initialEntityCapacity: Amount);
        _entities = new Entity[Amount];
    }

    [Benchmark]
    public void BatchCreateDestroy()
    {
        _world.CreateBatch(_createComponents, _entities);
        _world.DestroyBatch(_entities);
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
        var first = layouts.Register<TransitionValue>(new SchemaId(30_101));
        var second = layouts.Register<TransitionValue>(new SchemaId(30_102));
        _transitionComponents = new[] { second };
        _world = new World(layouts, initialEntityCapacity: Amount);
        _entities = new Entity[Amount];
        _world.CreateBatch(new[] { first }, _entities);
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
    private ReadRequest<ManagedValue> _binding;
    private Entity[] _entities = Array.Empty<Entity>();

    private struct ManagedValue
    {
        public string? Name;
        public int Value;
    }

    private struct State
    {
        public int Sum;
        public ReadRequest<ManagedValue> Binding;
    }

    private static readonly QueryAction<State> s_iteration = Iterate;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _component = layouts.Register<ManagedValue>(new SchemaId(31_001));
        _world = new World(layouts, initialEntityCapacity: Amount);
        var description = QuerySpec.ForComponents(_component);
        _query = _world.CreateQuery(in description);
        _binding = _query.Access<ManagedValue>(_component, AccessMode.Read);
        _entities = new Entity[Amount];
        _world.CreateBatch(new[] { _component }, _entities);
        for (var i = 0; i < _entities.Length; i++)
        {
            _world.SetComponent(_entities[i], _component, new ManagedValue { Name = "managed", Value = i });
        }
    }

    [Benchmark]
    public void ManagedArrayDenseIteration()
    {
        var state = new State { Binding = _binding };
        _world.Query(in _query, ref state, s_iteration);
        GC.KeepAlive(state.Sum);
    }

    private static void Iterate(ref State state, ref QueryChunkCursor lease)
    {
        var values = lease.Get(state.Binding);
        while (lease.MoveNext())
        {
            if (lease.IsActiveSlot(lease.CurrentIndex))
            {
                state.Sum += values[lease].Value;
            }
        }
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
    private ReadRequest<Value> _firstBinding;
    private ReadRequest<Value> _secondReadBinding;
    private WriteRequest<Value> _firstWriteBinding;
    private WriteRequest<Value> _secondBinding;
    private Entity[] _entities = Array.Empty<Entity>();

    private struct Value
    {
        public float X;
        public float Y;
    }

    private struct ProfileState
    {
        public int Chunks;
        public ReadRequest<Value> First;
        public WriteRequest<Value> Second;
        public WriteRequest<Value> FirstWrite;
        public ReadRequest<Value> SecondRead;
    }

    private static readonly QueryAction<ProfileState> s_dispatch = CountChunk;
    private static readonly QueryAction<ProfileState> s_lookup = LookupComponentRows;
    private static readonly QueryAction<ProfileState> s_slots = IterateSlots;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _first = layouts.Register<Value>(new SchemaId(20_001));
        _second = layouts.Register<Value>(new SchemaId(20_002));
        _world = new World(layouts, initialEntityCapacity: 100_000);
        var description = QuerySpec.ForComponents(_first, _second);
        _query = _world.CreateQuery(in description);
        _firstBinding = _query.Access<Value>(_first, AccessMode.Read);
        _secondReadBinding = _query.Access<Value>(_second, AccessMode.Read);
        _firstWriteBinding = _query.Access<Value>(_first, AccessMode.Write);
        _secondBinding = _query.Access<Value>(_second, AccessMode.Write);
        _entities = new Entity[100_000];
        _world.CreateBatch(new[] { _first, _second }, _entities);
        for (var i = 0; i < _entities.Length; i++)
        {
            _world.SetComponent(_entities[i], _first, new Value { X = 1, Y = 2 });
            _world.SetComponent(_entities[i], _second, new Value { X = 1, Y = 2 });
        }
    }

    [Benchmark]
    public void QueryPlanDispatchOnly()
    {
        var state = new ProfileState { First = _firstBinding, Second = _secondBinding, FirstWrite = _firstWriteBinding, SecondRead = _secondReadBinding };
        _world.Query(in _query, ref state, s_dispatch);
        GC.KeepAlive(state.Chunks);
    }

    [Benchmark]
    public void QueryPlanComponentRowLookup()
    {
        var state = new ProfileState { First = _firstBinding, Second = _secondBinding, FirstWrite = _firstWriteBinding, SecondRead = _secondReadBinding };
        _world.Query(in _query, ref state, s_lookup);
        GC.KeepAlive(state.Chunks);
    }

    [Benchmark]
    public void QueryPlanSlotLoop()
    {
        var state = new ProfileState { First = _firstBinding, Second = _secondBinding, FirstWrite = _firstWriteBinding, SecondRead = _secondReadBinding };
        _world.Query(in _query, ref state, s_slots);
        GC.KeepAlive(state.Chunks);
    }


    private static void CountChunk(ref ProfileState state, ref QueryChunkCursor lease)
    {
        state.Chunks++;
    }

    private static void LookupComponentRows(ref ProfileState state, ref QueryChunkCursor lease)
    {
        _ = lease.Get(state.First);
        _ = lease.Get(state.SecondRead);
        state.Chunks++;
    }

    private static void IterateSlots(ref ProfileState state, ref QueryChunkCursor lease)
    {
        var first = lease.Get(state.FirstWrite);
        var second = lease.Get(state.Second);
        while (lease.MoveNext())
        {
            first[lease].X += first[lease].Y;
            second[lease].X += second[lease].Y;
        }

        state.Chunks++;
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
