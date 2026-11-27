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
        set => Array.Resize(ref _items, value);
    }

    internal void Clear()
    {
        _items = [];
        Count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(EntityRecord value) => _items[Count++] = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<EntityRecord> Append(int count)
    {
        Span<EntityRecord> records = _items.AsSpan(Count, count);
        Count += count;
        return records;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref EntityRecord RefAt(int index) => ref _items[index];
}
