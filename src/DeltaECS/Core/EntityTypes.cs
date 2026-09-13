namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

public readonly struct Entity : IEquatable<Entity>
{
    public int Index { get; }

    public int Generation { get; }

    public Entity(int index, int generation)
    {
        Index = index;
        Generation = generation;
    }

    /// <summary>
    /// Gets whether this handle has a non-negative index and a positive generation.
    /// </summary>
    /// <remarks>
    /// An entity handle does not own a reference to a world and therefore
    /// cannot determine whether it is currently alive. Use
    /// <see cref="World.IsAlive(Entity)"/> for a world-specific liveness check.
    /// </remarks>
    public bool IsValid => Index >= 0 && Generation > 0;

    public bool Equals(Entity other) => Index == other.Index && Generation == other.Generation;

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Index, Generation);

    public static bool operator ==(Entity left, Entity right) => left.Equals(right);

    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);

    public override string ToString() => $"[{Index}:{Generation}]";
}

internal struct EntityRecord
{
    internal int Generation;
    internal int ChunkId;
    internal int SlotIndex;
}

public readonly struct Query
{
    private readonly World _owner;
    private readonly QueryPlan _cached;
    private readonly QuerySpec _description;

    internal Query(World owner, QueryPlan cached, QuerySpec spec)
    {
        _owner = owner;
        _cached = cached;
        _description = spec;
    }

    internal World Owner => _owner;

    internal QueryPlan Cached => _cached;

    internal QuerySpec Description => _description;

    public bool IsValid => _owner is not null && _cached is not null && !_owner.IsDisposed;

    /// <summary>Extends this query's required component registrations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query WhereAll(params ReadOnlySpan<ComponentId> components)
        => Compose(QuerySpec.WhereAll(components));

    /// <summary>Extends this query's optional component registrations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query WhereAny(params ReadOnlySpan<ComponentId> components)
        => Compose(QuerySpec.WhereAny(components));

    /// <summary>Extends this query's excluded component registrations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query WhereNone(params ReadOnlySpan<ComponentId> components)
        => Compose(QuerySpec.WhereNone(components));

    public ReadAccess AccessRead(ComponentId componentId)
    {
        EnsureValid();
        int rowIndex = _cached.ResolveReadRoute(componentId);
        return new ReadAccess(_cached, rowIndex);
    }

    public WriteAccess AccessWrite(ComponentId componentId)
    {
        EnsureValid();
        int rowIndex = _cached.UpgradeReadRouteToWrite(_cached.ResolveReadRoute(componentId));
        return new WriteAccess(_cached, rowIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Query Compose(QuerySpec additions)
    {
        EnsureValid();
        QuerySpec composed = _description.Compose(in additions);
        return _owner.CreateQuery(in composed);
    }

    private void EnsureValid()
    {
        if (!IsValid)
        {
            ThrowHelper.ThrowInvalidEntityQueryHandle();
        }
    }

}
