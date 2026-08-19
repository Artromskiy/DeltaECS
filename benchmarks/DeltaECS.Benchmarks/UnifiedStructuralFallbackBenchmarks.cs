using Arch.Core;
using Arch.Core.Utils;
using BenchmarkDotNet.Attributes;
using DefaultEcs;
using Friflo.Engine.ECS;
using Leopotam.EcsLite;
using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using ArchComponentType = Arch.Core.Utils.ComponentType;
using DefaultEntity = DefaultEcs.Entity;
using DefaultWorld = DefaultEcs.World;
using FrifloEntity = Friflo.Engine.ECS.Entity;

namespace Delta.ECS.Benchmarks;

public partial class ComparativeStructuralListBenchmarks
{
    private ArchWorld _archList = null!;
    private ArchComponentType[] _archListBaseTypes = null!;
    private ArchComponentType[] _archListTargetTypes = null!;
    private ArchEntity[] _archListEntities = null!;
    private ArchEntity[] _archListCreated = null!;
    private ListFallbackState _archListState;

    private EntityStore _frifloList = null!;
    private FrifloEntity[] _frifloListEntities = null!;
    private FrifloEntity[] _frifloListCreated = null!;
    private EntityList _frifloEntityList = null!;
    private EntityBatch _frifloListAddBatch = null!;
    private EntityBatch _frifloListRemoveBatch = null!;
    private ListFallbackState _frifloListState;

    private DefaultWorld _defaultList = null!;
    private DefaultEntity[] _defaultListEntities = null!;
    private DefaultEntity[] _defaultListCreated = null!;
    private ListFallbackState _defaultListState;

    private EcsWorld _leoList = null!;
    private int[] _leoListEntities = null!;
    private int[] _leoListCreated = null!;
    private EcsPool<ListLeoBase> _leoListBase = null!;
    private EcsPool<ListLeoK0> _leoListK0 = null!;
    private EcsPool<ListLeoK1> _leoListK1 = null!;
    private EcsPool<ListLeoK2> _leoListK2 = null!;
    private EcsPool<ListLeoK3> _leoListK3 = null!;
    private ListFallbackState _leoListState;

    private void SetupListFallbacks()
    {
        _archList = ArchWorld.Create();
        _archListBaseTypes = new ArchComponentType[] { typeof(ListArchBase) };
        _archListTargetTypes = ChangeWidth == 1
            ? new ArchComponentType[] { typeof(ListArchBase), typeof(ListArchK0) }
            : new ArchComponentType[] { typeof(ListArchBase), typeof(ListArchK0), typeof(ListArchK1), typeof(ListArchK2), typeof(ListArchK3) };
        _archList.Reserve(_archListBaseTypes, Amount);
        _archList.Reserve(_archListTargetTypes, Amount);
        _archListEntities = new ArchEntity[Amount];
        _archListCreated = new ArchEntity[Amount];
        for (var index = 0; index < Amount; index++) _archListEntities[index] = _archList.Create(_archListBaseTypes);
        _archListState = ListFallbackState.Base;

        _frifloList = new EntityStore();
        _frifloListEntities = new FrifloEntity[Amount];
        _frifloListCreated = new FrifloEntity[Amount];
        _frifloEntityList = new EntityList(_frifloList);
        for (var index = 0; index < Amount; index++)
        {
            var entity = _frifloListEntities[index] = CreateFrifloListEntity(withChange: false);
            _frifloEntityList.Add(entity);
        }
        _frifloListAddBatch = CreateFrifloBatch(add: true);
        _frifloListRemoveBatch = CreateFrifloBatch(add: false);
        _frifloListState = ListFallbackState.Base;

        _defaultList = new DefaultWorld();
        _defaultListEntities = new DefaultEntity[Amount];
        _defaultListCreated = new DefaultEntity[Amount];
        for (var index = 0; index < Amount; index++) _defaultListEntities[index] = CreateDefaultListEntity(withChange: false);
        _defaultListState = ListFallbackState.Base;

        _leoList = new EcsWorld();
        _leoListBase = _leoList.GetPool<ListLeoBase>();
        _leoListK0 = _leoList.GetPool<ListLeoK0>();
        _leoListK1 = _leoList.GetPool<ListLeoK1>();
        _leoListK2 = _leoList.GetPool<ListLeoK2>();
        _leoListK3 = _leoList.GetPool<ListLeoK3>();
        _leoListEntities = new int[Amount];
        _leoListCreated = new int[Amount];
        for (var index = 0; index < Amount; index++) _leoListEntities[index] = CreateLeoListEntity(withChange: false);
        _leoListState = ListFallbackState.Base;
    }

    [IterationSetup(Target = nameof(Arch_List_CreateBatch))] public void PrepareArchListCreate() => RestoreArchListBase();
    [IterationSetup(Target = nameof(Arch_List_DestroyBatch))] public void PrepareArchListDestroy() => RestoreArchListBase();
    [IterationSetup(Target = nameof(Arch_List_AddBatch))] public void PrepareArchListAdd() => RestoreArchListBase();
    [IterationSetup(Target = nameof(Arch_List_RemoveBatch))] public void PrepareArchListRemove() { RestoreArchListBase(); AddArchList(); _archListState = ListFallbackState.Added; }

    [IterationSetup(Target = nameof(FrifloEngineECS_List_CreateBatch))] public void PrepareFrifloListCreate() => RestoreFrifloListBase();
    [IterationSetup(Target = nameof(FrifloEngineECS_List_DestroyBatch))] public void PrepareFrifloListDestroy() => RestoreFrifloListBase();
    [IterationSetup(Target = nameof(FrifloEngineECS_List_AddBatch))] public void PrepareFrifloListAdd() => RestoreFrifloListBase();
    [IterationSetup(Target = nameof(FrifloEngineECS_List_RemoveBatch))] public void PrepareFrifloListRemove() { RestoreFrifloListBase(); AddFrifloList(); _frifloListState = ListFallbackState.Added; }

    [IterationSetup(Target = nameof(DefaultEcs_List_CreateBatch))] public void PrepareDefaultListCreate() => RestoreDefaultListBase();
    [IterationSetup(Target = nameof(DefaultEcs_List_DestroyBatch))] public void PrepareDefaultListDestroy() => RestoreDefaultListBase();
    [IterationSetup(Target = nameof(DefaultEcs_List_AddBatch))] public void PrepareDefaultListAdd() => RestoreDefaultListBase();
    [IterationSetup(Target = nameof(DefaultEcs_List_RemoveBatch))] public void PrepareDefaultListRemove() { RestoreDefaultListBase(); AddDefaultList(); _defaultListState = ListFallbackState.Added; }

    [IterationSetup(Target = nameof(LeoEcsLite_List_CreateBatch))] public void PrepareLeoListCreate() => RestoreLeoListBase();
    [IterationSetup(Target = nameof(LeoEcsLite_List_DestroyBatch))] public void PrepareLeoListDestroy() => RestoreLeoListBase();
    [IterationSetup(Target = nameof(LeoEcsLite_List_AddBatch))] public void PrepareLeoListAdd() => RestoreLeoListBase();
    [IterationSetup(Target = nameof(LeoEcsLite_List_RemoveBatch))] public void PrepareLeoListRemove() { RestoreLeoListBase(); AddLeoList(); _leoListState = ListFallbackState.Added; }

    [Benchmark, BenchmarkCategory("Structural.List.CreateBatch")]
    public int Arch_List_CreateBatch()
    {
        for (var index = 0; index < Amount; index++) _archListCreated[index] = CreateArchListEntity(withChange: true);
        _archListState = ListFallbackState.Created;
        return Amount;
    }

    [Benchmark, BenchmarkCategory("Structural.List.DestroyBatch")]
    public int Arch_List_DestroyBatch()
    {
        for (var index = Amount - 1; index >= 0; index--) _archList.Destroy(_archListEntities[index]);
        _archListState = ListFallbackState.Destroyed;
        return Amount;
    }

    [Benchmark, BenchmarkCategory("Structural.List.AddBatch")]
    public int Arch_List_AddBatch() { AddArchList(); _archListState = ListFallbackState.Added; return Amount; }

    [Benchmark, BenchmarkCategory("Structural.List.RemoveBatch")]
    public int Arch_List_RemoveBatch() { RemoveArchList(); _archListState = ListFallbackState.Base; return Amount; }

    [Benchmark, BenchmarkCategory("Structural.List.CreateBatch")]
    public int FrifloEngineECS_List_CreateBatch()
    {
        for (var index = 0; index < Amount; index++) _frifloListCreated[index] = CreateFrifloListEntity(withChange: true);
        _frifloListState = ListFallbackState.Created;
        return Amount;
    }

    [Benchmark, BenchmarkCategory("Structural.List.DestroyBatch")]
    public int FrifloEngineECS_List_DestroyBatch()
    {
        for (var index = Amount - 1; index >= 0; index--) _frifloListEntities[index].DeleteEntity();
        _frifloListState = ListFallbackState.Destroyed;
        return Amount;
    }

    [Benchmark, BenchmarkCategory("Structural.List.AddBatch")]
    public int FrifloEngineECS_List_AddBatch() { AddFrifloList(); _frifloListState = ListFallbackState.Added; return Amount; }

    [Benchmark, BenchmarkCategory("Structural.List.RemoveBatch")]
    public int FrifloEngineECS_List_RemoveBatch() { RemoveFrifloList(); _frifloListState = ListFallbackState.Base; return Amount; }

    [Benchmark, BenchmarkCategory("Structural.List.CreateBatch")]
    public int DefaultEcs_List_CreateBatch()
    {
        for (var index = 0; index < Amount; index++) _defaultListCreated[index] = CreateDefaultListEntity(withChange: true);
        _defaultListState = ListFallbackState.Created;
        return Amount;
    }

    [Benchmark, BenchmarkCategory("Structural.List.DestroyBatch")]
    public int DefaultEcs_List_DestroyBatch()
    {
        for (var index = Amount - 1; index >= 0; index--) _defaultListEntities[index].Dispose();
        _defaultListState = ListFallbackState.Destroyed;
        return Amount;
    }

    [Benchmark, BenchmarkCategory("Structural.List.AddBatch")]
    public int DefaultEcs_List_AddBatch() { AddDefaultList(); _defaultListState = ListFallbackState.Added; return Amount; }

    [Benchmark, BenchmarkCategory("Structural.List.RemoveBatch")]
    public int DefaultEcs_List_RemoveBatch() { RemoveDefaultList(); _defaultListState = ListFallbackState.Base; return Amount; }

    [Benchmark, BenchmarkCategory("Structural.List.CreateBatch")]
    public int LeoEcsLite_List_CreateBatch()
    {
        for (var index = 0; index < Amount; index++) _leoListCreated[index] = CreateLeoListEntity(withChange: true);
        _leoListState = ListFallbackState.Created;
        return Amount;
    }

    [Benchmark, BenchmarkCategory("Structural.List.DestroyBatch")]
    public int LeoEcsLite_List_DestroyBatch()
    {
        for (var index = Amount - 1; index >= 0; index--) _leoList.DelEntity(_leoListEntities[index]);
        _leoListState = ListFallbackState.Destroyed;
        return Amount;
    }

    [Benchmark, BenchmarkCategory("Structural.List.AddBatch")]
    public int LeoEcsLite_List_AddBatch() { AddLeoList(); _leoListState = ListFallbackState.Added; return Amount; }

    [Benchmark, BenchmarkCategory("Structural.List.RemoveBatch")]
    public int LeoEcsLite_List_RemoveBatch() { RemoveLeoList(); _leoListState = ListFallbackState.Base; return Amount; }

    private void AddArchList()
    {
        for (var index = 0; index < Amount; index++)
        {
            var entity = _archListEntities[index];
            if (ChangeWidth == 1) _archList.Add(entity, new ListArchK0());
            else _archList.Add(entity, new ListArchK0(), new ListArchK1(), new ListArchK2(), new ListArchK3());
        }
    }

    private void RemoveArchList()
    {
        for (var index = 0; index < Amount; index++)
        {
            var entity = _archListEntities[index];
            if (ChangeWidth == 1) _archList.Remove<ListArchK0>(entity);
            else _archList.Remove<ListArchK0, ListArchK1, ListArchK2, ListArchK3>(entity);
        }
    }

    private ArchEntity CreateArchListEntity(bool withChange)
    {
        if (!withChange) return _archList.Create<ListArchBase>();
        return ChangeWidth == 1
            ? _archList.Create(new ListArchBase(), new ListArchK0())
            : _archList.Create(new ListArchBase(), new ListArchK0(), new ListArchK1(), new ListArchK2(), new ListArchK3());
    }

    private void AddFrifloList() => _frifloEntityList.ApplyBatch(_frifloListAddBatch);

    private void RemoveFrifloList() => _frifloEntityList.ApplyBatch(_frifloListRemoveBatch);

    private EntityBatch CreateFrifloBatch(bool add)
    {
        var batch = new EntityBatch();
        if (add)
        {
            batch.Add(new ListFrifloK0());
            if (ChangeWidth != 1) { batch.Add(new ListFrifloK1()); batch.Add(new ListFrifloK2()); batch.Add(new ListFrifloK3()); }
        }
        else
        {
            batch.Remove<ListFrifloK0>();
            if (ChangeWidth != 1) { batch.Remove<ListFrifloK1>(); batch.Remove<ListFrifloK2>(); batch.Remove<ListFrifloK3>(); }
        }
        return batch;
    }

    private void AddDefaultList()
    {
        for (var index = 0; index < Amount; index++)
        {
            var entity = _defaultListEntities[index];
            entity.Set<ListDefaultK0>();
            if (ChangeWidth == 1) continue;
            entity.Set<ListDefaultK1>(); entity.Set<ListDefaultK2>(); entity.Set<ListDefaultK3>();
        }
    }

    private void RemoveDefaultList()
    {
        for (var index = 0; index < Amount; index++)
        {
            var entity = _defaultListEntities[index];
            entity.Remove<ListDefaultK0>();
            if (ChangeWidth == 1) continue;
            entity.Remove<ListDefaultK1>(); entity.Remove<ListDefaultK2>(); entity.Remove<ListDefaultK3>();
        }
    }

    private void AddLeoList()
    {
        for (var index = 0; index < Amount; index++)
        {
            var entity = _leoListEntities[index];
            _leoListK0.Add(entity);
            if (ChangeWidth == 1) continue;
            _leoListK1.Add(entity); _leoListK2.Add(entity); _leoListK3.Add(entity);
        }
    }

    private void RemoveLeoList()
    {
        for (var index = 0; index < Amount; index++)
        {
            var entity = _leoListEntities[index];
            _leoListK0.Del(entity);
            if (ChangeWidth == 1) continue;
            _leoListK1.Del(entity); _leoListK2.Del(entity); _leoListK3.Del(entity);
        }
    }

    private FrifloEntity CreateFrifloListEntity(bool withChange) => !withChange
        ? _frifloList.CreateEntity(new ListFrifloBase())
        : ChangeWidth == 1
            ? _frifloList.CreateEntity(new ListFrifloBase(), new ListFrifloK0())
            : _frifloList.CreateEntity(new ListFrifloBase(), new ListFrifloK0(), new ListFrifloK1(), new ListFrifloK2(), new ListFrifloK3());

    private DefaultEntity CreateDefaultListEntity(bool withChange)
    {
        var entity = _defaultList.CreateEntity();
        entity.Set<ListDefaultBase>();
        if (!withChange) return entity;
        entity.Set<ListDefaultK0>();
        if (ChangeWidth != 1) { entity.Set<ListDefaultK1>(); entity.Set<ListDefaultK2>(); entity.Set<ListDefaultK3>(); }
        return entity;
    }

    private int CreateLeoListEntity(bool withChange)
    {
        var entity = _leoList.NewEntity();
        _leoListBase.Add(entity);
        if (!withChange) return entity;
        _leoListK0.Add(entity);
        if (ChangeWidth != 1) { _leoListK1.Add(entity); _leoListK2.Add(entity); _leoListK3.Add(entity); }
        return entity;
    }

    private void RestoreListFallbacks()
    {
        RestoreArchListBase(); RestoreFrifloListBase(); RestoreDefaultListBase(); RestoreLeoListBase();
    }

    private void RestoreArchListBase()
    {
        if (_archListState == ListFallbackState.Created) for (var index = Amount - 1; index >= 0; index--) _archList.Destroy(_archListCreated[index]);
        else if (_archListState == ListFallbackState.Destroyed) for (var index = 0; index < Amount; index++) _archListEntities[index] = CreateArchListEntity(withChange: false);
        else if (_archListState == ListFallbackState.Added) RemoveArchList();
        _archListState = ListFallbackState.Base;
    }

    private void RestoreFrifloListBase()
    {
        if (_frifloListState == ListFallbackState.Created) for (var index = Amount - 1; index >= 0; index--) _frifloListCreated[index].DeleteEntity();
        else if (_frifloListState == ListFallbackState.Destroyed)
        {
            _frifloEntityList.Clear();
            for (var index = 0; index < Amount; index++)
            {
                var entity = _frifloListEntities[index] = CreateFrifloListEntity(withChange: false);
                _frifloEntityList.Add(entity);
            }
        }
        else if (_frifloListState == ListFallbackState.Added) RemoveFrifloList();
        _frifloListState = ListFallbackState.Base;
    }

    private void RestoreDefaultListBase()
    {
        if (_defaultListState == ListFallbackState.Created) for (var index = Amount - 1; index >= 0; index--) _defaultListCreated[index].Dispose();
        else if (_defaultListState == ListFallbackState.Destroyed) for (var index = 0; index < Amount; index++) _defaultListEntities[index] = CreateDefaultListEntity(withChange: false);
        else if (_defaultListState == ListFallbackState.Added) RemoveDefaultList();
        _defaultListState = ListFallbackState.Base;
    }

    private void RestoreLeoListBase()
    {
        if (_leoListState == ListFallbackState.Created) for (var index = Amount - 1; index >= 0; index--) _leoList.DelEntity(_leoListCreated[index]);
        else if (_leoListState == ListFallbackState.Destroyed) for (var index = 0; index < Amount; index++) _leoListEntities[index] = CreateLeoListEntity(withChange: false);
        else if (_leoListState == ListFallbackState.Added) RemoveLeoList();
        _leoListState = ListFallbackState.Base;
    }

    private enum ListFallbackState { Base, Created, Destroyed, Added }
}

internal struct ListArchBase { }
internal struct ListArchK0 { }
internal struct ListArchK1 { }
internal struct ListArchK2 { }
internal struct ListArchK3 { }
internal struct ListFrifloBase : IComponent { }
internal struct ListFrifloK0 : IComponent { }
internal struct ListFrifloK1 : IComponent { }
internal struct ListFrifloK2 : IComponent { }
internal struct ListFrifloK3 : IComponent { }
internal struct ListDefaultBase { }
internal struct ListDefaultK0 { }
internal struct ListDefaultK1 { }
internal struct ListDefaultK2 { }
internal struct ListDefaultK3 { }
internal struct ListLeoBase { }
internal struct ListLeoK0 { }
internal struct ListLeoK1 { }
internal struct ListLeoK2 { }
internal struct ListLeoK3 { }

public partial class ComparativeStructuralQueryBenchmarks
{
    private ArchWorld _archQueryWorld = null!;
    private Arch.Core.QueryDescription _archQueryDescription;
    private ArchComponentType[] _archQueryMatchTypes = null!;
    private ArchComponentType[] _archQueryNonMatchTypes = null!;
    private ArchComponentType[] _archQueryMatchChangedTypes = null!;
    private ArchComponentType[] _archQueryNonMatchChangedTypes = null!;
    private ArchEntity[] _archQueryEntities = null!;
    private ArchEntity[] _archQueryNonMatches = null!;
    private ArchEntity[] _archQueryCreated = null!;
    private QueryFallbackState _archQueryState;

    private EntityStore _frifloQueryWorld = null!;
    private ArchetypeQuery<QueryFrifloA, QueryFrifloB> _frifloQuery = null!;
    private FrifloEntity[] _frifloQueryEntities = null!;
    private FrifloEntity[] _frifloQueryNonMatches = null!;
    private FrifloEntity[] _frifloQueryCreated = null!;
    private FrifloEntity[] _frifloQueryMatches = null!;
    private EntityBatch _frifloQueryAddBatch = null!;
    private EntityBatch _frifloQueryRemoveBatch = null!;
    private QueryFallbackState _frifloQueryState;

    private DefaultWorld _defaultQueryWorld = null!;
    private EntitySet _defaultQuerySet = null!;
    private DefaultEntity[] _defaultQueryEntities = null!;
    private DefaultEntity[] _defaultQueryNonMatches = null!;
    private DefaultEntity[] _defaultQueryCreated = null!;
    private DefaultEntity[] _defaultQueryMatches = null!;
    private QueryFallbackState _defaultQueryState;

    private EcsWorld _leoQueryWorld = null!;
    private EcsFilter _leoQueryFilter = null!;
    private int[] _leoQueryEntities = null!;
    private int[] _leoQueryNonMatches = null!;
    private int[] _leoQueryCreated = null!;
    private int[] _leoQueryMatches = null!;
    private EcsPool<QueryLeoA> _leoQueryA = null!;
    private EcsPool<QueryLeoB> _leoQueryB = null!;
    private EcsPool<QueryLeoC> _leoQueryC = null!;
    private EcsPool<QueryLeoK0> _leoQueryK0 = null!;
    private EcsPool<QueryLeoK1> _leoQueryK1 = null!;
    private EcsPool<QueryLeoK2> _leoQueryK2 = null!;
    private EcsPool<QueryLeoK3> _leoQueryK3 = null!;
    private QueryFallbackState _leoQueryState;

    private void SetupQueryFallbacks()
    {
        _archQueryWorld = ArchWorld.Create();
        var a = (ArchComponentType)typeof(QueryArchA); var b = (ArchComponentType)typeof(QueryArchB); var c = (ArchComponentType)typeof(QueryArchC);
        var k0 = (ArchComponentType)typeof(QueryArchK0); var k1 = (ArchComponentType)typeof(QueryArchK1); var k2 = (ArchComponentType)typeof(QueryArchK2); var k3 = (ArchComponentType)typeof(QueryArchK3);
        _archQueryMatchTypes = new[] { a, b };
        _archQueryNonMatchTypes = new[] { a, b, c };
        _archQueryMatchChangedTypes = ChangeWidth == 1 ? new[] { a, b, k0 } : new[] { a, b, k0, k1, k2, k3 };
        _archQueryNonMatchChangedTypes = ChangeWidth == 1 ? new[] { a, b, c, k0 } : new[] { a, b, c, k0, k1, k2, k3 };
        _archQueryWorld.Reserve(_archQueryMatchTypes, Amount);
        _archQueryWorld.Reserve(_archQueryNonMatchTypes, Amount);
        _archQueryWorld.Reserve(_archQueryMatchChangedTypes, Amount);
        _archQueryWorld.Reserve(_archQueryNonMatchChangedTypes, Amount);
        _archQueryDescription = new Arch.Core.QueryDescription { All = new[] { a, b }, None = new[] { c } };
        _archQueryEntities = new ArchEntity[Amount];
        _archQueryNonMatches = new ArchEntity[Amount];
        _archQueryCreated = new ArchEntity[Amount];
        _archQueryState = QueryFallbackState.Empty;

        _frifloQueryWorld = new EntityStore();
        var frifloFilter = new QueryFilter();
        frifloFilter.WithoutAllComponents(ComponentTypes.Get<QueryFrifloC>());
        _frifloQuery = _frifloQueryWorld.Query<QueryFrifloA, QueryFrifloB>(frifloFilter);
        _frifloQueryEntities = new FrifloEntity[Amount];
        _frifloQueryNonMatches = new FrifloEntity[Amount];
        _frifloQueryCreated = new FrifloEntity[Amount];
        _frifloQueryMatches = new FrifloEntity[Amount];
        _frifloQueryAddBatch = CreateFrifloQueryBatch(add: true);
        _frifloQueryRemoveBatch = CreateFrifloQueryBatch(add: false);
        _frifloQueryState = QueryFallbackState.Empty;

        _defaultQueryWorld = new DefaultWorld();
        _defaultQuerySet = _defaultQueryWorld.GetEntities().With<QueryDefaultA>().With<QueryDefaultB>().Without<QueryDefaultC>().AsSet();
        _defaultQueryEntities = new DefaultEntity[Amount];
        _defaultQueryNonMatches = new DefaultEntity[Amount];
        _defaultQueryCreated = new DefaultEntity[Amount];
        _defaultQueryMatches = new DefaultEntity[Amount];
        _defaultQueryState = QueryFallbackState.Empty;

        _leoQueryWorld = new EcsWorld();
        _leoQueryA = _leoQueryWorld.GetPool<QueryLeoA>(); _leoQueryB = _leoQueryWorld.GetPool<QueryLeoB>(); _leoQueryC = _leoQueryWorld.GetPool<QueryLeoC>();
        _leoQueryK0 = _leoQueryWorld.GetPool<QueryLeoK0>(); _leoQueryK1 = _leoQueryWorld.GetPool<QueryLeoK1>(); _leoQueryK2 = _leoQueryWorld.GetPool<QueryLeoK2>(); _leoQueryK3 = _leoQueryWorld.GetPool<QueryLeoK3>();
        _leoQueryFilter = _leoQueryWorld.Filter<QueryLeoA>().Inc<QueryLeoB>().Exc<QueryLeoC>().End();
        _leoQueryEntities = new int[Amount];
        _leoQueryNonMatches = new int[Amount];
        _leoQueryCreated = new int[Amount];
        _leoQueryMatches = new int[Amount];
        _leoQueryState = QueryFallbackState.Empty;
    }

    [IterationSetup(Target = nameof(Arch_Query_CreateBatch))] public void PrepareArchQueryCreate() => RestoreArchQueryEmpty();
    [IterationSetup(Target = nameof(Arch_Query_DestroyBatch))] public void PrepareArchQueryDestroy() { RestoreArchQueryEmpty(); CreateArchQueryEntities(withChange: false); }
    [IterationSetup(Target = nameof(Arch_Query_AddBatch))] public void PrepareArchQueryAdd() { RestoreArchQueryEmpty(); CreateArchQueryEntities(withChange: false); }
    [IterationSetup(Target = nameof(Arch_Query_RemoveBatch))] public void PrepareArchQueryRemove() { RestoreArchQueryEmpty(); CreateArchQueryEntities(withChange: true); }

    [IterationSetup(Target = nameof(FrifloEngineECS_Query_CreateBatch))] public void PrepareFrifloQueryCreate() => RestoreFrifloQueryEmpty();
    [IterationSetup(Target = nameof(FrifloEngineECS_Query_DestroyBatch))] public void PrepareFrifloQueryDestroy() { RestoreFrifloQueryEmpty(); CreateFrifloQueryEntities(withChange: false); }
    [IterationSetup(Target = nameof(FrifloEngineECS_Query_AddBatch))] public void PrepareFrifloQueryAdd() { RestoreFrifloQueryEmpty(); CreateFrifloQueryEntities(withChange: false); }
    [IterationSetup(Target = nameof(FrifloEngineECS_Query_RemoveBatch))] public void PrepareFrifloQueryRemove() { RestoreFrifloQueryEmpty(); CreateFrifloQueryEntities(withChange: true); }

    [IterationSetup(Target = nameof(DefaultEcs_Query_CreateBatch))] public void PrepareDefaultQueryCreate() => RestoreDefaultQueryEmpty();
    [IterationSetup(Target = nameof(DefaultEcs_Query_DestroyBatch))] public void PrepareDefaultQueryDestroy() { RestoreDefaultQueryEmpty(); CreateDefaultQueryEntities(withChange: false); }
    [IterationSetup(Target = nameof(DefaultEcs_Query_AddBatch))] public void PrepareDefaultQueryAdd() { RestoreDefaultQueryEmpty(); CreateDefaultQueryEntities(withChange: false); }
    [IterationSetup(Target = nameof(DefaultEcs_Query_RemoveBatch))] public void PrepareDefaultQueryRemove() { RestoreDefaultQueryEmpty(); CreateDefaultQueryEntities(withChange: true); }

    [IterationSetup(Target = nameof(LeoEcsLite_Query_CreateBatch))] public void PrepareLeoQueryCreate() => RestoreLeoQueryEmpty();
    [IterationSetup(Target = nameof(LeoEcsLite_Query_DestroyBatch))] public void PrepareLeoQueryDestroy() { RestoreLeoQueryEmpty(); CreateLeoQueryEntities(withChange: false); }
    [IterationSetup(Target = nameof(LeoEcsLite_Query_AddBatch))] public void PrepareLeoQueryAdd() { RestoreLeoQueryEmpty(); CreateLeoQueryEntities(withChange: false); }
    [IterationSetup(Target = nameof(LeoEcsLite_Query_RemoveBatch))] public void PrepareLeoQueryRemove() { RestoreLeoQueryEmpty(); CreateLeoQueryEntities(withChange: true); }

    [Benchmark, BenchmarkCategory("Structural.Query.CreateBatch")]
    public int Arch_Query_CreateBatch()
    {
        for (var index = 0; index < Amount; index++) _archQueryCreated[index] = CreateArchQueryEntity(match: true, withChange: true);
        _archQueryState = QueryFallbackState.Created;
        return Amount;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.DestroyBatch")]
    public int Arch_Query_DestroyBatch() { _archQueryWorld.Destroy(in _archQueryDescription); _archQueryState = QueryFallbackState.Destroyed; return ExpectedMatches; }

    [Benchmark, BenchmarkCategory("Structural.Query.AddBatch")]
    public int Arch_Query_AddBatch()
    {
        if (ChangeWidth == 1) _archQueryWorld.Add(in _archQueryDescription, new QueryArchK0());
        else _archQueryWorld.Add(in _archQueryDescription, new QueryArchK0(), new QueryArchK1(), new QueryArchK2(), new QueryArchK3());
        _archQueryState = QueryFallbackState.Mixed;
        return ExpectedMatches;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.RemoveBatch")]
    public int Arch_Query_RemoveBatch()
    {
        if (ChangeWidth == 1) _archQueryWorld.Remove<QueryArchK0>(in _archQueryDescription);
        else _archQueryWorld.Remove<QueryArchK0, QueryArchK1, QueryArchK2, QueryArchK3>(in _archQueryDescription);
        _archQueryState = QueryFallbackState.Mixed;
        return ExpectedMatches;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.CreateBatch")]
    public int FrifloEngineECS_Query_CreateBatch()
    {
        for (var index = 0; index < Amount; index++) _frifloQueryCreated[index] = CreateFrifloQueryEntity(match: true, withChange: true);
        _frifloQueryState = QueryFallbackState.Created;
        return Amount;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.DestroyBatch")]
    public int FrifloEngineECS_Query_DestroyBatch()
    {
        var count = CollectFrifloQueryMatches();
        for (var index = count - 1; index >= 0; index--) _frifloQueryMatches[index].DeleteEntity();
        _frifloQueryState = QueryFallbackState.Destroyed;
        return count;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.AddBatch")]
    public int FrifloEngineECS_Query_AddBatch()
    {
        var count = _frifloQuery.Entities.Count;
        _frifloQuery.Entities.ApplyBatch(_frifloQueryAddBatch);
        _frifloQueryState = QueryFallbackState.Mixed;
        return count;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.RemoveBatch")]
    public int FrifloEngineECS_Query_RemoveBatch()
    {
        var count = _frifloQuery.Entities.Count;
        _frifloQuery.Entities.ApplyBatch(_frifloQueryRemoveBatch);
        _frifloQueryState = QueryFallbackState.Mixed;
        return count;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.CreateBatch")]
    public int DefaultEcs_Query_CreateBatch()
    {
        for (var index = 0; index < Amount; index++) _defaultQueryCreated[index] = CreateDefaultQueryEntity(match: true, withChange: true);
        _defaultQueryState = QueryFallbackState.Created;
        return Amount;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.DestroyBatch")]
    public int DefaultEcs_Query_DestroyBatch()
    {
        var count = CollectDefaultQueryMatches();
        for (var index = count - 1; index >= 0; index--) _defaultQueryMatches[index].Dispose();
        _defaultQueryState = QueryFallbackState.Destroyed;
        return count;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.AddBatch")]
    public int DefaultEcs_Query_AddBatch()
    {
        var count = CollectDefaultQueryMatches();
        for (var index = 0; index < count; index++) AddDefaultQuery(_defaultQueryMatches[index]);
        _defaultQueryState = QueryFallbackState.Mixed;
        return count;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.RemoveBatch")]
    public int DefaultEcs_Query_RemoveBatch()
    {
        var count = CollectDefaultQueryMatches();
        for (var index = 0; index < count; index++) RemoveDefaultQuery(_defaultQueryMatches[index]);
        _defaultQueryState = QueryFallbackState.Mixed;
        return count;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.CreateBatch")]
    public int LeoEcsLite_Query_CreateBatch()
    {
        for (var index = 0; index < Amount; index++) _leoQueryCreated[index] = CreateLeoQueryEntity(match: true, withChange: true);
        _leoQueryState = QueryFallbackState.Created;
        return Amount;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.DestroyBatch")]
    public int LeoEcsLite_Query_DestroyBatch()
    {
        var count = CollectLeoQueryMatches();
        for (var index = count - 1; index >= 0; index--) _leoQueryWorld.DelEntity(_leoQueryMatches[index]);
        _leoQueryState = QueryFallbackState.Destroyed;
        return count;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.AddBatch")]
    public int LeoEcsLite_Query_AddBatch()
    {
        var count = CollectLeoQueryMatches();
        for (var index = 0; index < count; index++) AddLeoQuery(_leoQueryMatches[index]);
        _leoQueryState = QueryFallbackState.Mixed;
        return count;
    }

    [Benchmark, BenchmarkCategory("Structural.Query.RemoveBatch")]
    public int LeoEcsLite_Query_RemoveBatch()
    {
        var count = CollectLeoQueryMatches();
        for (var index = 0; index < count; index++) RemoveLeoQuery(_leoQueryMatches[index]);
        _leoQueryState = QueryFallbackState.Mixed;
        return count;
    }

    private void CreateArchQueryEntities(bool withChange)
    {
        var nonMatchCount = 0;
        for (var index = 0; index < Amount; index++)
        {
            var match = index % ComparativeBenchmarkParameters.SparseMatchStride == 0;
            var entity = _archQueryEntities[index] = CreateArchQueryEntity(match, withChange);
            if (!match) _archQueryNonMatches[nonMatchCount++] = entity;
        }
        _archQueryState = QueryFallbackState.Mixed;
    }

    private ArchEntity CreateArchQueryEntity(bool match, bool withChange)
    {
        if (match)
        {
            if (!withChange) return _archQueryWorld.Create<QueryArchA, QueryArchB>();
            return ChangeWidth == 1
                ? _archQueryWorld.Create(new QueryArchA(), new QueryArchB(), new QueryArchK0())
                : _archQueryWorld.Create(new QueryArchA(), new QueryArchB(), new QueryArchK0(), new QueryArchK1(), new QueryArchK2(), new QueryArchK3());
        }

        if (!withChange) return _archQueryWorld.Create<QueryArchA, QueryArchB, QueryArchC>();
        return ChangeWidth == 1
            ? _archQueryWorld.Create(new QueryArchA(), new QueryArchB(), new QueryArchC(), new QueryArchK0())
            : _archQueryWorld.Create(new QueryArchA(), new QueryArchB(), new QueryArchC(), new QueryArchK0(), new QueryArchK1(), new QueryArchK2(), new QueryArchK3());
    }

    private void CreateFrifloQueryEntities(bool withChange)
    {
        var nonMatchCount = 0;
        for (var index = 0; index < Amount; index++)
        {
            var match = index % ComparativeBenchmarkParameters.SparseMatchStride == 0;
            var entity = _frifloQueryEntities[index] = CreateFrifloQueryEntity(match, withChange);
            if (!match) _frifloQueryNonMatches[nonMatchCount++] = entity;
        }
        _frifloQueryState = QueryFallbackState.Mixed;
    }

    private FrifloEntity CreateFrifloQueryEntity(bool match, bool withChange)
    {
        var entity = _frifloQueryWorld.CreateEntity(new QueryFrifloA(), new QueryFrifloB());
        if (!match) entity.AddComponent(new QueryFrifloC());
        if (withChange) AddFrifloQuery(entity);
        return entity;
    }

    private void CreateDefaultQueryEntities(bool withChange)
    {
        var nonMatchCount = 0;
        for (var index = 0; index < Amount; index++)
        {
            var match = index % ComparativeBenchmarkParameters.SparseMatchStride == 0;
            var entity = _defaultQueryEntities[index] = CreateDefaultQueryEntity(match, withChange);
            if (!match) _defaultQueryNonMatches[nonMatchCount++] = entity;
        }
        _defaultQueryState = QueryFallbackState.Mixed;
    }

    private DefaultEntity CreateDefaultQueryEntity(bool match, bool withChange)
    {
        var entity = _defaultQueryWorld.CreateEntity();
        entity.Set<QueryDefaultA>(); entity.Set<QueryDefaultB>();
        if (!match) entity.Set<QueryDefaultC>();
        if (withChange) AddDefaultQuery(entity);
        return entity;
    }

    private void CreateLeoQueryEntities(bool withChange)
    {
        var nonMatchCount = 0;
        for (var index = 0; index < Amount; index++)
        {
            var match = index % ComparativeBenchmarkParameters.SparseMatchStride == 0;
            var entity = _leoQueryEntities[index] = CreateLeoQueryEntity(match, withChange);
            if (!match) _leoQueryNonMatches[nonMatchCount++] = entity;
        }
        _leoQueryState = QueryFallbackState.Mixed;
    }

    private int CreateLeoQueryEntity(bool match, bool withChange)
    {
        var entity = _leoQueryWorld.NewEntity();
        _leoQueryA.Add(entity); _leoQueryB.Add(entity);
        if (!match) _leoQueryC.Add(entity);
        if (withChange) AddLeoQuery(entity);
        return entity;
    }

    private int CollectFrifloQueryMatches()
    {
        var count = 0;
        _frifloQuery.ForEachEntity((ref QueryFrifloA _, ref QueryFrifloB _, FrifloEntity entity) => _frifloQueryMatches[count++] = entity);
        return count;
    }

    private int CollectDefaultQueryMatches()
    {
        var entities = _defaultQuerySet.GetEntities();
        entities.CopyTo(_defaultQueryMatches);
        return entities.Length;
    }

    private int CollectLeoQueryMatches()
    {
        var count = 0;
        foreach (var entity in _leoQueryFilter) _leoQueryMatches[count++] = entity;
        return count;
    }

    private void AddFrifloQuery(FrifloEntity entity)
    {
        entity.AddComponent(new QueryFrifloK0());
        if (ChangeWidth != 1) { entity.AddComponent(new QueryFrifloK1()); entity.AddComponent(new QueryFrifloK2()); entity.AddComponent(new QueryFrifloK3()); }
    }

    private void RemoveFrifloQuery(FrifloEntity entity)
    {
        entity.RemoveComponent<QueryFrifloK0>();
        if (ChangeWidth != 1) { entity.RemoveComponent<QueryFrifloK1>(); entity.RemoveComponent<QueryFrifloK2>(); entity.RemoveComponent<QueryFrifloK3>(); }
    }

    private EntityBatch CreateFrifloQueryBatch(bool add)
    {
        var batch = new EntityBatch();
        if (add)
        {
            batch.Add(new QueryFrifloK0());
            if (ChangeWidth != 1) { batch.Add(new QueryFrifloK1()); batch.Add(new QueryFrifloK2()); batch.Add(new QueryFrifloK3()); }
        }
        else
        {
            batch.Remove<QueryFrifloK0>();
            if (ChangeWidth != 1) { batch.Remove<QueryFrifloK1>(); batch.Remove<QueryFrifloK2>(); batch.Remove<QueryFrifloK3>(); }
        }
        return batch;
    }

    private void AddDefaultQuery(DefaultEntity entity)
    {
        entity.Set<QueryDefaultK0>();
        if (ChangeWidth != 1) { entity.Set<QueryDefaultK1>(); entity.Set<QueryDefaultK2>(); entity.Set<QueryDefaultK3>(); }
    }

    private void RemoveDefaultQuery(DefaultEntity entity)
    {
        entity.Remove<QueryDefaultK0>();
        if (ChangeWidth != 1) { entity.Remove<QueryDefaultK1>(); entity.Remove<QueryDefaultK2>(); entity.Remove<QueryDefaultK3>(); }
    }

    private void AddLeoQuery(int entity)
    {
        _leoQueryK0.Add(entity);
        if (ChangeWidth != 1) { _leoQueryK1.Add(entity); _leoQueryK2.Add(entity); _leoQueryK3.Add(entity); }
    }

    private void RemoveLeoQuery(int entity)
    {
        _leoQueryK0.Del(entity);
        if (ChangeWidth != 1) { _leoQueryK1.Del(entity); _leoQueryK2.Del(entity); _leoQueryK3.Del(entity); }
    }

    private void RestoreQueryFallbacks()
    {
        RestoreArchQueryEmpty(); RestoreFrifloQueryEmpty(); RestoreDefaultQueryEmpty(); RestoreLeoQueryEmpty();
    }

    private void RestoreArchQueryEmpty()
    {
        if (_archQueryState == QueryFallbackState.Created) for (var index = Amount - 1; index >= 0; index--) _archQueryWorld.Destroy(_archQueryCreated[index]);
        else if (_archQueryState == QueryFallbackState.Destroyed) for (var index = Amount - ExpectedMatches - 1; index >= 0; index--) _archQueryWorld.Destroy(_archQueryNonMatches[index]);
        else if (_archQueryState == QueryFallbackState.Mixed) for (var index = Amount - 1; index >= 0; index--) _archQueryWorld.Destroy(_archQueryEntities[index]);
        _archQueryState = QueryFallbackState.Empty;
    }

    private void RestoreFrifloQueryEmpty()
    {
        if (_frifloQueryState == QueryFallbackState.Created) for (var index = Amount - 1; index >= 0; index--) _frifloQueryCreated[index].DeleteEntity();
        else if (_frifloQueryState == QueryFallbackState.Destroyed) for (var index = Amount - ExpectedMatches - 1; index >= 0; index--) _frifloQueryNonMatches[index].DeleteEntity();
        else if (_frifloQueryState == QueryFallbackState.Mixed) for (var index = Amount - 1; index >= 0; index--) _frifloQueryEntities[index].DeleteEntity();
        _frifloQueryState = QueryFallbackState.Empty;
    }

    private void RestoreDefaultQueryEmpty()
    {
        if (_defaultQueryState == QueryFallbackState.Created) for (var index = Amount - 1; index >= 0; index--) _defaultQueryCreated[index].Dispose();
        else if (_defaultQueryState == QueryFallbackState.Destroyed) for (var index = Amount - ExpectedMatches - 1; index >= 0; index--) _defaultQueryNonMatches[index].Dispose();
        else if (_defaultQueryState == QueryFallbackState.Mixed) for (var index = Amount - 1; index >= 0; index--) _defaultQueryEntities[index].Dispose();
        _defaultQueryState = QueryFallbackState.Empty;
    }

    private void RestoreLeoQueryEmpty()
    {
        if (_leoQueryState == QueryFallbackState.Created) for (var index = Amount - 1; index >= 0; index--) _leoQueryWorld.DelEntity(_leoQueryCreated[index]);
        else if (_leoQueryState == QueryFallbackState.Destroyed) for (var index = Amount - ExpectedMatches - 1; index >= 0; index--) _leoQueryWorld.DelEntity(_leoQueryNonMatches[index]);
        else if (_leoQueryState == QueryFallbackState.Mixed) for (var index = Amount - 1; index >= 0; index--) _leoQueryWorld.DelEntity(_leoQueryEntities[index]);
        _leoQueryState = QueryFallbackState.Empty;
    }

    private enum QueryFallbackState { Empty, Mixed, Destroyed, Created }
}

internal struct QueryArchA { }
internal struct QueryArchB { }
internal struct QueryArchC { }
internal struct QueryArchK0 { }
internal struct QueryArchK1 { }
internal struct QueryArchK2 { }
internal struct QueryArchK3 { }
internal struct QueryFrifloA : IComponent { }
internal struct QueryFrifloB : IComponent { }
internal struct QueryFrifloC : IComponent { }
internal struct QueryFrifloK0 : IComponent { }
internal struct QueryFrifloK1 : IComponent { }
internal struct QueryFrifloK2 : IComponent { }
internal struct QueryFrifloK3 : IComponent { }
internal struct QueryDefaultA { }
internal struct QueryDefaultB { }
internal struct QueryDefaultC { }
internal struct QueryDefaultK0 { }
internal struct QueryDefaultK1 { }
internal struct QueryDefaultK2 { }
internal struct QueryDefaultK3 { }
internal struct QueryLeoA { }
internal struct QueryLeoB { }
internal struct QueryLeoC { }
internal struct QueryLeoK0 { }
internal struct QueryLeoK1 { }
internal struct QueryLeoK2 { }
internal struct QueryLeoK3 { }
