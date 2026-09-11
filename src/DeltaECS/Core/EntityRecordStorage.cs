namespace Delta.ECS;

using System.Collections.Generic;
using System.Runtime.CompilerServices;

internal sealed class EntityRecordStorage
{
    private readonly List<EntityRecord> _items = new();

    internal int Count => _items.Count;

    internal int Capacity
    {
        set => _items.Capacity = value;
    }

    internal void EnsureCapacity(int capacity)
    {
        if (capacity > _items.Capacity)
        {
            _items.Capacity = capacity;
        }
    }

    internal void Add(EntityRecord value) => _items.Add(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref EntityRecord RefAt(int index) => ref ListSpanCompat.AsSpan(_items)[index];
}
