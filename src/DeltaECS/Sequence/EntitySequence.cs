namespace DeltaECS;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// An ordered, non-owning view over an explicit entity sequence.
/// </summary>
public readonly ref partial struct EntitySequence
{
    private readonly World _world;
    private readonly ReadOnlySpan<Entity> _entities;

    internal EntitySequence(World world, ReadOnlySpan<Entity> entities)
    {
        _world = world;
        _entities = entities;
    }

    public int Count => _entities.Length;

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public World GeneratedWorld => _world;

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public ReadOnlySpan<Entity> GeneratedEntities => _entities;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEachEntity(ForEachEntityAction action) => _world.ExecuteSequence(_entities, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEachEntity<TContext>(ref TContext context, ForEachContextEntityAction<TContext> action)
        => _world.ExecuteSequence(_entities, ref context, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilteredEntitySequence Where(in Query query) => new(_world, _entities, query);

    public int Add(ComponentId[] componentIds) => _world.Add(componentIds, _entities);

    public int Remove(ComponentId[] componentIds) => _world.Remove(componentIds, _entities);

    public int Destroy() => _world.Destroy(_entities);
}

/// <summary>
/// An ordered entity sequence narrowed by a prepared query filter.
/// </summary>
public readonly ref partial struct FilteredEntitySequence
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

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public World GeneratedWorld => _world;

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public ReadOnlySpan<Entity> GeneratedEntities => _entities;

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public Query GeneratedQuery => _query;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEachEntity(ForEachEntityAction action) => _world.ExecuteSequence(_entities, in _query, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEachEntity<TContext>(ref TContext context, ForEachContextEntityAction<TContext> action)
        => _world.ExecuteSequence(_entities, in _query, ref context, action);

    public int Add(ComponentId[] componentIds) => _world.Add(_entities, in _query, componentIds);

    public int Remove(ComponentId[] componentIds) => _world.Remove(_entities, in _query, componentIds);

    public int Destroy() => _world.Destroy(_entities, in _query);
}
