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
    private Entity[] _deltaCreated = Array.Empty<Entity>();
    private World _arrayWorld = null!;
    private ComponentId[] _arrayComponents = Array.Empty<ComponentId>();
    private Query _arrayQuery;
    private Entity[] _arrayCreated = Array.Empty<Entity>();

    private Arch.Core.World _archWorld = null!;
    private ComponentType[] _archComponents = Array.Empty<ComponentType>();
    private Arch.Core.QueryDescription _archQuery;

    internal struct Value
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
        var spec = QuerySpec.WhereAll(_deltaComponents);
        _deltaQuery = _deltaWorld.CreateQuery(in spec);
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
        var arrayDescription = QuerySpec.WhereAll(_arrayComponents);
        _arrayQuery = _arrayWorld.CreateQuery(in arrayDescription);
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
        IterateDelta(_deltaWorld, in _deltaQuery, _deltaComponents, ComponentCount);
    }

    [Benchmark(Baseline = true)]
    public void DeltaECS_Array_DenseIteration()
    {
        IterateDelta(_arrayWorld, in _arrayQuery, _arrayComponents, ComponentCount);
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
    private static void IterateDelta(World world, in Query query, ComponentId[] components, int componentCount)
    {
        switch (componentCount)
        {
            case 1:
                world.ForEach(in query, components[0], static (ref Value value) =>
                {
                    Update(ref value);
                });
                break;
            case 2:
                world.ForEach(in query, components[0], components[1], static (ref Value c0, ref Value c1) =>
                {
                    Update(ref c0);
                    Update(ref c1);
                });
                break;
            case 4:
                world.ForEach(in query, components[0], components[1], components[2], components[3], static (ref Value c0, ref Value c1, ref Value c2, ref Value c3) =>
                {
                    Update(ref c0);
                    Update(ref c1);
                    Update(ref c2);
                    Update(ref c3);
                });
                break;
            case 8:
                world.ForEach(in query, components[0], components[1], components[2], components[3], components[4], components[5], components[6], components[7], static (ref Value c0, ref Value c1, ref Value c2, ref Value c3, ref Value c4, ref Value c5, ref Value c6, ref Value c7) =>
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
                throw new ArgumentOutOfRangeException(nameof(componentCount));
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
        _world.Add(_transitionComponents, _entities);
        _world.Remove(_transitionComponents, _entities);
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
    private Entity[] _entities = Array.Empty<Entity>();

    internal struct ManagedValue
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
        var spec = QuerySpec.WhereAll(_component);
        _query = _world.CreateQuery(in spec);
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
        _world.ForEach(in _query, (in ManagedValue value) =>
        {
            sum += value.Value;
        });

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
    private Entity[] _entities = Array.Empty<Entity>();

    internal struct Value
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
        var spec = QuerySpec.WhereAll(_first, _second);
        _query = _world.CreateQuery(in spec);
        _firstBinding = _query.AccessRead(_first);
        _secondReadBinding = _query.AccessRead(_second);
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
        var first = _firstBinding;
        var second = _secondReadBinding;
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
        _world.ForEach(in _query, _first, _second, static (ref Value first, ref Value second) =>
        {
            first.X += first.Y;
            second.X += second.Y;
        });
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
