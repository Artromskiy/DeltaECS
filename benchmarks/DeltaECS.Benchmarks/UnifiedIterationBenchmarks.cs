using Arch.Core;
using Arch.Core.Utils;
using BenchmarkDotNet.Attributes;
using DefaultEcs;
using Delta.ECS;
using Friflo.Engine.ECS;
using Leopotam.EcsLite;
using System.Runtime.CompilerServices;
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
// All backends iterate the same one-component int fixture and use ApplyDense;
// only the traversal/callback mechanism differs.
public class ComparativeDenseIterationBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private DeltaWorld _delta = null!;
    private Query _deltaQuery;
    private ComponentId _deltaValue;
    private Arch.Core.World _arch = null!;
    private ArchComponentType[] _archTypes = null!;
    private Arch.Core.QueryDescription _archQuery;
    private EntityStore _friflo = null!;
    private ArchetypeQuery<DenseValue> _frifloQuery = null!;
    private DefaultWorld _default = null!;
    private DefaultEcs.Entity[] _defaultEntities = null!;
    private DefaultEcs.EntitySet _defaultQuery = null!;
    private EcsWorld _leo = null!;
    private EcsPool<DenseValue> _leoPool = null!;
    private int[] _leoEntities = null!;
    private EcsFilter _leoQuery = null!;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaValue = layouts.Register(typeof(DenseValue), new SchemaId(200_000));
        _delta = new DeltaWorld(layouts, initialEntityCapacity: Amount);
        var deltaEntities = new DeltaEntity[Amount];
        _delta.Create(new[] { _deltaValue }, deltaEntities);
        for (var i = 0; i < Amount; i++)
            _delta.Set(deltaEntities[i], _deltaValue, new DenseValue { Value = i + 1 });
        var spec = QuerySpec.WhereAll(_deltaValue);
        _deltaQuery = _delta.CreateQuery(in spec);

        _arch = Arch.Core.World.Create();
        _archTypes = new ArchComponentType[] { typeof(DenseValue) };
        _arch.Reserve(_archTypes, Amount);
        _archQuery = new Arch.Core.QueryDescription { All = _archTypes };
        for (var i = 0; i < Amount; i++)
        {
            var entity = _arch.Create(_archTypes);
            _arch.Set(entity, new DenseValue { Value = i + 1 });
        }

        _friflo = new EntityStore();
        for (var i = 0; i < Amount; i++) _friflo.CreateEntity(new DenseValue { Value = i + 1 });
        _frifloQuery = _friflo.Query<DenseValue>();

        _default = new DefaultWorld();
        _defaultEntities = new DefaultEcs.Entity[Amount];
        for (var i = 0; i < Amount; i++)
        {
            _defaultEntities[i] = _default.CreateEntity();
            _defaultEntities[i].Set(new DenseValue { Value = i + 1 });
        }
        _defaultQuery = _default.GetEntities().With<DenseValue>().AsSet();

        _leo = new EcsWorld();
        _leoPool = _leo.GetPool<DenseValue>();
        _leoEntities = new int[Amount];
        for (var i = 0; i < Amount; i++)
        {
            _leoEntities[i] = _leo.NewEntity();
            _leoPool.Add(_leoEntities[i]).Value = i + 1;
        }
        _leoQuery = _leo.Filter<DenseValue>().End();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _delta?.Dispose();
        _arch?.Dispose();
        _defaultQuery?.Dispose();
        _default?.Dispose();
        (_leo as IDisposable)?.Dispose();
    }

    [Benchmark(Baseline = true)] public long DeltaECS_Dense() { long sum = 0; _delta.ForEach(in _deltaQuery, ref sum, static (ref long checksum, ref readonly DenseValue value) => ApplyDense(in value, ref checksum)); return Checksum(sum, (long)Amount * (Amount + 1) / 2, "dense"); }
    [Benchmark] public long Arch_Dense() { long sum = 0; _arch.Query(_archQuery, (ref DenseValue value) => ApplyDense(in value, ref sum)); return Checksum(sum, (long)Amount * (Amount + 1) / 2, "dense"); }
    [Benchmark] public long FrifloEngineECS_Dense() { long sum = 0; _frifloQuery.ForEachEntity((ref DenseValue value, FrifloEntity _) => ApplyDense(in value, ref sum)); return Checksum(sum, (long)Amount * (Amount + 1) / 2, "dense"); }
    [Benchmark] public long DefaultEcs_Dense() { long sum = 0; var entities = _defaultQuery.GetEntities(); for (var i = entities.Length - 1; i >= 0; i--) { var value = entities[i].Get<DenseValue>(); ApplyDense(in value, ref sum); } return Checksum(sum, (long)Amount * (Amount + 1) / 2, "dense"); }
    [Benchmark] public long LeoEcsLite_Dense() { long sum = 0; foreach (var entity in _leoQuery) { var value = _leoPool.Get(entity); ApplyDense(in value, ref sum); } return Checksum(sum, (long)Amount * (Amount + 1) / 2, "dense"); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ApplyDense(ref readonly DenseValue value, ref long checksum) => checksum += value.Value;

    internal static long Checksum(long actual, long expected, string name) => actual == expected ? actual : throw new InvalidOperationException($"{name} checksum mismatch: {actual} != {expected}");

}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BenchmarkCategory("Iteration.Movement2Components")]
// All backends use the same two float components, values (1, 2) and (3, 4),
// and ApplyMovement2. State is initialized once in GlobalSetup; repeated
// invocations intentionally preserve the same traversal and arithmetic path.
public class ComparativeMovement2ComponentsBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private DeltaWorld _delta = null!;
    private Query _deltaQuery;
    private ComponentId _deltaPosition, _deltaVelocity;
    private DeltaEntity[] _deltaEntities = null!;
    private Arch.Core.World _arch = null!;
    private Arch.Core.QueryDescription _archQuery;
    private ArchComponentType[] _archTypes = null!;
    private Arch.Core.Entity[] _archEntities = null!;
    private EntityStore _friflo = null!;
    private ArchetypeQuery<Movement2Position, Movement2Velocity> _frifloQuery = null!;
    private FrifloEntity[] _frifloEntities = null!;
    private DefaultWorld _default = null!;
    private DefaultEcs.Entity[] _defaultEntities = null!;
    private DefaultEcs.EntitySet _defaultQuery = null!;
    private EcsWorld _leo = null!;
    private EcsPool<Movement2Position> _leoPosition = null!;
    private EcsPool<Movement2Velocity> _leoVelocity = null!;
    private int[] _leoEntities = null!;
    private EcsFilter _leoQuery = null!;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaPosition = layouts.Register(typeof(Movement2Position), new SchemaId(201_000));
        _deltaVelocity = layouts.Register(typeof(Movement2Velocity), new SchemaId(201_001));
        _delta = new DeltaWorld(layouts, initialEntityCapacity: Amount);
        _deltaEntities = new DeltaEntity[Amount];
        _delta.Create(new[] { _deltaPosition, _deltaVelocity }, _deltaEntities);
        for (var i = 0; i < Amount; i++)
        {
            _delta.Set(_deltaEntities[i], _deltaPosition, new Movement2Position { X = 1, Y = 2 });
            _delta.Set(_deltaEntities[i], _deltaVelocity, new Movement2Velocity { X = 3, Y = 4 });
        }
        var deltaDescription = QuerySpec.WhereAll(_deltaPosition, _deltaVelocity);
        _deltaQuery = _delta.CreateQuery(in deltaDescription);

        _arch = Arch.Core.World.Create();
        _archTypes = new ArchComponentType[] { typeof(Movement2Position), typeof(Movement2Velocity) };
        _arch.Reserve(_archTypes, Amount);
        _archQuery = new Arch.Core.QueryDescription { All = _archTypes };
        _archEntities = new Arch.Core.Entity[Amount];
        for (var i = 0; i < Amount; i++)
        {
            _archEntities[i] = _arch.Create(_archTypes);
            _arch.Set(_archEntities[i], new Movement2Position { X = 1, Y = 2 });
            _arch.Set(_archEntities[i], new Movement2Velocity { X = 3, Y = 4 });
        }

        _friflo = new EntityStore();
        _frifloEntities = new FrifloEntity[Amount];
        for (var i = 0; i < Amount; i++) _frifloEntities[i] = _friflo.CreateEntity(new Movement2Position { X = 1, Y = 2 }, new Movement2Velocity { X = 3, Y = 4 });
        _frifloQuery = _friflo.Query<Movement2Position, Movement2Velocity>();

        _default = new DefaultWorld();
        _defaultEntities = new DefaultEcs.Entity[Amount];
        for (var i = 0; i < Amount; i++)
        {
            _defaultEntities[i] = _default.CreateEntity();
            _defaultEntities[i].Set(new Movement2Position { X = 1, Y = 2 });
            _defaultEntities[i].Set(new Movement2Velocity { X = 3, Y = 4 });
        }
        _defaultQuery = _default.GetEntities().With<Movement2Position>().With<Movement2Velocity>().AsSet();

        _leo = new EcsWorld();
        _leoPosition = _leo.GetPool<Movement2Position>();
        _leoVelocity = _leo.GetPool<Movement2Velocity>();
        _leoEntities = new int[Amount];
        for (var i = 0; i < Amount; i++)
        {
            var entity = _leoEntities[i] = _leo.NewEntity();
            ref var position = ref _leoPosition.Add(entity);
            position.X = 1;
            position.Y = 2;
            ref var velocity = ref _leoVelocity.Add(entity);
            velocity.X = 3;
            velocity.Y = 4;
        }
        _leoQuery = _leo.Filter<Movement2Position>().Inc<Movement2Velocity>().End();
    }

    public void ResetMovement()
    {
        ResetDeltaMovement();
        ResetArchMovement();
        ResetFrifloMovement();
        ResetDefaultMovement();
        ResetLeoMovement();
    }

    public void ResetDeltaMovement()
    {
        for (var i = 0; i < Amount; i++)
        {
            _delta.Set(_deltaEntities[i], _deltaPosition, new Movement2Position { X = 1, Y = 2 });
            _delta.Set(_deltaEntities[i], _deltaVelocity, new Movement2Velocity { X = 3, Y = 4 });
        }
    }

    public void ResetArchMovement()
    {
        for (var i = 0; i < Amount; i++)
        {
            _arch.Set(_archEntities[i], new Movement2Position { X = 1, Y = 2 });
            _arch.Set(_archEntities[i], new Movement2Velocity { X = 3, Y = 4 });
        }
    }

    public void ResetFrifloMovement()
    {
        for (var i = 0; i < Amount; i++)
        {
            _frifloEntities[i].GetComponent<Movement2Position>().X = 1;
            _frifloEntities[i].GetComponent<Movement2Position>().Y = 2;
            _frifloEntities[i].GetComponent<Movement2Velocity>().X = 3;
            _frifloEntities[i].GetComponent<Movement2Velocity>().Y = 4;
        }
    }

    public void ResetDefaultMovement()
    {
        for (var i = 0; i < Amount; i++)
        {
            _defaultEntities[i].Set(new Movement2Position { X = 1, Y = 2 });
            _defaultEntities[i].Set(new Movement2Velocity { X = 3, Y = 4 });
        }
    }

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

    [GlobalCleanup]
    public void Cleanup()
    {
        _delta?.Dispose();
        _arch?.Dispose();
        _defaultQuery?.Dispose();
        _default?.Dispose();
        (_leo as IDisposable)?.Dispose();
    }

    [Benchmark(Baseline = true)] public double DeltaECS_Movement2Components() { double sum = 0; _delta.ForEach(in _deltaQuery, ref sum, static (ref double checksum, ref Movement2Position position, ref readonly Movement2Velocity velocity) => ApplyMovement2(ref position, in velocity, ref checksum)); return sum; }
    [Benchmark] public double Arch_Movement2Components() { double sum = 0; _arch.Query(_archQuery, (ref Movement2Position position, ref Movement2Velocity velocity) => ApplyMovement2(ref position, in velocity, ref sum)); return sum; }
    [Benchmark] public double FrifloEngineECS_Movement2Components() { double sum = 0; _frifloQuery.ForEachEntity((ref Movement2Position position, ref Movement2Velocity velocity, FrifloEntity _) => ApplyMovement2(ref position, in velocity, ref sum)); return sum; }
    [Benchmark] public double DefaultEcs_Movement2Components() { double sum = 0; var entities = _defaultQuery.GetEntities(); for (var i = entities.Length - 1; i >= 0; i--) { ref var position = ref entities[i].Get<Movement2Position>(); var velocity = entities[i].Get<Movement2Velocity>(); ApplyMovement2(ref position, in velocity, ref sum); } return sum; }
    [Benchmark] public double LeoEcsLite_Movement2Components() { double sum = 0; foreach (var entity in _leoQuery) { ref var position = ref _leoPosition.Get(entity); var velocity = _leoVelocity.Get(entity); ApplyMovement2(ref position, in velocity, ref sum); } return sum; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ApplyMovement2(ref Movement2Position position, ref readonly Movement2Velocity velocity, ref double checksum)
    {
        position.X += velocity.X / 60f;
        position.Y += velocity.Y / 60f;
        checksum += position.X + position.Y;
    }

}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BenchmarkCategory("Iteration.Movement4Components")]
// Every backend uses the same four int components, values (1, 2, 3, 4), and
// ApplyMovement4. State is initialized once in GlobalSetup; repeated
// invocations intentionally preserve the same traversal and arithmetic path.
public class ComparativeMovement4ComponentsBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private DeltaWorld _delta = null!; private Query _deltaQuery; private ComponentId[] _deltaIds = null!; private DeltaEntity[] _deltaEntities = null!;
    private Arch.Core.World _arch = null!; private ArchComponentType[] _archTypes = null!; private Arch.Core.QueryDescription _archQuery; private Arch.Core.Entity[] _archEntities = null!;
    private EntityStore _friflo = null!; private ArchetypeQuery<Movement4A, Movement4B, Movement4C, Movement4D> _frifloQuery = null!; private FrifloEntity[] _frifloEntities = null!;
    private DefaultWorld _default = null!; private DefaultEcs.Entity[] _defaultEntities = null!; private DefaultEcs.EntitySet _defaultQuery = null!;
    private EcsWorld _leo = null!; private EcsPool<Movement4A> _leo0 = null!; private EcsPool<Movement4B> _leo1 = null!; private EcsPool<Movement4C> _leo2 = null!; private EcsPool<Movement4D> _leo3 = null!; private int[] _leoEntities = null!; private EcsFilter _leoQuery = null!;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry(); _deltaIds = new[] { layouts.Register(typeof(Movement4A), new SchemaId(202_000)), layouts.Register(typeof(Movement4B), new SchemaId(202_001)), layouts.Register(typeof(Movement4C), new SchemaId(202_002)), layouts.Register(typeof(Movement4D), new SchemaId(202_003)) }; _delta = new DeltaWorld(layouts, initialEntityCapacity: Amount); _deltaEntities = new DeltaEntity[Amount]; _delta.Create(_deltaIds, _deltaEntities); for (var i = 0; i < Amount; i++) { _delta.Set(_deltaEntities[i], _deltaIds[0], new Movement4A { Value = 1 }); _delta.Set(_deltaEntities[i], _deltaIds[1], new Movement4B { Value = 2 }); _delta.Set(_deltaEntities[i], _deltaIds[2], new Movement4C { Value = 3 }); _delta.Set(_deltaEntities[i], _deltaIds[3], new Movement4D { Value = 4 }); }
        var d = QuerySpec.WhereAll(_deltaIds); _deltaQuery = _delta.CreateQuery(in d);
        _arch = Arch.Core.World.Create(); _archTypes = new ArchComponentType[] { typeof(Movement4A), typeof(Movement4B), typeof(Movement4C), typeof(Movement4D) }; _arch.Reserve(_archTypes, Amount); _archQuery = new Arch.Core.QueryDescription { All = _archTypes }; _archEntities = new Arch.Core.Entity[Amount]; for (var i = 0; i < Amount; i++) { _archEntities[i] = _arch.Create(_archTypes); _arch.Set(_archEntities[i], new Movement4A { Value = 1 }); _arch.Set(_archEntities[i], new Movement4B { Value = 2 }); _arch.Set(_archEntities[i], new Movement4C { Value = 3 }); _arch.Set(_archEntities[i], new Movement4D { Value = 4 }); }
        _friflo = new EntityStore(); _frifloEntities = new FrifloEntity[Amount]; for (var i = 0; i < Amount; i++) _frifloEntities[i] = _friflo.CreateEntity(new Movement4A { Value = 1 }, new Movement4B { Value = 2 }, new Movement4C { Value = 3 }, new Movement4D { Value = 4 }); _frifloQuery = _friflo.Query<Movement4A, Movement4B, Movement4C, Movement4D>();
        _default = new DefaultWorld(); _defaultEntities = new DefaultEcs.Entity[Amount]; for (var i = 0; i < Amount; i++) { _defaultEntities[i] = _default.CreateEntity(); SetDefault(_defaultEntities[i]); }
        _defaultQuery = _default.GetEntities().With<Movement4A>().With<Movement4B>().With<Movement4C>().With<Movement4D>().AsSet();
        _leo = new EcsWorld(); _leo0 = _leo.GetPool<Movement4A>(); _leo1 = _leo.GetPool<Movement4B>(); _leo2 = _leo.GetPool<Movement4C>(); _leo3 = _leo.GetPool<Movement4D>(); _leoEntities = new int[Amount]; for (var i = 0; i < Amount; i++) { var e = _leoEntities[i] = _leo.NewEntity(); _leo0.Add(e).Value = 1; _leo1.Add(e).Value = 2; _leo2.Add(e).Value = 3; _leo3.Add(e).Value = 4; }
        _leoQuery = _leo.Filter<Movement4A>().Inc<Movement4B>().Inc<Movement4C>().Inc<Movement4D>().End();
    }
    public void ResetMovement4()
    {
        ResetDeltaMovement4();
        ResetArchMovement4();
        ResetFrifloMovement4();
        ResetDefaultMovement4();
        ResetLeoMovement4();
    }

    public void ResetDeltaMovement4()
    {
        for (var i = 0; i < Amount; i++)
        {
            _delta.Set(_deltaEntities[i], _deltaIds[0], new Movement4A { Value = 1 });
            _delta.Set(_deltaEntities[i], _deltaIds[1], new Movement4B { Value = 2 });
            _delta.Set(_deltaEntities[i], _deltaIds[2], new Movement4C { Value = 3 });
            _delta.Set(_deltaEntities[i], _deltaIds[3], new Movement4D { Value = 4 });
        }
    }

    public void ResetArchMovement4()
    {
        for (var i = 0; i < Amount; i++)
        {
            _arch.Set(_archEntities[i], new Movement4A { Value = 1 });
            _arch.Set(_archEntities[i], new Movement4B { Value = 2 });
            _arch.Set(_archEntities[i], new Movement4C { Value = 3 });
            _arch.Set(_archEntities[i], new Movement4D { Value = 4 });
        }
    }

    public void ResetFrifloMovement4()
    {
        for (var i = 0; i < Amount; i++)
        {
            _frifloEntities[i].GetComponent<Movement4A>().Value = 1;
            _frifloEntities[i].GetComponent<Movement4B>().Value = 2;
            _frifloEntities[i].GetComponent<Movement4C>().Value = 3;
            _frifloEntities[i].GetComponent<Movement4D>().Value = 4;
        }
    }

    public void ResetDefaultMovement4()
    {
        for (var i = 0; i < Amount; i++)
        {
            _defaultEntities[i].Set(new Movement4A { Value = 1 });
            _defaultEntities[i].Set(new Movement4B { Value = 2 });
            _defaultEntities[i].Set(new Movement4C { Value = 3 });
            _defaultEntities[i].Set(new Movement4D { Value = 4 });
        }
    }

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

    [GlobalCleanup]
    public void Cleanup()
    {
        _delta?.Dispose();
        _arch?.Dispose();
        _defaultQuery?.Dispose();
        _default?.Dispose();
        (_leo as IDisposable)?.Dispose();
    }

    [Benchmark(Baseline = true)] public int DeltaECS_Movement4Components() { var sum = 0; _delta.ForEach(in _deltaQuery, ref sum, static (ref int checksum, ref Movement4A rowA, ref Movement4B rowB, ref Movement4C rowC, ref readonly Movement4D rowD) => ApplyMovement4(ref rowA, ref rowB, ref rowC, in rowD, ref checksum)); return sum; }
    [Benchmark] public int Arch_Movement4Components() { var sum = 0; _arch.Query(_archQuery, (ref Movement4A rowA, ref Movement4B rowB, ref Movement4C rowC, ref Movement4D rowD) => ApplyMovement4(ref rowA, ref rowB, ref rowC, in rowD, ref sum)); return sum; }
    [Benchmark] public int FrifloEngineECS_Movement4Components() { var sum = 0; _frifloQuery.ForEachEntity((ref Movement4A rowA, ref Movement4B rowB, ref Movement4C rowC, ref Movement4D rowD, FrifloEntity _) => ApplyMovement4(ref rowA, ref rowB, ref rowC, in rowD, ref sum)); return sum; }
    [Benchmark] public int DefaultEcs_Movement4Components() { var sum = 0; var entities = _defaultQuery.GetEntities(); for (var i = entities.Length - 1; i >= 0; i--) { ref var rowA = ref entities[i].Get<Movement4A>(); ref var rowB = ref entities[i].Get<Movement4B>(); ref var rowC = ref entities[i].Get<Movement4C>(); var rowD = entities[i].Get<Movement4D>(); ApplyMovement4(ref rowA, ref rowB, ref rowC, in rowD, ref sum); } return sum; }
    [Benchmark] public int LeoEcsLite_Movement4Components() { var sum = 0; foreach (var e in _leoQuery) { ref var rowA = ref _leo0.Get(e); ref var rowB = ref _leo1.Get(e); ref var rowC = ref _leo2.Get(e); var rowD = _leo3.Get(e); ApplyMovement4(ref rowA, ref rowB, ref rowC, in rowD, ref sum); } return sum; }

    private static void SetDefault(DefaultEcs.Entity e) { e.Set(new Movement4A { Value = 1 }); e.Set(new Movement4B { Value = 2 }); e.Set(new Movement4C { Value = 3 }); e.Set(new Movement4D { Value = 4 }); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ApplyMovement4(ref Movement4A rowA, ref Movement4B rowB, ref Movement4C rowC, ref readonly Movement4D rowD, ref int checksum)
    {
        var updatedA = rowA.Value + rowD.Value;
        var updatedB = rowB.Value + rowD.Value;
        rowA.Value = updatedA;
        rowB.Value = updatedB;
        rowC.Value = (updatedA + updatedB) / 2;
        checksum += rowA.Value + rowB.Value + rowC.Value + rowD.Value;
    }
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BenchmarkCategory("Iteration.WideArchetypeNarrowQuery")]
// All backends store the same eight-component archetype and query only Wide0
// and Wide7; ApplyWide owns the identical terminal checksum operation.
public class ComparativeWideArchetypeNarrowQueryBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private DeltaWorld _delta = null!; private Query _deltaQuery; private ComponentId[] _deltaIds = null!;
    private Arch.Core.World _arch = null!; private ArchComponentType[] _archTypes = null!; private Arch.Core.QueryDescription _archQuery;
    private EntityStore _friflo = null!; private ArchetypeQuery<Wide0, Wide7> _frifloQuery = null!;
    private DefaultWorld _default = null!; private DefaultEcs.Entity[] _defaultEntities = null!; private DefaultEcs.EntitySet _defaultQuery = null!;
    private EcsWorld _leo = null!; private EcsPool<Wide0> _leo0 = null!; private EcsPool<Wide7> _leo7 = null!; private int[] _leoEntities = null!; private EcsFilter _leoQuery = null!;
    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry(); _deltaIds = new[] { layouts.Register(typeof(Wide0), new SchemaId(203_000)), layouts.Register(typeof(Wide1), new SchemaId(203_001)), layouts.Register(typeof(Wide2), new SchemaId(203_002)), layouts.Register(typeof(Wide3), new SchemaId(203_003)), layouts.Register(typeof(Wide4), new SchemaId(203_004)), layouts.Register(typeof(Wide5), new SchemaId(203_005)), layouts.Register(typeof(Wide6), new SchemaId(203_006)), layouts.Register(typeof(Wide7), new SchemaId(203_007)) }; _delta = new DeltaWorld(layouts, initialEntityCapacity: Amount); var de = new DeltaEntity[Amount]; _delta.Create(_deltaIds, de); for (var i = 0; i < Amount; i++) { _delta.Set(de[i], _deltaIds[0], new Wide0 { Value = 1 }); _delta.Set(de[i], _deltaIds[7], new Wide7 { Value = 8 }); }
        var d = QuerySpec.WhereAll(_deltaIds[0], _deltaIds[7]); _deltaQuery = _delta.CreateQuery(in d);
        _arch = Arch.Core.World.Create(); _archTypes = new ArchComponentType[] { typeof(Wide0), typeof(Wide1), typeof(Wide2), typeof(Wide3), typeof(Wide4), typeof(Wide5), typeof(Wide6), typeof(Wide7) }; _arch.Reserve(_archTypes, Amount); _archQuery = new Arch.Core.QueryDescription { All = new ArchComponentType[] { _archTypes[0], _archTypes[7] } }; for (var i = 0; i < Amount; i++) { var e = _arch.Create(_archTypes); _arch.Set(e, new Wide0 { Value = 1 }); _arch.Set(e, new Wide7 { Value = 8 }); }
        _friflo = new EntityStore(); for (var i = 0; i < Amount; i++) _friflo.CreateEntity(new Wide0 { Value = 1 }, new Wide1(), new Wide2(), new Wide3(), new Wide4(), new Wide5(), new Wide6(), new Wide7 { Value = 8 }); _frifloQuery = _friflo.Query<Wide0, Wide7>();
        _default = new DefaultWorld(); _defaultEntities = new DefaultEcs.Entity[Amount]; for (var i = 0; i < Amount; i++) { var e = _defaultEntities[i] = _default.CreateEntity(); e.Set(new Wide0 { Value = 1 }); e.Set(new Wide7 { Value = 8 }); e.Set<Wide1>(); e.Set<Wide2>(); e.Set<Wide3>(); e.Set<Wide4>(); e.Set<Wide5>(); e.Set<Wide6>(); }
        _defaultQuery = _default.GetEntities().With<Wide0>().With<Wide7>().AsSet();
        _leo = new EcsWorld(); _leo0 = _leo.GetPool<Wide0>(); _leo7 = _leo.GetPool<Wide7>(); _leoEntities = new int[Amount]; var p1 = _leo.GetPool<Wide1>(); var p2 = _leo.GetPool<Wide2>(); var p3 = _leo.GetPool<Wide3>(); var p4 = _leo.GetPool<Wide4>(); var p5 = _leo.GetPool<Wide5>(); var p6 = _leo.GetPool<Wide6>(); for (var i = 0; i < Amount; i++) { var e = _leoEntities[i] = _leo.NewEntity(); _leo0.Add(e).Value = 1; _leo7.Add(e).Value = 8; p1.Add(e); p2.Add(e); p3.Add(e); p4.Add(e); p5.Add(e); p6.Add(e); }
        _leoQuery = _leo.Filter<Wide0>().Inc<Wide7>().End();
    }
    [GlobalCleanup]
    public void Cleanup()
    {
        _delta?.Dispose();
        _arch?.Dispose();
        _defaultQuery?.Dispose();
        _default?.Dispose();
        (_leo as IDisposable)?.Dispose();
    }

    [Benchmark(Baseline = true)] public int DeltaECS_WideArchetypeNarrowQuery() { var sum = 0; _delta.ForEach(in _deltaQuery, ref sum, static (ref int checksum, ref readonly Wide0 a, ref readonly Wide7 z) => ApplyWide(in a, in z, ref checksum)); return Check(sum, Amount * 9); }
    [Benchmark] public int Arch_WideArchetypeNarrowQuery() { var sum = 0; _arch.Query(_archQuery, (ref Wide0 a, ref Wide7 z) => ApplyWide(in a, in z, ref sum)); return Check(sum, Amount * 9); }
    [Benchmark] public int FrifloEngineECS_WideArchetypeNarrowQuery() { var sum = 0; _frifloQuery.ForEachEntity((ref Wide0 a, ref Wide7 z, FrifloEntity _) => ApplyWide(in a, in z, ref sum)); return Check(sum, Amount * 9); }
    [Benchmark] public int DefaultEcs_WideArchetypeNarrowQuery() { var sum = 0; var entities = _defaultQuery.GetEntities(); for (var i = entities.Length - 1; i >= 0; i--) { var a = entities[i].Get<Wide0>(); var z = entities[i].Get<Wide7>(); ApplyWide(in a, in z, ref sum); } return Check(sum, Amount * 9); }
    [Benchmark] public int LeoEcsLite_WideArchetypeNarrowQuery() { var sum = 0; foreach (var e in _leoQuery) { var a = _leo0.Get(e); var z = _leo7.Get(e); ApplyWide(in a, in z, ref sum); } return Check(sum, Amount * 9); }
    private static int Check(int actual, int expected) => actual == expected ? actual : throw new InvalidOperationException($"wide checksum mismatch: {actual} != {expected}");
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ApplyWide(ref readonly Wide0 a, ref readonly Wide7 z, ref int checksum) => checksum += a.Value + z.Value;
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
// All backends use the same matching/non-matching component signatures and
// count matches through the same ApplySparse terminal operation.
public class ComparativeSparseQueryBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private DeltaWorld _delta = null!; private Query _deltaQuery; private ComponentId _deltaA, _deltaB, _deltaC; private DeltaEntity[] _deltaEntities = null!;
    private Arch.Core.World _arch = null!; private ArchComponentType[] _archMatchTypes = null!; private ArchComponentType[] _archNonMatchTypes = null!; private ArchComponentType _archCType; private Arch.Core.QueryDescription _archQuery;
    private EntityStore _friflo = null!; private ArchetypeQuery<SparseA, SparseB> _frifloQuery = null!;
    private DefaultWorld _default = null!; private DefaultEcs.EntitySet _defaultQuery = null!;
    private EcsWorld _leo = null!; private EcsPool<SparseA> _leoA = null!; private EcsPool<SparseB> _leoB = null!; private EcsFilter _leoQuery = null!; private int[] _leoEntities = null!;
    private int ExpectedChecksum => ((Amount + ComparativeBenchmarkParameters.SparseMatchStride - 1) / ComparativeBenchmarkParameters.SparseMatchStride) * 3;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry(); _deltaA = layouts.Register(typeof(SparseA), new SchemaId(204_000)); _deltaB = layouts.Register(typeof(SparseB), new SchemaId(204_001)); _deltaC = layouts.Register(typeof(SparseC), new SchemaId(204_002)); var n0 = layouts.Register(typeof(SparseNoise0), new SchemaId(204_003)); var n1 = layouts.Register(typeof(SparseNoise1), new SchemaId(204_004)); var n2 = layouts.Register(typeof(SparseNoise2), new SchemaId(204_005)); var n3 = layouts.Register(typeof(SparseNoise3), new SchemaId(204_006)); _delta = new DeltaWorld(layouts, initialEntityCapacity: Amount); _deltaEntities = new DeltaEntity[Amount]; for (var i = 0; i < Amount; i++) { var ids = i % ComparativeBenchmarkParameters.SparseMatchStride == 0 ? new[] { _deltaA, _deltaB, n0, n1, n2, n3 } : new[] { _deltaA, _deltaB, _deltaC, n0, n1, n2, n3 }; var entity = _deltaEntities[i] = _delta.Create(ids); _delta.Set(entity, _deltaA, new SparseA { Value = 1 }); _delta.Set(entity, _deltaB, new SparseB { Value = 2 }); }
        var d = new QuerySpec(new[] { _deltaA, _deltaB }, Array.Empty<ComponentId>(), new[] { _deltaC }); _deltaQuery = _delta.CreateQuery(in d);
        _arch = Arch.Core.World.Create(); _archCType = typeof(SparseC); _archMatchTypes = new ArchComponentType[] { typeof(SparseA), typeof(SparseB), typeof(SparseNoise0), typeof(SparseNoise1), typeof(SparseNoise2), typeof(SparseNoise3) }; _archNonMatchTypes = new ArchComponentType[] { typeof(SparseA), typeof(SparseB), _archCType, typeof(SparseNoise0), typeof(SparseNoise1), typeof(SparseNoise2), typeof(SparseNoise3) }; _arch.Reserve(_archMatchTypes, Amount); _arch.Reserve(_archNonMatchTypes, Amount); _archQuery = new Arch.Core.QueryDescription { All = new ArchComponentType[] { _archMatchTypes[0], _archMatchTypes[1] }, None = new ArchComponentType[] { _archCType } }; for (var i = 0; i < Amount; i++) { var e = i % ComparativeBenchmarkParameters.SparseMatchStride == 0 ? _arch.Create(_archMatchTypes) : _arch.Create(_archNonMatchTypes); _arch.Set(e, new SparseA { Value = 1 }); _arch.Set(e, new SparseB { Value = 2 }); }
        _friflo = new EntityStore(); for (var i = 0; i < Amount; i++) { var e = _friflo.CreateEntity(new SparseA { Value = 1 }, new SparseB { Value = 2 }, new SparseNoise0(), new SparseNoise1(), new SparseNoise2(), new SparseNoise3()); if (i % ComparativeBenchmarkParameters.SparseMatchStride != 0) e.AddComponent(new SparseC()); }
        _frifloQuery = CreateFrifloQuery();
        _default = new DefaultWorld(); for (var i = 0; i < Amount; i++) { var e = _default.CreateEntity(); e.Set(new SparseA { Value = 1 }); e.Set(new SparseB { Value = 2 }); e.Set<SparseNoise0>(); e.Set<SparseNoise1>(); e.Set<SparseNoise2>(); e.Set<SparseNoise3>(); if (i % ComparativeBenchmarkParameters.SparseMatchStride != 0) e.Set<SparseC>(); }
        _defaultQuery = CreateDefaultQuery();
        _leo = new EcsWorld(); _leoA = _leo.GetPool<SparseA>(); _leoB = _leo.GetPool<SparseB>(); var c = _leo.GetPool<SparseC>(); var n0l = _leo.GetPool<SparseNoise0>(); var n1l = _leo.GetPool<SparseNoise1>(); var n2l = _leo.GetPool<SparseNoise2>(); var n3l = _leo.GetPool<SparseNoise3>(); _leoEntities = new int[Amount]; for (var i = 0; i < Amount; i++) { var e = _leoEntities[i] = _leo.NewEntity(); _leoA.Add(e).Value = 1; _leoB.Add(e).Value = 2; n0l.Add(e); n1l.Add(e); n2l.Add(e); n3l.Add(e); if (i % ComparativeBenchmarkParameters.SparseMatchStride != 0) c.Add(e); }
        _leoQuery = _leo.Filter<SparseA>().Inc<SparseB>().Exc<SparseC>().End();
    }
    [GlobalCleanup]
    public void Cleanup()
    {
        _delta?.Dispose();
        _arch?.Dispose();
        _defaultQuery?.Dispose();
        _default?.Dispose();
        (_leo as IDisposable)?.Dispose();
    }
    [Benchmark(Baseline = true), BenchmarkCategory("Iteration.SparseWorldQueryPlan")] public int DeltaECS_SparseWorldQueryPlan() => DeltaQuery(_deltaQuery);
    [Benchmark, BenchmarkCategory("Iteration.SparseWorldQueryPlan")] public int Arch_SparseWorldQueryPlan() => ArchQuery(_archQuery);
    [Benchmark, BenchmarkCategory("Iteration.SparseWorldQueryPlan")] public int FrifloEngineECS_SparseWorldQueryPlan() => FrifloQuery(_frifloQuery);
    [Benchmark, BenchmarkCategory("Iteration.SparseWorldQueryPlan")] public int DefaultEcs_SparseWorldQueryPlan() => DefaultQuery(_defaultQuery.GetEntities());
    [Benchmark, BenchmarkCategory("Iteration.SparseWorldQueryPlan")] public int LeoEcsLite_SparseWorldQueryPlan() => LeoQuery(_leoQuery);
    [Benchmark(Baseline = true), BenchmarkCategory("Iteration.QueryPlanConstruction")] public int DeltaECS_QueryPlanConstruction() { var d = new QuerySpec(new[] { _deltaA, _deltaB }, Array.Empty<ComponentId>(), new[] { _deltaC }); var query = _delta.CreateQuery(in d); return DeltaQuery(query); }
    [Benchmark, BenchmarkCategory("Iteration.QueryPlanConstruction")] public int Arch_QueryPlanConstruction() { var d = new Arch.Core.QueryDescription { All = new ArchComponentType[] { _archMatchTypes[0], _archMatchTypes[1] }, None = new ArchComponentType[] { _archCType } }; return ArchQuery(d); }
    [Benchmark, BenchmarkCategory("Iteration.QueryPlanConstruction")] public int FrifloEngineECS_QueryPlanConstruction() => FrifloQuery(CreateFrifloQuery());
    [Benchmark, BenchmarkCategory("Iteration.QueryPlanConstruction")] public int DefaultEcs_QueryPlanConstruction() { using var q = CreateDefaultQuery(); return DefaultQuery(q.GetEntities()); }
    [Benchmark, BenchmarkCategory("Iteration.QueryPlanConstruction")] public int LeoEcsLite_QueryPlanConstruction() => LeoQuery(_leo.Filter<SparseA>().Inc<SparseB>().Exc<SparseC>().End());

    private int DeltaQuery(Query query)
    {
        var count = 0;
        _delta.ForEach(in query, ref count,
            static (ref int matches, ref readonly SparseA a, ref readonly SparseB b) => ApplySparse(ref matches, in a, in b));

        return Check(count);
    }
    private int ArchQuery(Arch.Core.QueryDescription query) { var count = 0; _arch.Query(query, (ref SparseA a, ref SparseB b) => ApplySparse(ref count, in a, in b)); return Check(count); }
    private int FrifloQuery(ArchetypeQuery<SparseA, SparseB> query) { var count = 0; query.ForEachEntity((ref SparseA a, ref SparseB b, FrifloEntity _) => ApplySparse(ref count, in a, in b)); return Check(count); }
    private int DefaultQuery(ReadOnlySpan<DefaultEcs.Entity> entities) { var count = 0; for (var i = entities.Length - 1; i >= 0; i--) { var a = entities[i].Get<SparseA>(); var b = entities[i].Get<SparseB>(); ApplySparse(ref count, in a, in b); } return Check(count); }
    private int LeoQuery(EcsFilter query) { var count = 0; foreach (var entity in query) { var a = _leoA.Get(entity); var b = _leoB.Get(entity); ApplySparse(ref count, in a, in b); } return Check(count); }
    private int Check(int actual) => actual == ExpectedChecksum ? actual : throw new InvalidOperationException($"sparse checksum mismatch: {actual} != {ExpectedChecksum}");
    private ArchetypeQuery<SparseA, SparseB> CreateFrifloQuery() { var f = new QueryFilter(); f.WithoutAllComponents(ComponentTypes.Get<SparseC>()); return _friflo.Query<SparseA, SparseB>(f); }
    private DefaultEcs.EntitySet CreateDefaultQuery() => _default.GetEntities().With<SparseA>().With<SparseB>().Without<SparseC>().AsSet();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplySparse(ref int count, ref readonly SparseA a, ref readonly SparseB b) => count += a.Value + b.Value;
}

internal struct DenseValue : IComponent { public int Value; }
internal struct Movement2Position : IComponent { public float X; public float Y; }
internal struct Movement2Velocity : IComponent { public float X; public float Y; }
internal struct Movement4A : IComponent { public int Value; }
internal struct Movement4B : IComponent { public int Value; }
internal struct Movement4C : IComponent { public int Value; }
internal struct Movement4D : IComponent { public int Value; }
internal struct Wide0 : IComponent { public int Value; }
internal struct Wide1 : IComponent { }
internal struct Wide2 : IComponent { }
internal struct Wide3 : IComponent { }
internal struct Wide4 : IComponent { }
internal struct Wide5 : IComponent { }
internal struct Wide6 : IComponent { }
internal struct Wide7 : IComponent { public int Value; }
internal struct SparseA : IComponent { public int Value; }
internal struct SparseB : IComponent { public int Value; }
internal struct SparseC : IComponent { }
internal struct SparseNoise0 : IComponent { }
internal struct SparseNoise1 : IComponent { }
internal struct SparseNoise2 : IComponent { }
internal struct SparseNoise3 : IComponent { }
