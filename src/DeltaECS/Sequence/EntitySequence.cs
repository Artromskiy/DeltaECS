namespace Delta.ECS;

using System;

/// <summary>
/// An ordered, non-owning view over an explicit entity sequence.
/// </summary>
public readonly ref struct EntitySequence
{
    private readonly World _world;
    private readonly ReadOnlySpan<Entity> _entities;

    internal EntitySequence(World world, ReadOnlySpan<Entity> entities)
    {
        _world = world;
        _entities = entities;
    }

    public int Count => _entities.Length;

    public void ForEach(SequenceAction action) => _world.ForEach(_entities, action);

    public void ForEach<TContext>(ref TContext context, SequenceAction<TContext> action)
        => _world.ForEach(_entities, ref context, action);

    public FilteredEntitySequence Where(in Query query) => new(_world, _entities, query);

    public int Add(ComponentId[] componentIds) => _world.AddComponents(componentIds, _entities);

    public int AddComponents(ComponentId[] componentIds) => Add(componentIds);

    public int Remove(ComponentId[] componentIds) => _world.RemoveComponents(componentIds, _entities);

    public int RemoveComponents(ComponentId[] componentIds) => Remove(componentIds);

    public int Destroy() => _world.DestroyBatch(_entities);
}

/// <summary>
/// An ordered entity sequence narrowed by a prepared query filter.
/// </summary>
public readonly ref struct FilteredEntitySequence
{
    private readonly World _world;
    private readonly ReadOnlySpan<Entity> _entities;
    private readonly Query _query;

    internal FilteredEntitySequence(World world, ReadOnlySpan<Entity> entities, in Query query)
    {
        _world = world;
        _entities = entities;
        _query = query;
    }

    public int Count => _entities.Length;

    public void ForEach(SequenceAction action) => _world.ForEach(_entities, in _query, action);

    public void ForEach<TContext>(ref TContext context, SequenceAction<TContext> action)
        => _world.ForEach(_entities, in _query, ref context, action);

    public int Add(ComponentId[] componentIds) => _world.AddComponents(_entities, in _query, componentIds);

    public int AddComponents(ComponentId[] componentIds) => Add(componentIds);

    public int Remove(ComponentId[] componentIds) => _world.RemoveComponents(_entities, in _query, componentIds);

    public int RemoveComponents(ComponentId[] componentIds) => Remove(componentIds);

    public int Destroy() => _world.Destroy(_entities, in _query);
}
