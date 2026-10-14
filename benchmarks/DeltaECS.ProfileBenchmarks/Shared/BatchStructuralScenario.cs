namespace DeltaECS.ProfileAdapter;

using Delta.ECS;

/// <summary>Isolates public batch create and destroy calls for the paired profiler.</summary>
public sealed class BatchStructuralScenario : IDisposable
{
    private readonly ComponentRegistry _registry;
    private readonly int _amount;
    private World _world;
    private readonly ComponentId _component;
    private readonly Entity[] _entities;
    private readonly Entity[] _shuffledEntities;
    private readonly Entity[] _halfEntities;

    public int AliveEntityCount => _world.AliveEntityCount;

    public BatchStructuralScenario(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (amount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Batch structural scenarios require at least two entities.");
        }

        _amount = amount;
        _registry = new ComponentRegistry();
        _component = _registry.Register<ProfileValue>(new SchemaId(400_001));
        _world = new World(_registry, initialEntityCapacity: amount + 8);
        _entities = new Entity[amount];
        _shuffledEntities = new Entity[amount];
        _halfEntities = new Entity[amount / 2];
    }

    public void PrepareForCreate()
    {
        ClearPreviousEntities();
    }

    public void PrepareForAppendOnlyCreate()
    {
        _world.Dispose();
        _world = new World(_registry, initialEntityCapacity: _amount + 8);
    }

    public int CreateBatch()
        => _world.Create(_component, _entities.Length, _entities);

    public void PrepareForDestroy()
    {
        ClearPreviousEntities();
        _world.Create(_component, _entities.Length, _entities);
        Array.Copy(_entities, _shuffledEntities, _entities.Length);
        new Random(42).Shuffle(_shuffledEntities);
        Array.Copy(_shuffledEntities, _halfEntities, _halfEntities.Length);
    }

    public int DestroyRandomHalf()
        => _world.Destroy(_halfEntities);

    public int DestroyRandomFullBatch()
        => _world.Destroy(_shuffledEntities);

    public void Dispose() => _world.Dispose();

    private void ClearPreviousEntities()
    {
        if (_world.AliveEntityCount != 0)
        {
            _world.Destroy(_entities);
        }
    }
}
