namespace DVG.ECS;

using System;
using System.Runtime.CompilerServices;

internal sealed class Chunk
{
    private readonly int _capacity;
    private readonly Array[] _componentRows;
    private readonly uint[] _componentVersions;
    private readonly Entity[] _entities;
    private int _size;

    public Chunk(int capacity, ComponentLayout[] layouts, int globalId)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (layouts.Length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layouts));
        }

        _capacity = capacity;
        GlobalId = globalId;
        _entities = new Entity[capacity];
        _componentRows = new Array[layouts.Length];
        _componentVersions = new uint[layouts.Length];
        for (var index = 0; index < layouts.Length; index++)
        {
            var runtimeType = layouts[index].RuntimeType;
            if (runtimeType is null)
            {
                throw new InvalidOperationException("ArrayRows requires a type-backed component layout. Use Register<T> or RegisterUnmanaged<T>.");
            }

            _componentRows[index] = Array.CreateInstance(runtimeType, capacity);
        }
    }

    public int GlobalId { get; }

    public int Capacity => _capacity;

    public int Size => _size;

    public bool IsFull => _size >= _capacity;

    public bool IsEmpty => _size == 0;

    public Span<Entity> Entities => new(_entities, 0, _size);

    public int Add(Entity entity)
    {
        if (IsFull)
        {
            throw new InvalidOperationException("Chunk is full.");
        }

        var slotIndex = _size++;
        _entities[slotIndex] = entity;
        return slotIndex;
    }

    public Entity RemoveSwapBack(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _size)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        var lastSlotIndex = _size - 1;
        var moved = _entities[lastSlotIndex];
        if (slotIndex < lastSlotIndex)
        {
            _entities[slotIndex] = moved;
            CopySlot(lastSlotIndex, slotIndex);
        }

        _entities[lastSlotIndex] = Entity.Null;
        ClearSlot(lastSlotIndex);
        _size = lastSlotIndex;
        return slotIndex < lastSlotIndex ? moved : Entity.Null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetComponentRow<T>(int componentIndex)
    {
        return ((T[])_componentRows[componentIndex]).AsSpan(0, _size);
    }

    public Array GetRawComponentRow(int componentIndex) => _componentRows[componentIndex];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal uint GetComponentVersion(int componentIndex) => _componentVersions[componentIndex];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkComponentWritten(int componentIndex, uint worldTick) => _componentVersions[componentIndex] = worldTick;

    internal void ClearComponentVersions() => Array.Clear(_componentVersions, 0, _componentVersions.Length);

    public void CopySlotTo(Chunk target, int sourceSlotIndex, int targetSlotIndex, int sourceComponentIndex, int targetComponentIndex)
    {
        Array.Copy(_componentRows[sourceComponentIndex], sourceSlotIndex, target._componentRows[targetComponentIndex], targetSlotIndex, 1);
    }

    public void CopySlot(int sourceSlotIndex, int destinationSlotIndex, int componentIndex)
    {
        if (sourceSlotIndex != destinationSlotIndex)
        {
            Array.Copy(_componentRows[componentIndex], sourceSlotIndex, _componentRows[componentIndex], destinationSlotIndex, 1);
        }
    }

    private void CopySlot(int sourceSlotIndex, int destinationSlotIndex)
    {
        for (var componentIndex = 0; componentIndex < _componentRows.Length; componentIndex++)
        {
            CopySlot(sourceSlotIndex, destinationSlotIndex, componentIndex);
        }
    }

    public void ClearSlot(int slotIndex)
    {
        for (var componentIndex = 0; componentIndex < _componentRows.Length; componentIndex++)
        {
            Array.Clear(_componentRows[componentIndex], slotIndex, 1);
        }
    }
}
