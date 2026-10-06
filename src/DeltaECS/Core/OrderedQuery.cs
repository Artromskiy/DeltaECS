namespace Delta.ECS;

using System;
using System.ComponentModel;

/// <summary>An immutable ordering of a query, with lexicographic keys added by generated component comparer functors.</summary>
public readonly struct OrderedQuery
{
    private readonly OrderedQueryState? _state;

    internal OrderedQuery(OrderedQueryState state) => _state = state;

    /// <summary>Returns the first entity in ordering order, or <see langword="default"/> if the query has no result.</summary>
    public Entity First() => GetState().First(predicate: null);

    /// <summary>Returns the first entity in ordering order accepted by <paramref name="predicate"/>, or <see langword="default"/>.</summary>
    public Entity First(Func<Entity, bool> predicate)
    {
        ThrowHelper.ThrowIfNull(predicate, nameof(predicate));

        return GetState().First(predicate);
    }

    /// <summary>Returns the first entity in ordering order, or <see langword="default"/> if the query has no result.</summary>
    public Entity FirstEntity() => First();

    /// <summary>Returns the first entity accepted by <paramref name="predicate"/> in ordering order, or <see langword="default"/>.</summary>
    public Entity FirstEntity(Func<Entity, bool> predicate) => First(predicate);

    /// <summary>Gets the source query used to create this ordered view.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Query SourceQuery => GetState().Query;

    /// <summary>Gets the owning world for generated ordered iteration extensions.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public World World => GetState().World;

    /// <summary>Collects and sorts the source query into reusable world-owned storage for generated iteration.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ReadOnlySpan<Entity> BeginForEach() => GetState().BeginForEach();

    /// <summary>Releases the ordered-query execution guard after generated iteration.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void EndForEach() => GetState().World.EndOrderedQueryOperation();

    /// <summary>Creates an ordered view using a generated component comparer.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static OrderedQuery CreateGenerated(Query query, IGeneratedComponentComparer comparer)
    {
        ThrowHelper.ThrowIfNull(comparer, nameof(comparer));
        if (!query.IsValid)
        {
            ThrowHelper.ThrowInvalidEntityQueryHandle();
        }

        World world = query.Owner;
        comparer.Validate(world, in query);
        return new OrderedQuery(new OrderedQueryState(
            query,
            new[] { comparer }));
    }

    /// <summary>Adds a generated component comparer as a lexicographic key.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public OrderedQuery AppendGenerated(IGeneratedComponentComparer comparer)
        => new(GetState().Append(comparer));

    /// <summary>Starts a generated ordered-query operation for a composed predicate view.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void BeginGeneratedOperation()
    {
        OrderedQueryState state = GetState();
        state.World.BeginOrderedQueryOperation();
        state.World.ClearOrderedQueryEntities();
    }

    /// <summary>Adds a predicate-matched entity to generated ordered-query storage.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void AppendGeneratedEntity(Entity entity) => GetState().World.AppendOrderedQueryEntity(entity);

    /// <summary>Sorts entities collected by a generated predicate view.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ReadOnlySpan<Entity> SortGeneratedEntities()
    {
        OrderedQueryState state = GetState();
        return state.World.SortOrderedQueryEntities(state);
    }

    /// <summary>Compares two entities using this ordered query's generated keys.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int CompareGeneratedEntities(Entity left, Entity right) => GetState().Compare(left, right);

    private OrderedQueryState GetState()
        => _state ?? ThrowHelper.ThrowOrderedQueryNotInitialized<OrderedQueryState>();
}

internal sealed class OrderedQueryState
{
    private readonly IGeneratedComponentComparer[] _keys;

    internal OrderedQueryState(Query query, IGeneratedComponentComparer[] keys)
    {
        Query = query;
        _keys = keys;
    }

    internal Query Query { get; }

    internal World World => Query.Owner;

    internal OrderedQueryState Append(IGeneratedComponentComparer comparer)
    {
        ThrowHelper.ThrowIfNull(comparer, nameof(comparer));

        Query query = Query;
        comparer.Validate(World, in query);
        var keys = new IGeneratedComponentComparer[_keys.Length + 1];
        Array.Copy(_keys, keys, _keys.Length);
        keys[^1] = comparer;
        return new OrderedQueryState(Query, keys);
    }

    internal int Compare(Entity left, Entity right)
    {
        for (int index = 0; index < _keys.Length; index++)
        {
            int comparison = _keys[index].Compare(World, left, right);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    internal Entity First(Func<Entity, bool>? predicate)
    {
        World.BeginOrderedQueryOperation();
        try
        {
            Query query = Query;
            ReadOnlySpan<Entity> entities = World.CollectOrderedQueryEntities(in query);
            Entity first = default;
            bool found = false;
            for (int index = 0; index < entities.Length; index++)
            {
                Entity candidate = entities[index];
                if (predicate is not null && !predicate(candidate))
                {
                    continue;
                }

                if (!found || Compare(candidate, first) < 0)
                {
                    first = candidate;
                    found = true;
                }
            }

            return first;
        }
        finally
        {
            World.EndOrderedQueryOperation();
        }
    }

    internal ReadOnlySpan<Entity> BeginForEach()
    {
        World.BeginOrderedQueryOperation();
        try
        {
            Query query = Query;
            World.CollectOrderedQueryEntities(in query);
            return World.SortOrderedQueryEntities(this);
        }
        catch (Exception exception)
        {
            World.EndOrderedQueryOperation();
            ThrowHelper.Rethrow(exception);
            return default;
        }
    }
}
