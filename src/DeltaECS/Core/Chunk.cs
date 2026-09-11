namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

internal sealed class Chunk
{
    private readonly int _capacity;
    private Array[] _componentRows;
    private ComponentRowOperations[] _rowOperations;
    private NativeMemory<Entity> _entities;
    private ComponentStampStorage _componentStamps;
    private int _archetypeId;
    private int _archetypeIndex;
    private int _count;
    private int _highWaterMark;
    private int _deferredDestroyedRecordCount;

    internal Chunk(
        int capacity,
        ComponentLayout[] layouts,
        ComponentRowOperations[] rowOperations,
        int globalId,
        int archetypeId,
        int archetypeIndex)
    {
        ThrowHelper.ThrowIfNegativeOrZero(capacity, nameof(capacity));
        if (rowOperations.Length != layouts.Length)
        {
            ThrowHelper.ThrowChunkRowOperationsMismatch(nameof(rowOperations));
        }

        _capacity = capacity;
        GlobalId = globalId;
        _archetypeId = archetypeId;
        _archetypeIndex = archetypeIndex;
        _entities = new NativeMemory<Entity>(capacity);
        _componentStamps = new ComponentStampStorage(layouts.Length, capacity);
        _componentRows = new Array[layouts.Length];
        _rowOperations = rowOperations;
        for (int index = 0; index < layouts.Length; index++)
        {
            var runtimeType = layouts.RefAt(index).RuntimeType;
            if (runtimeType is null)
            {
                ThrowHelper.ThrowArrayRowsRequiresRuntimeType();
            }

            _componentRows.RefAt(index) = _rowOperations.RefAt(index).CreateArray(runtimeType, capacity);
        }
    }

    internal int GlobalId { get; }

    internal int ArchetypeId => _archetypeId;

    internal int ArchetypeIndex => _archetypeIndex;

    internal int Capacity => _capacity;

    internal int Count => _count;

    internal int ComponentCount => _componentRows.Length;

    internal bool IsFull => _count >= _capacity;

    internal bool IsEmpty => _count == 0;

    internal int DeferredDestroyedRecordCount => _deferredDestroyedRecordCount;

    internal Span<Entity> Entities => _entities.Span[.._count];

    internal int Add(Entity entity, out bool reusedSlot)
    {
        if (IsFull)
        {
            ThrowHelper.ThrowChunkFull();
        }

        int slotIndex = _count++;
        reusedSlot = slotIndex < _highWaterMark;
        if (_count > _highWaterMark)
        {
            _highWaterMark = _count;
        }

        _entities.RefAt(slotIndex) = entity;
        return slotIndex;
    }

    internal int ReserveRange(int count, out int reusedCount)
    {
        if (count < 0 || _count + count > _capacity)
        {
            ThrowHelper.ThrowChunkCountOutOfRange(nameof(count));
        }

        int start = _count;
        reusedCount = Math.Max(0, Math.Min(count, _highWaterMark - start));
        _count += count;
        if (_count > _highWaterMark)
        {
            _highWaterMark = _count;
        }

        return start;
    }

    internal Entity RemoveSwapBack(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _count)
        {
            ThrowHelper.ThrowChunkSlotOutOfRange(nameof(slotIndex));
        }

        int lastSlotIndex = _count - 1;
        var moved = _entities.RefAt(lastSlotIndex);
        if (slotIndex < lastSlotIndex)
        {
            _entities.RefAt(slotIndex) = moved;
            CopySlot(lastSlotIndex, slotIndex);
            _componentStamps.CopySlot(lastSlotIndex, slotIndex);
        }

        _entities.RefAt(lastSlotIndex) = default;
        ClearReferenceRows(lastSlotIndex);
        _componentStamps.ClearSlot(lastSlotIndex);
        _count = lastSlotIndex;
        return slotIndex < lastSlotIndex ? moved : default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<T> GetComponentRow<T>(int componentIndex) =>
        // Component layout/type compatibility is validated before this
        // internal hot path is reached. Avoid repeating the array cast check
        // for every row requested by every chunk.
        Unsafe.As<T[]>(_componentRows.RefAt(componentIndex)).AsSpan(0, _count);

    internal Array GetRawComponentRow(int componentIndex) => _componentRows.RefAt(componentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Array GetRawComponentRowTrusted(int componentIndex) => _componentRows.RefAt(componentIndex);

    internal Span<Entity> RawEntities => _entities.Span;

    internal Array[] RawComponentRows => _componentRows;

    internal void SetArchetypeLocation(int archetypeId, int archetypeIndex)
    {
        _archetypeId = archetypeId;
        _archetypeIndex = archetypeIndex;
    }

    internal void AdoptLayout(
        ComponentLayout[] layouts,
        ComponentRowOperations[] rowOperations,
        ReadOnlySpan<int> sourceToTarget,
        ReadOnlySpan<int> addedTargetRows)
    {
        if (sourceToTarget.Length != _componentRows.Length)
        {
            ThrowHelper.ThrowChunkRowOperationsMismatch(nameof(sourceToTarget));
        }

        var sourceRows = _componentRows;
        var targetRows = new Array[layouts.Length];
        var copiedRows = new bool[layouts.Length];
        for (int sourceIndex = 0; sourceIndex < sourceToTarget.Length; sourceIndex++)
        {
            int targetIndex = sourceToTarget.RefAt(sourceIndex);
            if (targetIndex < 0)
            {
                continue;
            }

            targetRows[targetIndex] = sourceRows[sourceIndex];
            copiedRows[targetIndex] = true;
        }

        for (int targetIndex = 0; targetIndex < targetRows.Length; targetIndex++)
        {
            if (copiedRows[targetIndex])
            {
                continue;
            }

            Type? runtimeType = layouts.RefAt(targetIndex).RuntimeType;
            if (runtimeType is null)
            {
                ThrowHelper.ThrowArrayRowsRequiresRuntimeType();
            }

            targetRows[targetIndex] = rowOperations.RefAt(targetIndex).CreateArray(runtimeType, _capacity);
        }

        ComponentStampStorage replacement = _componentStamps.Remap(sourceToTarget, targetRows.Length);
        _componentStamps.Dispose();
        _componentStamps = replacement;
        _componentRows = targetRows;
        _rowOperations = rowOperations;
        StampRowsRange(0, _count, addedTargetRows, new Stamp(1));
    }

    internal void AdoptLayoutFromEmptyChunk(
        Chunk donor,
        ComponentLayout[] layouts,
        ComponentRowOperations[] rowOperations,
        ReadOnlySpan<int> sourceToTarget,
        ReadOnlySpan<int> addedTargetRows)
    {
        if (!donor.IsEmpty
            || donor.Capacity != _capacity
            || sourceToTarget.Length != _componentRows.Length
            || donor._componentRows.Length != layouts.Length)
        {
            ThrowHelper.ThrowChunkRowOperationsMismatch(nameof(sourceToTarget));
        }

        Array[] targetRows = donor._componentRows;
        for (int sourceIndex = 0; sourceIndex < sourceToTarget.Length; sourceIndex++)
        {
            int targetIndex = sourceToTarget.RefAt(sourceIndex);
            if (targetIndex >= 0)
            {
                targetRows[targetIndex] = _componentRows.RefAt(sourceIndex);
            }
        }

        _componentStamps.CopyRemappedTo(
            ref donor._componentStamps,
            sourceToTarget,
            _count);
        _componentStamps.Dispose();
        _componentStamps = donor._componentStamps;
        donor._componentStamps = default;
        donor._componentRows = Array.Empty<Array>();
        _componentRows = targetRows;
        _rowOperations = rowOperations;
        StampRowsRange(0, _count, addedTargetRows, new Stamp(1));
    }

    internal void TrimTail(int count)
    {
        if (count < 0 || count > _count)
        {
            ThrowHelper.ThrowChunkCountOutOfRange(nameof(count));
        }

        if (count == 0)
        {
            return;
        }

        int start = _count - count;
        for (int slotIndex = start; slotIndex < _count; slotIndex++)
        {
            ClearReferenceRows(slotIndex);
        }

        _componentStamps.ClearRange(start, count);
        _entities.Span.Slice(start, count).Clear();
        _count = start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<T> GetComponentRow<T>(Array[] componentRows, int componentIndex)
        => Unsafe.As<T[]>(componentRows.RefAt(componentIndex)).AsSpan(0, _count);

    internal void MarkComponentWritten(int componentIndex, int slotIndex, Stamp stamp)
    {
        _componentStamps.Set(componentIndex, slotIndex, stamp);
    }

    internal Stamp GetComponentStamp(int componentIndex, int slotIndex)
        => _componentStamps.Get(componentIndex, slotIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp GetComponentStampTrusted(int componentIndex, int slotIndex)
        => _componentStamps.GetTrusted(componentIndex, slotIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp IncrementComponentStamp(int componentIndex, int slotIndex)
        => _componentStamps.Increment(componentIndex, slotIndex);

    internal void MarkComponentStamped(int componentIndex, int slotIndex, Stamp stamp)
        => _componentStamps.Set(componentIndex, slotIndex, stamp);

    internal void StampAll(int slotIndex, Stamp stamp) => _componentStamps.SetSlot(slotIndex, stamp);

    internal void StampAllRange(int slotIndex, int count, Stamp stamp)
        => _componentStamps.SetSlotRange(slotIndex, count, stamp);

    internal void StampRowsRange(int slotIndex, int count, ReadOnlySpan<int> componentIndices, Stamp stamp)
        => _componentStamps.SetRowsRange(slotIndex, count, componentIndices, stamp);

    internal void CopySlotTo(Chunk target, int sourceSlotIndex, int targetSlotIndex, int sourceComponentIndex, int targetComponentIndex)
    {
        Array.Copy(
            GetRawComponentRowTrusted(sourceComponentIndex),
            sourceSlotIndex,
            target.GetRawComponentRowTrusted(targetComponentIndex),
            targetSlotIndex,
            1);
        _componentStamps.CopyComponentSlotTo(
            ref target._componentStamps,
            sourceSlotIndex,
            targetSlotIndex,
            sourceComponentIndex,
            targetComponentIndex);
    }

    internal void CopyStampRangeTo(
        Chunk target,
        int sourceSlotIndex,
        int targetSlotIndex,
        int count,
        int sourceComponentIndex,
        int targetComponentIndex)
    {
        _componentStamps.CopyComponentRangeTo(
            ref target._componentStamps,
            sourceSlotIndex,
            targetSlotIndex,
            count,
            sourceComponentIndex,
            targetComponentIndex);
    }

    internal void CopySlot(int sourceSlotIndex, int destinationSlotIndex, int componentIndex)
    {
        if (sourceSlotIndex != destinationSlotIndex)
        {
            Array.Copy(
                GetRawComponentRowTrusted(componentIndex),
                sourceSlotIndex,
                GetRawComponentRowTrusted(componentIndex),
                destinationSlotIndex,
                1);
        }
    }

    private void CopySlot(int sourceSlotIndex, int destinationSlotIndex)
    {
        for (int componentIndex = 0; componentIndex < _componentRows.Length; componentIndex++)
        {
            CopySlot(sourceSlotIndex, destinationSlotIndex, componentIndex);
        }
    }

    private void ClearReferenceRows(int slotIndex)
    {
        for (int componentIndex = 0; componentIndex < _componentRows.Length; componentIndex++)
        {
            ref readonly var operations = ref _rowOperations.RefAt(componentIndex);
            if (operations.ContainsReferences)
            {
                Array.Clear(_componentRows.RefAt(componentIndex), slotIndex, 1);
            }
        }
    }

    internal void InitializeSlot(int slotIndex)
        => InitializeSlotRange(slotIndex, 1);

    internal void InitializeSlotRange(int slotIndex, int count)
    {
        if (count == 0)
        {
            return;
        }

        for (int componentIndex = 0; componentIndex < _componentRows.Length; componentIndex++)
        {
            Array.Clear(_componentRows.RefAt(componentIndex), slotIndex, count);
        }
    }

    internal void InitializeRows(int slotIndex, ReadOnlySpan<int> componentIndices)
    {
        for (int index = 0; index < componentIndices.Length; index++)
        {
            int componentIndex = componentIndices.RefAt(index);
            Array.Clear(_componentRows.RefAt(componentIndex), slotIndex, 1);
        }
    }

    internal void InitializeRowsRange(int slotIndex, int count, ReadOnlySpan<int> componentIndices)
    {
        for (int index = 0; index < componentIndices.Length; index++)
        {
            Array.Clear(_componentRows.RefAt(componentIndices.RefAt(index)), slotIndex, count);
        }
    }

    internal void ClearAll()
    {
        for (int componentIndex = 0; componentIndex < _componentRows.Length; componentIndex++)
        {
            if (_rowOperations.RefAt(componentIndex).ContainsReferences)
            {
                Array.Clear(_componentRows.RefAt(componentIndex), 0, _count);
            }
        }

        _componentStamps.ClearRange(0, _count);
        _count = 0;
    }

    internal void DeferDestroyedRecords(int count)
    {
        if (count < 0 || count > _capacity || _deferredDestroyedRecordCount != 0)
        {
            ThrowHelper.ThrowChunkCountOutOfRange(nameof(count));
        }

        _deferredDestroyedRecordCount = count;
    }

    internal int TakeDeferredDestroyedRecords()
    {
        int count = _deferredDestroyedRecordCount;
        _deferredDestroyedRecordCount = 0;
        return count;
    }

    internal void Dispose()
    {
        _componentStamps.Dispose();
        _entities.Dispose();
    }
}
