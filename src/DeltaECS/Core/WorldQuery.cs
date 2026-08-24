namespace Delta.ECS;

/// <summary>
/// Allocation-free query pipeline. Matching is deferred until a terminal
/// operation or <see cref="Open"/> is requested.
/// </summary>
public readonly ref struct WorldQuery
{
    private readonly World _world;
    private readonly QuerySpec _spec;

    internal WorldQuery(World world, QuerySpec spec)
    {
        _world = world;
        _spec = spec;
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public World GeneratedWorld => _world;

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public Query GeneratedQuery => _world.CreateQuery(in _spec);

    public QueryScope Open()
    {
        Query query = GeneratedQuery;
        return new QueryScope(_world, query);
    }

    public int Add(ComponentId[] componentIds)
    {
        Query query = GeneratedQuery;
        return _world.AddComponents(in query, componentIds);
    }

    public int Remove(ComponentId[] componentIds)
    {
        Query query = GeneratedQuery;
        return _world.RemoveComponents(in query, componentIds);
    }

    public int Destroy()
    {
        Query query = GeneratedQuery;
        return _world.Destroy(in query);
    }
}

public sealed partial class World
{
    /// <summary>Starts a deferred query pipeline.</summary>
    public WorldQuery Where(in QuerySpec spec) => new(this, spec);
}
