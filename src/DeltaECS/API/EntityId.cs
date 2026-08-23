namespace Delta.ECS;

using System;

/// <summary>Identifies one entity generation within a world.</summary>
public readonly struct EntityId : IEquatable<EntityId>
{
    public EntityId(uint index, uint generation)
    {
        Index = index;
        Generation = generation;
    }

    public uint Index { get; }

    public uint Generation { get; }

    public bool Equals(EntityId other) => Index == other.Index && Generation == other.Generation;

    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Index, Generation);

    public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);

    public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);

    public override string ToString() => $"[{Index}:{Generation}]";
}
