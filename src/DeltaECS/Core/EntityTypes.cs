namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

/// <summary>Stable handle for an entity slot and its current lifetime generation.</summary>
public readonly struct Entity : IEquatable<Entity>
{
    /// <summary>Gets the entity's index in its world.</summary>
    public int Index { get; }

    /// <summary>Gets the generation that distinguishes this lifetime from reused slots.</summary>
    public int Generation { get; }

    /// <summary>Creates an entity handle from its slot index and generation.</summary>
    /// <param name="index">The entity slot index.</param>
    /// <param name="generation">The lifetime generation.</param>
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

    /// <summary>Determines whether this handle equals another entity handle.</summary>
    public bool Equals(Entity other) => Index == other.Index && Generation == other.Generation;

    /// <summary>Determines whether this handle equals the specified object.</summary>
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    /// <summary>Returns a hash code for this entity handle.</summary>
    public override int GetHashCode() => HashCode.Combine(Index, Generation);

    /// <summary>Determines whether two entity handles are equal.</summary>
    public static bool operator ==(Entity left, Entity right) => left.Equals(right);

    /// <summary>Determines whether two entity handles are different.</summary>
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);

    /// <summary>Returns the index and generation as text.</summary>
    public override string ToString() => $"[{Index}:{Generation}]";
}

internal struct EntityRecord
{
    internal int Generation;
    internal int ChunkId;
    internal int SlotIndex;
}

/// <summary>Cached selection of entities matching a set of component filters.</summary>
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

    /// <summary>Gets whether this query belongs to a live, undisposed world.</summary>
    public bool IsValid => _owner is not null && _cached is not null && !_owner.IsDisposed;

    /// <summary>Extends this query's required component registrations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query WhereAll(ReadOnlySpan<ComponentId> components) => Compose(QuerySpec.WhereAll(components));

    /// <summary>Extends this query's optional component registrations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query WhereAny(ReadOnlySpan<ComponentId> components) => Compose(QuerySpec.WhereAny(components));

    /// <summary>Extends this query's excluded component registrations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query WhereNone(ReadOnlySpan<ComponentId> components) => Compose(QuerySpec.WhereNone(components));

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
