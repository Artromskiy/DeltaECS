namespace Delta.ECS;

using System.Collections.Generic;

internal sealed class EntityRecordStorage
{
    private readonly List<EntityRecord> _items = new();

    internal int Count => _items.Count;

    internal int Capacity
    {
        set => _items.Capacity = value;
    }

    internal void Add(EntityRecord value) => _items.Add(value);

    internal ref EntityRecord RefAt(int index) => ref ListSpanCompat.AsSpan(_items)[index];
}
