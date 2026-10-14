namespace DeltaECS.VersionAdapter;

using Delta.ECS;

public sealed class EntityResolutionScenario
{
    private readonly World _world;
    private readonly Entity[] _entities;
    private readonly ComponentId _marker;

    public EntityResolutionScenario(int amount)
    {
        var layouts = new ComponentRegistry();
        _marker = layouts.Register<Marker>(new SchemaId(949_999));
        _world = new World(layouts, initialEntityCapacity: amount);
        _entities = new Entity[amount];
        _world.Create([_marker], _entities);
        for (int i = 0; i < amount; i++)
        {
            _world.GetRef<Marker>(_entities[i]).Value = i + 1;
        }
    }

    public int ResolveAll(bool shuffled)
    {
        int alive = 0;
        for (int i = 0; i < _entities.Length; i++)
        {
            int index = shuffled ? (int)((long)i * 7_919 % _entities.Length) : i;
            alive += _world.IsAlive(_entities[index]) ? 1 : 0;
        }

        return alive;
    }

    public int HasAll(bool shuffled)
    {
        int found = 0;
        for (int i = 0; i < _entities.Length; i++)
        {
            int index = shuffled ? (int)((long)i * 7_919 % _entities.Length) : i;
            found += _world.Has<Marker>(_entities[index]) ? 1 : 0;
        }

        return found;
    }

    public long GetRefAll(bool shuffled)
    {
        long sum = 0;
        for (int i = 0; i < _entities.Length; i++)
        {
            int index = shuffled ? (int)((long)i * 7_919 % _entities.Length) : i;
            sum += _world.GetRef<Marker>(_entities[index]).Value;
        }

        return sum;
    }

    public long TryGetAll()
    {
        long sum = 0;
        for (int i = 0; i < _entities.Length; i++)
        {
            if (_world.TryGet(_entities[i], _marker, out Marker value)) sum += value.Value;
        }

        return sum;
    }

    public long GetAll()
    {
        long sum = 0;
        for (int i = 0; i < _entities.Length; i++)
        {
            sum += _world.Get<Marker>(_entities[i], _marker).Value;
        }

        return sum;
    }

    private struct Marker { public int Value; }
}
