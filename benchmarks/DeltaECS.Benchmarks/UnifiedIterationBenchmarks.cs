using Arch.Core;
using Arch.Core.Utils;
using BenchmarkDotNet.Attributes;
using DefaultEcs;
using Delta.ECS;
using Friflo.Engine.ECS;
using Leopotam.EcsLite;
using DeltaEntity = Delta.ECS.Entity;
using DeltaWorld = Delta.ECS.World;
using DefaultWorld = DefaultEcs.World;
using FrifloEntity = Friflo.Engine.ECS.Entity;
using ArchComponentType = Arch.Core.Utils.ComponentType;

namespace Delta.ECS.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BenchmarkCategory("Iteration.Dense")]
public class ComparativeDenseIterationBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private DeltaWorld _delta = null!;
    private Query _deltaQuery;
    private ComponentId _deltaValue;
    private ReadRequest<UnifiedDeltaValue> _deltaValueBinding;
    private Arch.Core.World _arch = null!;
    private ArchComponentType[] _archTypes = null!;
    private Arch.Core.QueryDescription _archQuery;
    private EntityStore _friflo = null!;
    private ArchetypeQuery<UnifiedFrifloValue> _frifloQuery = null!;
    private DefaultWorld _default = null!;
    private DefaultEcs.Entity[] _defaultEntities = null!;
    private DefaultEcs.EntitySet _defaultQuery = null!;
    private EcsWorld _leo = null!;
    private EcsPool<UnifiedLeoValue> _leoPool = null!;
    private int[] _leoEntities = null!;
    private EcsFilter _leoQuery = null!;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaValue = layouts.Register<UnifiedDeltaValue>(new SchemaId(200_000));
        _delta = new DeltaWorld(layouts, initialEntityCapacity: Amount);
        var deltaEntities = new DeltaEntity[Amount];
        _delta.CreateBatch(new[] { _deltaValue }, deltaEntities);
        for (var i = 0; i < Amount; i++)
            _delta.SetComponent(deltaEntities[i], _deltaValue, new UnifiedDeltaValue { Value = i + 1 });
        var spec = QuerySpec.ForComponents(_deltaValue);
        _deltaQuery = _delta.CreateQuery(in spec);
        _deltaValueBinding = _deltaQuery.Access<UnifiedDeltaValue>(_deltaValue, AccessMode.Read);

        _arch = Arch.Core.World.Create();
        _archTypes = new ArchComponentType[] { typeof(UnifiedArchValue) };
        _arch.Reserve(_archTypes, Amount);
        _archQuery = new Arch.Core.QueryDescription { All = _archTypes };
        for (var i = 0; i < Amount; i++)
        {
            var entity = _arch.Create(_archTypes);
            _arch.Set(entity, new UnifiedArchValue { Value = i + 1 });
        }

        _friflo = new EntityStore();
        for (var i = 0; i < Amount; i++) _friflo.CreateEntity(new UnifiedFrifloValue { Value = i + 1 });
        _frifloQuery = _friflo.Query<UnifiedFrifloValue>();

        _default = new DefaultWorld();
        _defaultEntities = new DefaultEcs.Entity[Amount];
        for (var i = 0; i < Amount; i++)
        {
            _defaultEntities[i] = _default.CreateEntity();
            _defaultEntities[i].Set(new UnifiedDefaultValue { Value = i + 1 });
        }
        _defaultQuery = _default.GetEntities().With<UnifiedDefaultValue>().AsSet();

        _leo = new EcsWorld();
        _leoPool = _leo.GetPool<UnifiedLeoValue>();
        _leoEntities = new int[Amount];
        for (var i = 0; i < Amount; i++)
        {
            _leoEntities[i] = _leo.NewEntity();
            _leoPool.Add(_leoEntities[i]).Value = i + 1;
        }
        _leoQuery = _leo.Filter<UnifiedLeoValue>().End();
    }

    [GlobalCleanup] public void Cleanup() { _defaultQuery?.Dispose(); _default?.Dispose(); }

    [Benchmark(Baseline = true)]
    public long DeltaECS_Dense()
    {
        var state = new DenseState { Value = _deltaValueBinding };
        _delta.Query(in _deltaQuery, ref state, static (ref DenseState current, ref QueryChunkCursor cursor) =>
        {
            var row = cursor.Get(current.Value);
            while (cursor.MoveNext()) current.Sum += row[cursor].Value;
        });
        return Checksum(state.Sum, (long)Amount * (Amount + 1) / 2, "dense");
    }

    [Benchmark] public long Arch_Dense() { long sum = 0; _arch.Query(_archQuery, (ref UnifiedArchValue value) => sum += value.Value); return Checksum(sum, (long)Amount * (Amount + 1) / 2, "dense"); }
    [Benchmark] public long FrifloEngineECS_Dense() { long sum = 0; _frifloQuery.ForEachEntity((ref UnifiedFrifloValue value, FrifloEntity _) => sum += value.Value); return Checksum(sum, (long)Amount * (Amount + 1) / 2, "dense"); }
    [Benchmark] public long DefaultEcs_Dense() { long sum = 0; var entities = _defaultQuery.GetEntities(); for (var i = entities.Length - 1; i >= 0; i--) sum += entities[i].Get<UnifiedDefaultValue>().Value; return Checksum(sum, (long)Amount * (Amount + 1) / 2, "dense"); }
    [Benchmark] public long LeoEcsLite_Dense() { long sum = 0; foreach (var entity in _leoQuery) sum += _leoPool.Get(entity).Value; return Checksum(sum, (long)Amount * (Amount + 1) / 2, "dense"); }

    internal static long Checksum(long actual, long expected, string name) => actual == expected ? actual : throw new InvalidOperationException($"{name} checksum mismatch: {actual} != {expected}");

    private struct DenseState
    {
        public ReadRequest<UnifiedDeltaValue> Value;
        public long Sum;
    }
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BenchmarkCategory("Iteration.Movement2Components")]
public class ComparativeMovement2ComponentsBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private DeltaWorld _delta = null!;
    private Query _deltaQuery;
    private ComponentId _deltaPosition, _deltaVelocity;
    private DeltaEntity[] _deltaEntities = null!;
    private WriteRequest<MoveDeltaPosition> _deltaPositionBinding;
    private ReadRequest<MoveDeltaVelocity> _deltaVelocityBinding;
    private Arch.Core.World _arch = null!;
    private Arch.Core.QueryDescription _archQuery;
    private ArchComponentType[] _archTypes = null!;
    private Arch.Core.Entity[] _archEntities = null!;
    private EntityStore _friflo = null!;
    private ArchetypeQuery<MoveFrifloPosition, MoveFrifloVelocity> _frifloQuery = null!;
    private FrifloEntity[] _frifloEntities = null!;
    private DefaultWorld _default = null!;
    private DefaultEcs.Entity[] _defaultEntities = null!;
    private DefaultEcs.EntitySet _defaultQuery = null!;
    private EcsWorld _leo = null!;
    private EcsPool<MoveLeoPosition> _leoPosition = null!;
    private EcsPool<MoveLeoVelocity> _leoVelocity = null!;
    private int[] _leoEntities = null!;
    private EcsFilter _leoQuery = null!;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaPosition = layouts.Register<MoveDeltaPosition>(new SchemaId(201_000));
        _deltaVelocity = layouts.Register<MoveDeltaVelocity>(new SchemaId(201_001));
        _delta = new DeltaWorld(layouts, initialEntityCapacity: Amount);
        _deltaEntities = new DeltaEntity[Amount];
        _delta.CreateBatch(new[] { _deltaPosition, _deltaVelocity }, _deltaEntities);
        var deltaDescription = QuerySpec.ForComponents(_deltaPosition, _deltaVelocity);
        _deltaQuery = _delta.CreateQuery(in deltaDescription);
        _deltaPositionBinding = _deltaQuery.Access<MoveDeltaPosition>(_deltaPosition, AccessMode.Write);
        _deltaVelocityBinding = _deltaQuery.Access<MoveDeltaVelocity>(_deltaVelocity, AccessMode.Read);

        _arch = Arch.Core.World.Create();
        _archTypes = new ArchComponentType[] { typeof(MoveArchPosition), typeof(MoveArchVelocity) };
        _arch.Reserve(_archTypes, Amount);
        _archQuery = new Arch.Core.QueryDescription { All = _archTypes };
        _archEntities = new Arch.Core.Entity[Amount];
        for (var i = 0; i < Amount; i++) _archEntities[i] = _arch.Create(_archTypes);

        _friflo = new EntityStore();
        _frifloEntities = new FrifloEntity[Amount];
        for (var i = 0; i < Amount; i++) _frifloEntities[i] = _friflo.CreateEntity(new MoveFrifloPosition(), new MoveFrifloVelocity());
        _frifloQuery = _friflo.Query<MoveFrifloPosition, MoveFrifloVelocity>();

        _default = new DefaultWorld();
        _defaultEntities = new DefaultEcs.Entity[Amount];
        for (var i = 0; i < Amount; i++) _defaultEntities[i] = _default.CreateEntity();
        _defaultQuery = _default.GetEntities().With<MoveDefaultPosition>().With<MoveDefaultVelocity>().AsSet();

        _leo = new EcsWorld();
        _leoPosition = _leo.GetPool<MoveLeoPosition>();
        _leoVelocity = _leo.GetPool<MoveLeoVelocity>();
        _leoEntities = new int[Amount];
        for (var i = 0; i < Amount; i++)
        {
            var entity = _leoEntities[i] = _leo.NewEntity();
            _leoPosition.Add(entity);
            _leoVelocity.Add(entity);
        }
        ResetMovement();
        _leoQuery = _leo.Filter<MoveLeoPosition>().Inc<MoveLeoVelocity>().End();
    }

    public void ResetMovement()
    {
        ResetDeltaMovement();
        ResetArchMovement();
        ResetFrifloMovement();
        ResetDefaultMovement();
        ResetLeoMovement();
    }

    [IterationSetup(Target = nameof(DeltaECS_Movement2Components))]
    public void ResetDeltaMovement()
    {
        for (var i = 0; i < Amount; i++)
        {
            _delta.SetComponent(_deltaEntities[i], _deltaPosition, new MoveDeltaPosition { X = 1, Y = 2 });
            _delta.SetComponent(_deltaEntities[i], _deltaVelocity, new MoveDeltaVelocity { X = 3, Y = 4 });
        }
    }

    [IterationSetup(Target = nameof(Arch_Movement2Components))]
    public void ResetArchMovement()
    {
        for (var i = 0; i < Amount; i++)
        {
            _arch.Set(_archEntities[i], new MoveArchPosition { X = 1, Y = 2 });
            _arch.Set(_archEntities[i], new MoveArchVelocity { X = 3, Y = 4 });
        }
    }

    [IterationSetup(Target = nameof(FrifloEngineECS_Movement2Components))]
    public void ResetFrifloMovement()
    {
        for (var i = 0; i < Amount; i++)
        {
            _frifloEntities[i].GetComponent<MoveFrifloPosition>().X = 1;
            _frifloEntities[i].GetComponent<MoveFrifloPosition>().Y = 2;
            _frifloEntities[i].GetComponent<MoveFrifloVelocity>().X = 3;
            _frifloEntities[i].GetComponent<MoveFrifloVelocity>().Y = 4;
        }
    }

    [IterationSetup(Target = nameof(DefaultEcs_Movement2Components))]
    public void ResetDefaultMovement()
    {
        for (var i = 0; i < Amount; i++)
        {
            _defaultEntities[i].Set(new MoveDefaultPosition { X = 1, Y = 2 });
            _defaultEntities[i].Set(new MoveDefaultVelocity { X = 3, Y = 4 });
        }
    }

    [IterationSetup(Target = nameof(LeoEcsLite_Movement2Components))]
    public void ResetLeoMovement()
    {
        for (var i = 0; i < Amount; i++)
        {
            ref var leoPosition = ref _leoPosition.Get(_leoEntities[i]);
            leoPosition.X = 1; leoPosition.Y = 2;
            ref var leoVelocity = ref _leoVelocity.Get(_leoEntities[i]);
            leoVelocity.X = 3; leoVelocity.Y = 4;
        }
    }

    [GlobalCleanup] public void Cleanup() { _defaultQuery?.Dispose(); _default?.Dispose(); }

    [Benchmark(Baseline = true), InvocationCount(1)]
    public double DeltaECS_Movement2Components()
    {
        var state = new Movement2State { Position = _deltaPositionBinding, Velocity = _deltaVelocityBinding };
        _delta.Query(in _deltaQuery, ref state, static (ref Movement2State current, ref QueryChunkCursor cursor) =>
        {
            var positions = cursor.Get<MoveDeltaPosition>(current.Position);
            var velocities = cursor.Get<MoveDeltaVelocity>(current.Velocity);
            while (cursor.MoveNext())
            {
                ref var position = ref positions[cursor];
                ref readonly var velocity = ref velocities[cursor];
                position.X += velocity.X / 60f; position.Y += velocity.Y / 60f; current.Sum += position.X + position.Y;
            }
        });
        return state.Sum;
    }

    [Benchmark] public double Arch_Movement2Components() { double sum = 0; _arch.Query(_archQuery, (ref MoveArchPosition p, ref MoveArchVelocity v) => { p.X += v.X / 60f; p.Y += v.Y / 60f; sum += p.X + p.Y; }); return sum; }
    [Benchmark] public double FrifloEngineECS_Movement2Components() { double sum = 0; _frifloQuery.ForEachEntity((ref MoveFrifloPosition p, ref MoveFrifloVelocity v, FrifloEntity _) => { p.X += v.X / 60f; p.Y += v.Y / 60f; sum += p.X + p.Y; }); return sum; }
    [Benchmark] public double DefaultEcs_Movement2Components() { double sum = 0; var entities = _defaultQuery.GetEntities(); for (var i = entities.Length - 1; i >= 0; i--) { ref var p = ref entities[i].Get<MoveDefaultPosition>(); var v = entities[i].Get<MoveDefaultVelocity>(); p.X += v.X / 60f; p.Y += v.Y / 60f; sum += p.X + p.Y; } return sum; }
    [Benchmark] public double LeoEcsLite_Movement2Components() { double sum = 0; foreach (var entity in _leoQuery) { ref var p = ref _leoPosition.Get(entity); var v = _leoVelocity.Get(entity); p.X += v.X / 60f; p.Y += v.Y / 60f; sum += p.X + p.Y; } return sum; }

    private struct Movement2State
    {
        public WriteRequest<MoveDeltaPosition> Position;
        public ReadRequest<MoveDeltaVelocity> Velocity;
        public double Sum;
    }
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BenchmarkCategory("Iteration.Movement4Components")]
// Per slot, using integer arithmetic: a' = a + d; b' = b + d;
// c' = (a' + b') / 2; d' remains the read-only control/input row. The checksum
// adds a' + b' + c' + d'. Setup values (1, 2, 3, 4) therefore produce 20 per
// entity on the first invocation. The iteration setup restores the same
// pre-state before each measured invocation so the benchmark remains stable.
public class ComparativeMovement4ComponentsBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private DeltaWorld _delta = null!; private Query _deltaQuery; private ComponentId[] _deltaIds = null!; private DeltaEntity[] _deltaEntities = null!;
    private WriteRequest<DistinctDelta0> _delta0Binding; private WriteRequest<DistinctDelta1> _delta1Binding; private WriteRequest<DistinctDelta2> _delta2Binding; private ReadRequest<DistinctDelta3> _delta3Binding;
    private Arch.Core.World _arch = null!; private ArchComponentType[] _archTypes = null!; private Arch.Core.QueryDescription _archQuery; private Arch.Core.Entity[] _archEntities = null!;
    private EntityStore _friflo = null!; private ArchetypeQuery<DistinctFriflo0, DistinctFriflo1, DistinctFriflo2, DistinctFriflo3> _frifloQuery = null!; private FrifloEntity[] _frifloEntities = null!;
    private DefaultWorld _default = null!; private DefaultEcs.Entity[] _defaultEntities = null!; private DefaultEcs.EntitySet _defaultQuery = null!;
    private EcsWorld _leo = null!; private EcsPool<DistinctLeo0> _leo0 = null!; private EcsPool<DistinctLeo1> _leo1 = null!; private EcsPool<DistinctLeo2> _leo2 = null!; private EcsPool<DistinctLeo3> _leo3 = null!; private int[] _leoEntities = null!; private EcsFilter _leoQuery = null!;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry(); _deltaIds = new[] { layouts.Register<DistinctDelta0>(new SchemaId(202_000)), layouts.Register<DistinctDelta1>(new SchemaId(202_001)), layouts.Register<DistinctDelta2>(new SchemaId(202_002)), layouts.Register<DistinctDelta3>(new SchemaId(202_003)) }; _delta = new DeltaWorld(layouts, initialEntityCapacity: Amount); _deltaEntities = new DeltaEntity[Amount]; _delta.CreateBatch(_deltaIds, _deltaEntities); for (var i = 0; i < Amount; i++) { _delta.SetComponent(_deltaEntities[i], _deltaIds[0], new DistinctDelta0 { Value = 1 }); _delta.SetComponent(_deltaEntities[i], _deltaIds[1], new DistinctDelta1 { Value = 2 }); _delta.SetComponent(_deltaEntities[i], _deltaIds[2], new DistinctDelta2 { Value = 3 }); _delta.SetComponent(_deltaEntities[i], _deltaIds[3], new DistinctDelta3 { Value = 4 }); }
        var d = QuerySpec.ForComponents(_deltaIds); _deltaQuery = _delta.CreateQuery(in d); _delta0Binding = _deltaQuery.Access<DistinctDelta0>(_deltaIds[0], AccessMode.Write); _delta1Binding = _deltaQuery.Access<DistinctDelta1>(_deltaIds[1], AccessMode.Write); _delta2Binding = _deltaQuery.Access<DistinctDelta2>(_deltaIds[2], AccessMode.Write); _delta3Binding = _deltaQuery.Access<DistinctDelta3>(_deltaIds[3], AccessMode.Read);
        _arch = Arch.Core.World.Create(); _archTypes = new ArchComponentType[] { typeof(DistinctArch0), typeof(DistinctArch1), typeof(DistinctArch2), typeof(DistinctArch3) }; _arch.Reserve(_archTypes, Amount); _archQuery = new Arch.Core.QueryDescription { All = _archTypes }; _archEntities = new Arch.Core.Entity[Amount]; for (var i = 0; i < Amount; i++) { _archEntities[i] = _arch.Create(_archTypes); _arch.Set(_archEntities[i], new DistinctArch0 { Value = 1 }); _arch.Set(_archEntities[i], new DistinctArch1 { Value = 2 }); _arch.Set(_archEntities[i], new DistinctArch2 { Value = 3 }); _arch.Set(_archEntities[i], new DistinctArch3 { Value = 4 }); }
        _friflo = new EntityStore(); _frifloEntities = new FrifloEntity[Amount]; for (var i = 0; i < Amount; i++) _frifloEntities[i] = _friflo.CreateEntity(new DistinctFriflo0 { Value = 1 }, new DistinctFriflo1 { Value = 2 }, new DistinctFriflo2 { Value = 3 }, new DistinctFriflo3 { Value = 4 }); _frifloQuery = _friflo.Query<DistinctFriflo0, DistinctFriflo1, DistinctFriflo2, DistinctFriflo3>();
        _default = new DefaultWorld(); _defaultEntities = new DefaultEcs.Entity[Amount]; for (var i = 0; i < Amount; i++) { _defaultEntities[i] = _default.CreateEntity(); SetDefault(_defaultEntities[i]); }
        _defaultQuery = _default.GetEntities().With<DistinctDefault0>().With<DistinctDefault1>().With<DistinctDefault2>().With<DistinctDefault3>().AsSet();
        _leo = new EcsWorld(); _leo0 = _leo.GetPool<DistinctLeo0>(); _leo1 = _leo.GetPool<DistinctLeo1>(); _leo2 = _leo.GetPool<DistinctLeo2>(); _leo3 = _leo.GetPool<DistinctLeo3>(); _leoEntities = new int[Amount]; for (var i = 0; i < Amount; i++) { var e = _leoEntities[i] = _leo.NewEntity(); _leo0.Add(e).Value = 1; _leo1.Add(e).Value = 2; _leo2.Add(e).Value = 3; _leo3.Add(e).Value = 4; }
        _leoQuery = _leo.Filter<DistinctLeo0>().Inc<DistinctLeo1>().Inc<DistinctLeo2>().Inc<DistinctLeo3>().End();
    }
    public void ResetMovement4()
    {
        ResetDeltaMovement4();
        ResetArchMovement4();
        ResetFrifloMovement4();
        ResetDefaultMovement4();
        ResetLeoMovement4();
    }

    [IterationSetup(Target = nameof(DeltaECS_Movement4Components))]
    public void ResetDeltaMovement4()
    {
        for (var i = 0; i < Amount; i++)
        {
            _delta.SetComponent(_deltaEntities[i], _deltaIds[0], new DistinctDelta0 { Value = 1 });
            _delta.SetComponent(_deltaEntities[i], _deltaIds[1], new DistinctDelta1 { Value = 2 });
            _delta.SetComponent(_deltaEntities[i], _deltaIds[2], new DistinctDelta2 { Value = 3 });
            _delta.SetComponent(_deltaEntities[i], _deltaIds[3], new DistinctDelta3 { Value = 4 });
        }
    }

    [IterationSetup(Target = nameof(Arch_Movement4Components))]
    public void ResetArchMovement4()
    {
        for (var i = 0; i < Amount; i++)
        {
            _arch.Set(_archEntities[i], new DistinctArch0 { Value = 1 });
            _arch.Set(_archEntities[i], new DistinctArch1 { Value = 2 });
            _arch.Set(_archEntities[i], new DistinctArch2 { Value = 3 });
            _arch.Set(_archEntities[i], new DistinctArch3 { Value = 4 });
        }
    }

    [IterationSetup(Target = nameof(FrifloEngineECS_Movement4Components))]
    public void ResetFrifloMovement4()
    {
        for (var i = 0; i < Amount; i++)
        {
            _frifloEntities[i].GetComponent<DistinctFriflo0>().Value = 1;
            _frifloEntities[i].GetComponent<DistinctFriflo1>().Value = 2;
            _frifloEntities[i].GetComponent<DistinctFriflo2>().Value = 3;
            _frifloEntities[i].GetComponent<DistinctFriflo3>().Value = 4;
        }
    }

    [IterationSetup(Target = nameof(DefaultEcs_Movement4Components))]
    public void ResetDefaultMovement4()
    {
        for (var i = 0; i < Amount; i++)
        {
            _defaultEntities[i].Set(new DistinctDefault0 { Value = 1 });
            _defaultEntities[i].Set(new DistinctDefault1 { Value = 2 });
            _defaultEntities[i].Set(new DistinctDefault2 { Value = 3 });
            _defaultEntities[i].Set(new DistinctDefault3 { Value = 4 });
        }
    }

    [IterationSetup(Target = nameof(LeoEcsLite_Movement4Components))]
    public void ResetLeoMovement4()
    {
        for (var i = 0; i < Amount; i++)
        {
            _leo0.Get(_leoEntities[i]).Value = 1;
            _leo1.Get(_leoEntities[i]).Value = 2;
            _leo2.Get(_leoEntities[i]).Value = 3;
            _leo3.Get(_leoEntities[i]).Value = 4;
        }
    }
    [GlobalCleanup] public void Cleanup() { _defaultQuery?.Dispose(); _default?.Dispose(); }
    [Benchmark(Baseline = true), InvocationCount(1)] public int DeltaECS_Movement4Components() { var state = new Movement4State { A = _delta0Binding, B = _delta1Binding, C = _delta2Binding, D = _delta3Binding }; _delta.Query(in _deltaQuery, ref state, static (ref Movement4State current, ref QueryChunkCursor cursor) => { var a = cursor.Get<DistinctDelta0>(current.A); var b = cursor.Get<DistinctDelta1>(current.B); var c = cursor.Get<DistinctDelta2>(current.C); var d = cursor.Get<DistinctDelta3>(current.D); while (cursor.MoveNext()) { ref var rowA = ref a[cursor]; ref var rowB = ref b[cursor]; ref var rowC = ref c[cursor]; ref readonly var rowD = ref d[cursor]; var updatedA = rowA.Value + rowD.Value; var updatedB = rowB.Value + rowD.Value; rowA.Value = updatedA; rowB.Value = updatedB; rowC.Value = (updatedA + updatedB) / 2; current.Sum += rowA.Value + rowB.Value + rowC.Value + rowD.Value; } }); return state.Sum; }
    [Benchmark] public int Arch_Movement4Components() { var s = 0; _arch.Query(_archQuery, (ref DistinctArch0 a, ref DistinctArch1 b, ref DistinctArch2 c, ref DistinctArch3 d) => { var updatedA = a.Value + d.Value; var updatedB = b.Value + d.Value; a.Value = updatedA; b.Value = updatedB; c.Value = (updatedA + updatedB) / 2; s += a.Value + b.Value + c.Value + d.Value; }); return s; }
    [Benchmark] public int FrifloEngineECS_Movement4Components() { var s = 0; _frifloQuery.ForEachEntity((ref DistinctFriflo0 a, ref DistinctFriflo1 b, ref DistinctFriflo2 c, ref DistinctFriflo3 d, FrifloEntity _) => { var updatedA = a.Value + d.Value; var updatedB = b.Value + d.Value; a.Value = updatedA; b.Value = updatedB; c.Value = (updatedA + updatedB) / 2; s += a.Value + b.Value + c.Value + d.Value; }); return s; }
    [Benchmark] public int DefaultEcs_Movement4Components() { var s = 0; var entities = _defaultQuery.GetEntities(); for (var i = entities.Length - 1; i >= 0; i--) { ref var a = ref entities[i].Get<DistinctDefault0>(); ref var b = ref entities[i].Get<DistinctDefault1>(); ref var c = ref entities[i].Get<DistinctDefault2>(); var d = entities[i].Get<DistinctDefault3>(); var updatedA = a.Value + d.Value; var updatedB = b.Value + d.Value; a.Value = updatedA; b.Value = updatedB; c.Value = (updatedA + updatedB) / 2; s += a.Value + b.Value + c.Value + d.Value; } return s; }
    [Benchmark] public int LeoEcsLite_Movement4Components() { var s = 0; foreach (var e in _leoQuery) { ref var a = ref _leo0.Get(e); ref var b = ref _leo1.Get(e); ref var c = ref _leo2.Get(e); var d = _leo3.Get(e); var updatedA = a.Value + d.Value; var updatedB = b.Value + d.Value; a.Value = updatedA; b.Value = updatedB; c.Value = (updatedA + updatedB) / 2; s += a.Value + b.Value + c.Value + d.Value; } return s; }
    private struct Movement4State
    {
        public WriteRequest<DistinctDelta0> A;
        public WriteRequest<DistinctDelta1> B;
        public WriteRequest<DistinctDelta2> C;
        public ReadRequest<DistinctDelta3> D;
        public int Sum;
    }
    private static void SetDefault(DefaultEcs.Entity e) { e.Set(new DistinctDefault0 { Value = 1 }); e.Set(new DistinctDefault1 { Value = 2 }); e.Set(new DistinctDefault2 { Value = 3 }); e.Set(new DistinctDefault3 { Value = 4 }); }
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BenchmarkCategory("Iteration.WideArchetypeNarrowQuery")]
public class ComparativeWideArchetypeNarrowQueryBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private DeltaWorld _delta = null!; private Query _deltaQuery; private ComponentId[] _deltaIds = null!;
    private ReadRequest<WideDelta0> _delta0Binding; private ReadRequest<WideDelta7> _delta7Binding;
    private Arch.Core.World _arch = null!; private ArchComponentType[] _archTypes = null!; private Arch.Core.QueryDescription _archQuery;
    private EntityStore _friflo = null!; private ArchetypeQuery<WideFriflo0, WideFriflo7> _frifloQuery = null!;
    private DefaultWorld _default = null!; private DefaultEcs.Entity[] _defaultEntities = null!; private DefaultEcs.EntitySet _defaultQuery = null!;
    private EcsWorld _leo = null!; private EcsPool<WideLeo0> _leo0 = null!; private EcsPool<WideLeo7> _leo7 = null!; private int[] _leoEntities = null!; private EcsFilter _leoQuery = null!;
    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry(); _deltaIds = new[] { layouts.Register<WideDelta0>(new SchemaId(203_000)), layouts.Register<WideDelta1>(new SchemaId(203_001)), layouts.Register<WideDelta2>(new SchemaId(203_002)), layouts.Register<WideDelta3>(new SchemaId(203_003)), layouts.Register<WideDelta4>(new SchemaId(203_004)), layouts.Register<WideDelta5>(new SchemaId(203_005)), layouts.Register<WideDelta6>(new SchemaId(203_006)), layouts.Register<WideDelta7>(new SchemaId(203_007)) }; _delta = new DeltaWorld(layouts, initialEntityCapacity: Amount); var de = new DeltaEntity[Amount]; _delta.CreateBatch(_deltaIds, de); for (var i = 0; i < Amount; i++) { _delta.SetComponent(de[i], _deltaIds[0], new WideDelta0 { Value = 1 }); _delta.SetComponent(de[i], _deltaIds[7], new WideDelta7 { Value = 8 }); }
        var d = QuerySpec.ForComponents(_deltaIds[0], _deltaIds[7]); _deltaQuery = _delta.CreateQuery(in d); _delta0Binding = _deltaQuery.Access<WideDelta0>(_deltaIds[0], AccessMode.Read); _delta7Binding = _deltaQuery.Access<WideDelta7>(_deltaIds[7], AccessMode.Read);
        _arch = Arch.Core.World.Create(); _archTypes = new ArchComponentType[] { typeof(WideArch0), typeof(WideArch1), typeof(WideArch2), typeof(WideArch3), typeof(WideArch4), typeof(WideArch5), typeof(WideArch6), typeof(WideArch7) }; _arch.Reserve(_archTypes, Amount); _archQuery = new Arch.Core.QueryDescription { All = new ArchComponentType[] { _archTypes[0], _archTypes[7] } }; for (var i = 0; i < Amount; i++) { var e = _arch.Create(_archTypes); _arch.Set(e, new WideArch0 { Value = 1 }); _arch.Set(e, new WideArch7 { Value = 8 }); }
        _friflo = new EntityStore(); for (var i = 0; i < Amount; i++) _friflo.CreateEntity(new WideFriflo0 { Value = 1 }, new WideFriflo1(), new WideFriflo2(), new WideFriflo3(), new WideFriflo4(), new WideFriflo5(), new WideFriflo6(), new WideFriflo7 { Value = 8 }); _frifloQuery = _friflo.Query<WideFriflo0, WideFriflo7>();
        _default = new DefaultWorld(); _defaultEntities = new DefaultEcs.Entity[Amount]; for (var i = 0; i < Amount; i++) { var e = _defaultEntities[i] = _default.CreateEntity(); e.Set(new WideDefault0 { Value = 1 }); e.Set(new WideDefault7 { Value = 8 }); e.Set<WideDefault1>(); e.Set<WideDefault2>(); e.Set<WideDefault3>(); e.Set<WideDefault4>(); e.Set<WideDefault5>(); e.Set<WideDefault6>(); }
        _defaultQuery = _default.GetEntities().With<WideDefault0>().With<WideDefault7>().AsSet();
        _leo = new EcsWorld(); _leo0 = _leo.GetPool<WideLeo0>(); _leo7 = _leo.GetPool<WideLeo7>(); _leoEntities = new int[Amount]; var p1 = _leo.GetPool<WideLeo1>(); var p2 = _leo.GetPool<WideLeo2>(); var p3 = _leo.GetPool<WideLeo3>(); var p4 = _leo.GetPool<WideLeo4>(); var p5 = _leo.GetPool<WideLeo5>(); var p6 = _leo.GetPool<WideLeo6>(); for (var i = 0; i < Amount; i++) { var e = _leoEntities[i] = _leo.NewEntity(); _leo0.Add(e).Value = 1; _leo7.Add(e).Value = 8; p1.Add(e); p2.Add(e); p3.Add(e); p4.Add(e); p5.Add(e); p6.Add(e); }
        _leoQuery = _leo.Filter<WideLeo0>().Inc<WideLeo7>().End();
    }
    [GlobalCleanup] public void Cleanup() { _defaultQuery?.Dispose(); _default?.Dispose(); }
    [Benchmark(Baseline = true)] public int DeltaECS_WideArchetypeNarrowQuery() { var state = new WideState { A = _delta0Binding, Z = _delta7Binding }; _delta.Query(in _deltaQuery, ref state, static (ref WideState current, ref QueryChunkCursor cursor) => { var a = cursor.Get(current.A); var z = cursor.Get(current.Z); while (cursor.MoveNext()) current.Sum += a[cursor].Value + z[cursor].Value; }); return Check(state.Sum, Amount * 9); }
    [Benchmark] public int Arch_WideArchetypeNarrowQuery() { var s = 0; _arch.Query(_archQuery, (ref WideArch0 a, ref WideArch7 z) => s += a.Value + z.Value); return Check(s, Amount * 9); }
    [Benchmark] public int FrifloEngineECS_WideArchetypeNarrowQuery() { var s = 0; _frifloQuery.ForEachEntity((ref WideFriflo0 a, ref WideFriflo7 z, FrifloEntity _) => s += a.Value + z.Value); return Check(s, Amount * 9); }
    [Benchmark] public int DefaultEcs_WideArchetypeNarrowQuery() { var s = 0; var entities = _defaultQuery.GetEntities(); for (var i = entities.Length - 1; i >= 0; i--) s += entities[i].Get<WideDefault0>().Value + entities[i].Get<WideDefault7>().Value; return Check(s, Amount * 9); }
    [Benchmark] public int LeoEcsLite_WideArchetypeNarrowQuery() { var s = 0; foreach (var e in _leoQuery) { s += _leo0.Get(e).Value + _leo7.Get(e).Value; } return Check(s, Amount * 9); }
    private struct WideState
    {
        public ReadRequest<WideDelta0> A;
        public ReadRequest<WideDelta7> Z;
        public int Sum;
    }
    private static int Check(int actual, int expected) => actual == expected ? actual : throw new InvalidOperationException($"wide checksum mismatch: {actual} != {expected}");
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ComparativeSparseQueryBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private DeltaWorld _delta = null!; private Query _deltaQuery; private ComponentId _deltaA, _deltaB, _deltaC; private DeltaEntity[] _deltaEntities = null!;
    private ReadRequest<SparseDeltaA> _deltaABinding; private ReadRequest<SparseDeltaB> _deltaBBinding;
    private Arch.Core.World _arch = null!; private ArchComponentType[] _archMatchTypes = null!; private ArchComponentType[] _archNonMatchTypes = null!; private ArchComponentType _archCType; private Arch.Core.QueryDescription _archQuery;
    private EntityStore _friflo = null!; private ArchetypeQuery<SparseFrifloA, SparseFrifloB> _frifloQuery = null!;
    private DefaultWorld _default = null!; private DefaultEcs.EntitySet _defaultQuery = null!;
    private EcsWorld _leo = null!; private EcsPool<SparseLeoA> _leoA = null!; private EcsPool<SparseLeoB> _leoB = null!; private EcsFilter _leoQuery = null!; private int[] _leoEntities = null!;
    private int ExpectedMatches => (Amount + ComparativeBenchmarkParameters.SparseMatchStride - 1) / ComparativeBenchmarkParameters.SparseMatchStride;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry(); _deltaA = layouts.Register<SparseDeltaA>(new SchemaId(204_000)); _deltaB = layouts.Register<SparseDeltaB>(new SchemaId(204_001)); _deltaC = layouts.Register<SparseDeltaC>(new SchemaId(204_002)); var n0 = layouts.Register<SparseDeltaNoise0>(new SchemaId(204_003)); var n1 = layouts.Register<SparseDeltaNoise1>(new SchemaId(204_004)); var n2 = layouts.Register<SparseDeltaNoise2>(new SchemaId(204_005)); var n3 = layouts.Register<SparseDeltaNoise3>(new SchemaId(204_006)); _delta = new DeltaWorld(layouts, initialEntityCapacity: Amount); _deltaEntities = new DeltaEntity[Amount]; for (var i = 0; i < Amount; i++) { var ids = i % ComparativeBenchmarkParameters.SparseMatchStride == 0 ? new[] { _deltaA, _deltaB, n0, n1, n2, n3 } : new[] { _deltaA, _deltaB, _deltaC, n0, n1, n2, n3 }; _deltaEntities[i] = _delta.Create(ids); }
        var d = new QuerySpec(new[] { _deltaA, _deltaB }, Array.Empty<ComponentId>(), new[] { _deltaC }, Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>()); _deltaQuery = _delta.CreateQuery(in d); _deltaABinding = _deltaQuery.Access<SparseDeltaA>(_deltaA, AccessMode.Read); _deltaBBinding = _deltaQuery.Access<SparseDeltaB>(_deltaB, AccessMode.Read);
        _arch = Arch.Core.World.Create(); _archCType = typeof(SparseArchC); _archMatchTypes = new ArchComponentType[] { typeof(SparseArchA), typeof(SparseArchB), typeof(SparseArchNoise0), typeof(SparseArchNoise1), typeof(SparseArchNoise2), typeof(SparseArchNoise3) }; _archNonMatchTypes = new ArchComponentType[] { typeof(SparseArchA), typeof(SparseArchB), _archCType, typeof(SparseArchNoise0), typeof(SparseArchNoise1), typeof(SparseArchNoise2), typeof(SparseArchNoise3) }; _arch.Reserve(_archMatchTypes, Amount); _arch.Reserve(_archNonMatchTypes, Amount); _archQuery = new Arch.Core.QueryDescription { All = new ArchComponentType[] { _archMatchTypes[0], _archMatchTypes[1] }, None = new ArchComponentType[] { _archCType } }; for (var i = 0; i < Amount; i++) { var e = i % ComparativeBenchmarkParameters.SparseMatchStride == 0 ? _arch.Create(_archMatchTypes) : _arch.Create(_archNonMatchTypes); _ = e; }
        _friflo = new EntityStore(); for (var i = 0; i < Amount; i++) { var e = _friflo.CreateEntity(new SparseFrifloA(), new SparseFrifloB(), new SparseFrifloNoise0(), new SparseFrifloNoise1(), new SparseFrifloNoise2(), new SparseFrifloNoise3()); if (i % ComparativeBenchmarkParameters.SparseMatchStride != 0) e.AddComponent(new SparseFrifloC()); }
        _frifloQuery = CreateFrifloQuery();
        _default = new DefaultWorld(); for (var i = 0; i < Amount; i++) { var e = _default.CreateEntity(); e.Set<SparseDefaultA>(); e.Set<SparseDefaultB>(); e.Set<SparseDefaultNoise0>(); e.Set<SparseDefaultNoise1>(); e.Set<SparseDefaultNoise2>(); e.Set<SparseDefaultNoise3>(); if (i % ComparativeBenchmarkParameters.SparseMatchStride != 0) e.Set<SparseDefaultC>(); }
        _defaultQuery = CreateDefaultQuery();
        _leo = new EcsWorld(); _leoA = _leo.GetPool<SparseLeoA>(); _leoB = _leo.GetPool<SparseLeoB>(); var c = _leo.GetPool<SparseLeoC>(); var n0l = _leo.GetPool<SparseLeoNoise0>(); var n1l = _leo.GetPool<SparseLeoNoise1>(); var n2l = _leo.GetPool<SparseLeoNoise2>(); var n3l = _leo.GetPool<SparseLeoNoise3>(); _leoEntities = new int[Amount]; for (var i = 0; i < Amount; i++) { var e = _leoEntities[i] = _leo.NewEntity(); _leoA.Add(e); _leoB.Add(e); n0l.Add(e); n1l.Add(e); n2l.Add(e); n3l.Add(e); if (i % ComparativeBenchmarkParameters.SparseMatchStride != 0) c.Add(e); }
        _leoQuery = _leo.Filter<SparseLeoA>().Inc<SparseLeoB>().Exc<SparseLeoC>().End();
    }
    [GlobalCleanup] public void Cleanup() { _defaultQuery?.Dispose(); _default?.Dispose(); }
    [Benchmark(Baseline = true), BenchmarkCategory("Iteration.SparseWorldQueryPlan")] public int DeltaECS_SparseWorldQueryPlan() => DeltaQuery(_deltaQuery, _deltaABinding, _deltaBBinding);
    [Benchmark, BenchmarkCategory("Iteration.SparseWorldQueryPlan")] public int Arch_SparseWorldQueryPlan() => ArchQuery(_archQuery);
    [Benchmark, BenchmarkCategory("Iteration.SparseWorldQueryPlan")] public int FrifloEngineECS_SparseWorldQueryPlan() => FrifloQuery(_frifloQuery);
    [Benchmark, BenchmarkCategory("Iteration.SparseWorldQueryPlan")] public int DefaultEcs_SparseWorldQueryPlan() => DefaultQuery(_defaultQuery.GetEntities());
    [Benchmark, BenchmarkCategory("Iteration.SparseWorldQueryPlan")] public int LeoEcsLite_SparseWorldQueryPlan() => LeoQuery(_leoQuery);
    [Benchmark(Baseline = true), InvocationCount(1), BenchmarkCategory("Iteration.QueryPlanConstruction")] public int DeltaECS_QueryPlanConstruction() { var d = new QuerySpec(new[] { _deltaA, _deltaB }, Array.Empty<ComponentId>(), new[] { _deltaC }, Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>()); var query = _delta.CreateQuery(in d); var a = query.Access<SparseDeltaA>(_deltaA, AccessMode.Read); var b = query.Access<SparseDeltaB>(_deltaB, AccessMode.Read); return DeltaQuery(query, a, b); }
    [Benchmark, InvocationCount(1), BenchmarkCategory("Iteration.QueryPlanConstruction")] public int Arch_QueryPlanConstruction() { var d = new Arch.Core.QueryDescription { All = new ArchComponentType[] { _archMatchTypes[0], _archMatchTypes[1] }, None = new ArchComponentType[] { _archCType } }; return ArchQuery(d); }
    [Benchmark, InvocationCount(1), BenchmarkCategory("Iteration.QueryPlanConstruction")] public int FrifloEngineECS_QueryPlanConstruction() => FrifloQuery(CreateFrifloQuery());
    [Benchmark, InvocationCount(1), BenchmarkCategory("Iteration.QueryPlanConstruction")] public int DefaultEcs_QueryPlanConstruction() { using var q = CreateDefaultQuery(); return DefaultQuery(q.GetEntities()); }
    [Benchmark, InvocationCount(1), BenchmarkCategory("Iteration.QueryPlanConstruction")] public int LeoEcsLite_QueryPlanConstruction() => LeoQuery(_leo.Filter<SparseLeoA>().Inc<SparseLeoB>().Exc<SparseLeoC>().End());

    private int DeltaQuery(Query query, ReadRequest<SparseDeltaA> a, ReadRequest<SparseDeltaB> b) { var state = new SparseDeltaState { A = a, B = b }; _delta.Query(in query, ref state, static (ref SparseDeltaState current, ref QueryChunkCursor cursor) => { var aRow = cursor.Get(current.A); var bRow = cursor.Get(current.B); while (cursor.MoveNext()) { _ = aRow[cursor]; _ = bRow[cursor]; current.Count++; } }); return Check(state.Count); }
    private int ArchQuery(Arch.Core.QueryDescription query) { var count = 0; _arch.Query(query, (ref SparseArchA _, ref SparseArchB _) => count++); return Check(count); }
    private int FrifloQuery(ArchetypeQuery<SparseFrifloA, SparseFrifloB> query) { var count = 0; query.ForEachEntity((ref SparseFrifloA _, ref SparseFrifloB _, FrifloEntity _) => count++); return Check(count); }
    private int DefaultQuery(ReadOnlySpan<DefaultEcs.Entity> entities) { var count = 0; for (var i = entities.Length - 1; i >= 0; i--) count++; return Check(count); }
    private int LeoQuery(EcsFilter query) { var count = 0; foreach (var _ in query) count++; return Check(count); }
    private int Check(int actual) => actual == ExpectedMatches ? actual : throw new InvalidOperationException($"sparse match mismatch: {actual} != {ExpectedMatches}");
    private ArchetypeQuery<SparseFrifloA, SparseFrifloB> CreateFrifloQuery() { var f = new QueryFilter(); f.WithoutAllComponents(ComponentTypes.Get<SparseFrifloC>()); return _friflo.Query<SparseFrifloA, SparseFrifloB>(f); }
    private DefaultEcs.EntitySet CreateDefaultQuery() => _default.GetEntities().With<SparseDefaultA>().With<SparseDefaultB>().Without<SparseDefaultC>().AsSet();
    private struct SparseDeltaState
    {
        public ReadRequest<SparseDeltaA> A;
        public ReadRequest<SparseDeltaB> B;
        public int Count;
    }
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
internal struct DistinctDelta0 { public int Value; }
internal struct DistinctDelta1 { public int Value; }
internal struct DistinctDelta2 { public int Value; }
internal struct DistinctDelta3 { public int Value; }
internal struct DistinctArch0 { public int Value; }
internal struct DistinctArch1 { public int Value; }
internal struct DistinctArch2 { public int Value; }
internal struct DistinctArch3 { public int Value; }
internal struct DistinctFriflo0 : IComponent { public int Value; }
internal struct DistinctFriflo1 : IComponent { public int Value; }
internal struct DistinctFriflo2 : IComponent { public int Value; }
internal struct DistinctFriflo3 : IComponent { public int Value; }
internal struct DistinctDefault0 { public int Value; }
internal struct DistinctDefault1 { public int Value; }
internal struct DistinctDefault2 { public int Value; }
internal struct DistinctDefault3 { public int Value; }
internal struct DistinctLeo0 { public int Value; }
internal struct DistinctLeo1 { public int Value; }
internal struct DistinctLeo2 { public int Value; }
internal struct DistinctLeo3 { public int Value; }
internal struct WideDelta0 { public int Value; }
internal struct WideDelta1 { }
internal struct WideDelta2 { }
internal struct WideDelta3 { }
internal struct WideDelta4 { }
internal struct WideDelta5 { }
internal struct WideDelta6 { }
internal struct WideDelta7 { public int Value; }
internal struct WideArch0 { public int Value; }
internal struct WideArch1 { }
internal struct WideArch2 { }
internal struct WideArch3 { }
internal struct WideArch4 { }
internal struct WideArch5 { }
internal struct WideArch6 { }
internal struct WideArch7 { public int Value; }
internal struct WideFriflo0 : IComponent { public int Value; }
internal struct WideFriflo1 : IComponent { }
internal struct WideFriflo2 : IComponent { }
internal struct WideFriflo3 : IComponent { }
internal struct WideFriflo4 : IComponent { }
internal struct WideFriflo5 : IComponent { }
internal struct WideFriflo6 : IComponent { }
internal struct WideFriflo7 : IComponent { public int Value; }
internal struct WideDefault0 { public int Value; }
internal struct WideDefault1 { }
internal struct WideDefault2 { }
internal struct WideDefault3 { }
internal struct WideDefault4 { }
internal struct WideDefault5 { }
internal struct WideDefault6 { }
internal struct WideDefault7 { public int Value; }
internal struct WideLeo0 { public int Value; }
internal struct WideLeo1 { }
internal struct WideLeo2 { }
internal struct WideLeo3 { }
internal struct WideLeo4 { }
internal struct WideLeo5 { }
internal struct WideLeo6 { }
internal struct WideLeo7 { public int Value; }
internal struct SparseDeltaA { }
internal struct SparseDeltaB { }
internal struct SparseDeltaC { }
internal struct SparseDeltaNoise0 { }
internal struct SparseDeltaNoise1 { }
internal struct SparseDeltaNoise2 { }
internal struct SparseDeltaNoise3 { }
internal struct SparseArchA { }
internal struct SparseArchB { }
internal struct SparseArchC { }
internal struct SparseArchNoise0 { }
internal struct SparseArchNoise1 { }
internal struct SparseArchNoise2 { }
internal struct SparseArchNoise3 { }
internal struct SparseFrifloA : IComponent { }
internal struct SparseFrifloB : IComponent { }
internal struct SparseFrifloC : IComponent { }
internal struct SparseFrifloNoise0 : IComponent { }
internal struct SparseFrifloNoise1 : IComponent { }
internal struct SparseFrifloNoise2 : IComponent { }
internal struct SparseFrifloNoise3 : IComponent { }
internal struct SparseDefaultA { }
internal struct SparseDefaultB { }
internal struct SparseDefaultC { }
internal struct SparseDefaultNoise0 { }
internal struct SparseDefaultNoise1 { }
internal struct SparseDefaultNoise2 { }
internal struct SparseDefaultNoise3 { }
internal struct SparseLeoA { }
internal struct SparseLeoB { }
internal struct SparseLeoC { }
internal struct SparseLeoNoise0 { }
internal struct SparseLeoNoise1 { }
internal struct SparseLeoNoise2 { }
internal struct SparseLeoNoise3 { }
