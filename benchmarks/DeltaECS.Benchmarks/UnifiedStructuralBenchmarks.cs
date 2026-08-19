using Arch.Core;
using BenchmarkDotNet.Attributes;
using DefaultEcs;
using DVG.ECS;
using Friflo.Engine.ECS;
using Leopotam.EcsLite;
using DeltaEntity = DVG.ECS.Entity;
using DeltaWorld = DVG.ECS.World;
using DefaultWorld = DefaultEcs.World;

namespace DVG.ECS.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
[InvocationCount(1)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ComparativeStructuralListBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    [Params(1, 4)] public int ChangeWidth { get; set; }
    private DeltaWorld _world = null!;
    private ComponentId[] _base = null!;
    private ComponentId[] _change = null!;
    private ComponentId[] _target = null!;
    private DeltaEntity[] _entities = null!;
    private DeltaEntity[] _created = null!;
    private ComponentId[] _activeChange = null!;
    private ArchetypeHandle _targetArchetype;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _base = new[] { layouts.Register<StructuralBase>(new SchemaId(210_000)) };
        _change = new[]
        {
            layouts.Register<StructuralA0>(new SchemaId(210_001)), layouts.Register<StructuralA1>(new SchemaId(210_002)),
            layouts.Register<StructuralA2>(new SchemaId(210_003)), layouts.Register<StructuralA3>(new SchemaId(210_004))
        };
        _world = new DeltaWorld(layouts, initialEntityCapacity: Amount * 2);
        _entities = new DeltaEntity[Amount];
        _created = new DeltaEntity[Amount];
        _world.CreateBatch(_base, _entities);
        _activeChange = _change.AsSpan(0, ChangeWidth).ToArray();
        _target = _base.Concat(_activeChange).ToArray();
        _targetArchetype = _world.GetArchetype(_target);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Structural.List.CreateBatch")]
    public int DeltaECS_List_CreateBatch()
    {
        var created = _world.CreateBatch(_targetArchetype, _created);
        _world.DestroyBatch(_created);
        return created;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Structural.List.DestroyBatch")] public int DeltaECS_List_DestroyBatch() { _world.DestroyBatch(_entities); return Amount; }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.List.AddBatch")] public int DeltaECS_List_AddBatch() { _world.AddComponents(_activeChange, _entities); _world.RemoveComponents(_activeChange, _entities); return Amount; }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.List.RemoveBatch")] public int DeltaECS_List_RemoveBatch() { _world.AddComponents(_activeChange, _entities); _world.RemoveComponents(_activeChange, _entities); return Amount; }
}

[MemoryDiagnoser]
[ShortRunJob]
[InvocationCount(1)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ComparativeStructuralQueryBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    [Params(1, 4)] public int ChangeWidth { get; set; }
    private ComparativeStructuralListBenchmarks _list = null!;
    [GlobalSetup] public void Setup() { _list = new ComparativeStructuralListBenchmarks { Amount = Amount, ChangeWidth = ChangeWidth }; _list.Setup(); }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Query.CreateBatch")] public int DeltaECS_Query_CreateBatch() => _list.DeltaECS_List_CreateBatch();
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Query.DestroyBatch")] public int DeltaECS_Query_DestroyBatch() => _list.DeltaECS_List_DestroyBatch();
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Query.AddBatch")] public int DeltaECS_Query_AddBatch() => _list.DeltaECS_List_AddBatch();
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Query.RemoveBatch")] public int DeltaECS_Query_RemoveBatch() => _list.DeltaECS_List_RemoveBatch();
}

[MemoryDiagnoser]
[ShortRunJob]
[InvocationCount(1)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ComparativeStructuralAtomicBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    [Params(1, 4)] public int ChangeWidth { get; set; }
    private DeltaWorld _delta = null!;
    private ComponentId _deltaBase;
    private ComponentId[] _deltaAdded = null!;
    private DeltaEntity _deltaEntity;
    private Arch.Core.World _arch = null!;
    private Arch.Core.Entity _archEntity;
    private EntityStore _friflo = null!;
    private Friflo.Engine.ECS.Entity _frifloEntity;
    private DefaultWorld _default = null!;
    private DefaultEcs.Entity _defaultEntity;
    private EcsWorld _leo = null!;
    private int _leoEntity;
    private EcsPool<AtomicLeoBase> _leoBase = null!;
    private EcsPool<AtomicLeoA0> _leoA0 = null!;
    private EcsPool<AtomicLeoA1> _leoA1 = null!;
    private EcsPool<AtomicLeoA2> _leoA2 = null!;
    private EcsPool<AtomicLeoA3> _leoA3 = null!;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaBase = layouts.Register<StructuralBase>(new SchemaId(220_000));
        _deltaAdded = new[] { layouts.Register<StructuralA0>(new SchemaId(220_001)), layouts.Register<StructuralA1>(new SchemaId(220_002)), layouts.Register<StructuralA2>(new SchemaId(220_003)), layouts.Register<StructuralA3>(new SchemaId(220_004)) };
        _delta = new DeltaWorld(layouts);
        _deltaEntity = _delta.Create(new[] { _deltaBase });
        _arch = Arch.Core.World.Create(); _archEntity = _arch.Create(typeof(AtomicArchBase));
        _friflo = new EntityStore(); _frifloEntity = _friflo.CreateEntity(new AtomicFrifloBase());
        _default = new DefaultWorld(); _defaultEntity = _default.CreateEntity(); _defaultEntity.Set<AtomicDefaultBase>();
        _leo = new EcsWorld(); _leoEntity = _leo.NewEntity();
        _leoBase = _leo.GetPool<AtomicLeoBase>(); _leoA0 = _leo.GetPool<AtomicLeoA0>(); _leoA1 = _leo.GetPool<AtomicLeoA1>(); _leoA2 = _leo.GetPool<AtomicLeoA2>(); _leoA3 = _leo.GetPool<AtomicLeoA3>(); _leoBase.Add(_leoEntity);
    }

    [GlobalCleanup] public void Cleanup() { _default?.Dispose(); }

    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Atomic.Create")] public int DeltaECS_Atomic_Create() { var entity = _delta.Create(new[] { _deltaBase }); _delta.Destroy(entity); return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Create")] public int Arch_Atomic_Create() { var entity = _arch.Create(typeof(AtomicArchBase)); _arch.Destroy(entity); return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Create")] public int FrifloEngineECS_Atomic_Create() { var entity = _friflo.CreateEntity(new AtomicFrifloBase()); entity.DeleteEntity(); return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Create")] public int DefaultEcs_Atomic_Create() { var entity = _default.CreateEntity(); entity.Set<AtomicDefaultBase>(); entity.Dispose(); return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Create")] public int LeoEcsLite_Atomic_Create() { var entity = _leo.NewEntity(); _leoBase.Add(entity); _leo.DelEntity(entity); return 1; }

    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Atomic.Destroy")] public int DeltaECS_Atomic_Destroy() { _delta.Destroy(_deltaEntity); _deltaEntity = _delta.Create(new[] { _deltaBase }); return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Destroy")] public int Arch_Atomic_Destroy() { _arch.Destroy(_archEntity); _archEntity = _arch.Create(typeof(AtomicArchBase)); return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Destroy")] public int FrifloEngineECS_Atomic_Destroy() { _frifloEntity.DeleteEntity(); _frifloEntity = _friflo.CreateEntity(new AtomicFrifloBase()); return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Destroy")] public int DefaultEcs_Atomic_Destroy() { _defaultEntity.Dispose(); _defaultEntity = _default.CreateEntity(); _defaultEntity.Set<AtomicDefaultBase>(); return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Destroy")] public int LeoEcsLite_Atomic_Destroy() { _leo.DelEntity(_leoEntity); _leoEntity = _leo.NewEntity(); _leoBase.Add(_leoEntity); return 1; }

    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Atomic.Add")] public int DeltaECS_Atomic_Add() { _delta.AddComponents(_deltaAdded.AsSpan(0, ChangeWidth).ToArray(), _deltaEntity); _delta.RemoveComponents(_deltaAdded.AsSpan(0, ChangeWidth).ToArray(), _deltaEntity); return ChangeWidth; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Add")] public int Arch_Atomic_Add() { AddArch(); RemoveArch(); return ChangeWidth; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Add")] public int FrifloEngineECS_Atomic_Add() { AddFriflo(); RemoveFriflo(); return ChangeWidth; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Add")] public int DefaultEcs_Atomic_Add() { AddDefault(); RemoveDefault(); return ChangeWidth; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Add")] public int LeoEcsLite_Atomic_Add() { AddLeo(); RemoveLeo(); return ChangeWidth; }

    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Atomic.Remove")] public int DeltaECS_Atomic_Remove() => DeltaECS_Atomic_Add();
    [Benchmark, BenchmarkCategory("Structural.Atomic.Remove")] public int Arch_Atomic_Remove() => Arch_Atomic_Add();
    [Benchmark, BenchmarkCategory("Structural.Atomic.Remove")] public int FrifloEngineECS_Atomic_Remove() => FrifloEngineECS_Atomic_Add();
    [Benchmark, BenchmarkCategory("Structural.Atomic.Remove")] public int DefaultEcs_Atomic_Remove() => DefaultEcs_Atomic_Add();
    [Benchmark, BenchmarkCategory("Structural.Atomic.Remove")] public int LeoEcsLite_Atomic_Remove() => LeoEcsLite_Atomic_Add();

    private void AddArch() { _arch.Set(_archEntity, new AtomicArchA0()); if (ChangeWidth > 1) { _arch.Set(_archEntity, new AtomicArchA1()); _arch.Set(_archEntity, new AtomicArchA2()); _arch.Set(_archEntity, new AtomicArchA3()); } }
    private void RemoveArch() { _arch.Remove<AtomicArchA0>(_archEntity); if (ChangeWidth > 1) { _arch.Remove<AtomicArchA1>(_archEntity); _arch.Remove<AtomicArchA2>(_archEntity); _arch.Remove<AtomicArchA3>(_archEntity); } }
    private void AddFriflo() { _frifloEntity.AddComponent(new AtomicFrifloA0()); if (ChangeWidth > 1) { _frifloEntity.AddComponent(new AtomicFrifloA1()); _frifloEntity.AddComponent(new AtomicFrifloA2()); _frifloEntity.AddComponent(new AtomicFrifloA3()); } }
    private void RemoveFriflo() { _frifloEntity.RemoveComponent<AtomicFrifloA0>(); if (ChangeWidth > 1) { _frifloEntity.RemoveComponent<AtomicFrifloA1>(); _frifloEntity.RemoveComponent<AtomicFrifloA2>(); _frifloEntity.RemoveComponent<AtomicFrifloA3>(); } }
    private void AddDefault() { _defaultEntity.Set<AtomicDefaultA0>(); if (ChangeWidth > 1) { _defaultEntity.Set<AtomicDefaultA1>(); _defaultEntity.Set<AtomicDefaultA2>(); _defaultEntity.Set<AtomicDefaultA3>(); } }
    private void RemoveDefault() { _defaultEntity.Remove<AtomicDefaultA0>(); if (ChangeWidth > 1) { _defaultEntity.Remove<AtomicDefaultA1>(); _defaultEntity.Remove<AtomicDefaultA2>(); _defaultEntity.Remove<AtomicDefaultA3>(); } }
    private void AddLeo() { _leoA0.Add(_leoEntity); if (ChangeWidth > 1) { _leoA1.Add(_leoEntity); _leoA2.Add(_leoEntity); _leoA3.Add(_leoEntity); } }
    private void RemoveLeo() { _leoA0.Del(_leoEntity); if (ChangeWidth > 1) { _leoA1.Del(_leoEntity); _leoA2.Del(_leoEntity); _leoA3.Del(_leoEntity); } }
}

internal struct StructuralBase { }
internal struct StructuralA0 { }
internal struct StructuralA1 { }
internal struct StructuralA2 { }
internal struct StructuralA3 { }
internal struct AtomicArchBase { }
internal struct AtomicArchA0 { }
internal struct AtomicArchA1 { }
internal struct AtomicArchA2 { }
internal struct AtomicArchA3 { }
internal struct AtomicFrifloBase : IComponent { }
internal struct AtomicFrifloA0 : IComponent { }
internal struct AtomicFrifloA1 : IComponent { }
internal struct AtomicFrifloA2 : IComponent { }
internal struct AtomicFrifloA3 : IComponent { }
internal struct AtomicDefaultBase { }
internal struct AtomicDefaultA0 { }
internal struct AtomicDefaultA1 { }
internal struct AtomicDefaultA2 { }
internal struct AtomicDefaultA3 { }
internal struct AtomicLeoBase { }
internal struct AtomicLeoA0 { }
internal struct AtomicLeoA1 { }
internal struct AtomicLeoA2 { }
internal struct AtomicLeoA3 { }
