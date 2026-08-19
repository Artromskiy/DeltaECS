using Arch.Core;
using BenchmarkDotNet.Attributes;
using DefaultEcs;
using Delta.ECS;
using Friflo.Engine.ECS;
using Leopotam.EcsLite;
using DeltaEntity = Delta.ECS.Entity;
using DeltaWorld = Delta.ECS.World;
using DefaultWorld = DefaultEcs.World;

namespace Delta.ECS.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
[InvocationCount(1)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public partial class ComparativeStructuralListBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    [Params(1, 4)] public int ChangeWidth { get; set; }
    private DeltaWorld _world = null!; private ComponentId[] _base = null!; private ComponentId[] _change = null!; private ComponentId[] _target = null!; private ComponentId[] _activeChange = null!; private DeltaEntity[] _entities = null!; private DeltaEntity[] _created = null!; private ArchetypeHandle _targetArchetype; private ListState _state;
    [GlobalSetup] public void Setup() { var l = new ComponentLayoutRegistry(); _base = new[] { l.Register<StructuralBase>(new SchemaId(210_000)) }; _change = new[] { l.Register<StructuralA0>(new SchemaId(210_001)), l.Register<StructuralA1>(new SchemaId(210_002)), l.Register<StructuralA2>(new SchemaId(210_003)), l.Register<StructuralA3>(new SchemaId(210_004)) }; _world = new DeltaWorld(l, initialEntityCapacity: Amount * 2); _entities = new DeltaEntity[Amount]; _created = new DeltaEntity[Amount]; _activeChange = _change.AsSpan(0, ChangeWidth).ToArray(); _target = _base.Concat(_activeChange).ToArray(); _targetArchetype = _world.GetArchetype(_target); _world.CreateBatch(_base, _entities); _state = ListState.Base; SetupListFallbacks(); }
    [IterationSetup(Target = nameof(DeltaECS_List_CreateBatch))] public void PrepareCreate() => RestoreBase();
    [IterationSetup(Target = nameof(DeltaECS_List_DestroyBatch))] public void PrepareDestroy() => RestoreBase();
    [IterationSetup(Target = nameof(DeltaECS_List_AddBatch))] public void PrepareAdd() => RestoreBase();
    [IterationSetup(Target = nameof(DeltaECS_List_RemoveBatch))] public void PrepareRemove() { RestoreBase(); _world.AddComponents(_activeChange, _entities); _state = ListState.Added; }
    [IterationCleanup] public void RestoreAfterIteration() { RestoreBase(); RestoreListFallbacks(); }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.List.CreateBatch")] public int DeltaECS_List_CreateBatch() { var c = _world.CreateBatch(_targetArchetype, _created); _state = ListState.Created; return c == Amount ? c : throw new InvalidOperationException("list create count mismatch"); }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.List.DestroyBatch")] public int DeltaECS_List_DestroyBatch() { var c = _world.DestroyBatch(_entities); _state = ListState.Destroyed; return c == Amount ? c : throw new InvalidOperationException("list destroy count mismatch"); }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.List.AddBatch")] public int DeltaECS_List_AddBatch() { var c = _world.AddComponents(_activeChange, _entities); _state = ListState.Added; return c == Amount ? c : throw new InvalidOperationException("list add count mismatch"); }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.List.RemoveBatch")] public int DeltaECS_List_RemoveBatch() { var c = _world.RemoveComponents(_activeChange, _entities); _state = ListState.Base; return c == Amount ? c : throw new InvalidOperationException("list remove count mismatch"); }
    private void RestoreBase() { switch (_state) { case ListState.Created: _world.DestroyBatch(_created); break; case ListState.Destroyed: _world.CreateBatch(_base, _entities); break; case ListState.Added: _world.RemoveComponents(_activeChange, _entities); break; } _state = ListState.Base; }
    private enum ListState { Base, Created, Destroyed, Added }
}

[MemoryDiagnoser]
[ShortRunJob]
[InvocationCount(1)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public partial class ComparativeStructuralQueryBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    [Params(1, 4)] public int ChangeWidth { get; set; }
    private DeltaWorld _world = null!; private ComponentId _a, _b, _c; private ComponentId[] _change = null!; private ComponentId[] _activeChange = null!; private ComponentId[] _target = null!; private DeltaEntity[] _entities = null!; private DeltaEntity[] _nonMatches = null!; private DeltaEntity[] _created = null!; private DeltaEntity[] _matches = null!; private ArchetypeHandle _targetArchetype; private QueryHandle _query; private QueryState _state;
    [GlobalSetup] public void Setup() { var l = new ComponentLayoutRegistry(); _a = l.Register<QueryA>(new SchemaId(211_000)); _b = l.Register<QueryB>(new SchemaId(211_001)); _c = l.Register<QueryC>(new SchemaId(211_002)); _change = new[] { l.Register<QueryK0>(new SchemaId(211_003)), l.Register<QueryK1>(new SchemaId(211_004)), l.Register<QueryK2>(new SchemaId(211_005)), l.Register<QueryK3>(new SchemaId(211_006)) }; _activeChange = _change.AsSpan(0, ChangeWidth).ToArray(); _target = new[] { _a, _b }.Concat(_activeChange).ToArray(); _world = new DeltaWorld(l, initialEntityCapacity: Amount * 2); _entities = new DeltaEntity[Amount]; _nonMatches = new DeltaEntity[Amount]; _created = new DeltaEntity[Amount]; _matches = new DeltaEntity[Amount]; _targetArchetype = _world.GetArchetype(_target); _query = CreateQuery(); _state = QueryState.Empty; SetupQueryFallbacks(); }
    [IterationSetup(Target = nameof(DeltaECS_Query_CreateBatch))] public void PrepareCreate() { RestoreEmpty(); }
    [IterationSetup(Target = nameof(DeltaECS_Query_DestroyBatch))] public void PrepareDestroy() { RestoreEmpty(); CreateQueryEntities(false); }
    [IterationSetup(Target = nameof(DeltaECS_Query_AddBatch))] public void PrepareAdd() { RestoreEmpty(); CreateQueryEntities(false); }
    [IterationSetup(Target = nameof(DeltaECS_Query_RemoveBatch))] public void PrepareRemove() { RestoreEmpty(); CreateQueryEntities(true); }
    [IterationCleanup] public void RestoreAfterIteration() { RestoreEmpty(); RestoreQueryFallbacks(); }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Query.CreateBatch")] public int DeltaECS_Query_CreateBatch() { var c = _world.CreateBatch(_targetArchetype, _created); _state = QueryState.Created; return c == Amount ? c : throw new InvalidOperationException("query create count mismatch"); }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Query.DestroyBatch")] public int DeltaECS_Query_DestroyBatch() { var count = CollectMatches(); var c = _world.DestroyBatch(_matches.AsSpan(0, count)); _state = QueryState.Destroyed; return c == ExpectedMatches ? c : throw new InvalidOperationException("query destroy selection mismatch"); }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Query.AddBatch")] public int DeltaECS_Query_AddBatch() { var count = CollectMatches(); var c = _world.AddComponents(_activeChange, _matches.AsSpan(0, count)); _state = QueryState.Mixed; return c == count ? c : throw new InvalidOperationException("query add selection mismatch"); }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Query.RemoveBatch")] public int DeltaECS_Query_RemoveBatch() { var count = CollectMatches(); var c = _world.RemoveComponents(_activeChange, _matches.AsSpan(0, count)); _state = QueryState.Mixed; return c == count ? c : throw new InvalidOperationException("query remove selection mismatch"); }
    private int ExpectedMatches => (Amount + ComparativeBenchmarkParameters.SparseMatchStride - 1) / ComparativeBenchmarkParameters.SparseMatchStride;
    private int CollectMatches() { var s = new QueryCollectState(_matches); _world.Query(in _query, QueryAccess.Read, ref s, static (ref QueryCollectState state, ref DenseChunkAccessor lease) => { var entities = lease.Entities; for (var i = entities.Length - 1; i >= 0; i--) state.Entities[state.Count++] = entities[i]; }); return s.Count; }
    private QueryHandle CreateQuery() { var d = new QueryDescription(new[] { _a, _b }, Array.Empty<ComponentId>(), new[] { _c }, Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>()); return _world.CreateQuery(in d); }
    private void CreateQueryEntities(bool withChange) { var matchingIds = withChange ? _target : new[] { _a, _b }; var nonMatchingIds = withChange ? new[] { _a, _b, _c }.Concat(_activeChange).ToArray() : new[] { _a, _b, _c }; var nonMatchCount = 0; for (var i = 0; i < Amount; i++) { if (i % ComparativeBenchmarkParameters.SparseMatchStride == 0) _entities[i] = _world.Create(matchingIds); else _nonMatches[nonMatchCount++] = _entities[i] = _world.Create(nonMatchingIds); } _state = QueryState.Mixed; }
    private void RestoreEmpty() { if (_state == QueryState.Created) _world.DestroyBatch(_created); else if (_state == QueryState.Destroyed) _world.DestroyBatch(_nonMatches.AsSpan(0, Amount - ExpectedMatches)); else if (_state == QueryState.Mixed) _world.DestroyBatch(_entities); _state = QueryState.Empty; }
    private enum QueryState { Empty, Mixed, Destroyed, Created }
    private struct QueryCollectState { public QueryCollectState(DeltaEntity[] entities) { Entities = entities; Count = 0; } public DeltaEntity[] Entities; public int Count; }
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
    private DeltaWorld _delta = null!; private ComponentId _deltaBase; private ComponentId[] _deltaActive = null!; private DeltaEntity _deltaEntity, _deltaExtra; private bool _deltaAlive, _deltaExtraAlive;
    private Arch.Core.World _arch = null!; private Arch.Core.Entity _archEntity, _archExtra; private bool _archAlive, _archExtraAlive; private EntityStore _friflo = null!; private Friflo.Engine.ECS.Entity _frifloEntity, _frifloExtra; private EntityBatch _frifloAddBatch = null!, _frifloRemoveBatch = null!; private bool _frifloAlive, _frifloExtraAlive; private DefaultWorld _default = null!; private DefaultEcs.Entity _defaultEntity, _defaultExtra; private bool _defaultAlive, _defaultExtraAlive; private EcsWorld _leo = null!; private int _leoEntity, _leoExtra; private bool _leoAlive, _leoExtraAlive; private EcsPool<AtomicLeoBase> _leoBase = null!; private EcsPool<AtomicLeoA0> _leoA0 = null!; private EcsPool<AtomicLeoA1> _leoA1 = null!; private EcsPool<AtomicLeoA2> _leoA2 = null!; private EcsPool<AtomicLeoA3> _leoA3 = null!;
    private bool _deltaAddedState, _archAddedState, _frifloAddedState, _defaultAddedState, _leoAddedState;
    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaBase = layouts.Register<StructuralBase>(new SchemaId(220_000));
        var added = new[]
        {
            layouts.Register<StructuralA0>(new SchemaId(220_001)),
            layouts.Register<StructuralA1>(new SchemaId(220_002)),
            layouts.Register<StructuralA2>(new SchemaId(220_003)),
            layouts.Register<StructuralA3>(new SchemaId(220_004))
        };
        _deltaActive = added.AsSpan(0, ChangeWidth).ToArray();
        _delta = new DeltaWorld(layouts);
        _deltaEntity = _delta.Create(new[] { _deltaBase });
        _deltaAlive = true;

        _arch = Arch.Core.World.Create();
        _arch.Reserve(new Arch.Core.Utils.ComponentType[] { typeof(AtomicArchBase) }, 2);
        _arch.Reserve(new Arch.Core.Utils.ComponentType[] { typeof(AtomicArchBase), typeof(AtomicArchA0) }, 2);
        _arch.Reserve(new Arch.Core.Utils.ComponentType[] { typeof(AtomicArchBase), typeof(AtomicArchA0), typeof(AtomicArchA1) }, 2);
        _arch.Reserve(new Arch.Core.Utils.ComponentType[] { typeof(AtomicArchBase), typeof(AtomicArchA0), typeof(AtomicArchA1), typeof(AtomicArchA2) }, 2);
        _arch.Reserve(new Arch.Core.Utils.ComponentType[] { typeof(AtomicArchBase), typeof(AtomicArchA0), typeof(AtomicArchA1), typeof(AtomicArchA2), typeof(AtomicArchA3) }, 2);
        _archEntity = _arch.Create(typeof(AtomicArchBase));
        _archAlive = true;

        _friflo = new EntityStore();
        _frifloEntity = _friflo.CreateEntity(new AtomicFrifloBase());
        _frifloAddBatch = CreateFrifloAtomicBatch(add: true);
        _frifloRemoveBatch = CreateFrifloAtomicBatch(add: false);
        _frifloAlive = true;

        _default = new DefaultWorld();
        _defaultEntity = _default.CreateEntity();
        _defaultEntity.Set<AtomicDefaultBase>();
        _defaultAlive = true;

        _leo = new EcsWorld();
        _leoEntity = _leo.NewEntity();
        _leoAlive = true;
        _leoBase = _leo.GetPool<AtomicLeoBase>();
        _leoA0 = _leo.GetPool<AtomicLeoA0>();
        _leoA1 = _leo.GetPool<AtomicLeoA1>();
        _leoA2 = _leo.GetPool<AtomicLeoA2>();
        _leoA3 = _leo.GetPool<AtomicLeoA3>();
        _leoBase.Add(_leoEntity);
    }
    [IterationSetup(Targets = new[]
    {
        nameof(DeltaECS_Atomic_Create), nameof(Arch_Atomic_Create), nameof(FrifloEngineECS_Atomic_Create), nameof(DefaultEcs_Atomic_Create), nameof(LeoEcsLite_Atomic_Create),
        nameof(DeltaECS_Atomic_Destroy), nameof(Arch_Atomic_Destroy), nameof(FrifloEngineECS_Atomic_Destroy), nameof(DefaultEcs_Atomic_Destroy), nameof(LeoEcsLite_Atomic_Destroy),
        nameof(DeltaECS_Atomic_Add), nameof(Arch_Atomic_Add), nameof(FrifloEngineECS_Atomic_Add), nameof(DefaultEcs_Atomic_Add), nameof(LeoEcsLite_Atomic_Add)
    })]
    public void PrepareAtomic() => RestoreAtomic();
    [IterationSetup(Target = nameof(DeltaECS_Atomic_Remove))] public void PrepareDeltaRemove() { RestoreAtomic(); AddDelta(); }
    [IterationSetup(Target = nameof(Arch_Atomic_Remove))] public void PrepareArchRemove() { RestoreAtomic(); AddArch(); }
    [IterationSetup(Target = nameof(FrifloEngineECS_Atomic_Remove))] public void PrepareFrifloRemove() { RestoreAtomic(); AddFriflo(); }
    [IterationSetup(Target = nameof(DefaultEcs_Atomic_Remove))] public void PrepareDefaultRemove() { RestoreAtomic(); AddDefault(); }
    [IterationSetup(Target = nameof(LeoEcsLite_Atomic_Remove))] public void PrepareLeoRemove() { RestoreAtomic(); AddLeo(); }
    [IterationCleanup] public void RestoreAfterIteration() => RestoreAtomic();
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Atomic.Create")] public int DeltaECS_Atomic_Create() { _deltaExtra = _delta.Create(new[] { _deltaBase }); _deltaExtraAlive = true; return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Create")] public int Arch_Atomic_Create() { _archExtra = _arch.Create(typeof(AtomicArchBase)); _archExtraAlive = true; return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Create")] public int FrifloEngineECS_Atomic_Create() { _frifloExtra = _friflo.CreateEntity(new AtomicFrifloBase()); _frifloExtraAlive = true; return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Create")] public int DefaultEcs_Atomic_Create() { _defaultExtra = _default.CreateEntity(); _defaultExtra.Set<AtomicDefaultBase>(); _defaultExtraAlive = true; return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Create")] public int LeoEcsLite_Atomic_Create() { _leoExtra = _leo.NewEntity(); _leoBase.Add(_leoExtra); _leoExtraAlive = true; return 1; }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Atomic.Destroy")] public int DeltaECS_Atomic_Destroy() { _delta.Destroy(_deltaEntity); _deltaAlive = false; return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Destroy")] public int Arch_Atomic_Destroy() { _arch.Destroy(_archEntity); _archAlive = false; return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Destroy")] public int FrifloEngineECS_Atomic_Destroy() { _frifloEntity.DeleteEntity(); _frifloAlive = false; return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Destroy")] public int DefaultEcs_Atomic_Destroy() { _defaultEntity.Dispose(); _defaultAlive = false; return 1; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Destroy")] public int LeoEcsLite_Atomic_Destroy() { _leo.DelEntity(_leoEntity); _leoAlive = false; return 1; }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Atomic.Add")] public int DeltaECS_Atomic_Add() { AddDelta(); return ChangeWidth; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Add")] public int Arch_Atomic_Add() { AddArch(); return ChangeWidth; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Add")] public int FrifloEngineECS_Atomic_Add() { AddFriflo(); return ChangeWidth; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Add")] public int DefaultEcs_Atomic_Add() { AddDefault(); return ChangeWidth; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Add")] public int LeoEcsLite_Atomic_Add() { AddLeo(); return ChangeWidth; }
    [Benchmark(Baseline = true), BenchmarkCategory("Structural.Atomic.Remove")] public int DeltaECS_Atomic_Remove() { RemoveDelta(); return ChangeWidth; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Remove")] public int Arch_Atomic_Remove() { RemoveArch(); return ChangeWidth; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Remove")] public int FrifloEngineECS_Atomic_Remove() { RemoveFriflo(); return ChangeWidth; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Remove")] public int DefaultEcs_Atomic_Remove() { RemoveDefault(); return ChangeWidth; }
    [Benchmark, BenchmarkCategory("Structural.Atomic.Remove")] public int LeoEcsLite_Atomic_Remove() { RemoveLeo(); return ChangeWidth; }
    private void RestoreAtomic() { if (_deltaAddedState) RemoveDelta(); if (_archAddedState) RemoveArch(); if (_frifloAddedState) RemoveFriflo(); if (_defaultAddedState) RemoveDefault(); if (_leoAddedState) RemoveLeo(); if (_deltaExtraAlive) { _delta.Destroy(_deltaExtra); _deltaExtraAlive = false; } if (_archExtraAlive) { _arch.Destroy(_archExtra); _archExtraAlive = false; } if (_frifloExtraAlive) { _frifloExtra.DeleteEntity(); _frifloExtraAlive = false; } if (_defaultExtraAlive) { _defaultExtra.Dispose(); _defaultExtraAlive = false; } if (_leoExtraAlive) { _leo.DelEntity(_leoExtra); _leoExtraAlive = false; } if (!_deltaAlive) { _deltaEntity = _delta.Create(new[] { _deltaBase }); _deltaAlive = true; } if (!_archAlive) { _archEntity = _arch.Create(typeof(AtomicArchBase)); _archAlive = true; } if (!_frifloAlive) { _frifloEntity = _friflo.CreateEntity(new AtomicFrifloBase()); _frifloAlive = true; } if (!_defaultAlive) { _defaultEntity = _default.CreateEntity(); _defaultEntity.Set<AtomicDefaultBase>(); _defaultAlive = true; } if (!_leoAlive) { _leoEntity = _leo.NewEntity(); _leoBase.Add(_leoEntity); _leoAlive = true; } }
    private void AddDelta() { _delta.AddComponents(_deltaActive, _deltaEntity); _deltaAddedState = true; } private void RemoveDelta() { _delta.RemoveComponents(_deltaActive, _deltaEntity); _deltaAddedState = false; }
    private void AddArch() { if (ChangeWidth == 1) _arch.Add(_archEntity, new AtomicArchA0()); else _arch.Add(_archEntity, new AtomicArchA0(), new AtomicArchA1(), new AtomicArchA2(), new AtomicArchA3()); _archAddedState = true; } private void RemoveArch() { if (ChangeWidth == 1) _arch.Remove<AtomicArchA0>(_archEntity); else _arch.Remove<AtomicArchA0, AtomicArchA1, AtomicArchA2, AtomicArchA3>(_archEntity); _archAddedState = false; }
    private void AddFriflo() { _frifloAddBatch.ApplyTo(_frifloEntity); _frifloAddedState = true; } private void RemoveFriflo() { _frifloRemoveBatch.ApplyTo(_frifloEntity); _frifloAddedState = false; }
    private EntityBatch CreateFrifloAtomicBatch(bool add) { var batch = new EntityBatch(); if (add) { batch.Add(new AtomicFrifloA0()); if (ChangeWidth > 1) { batch.Add(new AtomicFrifloA1()); batch.Add(new AtomicFrifloA2()); batch.Add(new AtomicFrifloA3()); } } else { batch.Remove<AtomicFrifloA0>(); if (ChangeWidth > 1) { batch.Remove<AtomicFrifloA1>(); batch.Remove<AtomicFrifloA2>(); batch.Remove<AtomicFrifloA3>(); } } return batch; }
    private void AddDefault() { _defaultEntity.Set<AtomicDefaultA0>(); if (ChangeWidth > 1) { _defaultEntity.Set<AtomicDefaultA1>(); _defaultEntity.Set<AtomicDefaultA2>(); _defaultEntity.Set<AtomicDefaultA3>(); } _defaultAddedState = true; } private void RemoveDefault() { _defaultEntity.Remove<AtomicDefaultA0>(); if (ChangeWidth > 1) { _defaultEntity.Remove<AtomicDefaultA1>(); _defaultEntity.Remove<AtomicDefaultA2>(); _defaultEntity.Remove<AtomicDefaultA3>(); } _defaultAddedState = false; }
    private void AddLeo() { _leoA0.Add(_leoEntity); if (ChangeWidth > 1) { _leoA1.Add(_leoEntity); _leoA2.Add(_leoEntity); _leoA3.Add(_leoEntity); } _leoAddedState = true; } private void RemoveLeo() { _leoA0.Del(_leoEntity); if (ChangeWidth > 1) { _leoA1.Del(_leoEntity); _leoA2.Del(_leoEntity); _leoA3.Del(_leoEntity); } _leoAddedState = false; }
    [GlobalCleanup] public void Cleanup() => _default?.Dispose();
}

internal struct StructuralBase { }
internal struct StructuralA0 { }
internal struct StructuralA1 { }
internal struct StructuralA2 { }
internal struct StructuralA3 { }
internal struct QueryA { }
internal struct QueryB { }
internal struct QueryC { }
internal struct QueryK0 { }
internal struct QueryK1 { }
internal struct QueryK2 { }
internal struct QueryK3 { }
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
