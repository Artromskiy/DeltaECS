namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

internal sealed class Chunk
{
    private readonly int _capacity;
    private readonly Array[] _componentRows;
    private readonly ComponentRowOperations[] _rowOperations;
    private readonly uint[] _componentVersions;
    private readonly Entity[] _entities;
    private int _count;
    private int _highWaterMark;

    public Chunk(
        int capacity,
        ComponentLayout[] layouts,
        ComponentRowOperations[] rowOperations,
        int globalId)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (layouts.Length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layouts));
        }

        if (rowOperations.Length != layouts.Length)
        {
            throw new ArgumentException("Each component row must have cached operations.", nameof(rowOperations));
        }

        _capacity = capacity;
        GlobalId = globalId;
        _entities = new Entity[capacity];
        _componentRows = new Array[layouts.Length];
        _rowOperations = rowOperations;
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

    public int Count => _count;

    public bool IsFull => _count >= _capacity;

    public bool IsEmpty => _count == 0;

    public Span<Entity> Entities => new(_entities, 0, _count);

    public int Add(Entity entity, out bool reusedSlot)
    {
        if (IsFull)
        {
            throw new InvalidOperationException("Chunk is full.");
        }

        var slotIndex = _count++;
        reusedSlot = slotIndex < _highWaterMark;
        if (_count > _highWaterMark)
        {
            _highWaterMark = _count;
        }

        _entities[slotIndex] = entity;
        return slotIndex;
    }

    public int ReserveRange(int count)
    {
        if (count < 0 || _count + count > _capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var start = _count;
        _count += count;
        if (_count > _highWaterMark)
        {
            _highWaterMark = _count;
        }

        return start;
    }

    public Entity RemoveSwapBack(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _count)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        var lastSlotIndex = _count - 1;
        var moved = _entities[lastSlotIndex];
        if (slotIndex < lastSlotIndex)
        {
            _entities[slotIndex] = moved;
            CopySlot(lastSlotIndex, slotIndex);
        }

        _entities[lastSlotIndex] = Entity.Null;
        ClearReferenceRows(lastSlotIndex);
        _count = lastSlotIndex;
        return slotIndex < lastSlotIndex ? moved : Entity.Null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetComponentRow<T>(int componentIndex)
    {
        // Component layout/type compatibility is validated before this
        // internal hot path is reached. Avoid repeating the array cast check
        // for every row requested by every chunk.
        return Unsafe.As<T[]>(_componentRows.Ref(componentIndex)).AsSpan(0, _count);
    }

    public Array GetRawComponentRow(int componentIndex) => _componentRows[componentIndex];

    internal Entity[] RawEntities => _entities;

    internal Array[] RawComponentRows => _componentRows;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<T> GetComponentRow<T>(Array[] componentRows, int componentIndex)
        => Unsafe.As<T[]>(componentRows.Ref(componentIndex)).AsSpan(0, _count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal uint GetComponentVersion(int componentIndex) => _componentVersions.Ref(componentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkComponentWritten(int componentIndex, uint worldTick) => _componentVersions.Ref(componentIndex) = worldTick;

    internal void ClearComponentVersions() => Array.Clear(_componentVersions, 0, _componentVersions.Length);

    public void CopySlotTo(Chunk target, int sourceSlotIndex, int targetSlotIndex, int sourceComponentIndex, int targetComponentIndex)
    {
        _rowOperations[sourceComponentIndex].CopyOne(
            _componentRows[sourceComponentIndex],
            sourceSlotIndex,
            target._componentRows[targetComponentIndex],
            targetSlotIndex);
    }

    public void CopySlot(int sourceSlotIndex, int destinationSlotIndex, int componentIndex)
    {
        if (sourceSlotIndex != destinationSlotIndex)
        {
            _rowOperations[componentIndex].CopyOne(
                _componentRows[componentIndex],
                sourceSlotIndex,
                _componentRows[componentIndex],
                destinationSlotIndex);
        }
    }

    private void CopySlot(int sourceSlotIndex, int destinationSlotIndex)
    {
        for (var componentIndex = 0; componentIndex < _componentRows.Length; componentIndex++)
        {
            CopySlot(sourceSlotIndex, destinationSlotIndex, componentIndex);
        }
    }

    private void ClearReferenceRows(int slotIndex)
    {
        for (var componentIndex = 0; componentIndex < _componentRows.Length; componentIndex++)
        {
            ref readonly var operations = ref _rowOperations[componentIndex];
            if (operations.ContainsReferences)
            {
                operations.ClearOne(_componentRows[componentIndex], slotIndex);
            }
        }
    }

    public void InitializeSlot(int slotIndex)
    {
        for (var componentIndex = 0; componentIndex < _componentRows.Length; componentIndex++)
        {
            _rowOperations[componentIndex].ClearOne(_componentRows[componentIndex], slotIndex);
        }
    }

    public void InitializeRows(int slotIndex, ReadOnlySpan<int> componentIndices)
    {
        for (var index = 0; index < componentIndices.Length; index++)
        {
            var componentIndex = componentIndices[index];
            _rowOperations[componentIndex].ClearOne(_componentRows[componentIndex], slotIndex);
        }
    }

    public void InitializeRowsRange(int slotIndex, int count, ReadOnlySpan<int> componentIndices)
    {
        for (var index = 0; index < componentIndices.Length; index++)
        {
            Array.Clear(_componentRows[componentIndices[index]], slotIndex, count);
        }
    }

    public void ClearAll()
    {
        for (var componentIndex = 0; componentIndex < _componentRows.Length; componentIndex++)
        {
            if (_rowOperations[componentIndex].ContainsReferences)
            {
                Array.Clear(_componentRows[componentIndex], 0, _count);
            }
        }

        Array.Clear(_entities, 0, _count);
        _count = 0;
    }
}
