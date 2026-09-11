using ArchComponentType = Arch.Core.Utils.ComponentType;
using BenchmarkDotNet.Attributes;
using DeltaWorld = Delta.ECS.World;

namespace Delta.ECS.MicroBenchmarks;

/// <summary>
/// Compares query batch structural operations with Arch.Core.
/// Arch exposes query batch Add, Remove and Destroy, while Create is scalar-only
/// in the referenced Arch version and is therefore measured as a generic loop.
/// </summary>
public class QueryBatchStructuralOperationsMicroBenchmarkImplementation
{
    public int Amount { get; set; } = MicroBenchmarkConfiguration.CurrentAmount;

    private MicroWorld _deltaFixture = null!;
    private DeltaWorld _deltaWorld = null!;
    private Entity[] _deltaEntities = null!;
    private Entity[] _deltaCreatedEntities = null!;
    private ComponentId[] _deltaBaseComponents = null!;
    private ComponentId[] _deltaMarkerComponents = null!;
    private ComponentId[] _deltaMarkedComponents = null!;
    private Query _deltaBaseQuery;
    private Query _deltaMarkedQuery;
    private bool _deltaDestroyed;
    private bool _deltaCreatedAlive;

    private Arch.Core.World _archWorld = null!;
    private ArchComponentType[] _archBaseTypes = null!;
    private ArchComponentType[] _archMarkedTypes = null!;
    private Arch.Core.QueryDescription _archBaseQuery;
    private Arch.Core.QueryDescription _archMarkedQuery;
    private Arch.Core.Entity[] _archCreatedEntities = null!;
    private Position _archPosition;
    private Auxiliary _archMarker;
    private bool _archDestroyed;
    private bool _archCreatedAlive;

    [GlobalSetup]
    public void Setup()
    {
        // Arch computes 800 entities per chunk for the Position+Auxiliary
        // archetype on its default 16 KB chunk size. Use that capacity here
        // so the structural migration has the same destination chunk count.
        _deltaFixture = new MicroWorld(chunkCapacity: 800, initialEntityCapacity: Amount * 2);
        _deltaWorld = _deltaFixture.World;
        _deltaBaseComponents = [_deltaFixture.Position];
        _deltaMarkerComponents = [_deltaFixture.Auxiliary];
        _deltaMarkedComponents = [_deltaFixture.Position, _deltaFixture.Auxiliary];
        Entity[] markedReserve = _deltaWorld.Create(_deltaMarkedComponents, Amount);
        _ = _deltaWorld.Destroy(markedReserve);
        _deltaEntities = _deltaWorld.Create(_deltaBaseComponents, Amount);
        _deltaCreatedEntities = new Entity[Amount];
        var deltaBaseSpec = QuerySpec.WhereAll(_deltaFixture.Position);
        _deltaBaseQuery = _deltaWorld.CreateQuery(in deltaBaseSpec);
        var deltaMarkedSpec = QuerySpec.WhereAll(_deltaFixture.Position, _deltaFixture.Auxiliary);
        _deltaMarkedQuery = _deltaWorld.CreateQuery(in deltaMarkedSpec);

        _ = _deltaWorld.Add(in _deltaBaseQuery, _deltaMarkerComponents);
        _ = _deltaWorld.Remove(in _deltaMarkedQuery, _deltaMarkerComponents);
        PrepareDeltaWithoutMarker();

        _archWorld = Arch.Core.World.Create();
        _archBaseTypes = [typeof(Position)];
        _archMarkedTypes = [typeof(Position), typeof(Auxiliary)];
        _archWorld.Reserve(_archBaseTypes, Amount);
        _archWorld.Reserve(_archMarkedTypes, Amount);
        _archBaseQuery = new Arch.Core.QueryDescription { All = _archBaseTypes };
        _archMarkedQuery = new Arch.Core.QueryDescription { All = _archMarkedTypes };
        _archCreatedEntities = new Arch.Core.Entity[Amount];
        _archPosition = new Position { X = 1, Y = 2 };
        _archMarker = new Auxiliary { Value = 1 };
        CreateArchBaseEntities();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _deltaWorld.Dispose();
        _archWorld.Dispose();
    }

    [IterationSetup(Target = nameof(DeltaQueryAdd))]
    public void PrepareDeltaQueryAdd() => PrepareDeltaWithoutMarker();

    [IterationSetup(Target = nameof(ArchQueryAdd))]
    public void PrepareArchQueryAdd() => PrepareArchWithoutMarker();

    [IterationSetup(Target = nameof(DeltaQueryRemove))]
    public void PrepareDeltaQueryRemove() => PrepareDeltaWithMarker();

    [IterationSetup(Target = nameof(ArchQueryRemove))]
    public void PrepareArchQueryRemove() => PrepareArchWithMarker();

    [IterationSetup(Target = nameof(DeltaQueryDestroy))]
    public void PrepareDeltaQueryDestroy() => PrepareDeltaWithoutMarker();

    [IterationSetup(Target = nameof(ArchQueryDestroy))]
    public void PrepareArchQueryDestroy() => PrepareArchWithoutMarker();

    [IterationSetup(Target = nameof(DeltaCreateBatch))]
    public void PrepareDeltaCreateBatch() => PrepareDeltaCreate();

    [IterationSetup(Target = nameof(ArchCreateGeneric))]
    public void PrepareArchCreateGeneric() => PrepareArchCreate();

    [Benchmark]
    public int DeltaQueryAdd() => _deltaWorld.Add(in _deltaBaseQuery, _deltaMarkerComponents);

    [Benchmark]
    public int ArchQueryAdd()
    {
        _archWorld.Add<Auxiliary>(in _archBaseQuery, in _archMarker);
        return Amount;
    }

    [Benchmark]
    public int DeltaQueryRemove() => _deltaWorld.Remove(in _deltaMarkedQuery, _deltaMarkerComponents);

    [Benchmark]
    public int ArchQueryRemove()
    {
        _archWorld.Remove<Auxiliary>(in _archMarkedQuery);
        return Amount;
    }

    [Benchmark]
    public int DeltaQueryDestroy()
    {
        int destroyed = _deltaWorld.Destroy(in _deltaBaseQuery);
        _deltaDestroyed = true;
        return destroyed;
    }

    [Benchmark]
    public int ArchQueryDestroy()
    {
        _archWorld.Destroy(in _archBaseQuery);
        _archDestroyed = true;
        return Amount;
    }

    [Benchmark]
    public int DeltaCreateBatch()
    {
        int created = _deltaWorld.Create(_deltaBaseComponents, _deltaCreatedEntities);
        _deltaCreatedAlive = true;
        return created;
    }

    [Benchmark]
    public int ArchCreateGeneric()
    {
        for (int index = 0; index < Amount; index++)
        {
            _archCreatedEntities[index] = _archWorld.Create(in _archPosition);
        }

        _archCreatedAlive = true;
        return Amount;
    }

    internal int ExpectedCount => Amount;

    internal void PrepareDeltaAdd() => PrepareDeltaQueryAdd();

    internal void PrepareArchAdd() => PrepareArchQueryAdd();

    internal void PrepareDeltaRemove() => PrepareDeltaQueryRemove();

    internal void PrepareArchRemove() => PrepareArchQueryRemove();

    internal void PrepareDeltaDestroy() => PrepareDeltaQueryDestroy();

    internal void PrepareArchDestroy() => PrepareArchQueryDestroy();

    internal void PrepareDeltaCreateForSmoke() => PrepareDeltaCreateBatch();

    internal void PrepareArchCreateForSmoke() => PrepareArchCreateGeneric();

    private void PrepareDeltaWithoutMarker()
    {
        EnsureDeltaBaseEntitiesAlive();
        _ = _deltaWorld.Remove(in _deltaMarkedQuery, _deltaMarkerComponents);
        Entity[] markedReserve = _deltaWorld.Create(_deltaMarkedComponents, Amount);
        _ = _deltaWorld.Destroy(markedReserve);
    }

    private void PrepareDeltaWithMarker()
    {
        EnsureDeltaBaseEntitiesAlive();
        _ = _deltaWorld.Add(in _deltaBaseQuery, _deltaMarkerComponents);
    }

    private void PrepareArchWithoutMarker()
    {
        EnsureArchBaseEntitiesAlive();
        _archWorld.Remove<Auxiliary>(in _archMarkedQuery);
    }

    private void PrepareArchWithMarker()
    {
        EnsureArchBaseEntitiesAlive();
        _archWorld.Add<Auxiliary>(in _archBaseQuery, in _archMarker);
    }

    private void PrepareDeltaCreate()
    {
        if (!_deltaCreatedAlive)
        {
            return;
        }

        _ = _deltaWorld.Destroy(_deltaCreatedEntities);
        _deltaCreatedAlive = false;
    }

    private void PrepareArchCreate()
    {
        if (!_archCreatedAlive)
        {
            return;
        }

        for (int index = 0; index < Amount; index++)
        {
            _archWorld.Destroy(_archCreatedEntities[index]);
        }

        _archCreatedAlive = false;
    }

    private void EnsureDeltaBaseEntitiesAlive()
    {
        if (!_deltaDestroyed)
        {
            return;
        }

        _deltaWorld.Create(_deltaBaseComponents, _deltaEntities);
        _deltaDestroyed = false;
    }

    private void EnsureArchBaseEntitiesAlive()
    {
        if (!_archDestroyed)
        {
            return;
        }

        CreateArchBaseEntities();
        _archDestroyed = false;
    }

    private void CreateArchBaseEntities()
    {
        for (int index = 0; index < Amount; index++)
        {
            _ = _archWorld.Create(in _archPosition);
        }
    }
}
