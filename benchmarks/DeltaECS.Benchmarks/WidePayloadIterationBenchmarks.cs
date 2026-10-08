using System.Runtime.InteropServices;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Delta.ECS;
using DeltaEntity = Delta.ECS.Entity;
using DeltaWorld = Delta.ECS.World;

namespace Delta.ECS.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BenchmarkCategory("Iteration.WidePayloadPartialRead")]
// The archetype contains eight wide rows, while the callback consumes only
// the first and last row. This isolates wide storage from the actual read set.
public class WidePayloadPartialReadIterationBenchmarks
{
    public int Amount { get; set; } = BenchmarkConfiguration.GetAmount();

    public int WorkerCount { get; set; } = ParallelBenchmarkConfiguration.GetWorkerCount();

    private DeltaWorld _world = null!;
    private Query _query;
    private ComponentId[] _components = null!;
    private DeltaEntity[] _entities = null!;
    private EcsOperation<int>? _iteration;
    private EcsOperation<WidePayloadSingleThreadFunctor>? _functorIteration;
    private EcsOperation? _parallelIteration;
    private EcsOperation<WidePayloadParallelFunctor>? _parallelFunctorIteration;
    private EcsOperation? _entityRefParallelIteration;
    private EcsOperation<WidePayloadEntityRefFunctor>? _entityRefFunctorParallelIteration;
    private readonly WidePayloadChecksum _entityRefChecksum = new();

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _components =
        [
            layouts.Register<WidePayload0>( new SchemaId(206_000)),
            layouts.Register<WidePayload1>( new SchemaId(206_001)),
            layouts.Register<WidePayload2>( new SchemaId(206_002)),
            layouts.Register<WidePayload3>( new SchemaId(206_003)),
            layouts.Register<WidePayload4>( new SchemaId(206_004)),
            layouts.Register<WidePayload5>( new SchemaId(206_005)),
            layouts.Register<WidePayload6>( new SchemaId(206_006)),
            layouts.Register<WidePayload7>( new SchemaId(206_007)),
        ];
        _entityRefChecksum.FirstId = _components[0];
        _entityRefChecksum.LastId = _components[7];

        _world = new DeltaWorld(layouts, initialEntityCapacity: Amount);
        _entities = new DeltaEntity[Amount];
        _world.Create(_components, _entities);

        for (int index = 0; index < Amount; index++)
        {
            DeltaEntity entity = _entities[index];
            _world.GetRef<WidePayload0>(entity, _components[0]) = new WidePayload0 { Value = 1 };
            _world.GetRef<WidePayload7>(entity, _components[7]) = new WidePayload7 { Value = 8 };
        }

        var spec = QuerySpec.WhereAll(_components);
        _query = _world.CreateQuery(in spec);
    }

    [GlobalCleanup]
    public void Cleanup() => _world?.Dispose();

    [BenchmarkCategory("SingleThread")]
    [Benchmark(Baseline = true)]
    public int DeltaECSWidePayloadPartialRead()
    {
        var checksum = 0;
        _iteration ??= _world.ForEach(
            in _query,
            ref checksum,
            static (
                ref int checksum,
                ref readonly WidePayload0 payload0,
                ref readonly WidePayload1 payload1,
                ref readonly WidePayload2 payload2,
                ref readonly WidePayload3 payload3,
                ref readonly WidePayload4 payload4,
                ref readonly WidePayload5 payload5,
                ref readonly WidePayload6 payload6,
                ref readonly WidePayload7 payload7) =>
            {
                _ = payload1;
                _ = payload2;
                _ = payload3;
                _ = payload4;
                _ = payload5;
                _ = payload6;
                checksum += payload0.Value + payload7.Value;
            });
        _iteration.Invoke(ref checksum);

        return checksum == Amount * 9
            ? checksum
            : throw new InvalidOperationException($"wide payload checksum mismatch: {checksum} != {Amount * 9}");
    }

    [BenchmarkCategory("SingleThread")]
    [Benchmark]
    public int DeltaECSWidePayloadPartialReadFunctor()
    {
        _entityRefChecksum.Value = 0;
        _functorIteration ??= _world.ForEach(
            in _query,
            new WidePayloadSingleThreadFunctor(_entityRefChecksum));
        _functorIteration.Invoke();

        return ValidateEntityRefChecksum();
    }

    [BenchmarkCategory("MultiThread")]
    [Benchmark]
    public int DeltaECSWidePayloadPartialReadParallel()
    {
        _entityRefChecksum.Value = 0;
        _parallelIteration ??= _world.ForEachParallel(
            in _query,
            in _entityRefChecksum,
            static (
                in WidePayloadChecksum checksum,
                ref readonly WidePayload0 payload0,
                ref readonly WidePayload1 payload1,
                ref readonly WidePayload2 payload2,
                ref readonly WidePayload3 payload3,
                ref readonly WidePayload4 payload4,
                ref readonly WidePayload5 payload5,
                ref readonly WidePayload6 payload6,
                ref readonly WidePayload7 payload7) =>
            {
                _ = payload1;
                _ = payload2;
                _ = payload3;
                _ = payload4;
                _ = payload5;
                _ = payload6;
                Interlocked.Add(ref checksum.Value, payload0.Value + payload7.Value);
            },
            WorkerCount);
        _parallelIteration.Invoke();

        return ValidateEntityRefChecksum();
    }

    [BenchmarkCategory("MultiThread")]
    [Benchmark]
    public int DeltaECSWidePayloadPartialReadFunctorParallel()
    {
        _entityRefChecksum.Value = 0;
        _parallelFunctorIteration ??= _world.ForEachParallel(
            in _query,
            new WidePayloadParallelFunctor(_entityRefChecksum),
            WorkerCount);
        _parallelFunctorIteration.Invoke();

        return ValidateEntityRefChecksum();
    }

    [BenchmarkCategory("MultiThread")]
    [Benchmark]
    public int DeltaECSWidePayloadPartialReadEntityParallel()
    {
        _entityRefChecksum.Value = 0;
        _entityRefParallelIteration ??= _world.ForEachEntityParallel(
            in _query,
            in _entityRefChecksum,
            static (in WidePayloadChecksum checksum, EntityRef entity) =>
            {
                ref WidePayload0 payload0 = ref entity.GetRef<WidePayload0>(checksum.FirstId);
                ref WidePayload7 payload7 = ref entity.GetRef<WidePayload7>(checksum.LastId);
                Interlocked.Add(ref checksum.Value, payload0.Value + payload7.Value);
            },
            WorkerCount);
        _entityRefParallelIteration.Invoke();

        return ValidateEntityRefChecksum();
    }

    [BenchmarkCategory("MultiThread")]
    [Benchmark]
    public int DeltaECSWidePayloadPartialReadEntityFunctorParallel()
    {
        _entityRefChecksum.Value = 0;
        _entityRefFunctorParallelIteration ??= _world.ForEachEntityParallel(
            in _query,
            new WidePayloadEntityRefFunctor(_entityRefChecksum),
            WorkerCount);
        _entityRefFunctorParallelIteration.Invoke();

        return ValidateEntityRefChecksum();
    }

    private int ValidateEntityRefChecksum()
        => _entityRefChecksum.Value == Amount * 9
            ? _entityRefChecksum.Value
            : throw new InvalidOperationException($"wide payload checksum mismatch: {_entityRefChecksum.Value} != {Amount * 9}");
}

internal sealed class WidePayloadChecksum
{
    internal ComponentId FirstId;
    internal ComponentId LastId;
    internal int Value;
}

internal struct WidePayloadSingleThreadFunctor : IForEach
{
    private readonly WidePayloadChecksum _checksum;

    internal WidePayloadSingleThreadFunctor(WidePayloadChecksum checksum) => _checksum = checksum;

    public void Invoke(
        ref readonly WidePayload0 payload0,
        ref readonly WidePayload1 payload1,
        ref readonly WidePayload2 payload2,
        ref readonly WidePayload3 payload3,
        ref readonly WidePayload4 payload4,
        ref readonly WidePayload5 payload5,
        ref readonly WidePayload6 payload6,
        ref readonly WidePayload7 payload7)
    {
        _ = payload1;
        _ = payload2;
        _ = payload3;
        _ = payload4;
        _ = payload5;
        _ = payload6;
        _checksum.Value += payload0.Value + payload7.Value;
    }
}

internal struct WidePayloadParallelFunctor : IForEach
{
    private readonly WidePayloadChecksum _checksum;

    internal WidePayloadParallelFunctor(WidePayloadChecksum checksum) => _checksum = checksum;

    public void Invoke(
        ref readonly WidePayload0 payload0,
        ref readonly WidePayload1 payload1,
        ref readonly WidePayload2 payload2,
        ref readonly WidePayload3 payload3,
        ref readonly WidePayload4 payload4,
        ref readonly WidePayload5 payload5,
        ref readonly WidePayload6 payload6,
        ref readonly WidePayload7 payload7)
    {
        _ = payload1;
        _ = payload2;
        _ = payload3;
        _ = payload4;
        _ = payload5;
        _ = payload6;
        Interlocked.Add(ref _checksum.Value, payload0.Value + payload7.Value);
    }
}

internal struct WidePayloadEntityRefFunctor : IForEachEntity
{
    private readonly WidePayloadChecksum _checksum;

    internal WidePayloadEntityRefFunctor(WidePayloadChecksum checksum) => _checksum = checksum;

    public void Invoke(EntityRef entity)
    {
        ref WidePayload0 payload0 = ref entity.GetRef<WidePayload0>(_checksum.FirstId);
        ref WidePayload7 payload7 = ref entity.GetRef<WidePayload7>(_checksum.LastId);
        Interlocked.Add(ref _checksum.Value, payload0.Value + payload7.Value);
    }
}

[StructLayout(LayoutKind.Sequential, Size = 512)]
internal struct WidePayload0 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 512)]
internal struct WidePayload1 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 512)]
internal struct WidePayload2 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 512)]
internal struct WidePayload3 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 512)]
internal struct WidePayload4 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 512)]
internal struct WidePayload5 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 512)]
internal struct WidePayload6 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 512)]
internal struct WidePayload7 { public int Value; }
