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

public readonly struct ReadRowAccess
{
}

public readonly struct WriteRowAccess
{
}

public static class RowAccess
{
    public static ReadRowAccess Read => default;

    public static WriteRowAccess Write => default;
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

    public CursorReadBinding<T> CursorBind<T>(ComponentId componentId, ReadRowAccess _)
    {
        return new CursorReadBinding<T>(_cached, ResolveComponentRow<T>(componentId));
    }

    public CursorWriteBinding<T> CursorBind<T>(ComponentId componentId, WriteRowAccess _)
    {
        var rowIndex = ResolveComponentRow<T>(componentId);
        _cached.RegisterWriteBinding();
        return new CursorWriteBinding<T>(_cached, rowIndex);
    }

    private int ResolveComponentRow<T>(ComponentId componentId)
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("Cannot bind a row from an invalid query handle.");
        }

        if (!_description.AllMask.Contains(componentId))
        {
            throw new ArgumentException("A row binding must target a component guaranteed by the query All mask.", nameof(componentId));
        }

        if (!_owner.Layouts.TryGet(componentId, out var layout))
        {
            throw new ArgumentException("The component is not registered in the query's world.", nameof(componentId));
        }

        if (layout.RuntimeType != typeof(T))
        {
            throw new ArgumentException($"Component {componentId} is registered as {layout.RuntimeType}, not {typeof(T)}.", nameof(componentId));
        }

        return _description.AllMask.Rank(componentId);
    }
}

public delegate void QueryCursorAction<TContext>(ref TContext context, ref DenseChunkCursor cursor);
