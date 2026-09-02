namespace Delta.ECS.Benchmarks;

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using DeltaEntity = Delta.ECS.Entity;
using DeltaWorld = Delta.ECS.World;

[MemoryDiagnoser]
[CategoriesColumn]
[BenchmarkCategory("Iteration.ParallelMovement4")]
public class ParallelMovement4IterationBenchmarks
{
    public int Amount { get; set; } = ParallelBenchmarkConfiguration.Amount;

    public int WorkerCount { get; set; } = ParallelBenchmarkConfiguration.WorkerCount;

    private DeltaWorld _world = null!;
    private Query _query;
    private ComponentId[] _componentIds = null!;
    private DeltaEntity[] _entities = null!;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _componentIds =
        [
            layouts.Register<Movement4A>(new SchemaId(280_000)),
            layouts.Register<Movement4B>(new SchemaId(280_001)),
            layouts.Register<Movement4C>(new SchemaId(280_002)),
            layouts.Register<Movement4D>(new SchemaId(280_003))
        ];
        _world = new DeltaWorld(layouts, initialEntityCapacity: Amount);
        _entities = new DeltaEntity[Amount];
        _world.Create(_componentIds, _entities);
        for (int index = 0; index < Amount; index++)
        {
            _world.Set(_entities[index], _componentIds[0], new Movement4A { Value = 1 });
            _world.Set(_entities[index], _componentIds[1], new Movement4B { Value = 2 });
            _world.Set(_entities[index], _componentIds[2], new Movement4C { Value = 3 });
            _world.Set(_entities[index], _componentIds[3], new Movement4D { Value = 4 });
        }

        _query = _world.CreateQuery(QuerySpec.WhereAll(_componentIds));

        // Exclude worker creation, route preparation and range construction from
        // the measured steady-state calls.
        _world.ForEachParallel(
            in _query,
            static (ref Movement4A a, ref Movement4B b, ref Movement4C c, in Movement4D d) =>
                ApplyMovement4(ref a, ref b, ref c, in d),
            WorkerCount);
    }

    [GlobalCleanup]
    public void Cleanup() => _world?.Dispose();

    [Benchmark(Baseline = true)]
    public void DeltaECS_Movement4() =>
        _world.ForEach(
            in _query,
            static (ref Movement4A a, ref Movement4B b, ref Movement4C c, in Movement4D d) =>
                ApplyMovement4(ref a, ref b, ref c, in d));

    [Benchmark]
    public void DeltaECS_Movement4Parallel() =>
        _world.ForEachParallel(
            in _query,
            static (ref Movement4A a, ref Movement4B b, ref Movement4C c, in Movement4D d) =>
                ApplyMovement4(ref a, ref b, ref c, in d),
            WorkerCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ApplyMovement4(
        ref Movement4A a,
        ref Movement4B b,
        ref Movement4C c,
        in Movement4D d)
    {
        int updatedA = a.Value + d.Value;
        int updatedB = b.Value + d.Value;
        a.Value = updatedA;
        b.Value = updatedB;
        c.Value = (updatedA + updatedB) / 2;
    }
}

internal static class ParallelBenchmarkConfiguration
{
    internal static int Amount { get; set; }
    internal static int WorkerCount { get; set; }
    internal static int[] Amounts { get; set; } = [100, 1_000, 10_000, 100_000, 1_000_000, 5_000_000];

    internal static int[] WorkerCounts { get; set; } = [4];
}
