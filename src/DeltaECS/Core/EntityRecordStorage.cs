namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

internal sealed class EntityRecordStorage
{
    private EntityRecord[] _items = Array.Empty<EntityRecord>();
    private int _count;

    internal int Count => _count;

    internal int Capacity
    {
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
        if (_count == _items.Length)
        {
            int capacity = _items.Length == 0
                ? 4
                : checked(_items.Length * 2);
            EnsureCapacity(capacity);
        }

        _items[_count++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<EntityRecord> Append(int count)
    {
        EnsureCapacity(checked(_count + count));
        int start = _count;
        _count += count;
        return _items.AsSpan(start, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref EntityRecord RefAt(int index) => ref _items[index];
}
