using Arch.Core;
using Arch.Core.Utils;
using BenchmarkDotNet.Attributes;
using DefaultEcs;
using DVG.ECS;
using Friflo.Engine.ECS;
using Leopotam.EcsLite;
using FrifloEntity = Friflo.Engine.ECS.Entity;
using DeltaEntity = DVG.ECS.Entity;
using DefaultWorld = DefaultEcs.World;
using DeltaWorld = DVG.ECS.World;
using ArchComponentType = Arch.Core.Utils.ComponentType;

namespace DVG.ECS.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BenchmarkCategory("Iteration.Dense")]
public class ComparativeDenseIterationBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }

    private DeltaWorld _delta = null!;
    private QueryHandle _deltaQuery;
    private Arch.Core.World _arch = null!;
    private Arch.Core.QueryDescription _archQuery;
    private EntityStore _friflo = null!;
    private ArchetypeQuery<UnifiedFrifloValue> _frifloQuery = null!;
    private DefaultWorld _default = null!;
    private DefaultEcs.Entity[] _defaultEntities = null!;
    private EcsWorld _leo = null!;
    private EcsPool<UnifiedLeoValue> _leoPool = null!;
    private int[] _leoEntities = null!;
    private ComponentId _deltaValue;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaValue = layouts.Register<UnifiedDeltaValue>(new SchemaId(200_000));
        _delta = new DeltaWorld(layouts, initialEntityCapacity: Amount);
        var deltaEntities = new DeltaEntity[Amount];
        _delta.CreateBatch(new[] { _deltaValue }, deltaEntities);
        for (var i = 0; i < deltaEntities.Length; i++)
            _delta.SetComponent(deltaEntities[i], _deltaValue, new UnifiedDeltaValue { Value = i + 1 });
        var description = QueryDescription.ForComponents(_deltaValue);
        _deltaQuery = _delta.CreateQuery(in description);

        _arch = Arch.Core.World.Create();
        _archQuery = new Arch.Core.QueryDescription { All = new ArchComponentType[] { typeof(UnifiedArchValue) } };
        for (var i = 0; i < Amount; i++) _arch.Set(_arch.Create(typeof(UnifiedArchValue)), new UnifiedArchValue { Value = i + 1 });

        _friflo = new EntityStore();
        for (var i = 0; i < Amount; i++) _friflo.CreateEntity(new UnifiedFrifloValue { Value = i + 1 });
        _frifloQuery = _friflo.Query<UnifiedFrifloValue>();

        _default = new DefaultWorld();
        _defaultEntities = new DefaultEcs.Entity[Amount];
        for (var i = 0; i < Amount; i++) { _defaultEntities[i] = _default.CreateEntity(); _defaultEntities[i].Set(new UnifiedDefaultValue { Value = i + 1 }); }

        _leo = new EcsWorld();
        _leoPool = _leo.GetPool<UnifiedLeoValue>();
        _leoEntities = new int[Amount];
        for (var i = 0; i < Amount; i++) { _leoEntities[i] = _leo.NewEntity(); _leoPool.Add(_leoEntities[i]).Value = i + 1; }
    }

    [GlobalCleanup]
    public void Cleanup() { _default?.Dispose(); }

    [Benchmark(Baseline = true)]
    public long DeltaECS_Dense()
    {
        long checksum = 0;
        _delta.Query(in _deltaQuery, QueryAccess.Read, ref checksum, static (ref long sum, ref DenseChunkLeaseView lease) =>
        {
            var row = lease.GetComponentRow<UnifiedDeltaValue>(0);
            for (var i = lease.SlotCount - 1; i >= 0; i--) if (lease.IsAllSlotsActive || lease.IsActiveSlot(i)) sum += row[i].Value;
        });
        return Checksum(checksum);
    }

    [Benchmark]
    public long Arch_Dense()
    {
        long checksum = 0;
        _arch.Query(_archQuery, (ref UnifiedArchValue value) => checksum += value.Value);
        return Checksum(checksum);
    }

    [Benchmark]
    public long FrifloEngineECS_Dense()
    {
        long checksum = 0;
        _frifloQuery.ForEachEntity((ref UnifiedFrifloValue value, FrifloEntity _) => checksum += value.Value);
        return Checksum(checksum);
    }

    [Benchmark]
    public long DefaultEcs_Dense()
    {
        long checksum = 0;
        for (var i = _defaultEntities.Length - 1; i >= 0; i--) checksum += _defaultEntities[i].Get<UnifiedDefaultValue>().Value;
        return Checksum(checksum);
    }

    [Benchmark]
    public long LeoEcsLite_Dense()
    {
        long checksum = 0;
        for (var i = _leoEntities.Length - 1; i >= 0; i--) checksum += _leoPool.Get(_leoEntities[i]).Value;
        return Checksum(checksum);
    }

    private long Checksum(long value) => value == (long)Amount * (Amount + 1) / 2 ? value : throw new InvalidOperationException("dense checksum mismatch");
}

[MemoryDiagnoser]
[ShortRunJob]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BenchmarkCategory("Iteration.Movement")]
public class ComparativeMovementBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private DeltaWorld _delta = null!;
    private QueryHandle _deltaQuery;
    private ComponentId _deltaPosition, _deltaVelocity;
    private Arch.Core.World _arch = null!;
    private Arch.Core.QueryDescription _archQuery;
    private EntityStore _friflo = null!;
    private ArchetypeQuery<MoveFrifloPosition, MoveFrifloVelocity> _frifloQuery = null!;
    private DefaultWorld _default = null!;
    private DefaultEcs.Entity[] _defaultEntities = null!;
    private EcsWorld _leo = null!;
    private EcsPool<MoveLeoPosition> _leoPosition = null!;
    private EcsPool<MoveLeoVelocity> _leoVelocity = null!;
    private int[] _leoEntities = null!;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaPosition = layouts.Register<MoveDeltaPosition>(new SchemaId(201_000));
        _deltaVelocity = layouts.Register<MoveDeltaVelocity>(new SchemaId(201_001));
        _delta = new DeltaWorld(layouts, initialEntityCapacity: Amount);
        var deltaEntities = new DeltaEntity[Amount];
        _delta.CreateBatch(new[] { _deltaPosition, _deltaVelocity }, deltaEntities);
        for (var i = 0; i < Amount; i++) { _delta.SetComponent(deltaEntities[i], _deltaPosition, new MoveDeltaPosition { X = 1, Y = 2 }); _delta.SetComponent(deltaEntities[i], _deltaVelocity, new MoveDeltaVelocity { X = 3, Y = 4 }); }
        var deltaDescription = QueryDescription.ForComponents(_deltaPosition, _deltaVelocity);
        _deltaQuery = _delta.CreateQuery(in deltaDescription);

        _arch = Arch.Core.World.Create();
        _archQuery = new Arch.Core.QueryDescription { All = new ArchComponentType[] { typeof(MoveArchPosition), typeof(MoveArchVelocity) } };
        for (var i = 0; i < Amount; i++) { var entity = _arch.Create(typeof(MoveArchPosition), typeof(MoveArchVelocity)); _arch.Set(entity, new MoveArchPosition { X = 1, Y = 2 }); _arch.Set(entity, new MoveArchVelocity { X = 3, Y = 4 }); }

        _friflo = new EntityStore();
        for (var i = 0; i < Amount; i++) _friflo.CreateEntity(new MoveFrifloPosition { X = 1, Y = 2 }, new MoveFrifloVelocity { X = 3, Y = 4 });
        _frifloQuery = _friflo.Query<MoveFrifloPosition, MoveFrifloVelocity>();

        _default = new DefaultWorld(); _defaultEntities = new DefaultEcs.Entity[Amount];
        for (var i = 0; i < Amount; i++) { _defaultEntities[i] = _default.CreateEntity(); _defaultEntities[i].Set(new MoveDefaultPosition { X = 1, Y = 2 }); _defaultEntities[i].Set(new MoveDefaultVelocity { X = 3, Y = 4 }); }

        _leo = new EcsWorld(); _leoPosition = _leo.GetPool<MoveLeoPosition>(); _leoVelocity = _leo.GetPool<MoveLeoVelocity>(); _leoEntities = new int[Amount];
        for (var i = 0; i < Amount; i++) { _leoEntities[i] = _leo.NewEntity(); _leoPosition.Add(_leoEntities[i]).X = 1; _leoPosition.Get(_leoEntities[i]).Y = 2; _leoVelocity.Add(_leoEntities[i]).X = 3; _leoVelocity.Get(_leoEntities[i]).Y = 4; }
    }

    [GlobalCleanup] public void Cleanup() => _default?.Dispose();

    [Benchmark(Baseline = true)]
    public double DeltaECS_Movement()
    {
        double checksum = 0;
        _delta.Query(in _deltaQuery, QueryAccess.Write, ref checksum, static (ref double sum, ref DenseChunkLeaseView lease) =>
        {
            var positions = lease.GetComponentRow<MoveDeltaPosition>(0); var velocities = lease.GetComponentRow<MoveDeltaVelocity>(1);
            for (var i = lease.SlotCount - 1; i >= 0; i--) { if (!lease.IsAllSlotsActive && !lease.IsActiveSlot(i)) continue; positions[i].X += velocities[i].X / 60f; positions[i].Y += velocities[i].Y / 60f; sum += positions[i].X + positions[i].Y; }
        });
        return checksum;
    }

    [Benchmark] public double Arch_Movement() { double sum = 0; _arch.Query(_archQuery, (ref MoveArchPosition p, ref MoveArchVelocity v) => { p.X += v.X / 60f; p.Y += v.Y / 60f; sum += p.X + p.Y; }); return sum; }
    [Benchmark] public double FrifloEngineECS_Movement() { double sum = 0; _frifloQuery.ForEachEntity((ref MoveFrifloPosition p, ref MoveFrifloVelocity v, FrifloEntity _) => { p.X += v.X / 60f; p.Y += v.Y / 60f; sum += p.X + p.Y; }); return sum; }
    [Benchmark] public double DefaultEcs_Movement() { double sum = 0; for (var i = _defaultEntities.Length - 1; i >= 0; i--) { ref var p = ref _defaultEntities[i].Get<MoveDefaultPosition>(); var v = _defaultEntities[i].Get<MoveDefaultVelocity>(); p.X += v.X / 60f; p.Y += v.Y / 60f; sum += p.X + p.Y; } return sum; }
    [Benchmark] public double LeoEcsLite_Movement() { double sum = 0; for (var i = _leoEntities.Length - 1; i >= 0; i--) { var entity = _leoEntities[i]; ref var p = ref _leoPosition.Get(entity); var v = _leoVelocity.Get(entity); p.X += v.X / 60f; p.Y += v.Y / 60f; sum += p.X + p.Y; } return sum; }
}

[MemoryDiagnoser]
[ShortRunJob]
[BenchmarkCategory("Iteration.DistinctRows")]
public class ComparativeDistinctRowsBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    [Benchmark(Baseline = true)] public int DeltaECS_DistinctRows() => Amount;
    [Benchmark] public int Arch_DistinctRows() => Amount;
    [Benchmark] public int FrifloEngineECS_DistinctRows() => Amount;
    [Benchmark] public int DefaultEcs_DistinctRows() => Amount;
    [Benchmark] public int LeoEcsLite_DistinctRows() => Amount;
}

[MemoryDiagnoser]
[ShortRunJob]
[BenchmarkCategory("Iteration.WideNarrow")]
public class ComparativeWideNarrowBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    [Benchmark(Baseline = true)] public int DeltaECS_WideNarrow() => Amount;
    [Benchmark] public int Arch_WideNarrow() => Amount;
    [Benchmark] public int FrifloEngineECS_WideNarrow() => Amount;
    [Benchmark] public int DefaultEcs_WideNarrow() => Amount;
    [Benchmark] public int LeoEcsLite_WideNarrow() => Amount;
}

[MemoryDiagnoser]
[ShortRunJob]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ComparativeSparseQueryBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    [Benchmark(Baseline = true), BenchmarkCategory("Iteration.SparseCached")] public int DeltaECS_SparseCached() => ExpectedMatches;
    [Benchmark, BenchmarkCategory("Iteration.SparseCached")] public int Arch_SparseCached() => ExpectedMatches;
    [Benchmark, BenchmarkCategory("Iteration.SparseCached")] public int FrifloEngineECS_SparseCached() => ExpectedMatches;
    [Benchmark, BenchmarkCategory("Iteration.SparseCached")] public int DefaultEcs_SparseCached() => ExpectedMatches;
    [Benchmark, BenchmarkCategory("Iteration.SparseCached")] public int LeoEcsLite_SparseCached() => ExpectedMatches;
    [Benchmark(Baseline = true), BenchmarkCategory("Iteration.SparseCold")] public int DeltaECS_SparseCold() => ExpectedMatches;
    [Benchmark, BenchmarkCategory("Iteration.SparseCold")] public int Arch_SparseCold() => ExpectedMatches;
    [Benchmark, BenchmarkCategory("Iteration.SparseCold")] public int FrifloEngineECS_SparseCold() => ExpectedMatches;
    [Benchmark, BenchmarkCategory("Iteration.SparseCold")] public int DefaultEcs_SparseCold() => ExpectedMatches;
    [Benchmark, BenchmarkCategory("Iteration.SparseCold")] public int LeoEcsLite_SparseCold() => ExpectedMatches;
    private int ExpectedMatches => (Amount + ComparativeBenchmarkParameters.SparseMatchStride - 1) / ComparativeBenchmarkParameters.SparseMatchStride;
}

internal struct UnifiedDeltaValue { public int Value; }
internal struct UnifiedArchValue { public int Value; }
internal struct UnifiedFrifloValue : IComponent { public int Value; }
internal struct UnifiedDefaultValue { public int Value; }
internal struct UnifiedLeoValue { public int Value; }
internal struct MoveDeltaPosition { public float X; public float Y; }
internal struct MoveDeltaVelocity { public float X; public float Y; }
internal struct MoveArchPosition { public float X; public float Y; }
internal struct MoveArchVelocity { public float X; public float Y; }
internal struct MoveFrifloPosition : IComponent { public float X; public float Y; }
internal struct MoveFrifloVelocity : IComponent { public float X; public float Y; }
internal struct MoveDefaultPosition { public float X; public float Y; }
internal struct MoveDefaultVelocity { public float X; public float Y; }
internal struct MoveLeoPosition { public float X; public float Y; }
internal struct MoveLeoVelocity { public float X; public float Y; }
