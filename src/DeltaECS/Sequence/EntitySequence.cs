namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEach(ForEachEntityAction action) => _world.ForEach(_entities, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEach<TContext>(ref TContext context, ForEachContextEntityAction<TContext> action)
        => _world.ForEach(_entities, ref context, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEach<TFunctor>(ref TFunctor functor)
        where TFunctor : struct, IForEachEntity
        => _world.ForEach(_entities, ref functor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEach<TContext, TFunctor>(ref TContext context, ref TFunctor functor)
        where TFunctor : struct, IForEachContextEntity<TContext>
        => _world.ForEach(_entities, ref context, ref functor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilteredEntitySequence Where(in Query query) => new(_world, _entities, query);

    public int Add(ComponentId[] componentIds) => _world.AddComponents(componentIds, _entities);

    public int Remove(ComponentId[] componentIds) => _world.RemoveComponents(componentIds, _entities);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEach(ForEachEntityAction action) => _world.ForEach(_entities, in _query, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEach<TContext>(ref TContext context, ForEachContextEntityAction<TContext> action)
        => _world.ForEach(_entities, in _query, ref context, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEach<TFunctor>(ref TFunctor functor)
        where TFunctor : struct, IForEachEntity
        => _world.ForEach(_entities, in _query, ref functor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEach<TContext, TFunctor>(ref TContext context, ref TFunctor functor)
        where TFunctor : struct, IForEachContextEntity<TContext>
        => _world.ForEach(_entities, in _query, ref context, ref functor);

    public int Add(ComponentId[] componentIds) => _world.AddComponents(_entities, in _query, componentIds);

    public int Remove(ComponentId[] componentIds) => _world.RemoveComponents(_entities, in _query, componentIds);

    public int Destroy() => _world.Destroy(_entities, in _query);
}
