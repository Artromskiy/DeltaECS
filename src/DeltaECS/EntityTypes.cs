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

    public bool Equals(ArchetypeHandle other)
    {
        return ReferenceEquals(_owner, other._owner) && _archetypeId == other._archetypeId;
    }

    public override bool Equals(object? obj) => obj is ArchetypeHandle other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_owner, _archetypeId);

    public static bool operator ==(ArchetypeHandle left, ArchetypeHandle right) => left.Equals(right);

    public static bool operator !=(ArchetypeHandle left, ArchetypeHandle right) => !left.Equals(right);

    public static readonly ArchetypeHandle Invalid = default;
}

internal struct EntityRecord
{
    public int Generation;
    public int Archetype;
    public int Chunk;
    public int SlotIndex;
}

public readonly struct ReadAccessMode
{
}

public readonly struct WriteAccessMode
{
}

public static class AccessMode
{
    public static ReadAccessMode Read => default;

    public static WriteAccessMode Write => default;
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

    public AccessRequest Access(ComponentId componentId, ReadAccessMode _)
    {
        var rowIndex = ResolveComponentRow(componentId, expectedType: null, out var componentType);
        return new AccessRequest(_cached, rowIndex, write: false, componentType);
    }

    public AccessRequest Access(ComponentId componentId, WriteAccessMode _)
    {
        var rowIndex = ResolveComponentRow(componentId, expectedType: null, out var componentType);
        _cached.RegisterWriteAccess();
        return new AccessRequest(_cached, rowIndex, write: true, componentType);
    }

    public AccessRequest Access<T>(ComponentId componentId, ReadAccessMode _)
    {
        var access = Access(componentId, _);
        if (access.RuntimeType != typeof(T))
        {
            throw new ArgumentException($"Component {componentId} is registered as {access.RuntimeType}, not {typeof(T)}.", nameof(componentId));
        }

        return access;
    }

    public AccessRequest Access<T>(ComponentId componentId, WriteAccessMode _)
    {
        var access = Access(componentId, _);
        if (access.RuntimeType != typeof(T))
        {
            throw new ArgumentException($"Component {componentId} is registered as {access.RuntimeType}, not {typeof(T)}.", nameof(componentId));
        }

        return access;
    }

    private int ResolveComponentRow(ComponentId componentId, Type? expectedType, out Type componentType)
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("Cannot bind a row from an invalid query handle.");
        }

        if (!_description.AllMask.Contains(componentId))
        {
            throw new ArgumentException("A row access must target a component guaranteed by the query All mask.", nameof(componentId));
        }

        if (!_owner.Layouts.TryGet(componentId, out var layout))
        {
            throw new ArgumentException("The component is not registered in the query's world.", nameof(componentId));
        }

        componentType = layout.RuntimeType!;
        if (expectedType is not null && layout.RuntimeType != expectedType)
        {
            throw new ArgumentException($"Component {componentId} is registered as {layout.RuntimeType}, not {expectedType}.", nameof(componentId));
        }

        return _description.AllMask.Rank(componentId);
    }

}

public delegate void QueryAction<TContext>(ref TContext context, ref QueryChunkCursor cursor);
