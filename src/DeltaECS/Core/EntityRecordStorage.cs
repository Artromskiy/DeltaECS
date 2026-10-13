namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

internal sealed class EntityRecordStorage
{
    private EntityRecord[] _items = [];

    internal int Count { get; private set; }

    internal int Capacity
    {
        get => _items.Length;
        set => EnsureCapacity(value);
    }

    internal void EnsureCapacity(int capacity)
    {
        if (capacity > _items.Length)
        {
            Array.Resize(ref _items, capacity);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(EntityRecord value)
    {
        if (Count == _items.Length)
        {
            int capacity = _items.Length == 0
                ? 4
                : checked(_items.Length * 2);
            EnsureCapacity(capacity);
        }

        _items[Count++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<EntityRecord> Append(int count)
    {
        EnsureCapacity(checked(Count + count));
        int start = Count;
        Count += count;
        return _items.AsSpan(start, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref EntityRecord RefAt(int index) => ref _items[index];
}
