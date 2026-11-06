namespace Delta.ECS;

using System;
using System.Collections.Generic;

public sealed partial class ComponentRegistry
{
    private ComponentGrouping?[] _groupings = [];
    internal bool HasGrouping => _groupings.Length != 0;

    internal ComponentGrouping? GetGrouping(ComponentId id)
        => (uint)id.Value < (uint)_groupings.Length ? _groupings[id.Value] : null;
}

internal abstract class ComponentGrouping(ComponentId componentId)
{
    internal ComponentId ComponentId { get; } = componentId;
    internal abstract int GetValueId(Array row, int slot);
    internal abstract bool Matches(Array row, int slot, int valueId);
}

internal sealed class ComponentGrouping<T>(ComponentId componentId) : ComponentGrouping(componentId)
{
    // Registration restricts T to value types with IEquatable<T>; default is the zero-valued group.
    private readonly Dictionary<ValueTuple<T>, int> _ids = new() { [new(default!)] = 0 };
    private readonly List<T> _values = new() { default! };

    internal int GetValueId(in T value)
    {
        var key = new ValueTuple<T>(value);
        if (_ids.TryGetValue(key, out int id))
        {
            return id;
        }
        id = _values.Count;
        _ids.Add(key, id);
        _values.Add(value);
        return id;
    }

    internal override int GetValueId(Array row, int slot) => GetValueId(((T[])row)[slot]);
    internal override bool Matches(Array row, int slot, int valueId)
        => EqualityComparer<T>.Default.Equals(((T[])row)[slot], _values[valueId]);
}

internal enum GroupMatchKind { All, Any, None }

internal readonly record struct GroupFilter(ComponentGrouping Grouping, int ValueId, GroupMatchKind Kind);

internal readonly record struct GroupedComponentRow(ComponentGrouping Grouping, int Row, int ValueId);

internal readonly struct GroupedArchetypeKey(ComponentMask mask, int[] values) : IEquatable<GroupedArchetypeKey>
{
    private readonly ComponentMask _mask = mask;
    private readonly int[] _values = values;

    public bool Equals(GroupedArchetypeKey other) => _mask == other._mask && _values.AsSpan().SequenceEqual(other._values);
    public override bool Equals(object? obj) => obj is GroupedArchetypeKey other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_mask);
        foreach (int value in _values)
        {
            hash.Add(value);
        }
        return hash.ToHashCode();
    }
}
