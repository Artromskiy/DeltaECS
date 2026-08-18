namespace DVG.ECS;

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

internal struct EntityRecord
{
    public int Generation;
    public int Archetype;
    public int Chunk;
    public int SlotIndex;
}

public enum QueryAccess
{
    Read,
    Write
}

public readonly struct QueryHandle
{
    private readonly World _owner;
    private readonly CachedQuery _cached;
    private readonly QueryDescription _description;

    internal QueryHandle(World owner, CachedQuery cached, QueryDescription description)
    {
        _owner = owner;
        _cached = cached;
        _description = description;
    }

    internal World Owner => _owner;

    internal CachedQuery Cached => _cached;

    internal QueryDescription Description => _description;

    public bool IsValid => _owner is not null && _cached is not null;
}

public delegate void ChunkAction<TState>(ref TState state, ref DenseChunkLeaseView lease);
