using BenchmarkDotNet.Attributes;
using Delta.ECS;

namespace Delta.ECS.MicroBenchmarks;

/// <summary>Compares caller-owned batch structural operations with one-entity calls.</summary>
public class StructuralOperationsMicroBenchmarkImplementation
{
    public int Amount { get; set; } = MicroBenchmarkConfiguration.CurrentAmount;

    private MicroWorld _fixture = null!;
    private Entity[] _entities = null!;
    private Entity[] _createdEntities = null!;
    private ComponentId[] _baseComponents = null!;
    private ComponentId[] _markerComponents = null!;
    private bool _destroyed;
    private bool _createdAlive;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MicroWorld(chunkCapacity: 512, initialEntityCapacity: Amount * 2);
        _baseComponents = [_fixture.Position];
        _markerComponents = [_fixture.Auxiliary];
        _entities = _fixture.World.Create(_baseComponents, Amount);
        _createdEntities = new Entity[Amount];
    }

    [GlobalCleanup]
    public void Cleanup() => _fixture.World.Dispose();

    [IterationSetup(Target = nameof(AddBatch))]
    public void PrepareAddBatch() => PrepareWithoutMarker();

    [IterationSetup(Target = nameof(AddAtomic))]
    public void PrepareAddAtomic() => PrepareWithoutMarker();

    [IterationSetup(Target = nameof(RemoveBatch))]
    public void PrepareRemoveBatch() => PrepareWithMarker();

    [IterationSetup(Target = nameof(RemoveAtomic))]
    public void PrepareRemoveAtomic() => PrepareWithMarker();

    [IterationSetup(Target = nameof(DestroyBatch))]
    public void PrepareDestroyBatch() => PrepareWithoutMarker();

    [IterationSetup(Target = nameof(DestroyAtomic))]
    public void PrepareDestroyAtomic() => PrepareWithoutMarker();

    [IterationSetup(Target = nameof(CreateBatch))]
    public void PrepareCreateBatch() => PrepareCreate();

    [IterationSetup(Target = nameof(CreateAtomic))]
    public void PrepareCreateAtomic() => PrepareCreate();

    [Benchmark]
    public int AddBatch() => _fixture.World.Add(_markerComponents, _entities);

    [Benchmark]
    public int AddAtomic()
    {
        int changed = 0;
        for (int index = 0; index < _entities.Length; index++)
        {
            if (_fixture.World.Add(_markerComponents, _entities[index]))
            {
                changed++;
            }
        }

        return changed;
    }

    [Benchmark]
    public int RemoveBatch() => _fixture.World.Remove(_markerComponents, _entities);

    [Benchmark]
    public int RemoveAtomic()
    {
        int changed = 0;
        for (int index = 0; index < _entities.Length; index++)
        {
            if (_fixture.World.Remove(_markerComponents, _entities[index]))
            {
                changed++;
            }
        }

        return changed;
    }

    [Benchmark]
    public int DestroyBatch()
    {
        int destroyed = _fixture.World.Destroy(_entities);
        _destroyed = true;
        return destroyed;
    }

    [Benchmark]
    public int DestroyAtomic()
    {
        int destroyed = 0;
        for (int index = 0; index < _entities.Length; index++)
        {
            if (_fixture.World.Destroy(_entities[index]))
            {
                destroyed++;
            }
        }

        _destroyed = true;
        return destroyed;
    }

    [Benchmark]
    public int CreateBatch()
    {
        int created = _fixture.World.Create(_baseComponents, _createdEntities);
        _createdAlive = true;
        return created;
    }

    [Benchmark]
    public int CreateAtomic()
    {
        int created = 0;
        for (int index = 0; index < Amount; index++)
        {
            _createdEntities[index] = _fixture.World.Create(_baseComponents[0]);
            created++;
        }

        _createdAlive = true;
        return created;
    }

    internal int ExpectedCount => Amount;

    internal void PrepareAdd() => PrepareWithoutMarker();

    internal void PrepareRemove() => PrepareWithMarker();

    internal void PrepareDestroy() => PrepareWithoutMarker();

    internal void PrepareCreate() => PrepareCreateState();

    private void PrepareWithoutMarker()
    {
        EnsureEntitiesAlive();
        _ = _fixture.World.Remove(_markerComponents, _entities);
    }

    private void PrepareWithMarker()
    {
        EnsureEntitiesAlive();
        _ = _fixture.World.Add(_markerComponents, _entities);
    }

    private void PrepareCreateState()
    {
        if (_createdAlive)
        {
            _ = _fixture.World.Destroy(_createdEntities);
            _createdAlive = false;
        }
    }

    private void EnsureEntitiesAlive()
    {
        if (!_destroyed)
        {
            return;
        }

        _fixture.World.Create(_baseComponents, _entities);
        _destroyed = false;
    }
}
