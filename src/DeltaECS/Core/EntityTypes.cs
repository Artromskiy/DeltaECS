namespace Delta.ECS;

using System;

public readonly struct Entity : IEquatable<Entity>
{
    public int Index { get; }

    public int Generation { get; }

    public Entity(int index, int generation)
    {
        Index = index;
        Generation = generation;
    }

    public bool IsAlive => this != Null;

    public bool Equals(Entity other) => Index == other.Index && Generation == other.Generation;

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Index, Generation);

    public static bool operator ==(Entity left, Entity right) => left.Equals(right);

    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);

    public static readonly Entity Null = new(-1, -1);

    public override string ToString() => $"[{Index}:{Generation}]";
}

public readonly struct ArchetypeHandle : IEquatable<ArchetypeHandle>
{
    private readonly World? _owner;
    private readonly int _archetypeId;

    internal ArchetypeHandle(World owner, int archetypeId)
    {
        _owner = owner;
        _archetypeId = archetypeId;
    }

    public int ArchetypeId => _archetypeId;

    public bool IsValid => _owner is not null && _archetypeId >= 0;

    internal World? Owner => _owner;

    public bool Equals(ArchetypeHandle other) => ReferenceEquals(_owner, other._owner) && _archetypeId == other._archetypeId;

    public override bool Equals(object? obj) => obj is ArchetypeHandle other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_owner, _archetypeId);

    public static bool operator ==(ArchetypeHandle left, ArchetypeHandle right) => left.Equals(right);

    public static bool operator !=(ArchetypeHandle left, ArchetypeHandle right) => !left.Equals(right);

    public static readonly ArchetypeHandle Invalid = default;
}

internal struct EntityRecord
{
    internal int Generation;
    internal int Archetype;
    internal int Chunk;
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

    public bool IsValid => _owner is not null && _cached is not null;

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

    private void EnsureValid()
    {
        if (!IsValid)
        {
            ThrowHelper.ThrowInvalidEntityQueryHandle();
        }
    }

}
