namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal sealed class Chunk
{
    internal const int Capacity = 1024;
    internal const int WordCount = (Capacity + 63) >> 6;
    private readonly ComponentRowArrayPool _componentRowArrayPool;
    private Archetype _archetype;
    private ComponentRowOperations[] _rowOperations;
    private NativeMemory<Entity> _entities;
    private ChunkTagMasks? _tagMasks;
    private int _highWaterMark;
    private int _freeRecordBlockCount;
    private int _freeRecordBlockNextSlot;

    internal int GlobalId { get; }

    internal Archetype Archetype => _archetype;

    internal int ArchetypeId { get; private set; }

    internal int ArchetypeIndex { get; private set; }

    internal int Count { get; private set; }

    internal int ComponentCount => RawComponentRows.Length;

    internal bool IsFull => Count >= Capacity;

    internal bool IsEmpty => Count == 0;

    internal bool HasFreeRecordBlock => _freeRecordBlockCount != 0;

    internal int FreeRecordBlockCount => _freeRecordBlockCount;

    internal int FreeRecordListIndex { get; private set; } = -1;

    internal Span<Entity> RawEntities => _entities.Span;

    internal nint EntityAddress => _entities.Address;

    internal Array[] RawComponentRows { get; private set; }

    internal Chunk(
        Archetype archetype,
        ComponentLayout[] layouts,
        ComponentRowOperations[] rowOperations,
        int globalId,
        int archetypeId,
        int archetypeIndex,
        ComponentRowArrayPool componentRowArrayPool)
    {
        if (rowOperations.Length != layouts.Length)
        {
            ThrowHelper.ThrowChunkRowOperationsMismatch(nameof(rowOperations));
        }

        _componentRowArrayPool = componentRowArrayPool;
        _archetype = archetype;
        GlobalId = globalId;
        ArchetypeId = archetypeId;
        ArchetypeIndex = archetypeIndex;
        _entities = new NativeMemory<Entity>(Capacity);
        RawComponentRows = _componentRowArrayPool.RentRowReferences(layouts.Length);
        _rowOperations = rowOperations;
        for (int index = 0; index < layouts.Length; index++)
        {
            Type runtimeType = layouts.RefAt(index).RuntimeType;
            RawComponentRows.RefAt(index) = _componentRowArrayPool.Rent(
                runtimeType,
                _rowOperations.RefAt(index),
                Capacity);
        }
    }

    internal void EnsureTagCapacity(int tagCount)
    {
        if (tagCount != 0)
        {
            (_tagMasks ??= new ChunkTagMasks()).EnsureTagCapacity(tagCount);
        }
    }

    internal bool HasTag(int tagIndex, int slotIndex) => _tagMasks?.Contains(tagIndex, slotIndex) ?? false;

    internal bool SetTag(int tagIndex, int slotIndex) => (_tagMasks ??= new ChunkTagMasks()).Set(tagIndex, slotIndex);

    internal bool ClearTag(int tagIndex, int slotIndex) => _tagMasks?.Clear(tagIndex, slotIndex) ?? false;

    internal int ApplyTags(ReadOnlySpan<int> tagIndices, int slotIndex, int count, bool isAdd)
    {
        if (tagIndices.IsEmpty || count == 0)
        {
            return 0;
        }

        int changedEntities = 0;
        for (int offset = 0; offset < count; offset++)
        {
            bool changed = false;
            for (int index = 0; index < tagIndices.Length; index++)
            {
                changed |= isAdd
                    ? SetTag(tagIndices.RefAt(index), slotIndex + offset)
                    : ClearTag(tagIndices.RefAt(index), slotIndex + offset);
            }

            if (changed)
            {
                changedEntities++;
            }
        }

        return changedEntities;
    }

    internal ulong GetTagSummary(int tagIndex) => _tagMasks?.Summary(tagIndex) ?? 0;

    internal ulong GetTagWord(int tagIndex, int wordIndex) => _tagMasks?.Word(tagIndex, wordIndex) ?? 0;

    internal void SetFreeRecordListIndex(int index) => FreeRecordListIndex = index;

    internal int Add(Entity entity, out bool reusedSlot)
    {
        if (IsFull)
        {
            ThrowHelper.ThrowChunkFull();
        }

        int slotIndex = Count++;
        reusedSlot = slotIndex < _highWaterMark;
        if (Count > _highWaterMark)
        {
            _highWaterMark = Count;
        }

        _entities.RefAt(slotIndex) = entity;
        return slotIndex;
    }

    internal int ReserveRange(int count, out int reusedCount)
    {
        if (count < 0 || Count + count > Capacity)
        {
            ThrowHelper.ThrowChunkCountOutOfRange(nameof(count));
        }

        int start = Count;
        reusedCount = Math.Max(0, Math.Min(count, _highWaterMark - start));
        Count += count;
        if (Count > _highWaterMark)
        {
            _highWaterMark = Count;
        }

        return start;
    }

    internal Entity RemoveSwapBack(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= Count)
        {
            ThrowHelper.ThrowChunkSlotOutOfRange(nameof(slotIndex));
        }

        int lastSlotIndex = Count - 1;
        var moved = _entities.RefAt(lastSlotIndex);
        if (slotIndex < lastSlotIndex)
        {
            _entities.RefAt(slotIndex) = moved;
            CopySlot(lastSlotIndex, slotIndex);
            _tagMasks?.CopySlotTo(_tagMasks, lastSlotIndex, slotIndex);
        }

        _entities.RefAt(lastSlotIndex) = default;
        ClearReferenceRows(lastSlotIndex);
        _tagMasks?.ClearRange(lastSlotIndex, 1);
        Count = lastSlotIndex;
        return slotIndex < lastSlotIndex ? moved : default;
    }

    // Component layout/type compatibility is validated before these accessors run.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<T> GetComponentRow<T>(int componentIndex)
    {
        ref T first = ref Unsafe.As<T[]>(RawComponentRows.RefAt(componentIndex)).GetRefAtZero();
        return MemoryMarshal.CreateSpan(ref first, Count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref T GetComponentRef<T>(int componentIndex, int slotIndex) =>
        ref Unsafe.Add(
            ref Unsafe.As<T[]>(RawComponentRows.RefAt(componentIndex)).GetRefAtZero(),
            slotIndex);

    internal Array GetRawComponentRow(int componentIndex) => RawComponentRows.RefAt(componentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Array GetRawComponentRowTrusted(int componentIndex) => RawComponentRows.RefAt(componentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool ComponentRowContainsReferences(int componentIndex)
        => _rowOperations.RefAt(componentIndex).ContainsReferences;

    internal void SetArchetypeLocation(Archetype archetype, int archetypeIndex)
    {
        _archetype = archetype;
        ArchetypeId = archetype.Id;
        ArchetypeIndex = archetypeIndex;
    }

    internal void DetachArchetype()
    {
        ArchetypeId = -1;
        ArchetypeIndex = -1;
    }

    internal void AdoptLayout(
        ComponentLayout[] layouts,
        ComponentRowOperations[] rowOperations,
        ReadOnlySpan<int> sourceToTarget)
    {
        if (sourceToTarget.Length != RawComponentRows.Length)
        {
            ThrowHelper.ThrowChunkRowOperationsMismatch(nameof(sourceToTarget));
        }

        var sourceRows = RawComponentRows;
        var targetRows = _componentRowArrayPool.RentRowReferences(layouts.Length);
        for (int sourceIndex = 0; sourceIndex < sourceToTarget.Length; sourceIndex++)
        {
            int targetIndex = sourceToTarget.RefAt(sourceIndex);
            if (targetIndex < 0)
            {
                _componentRowArrayPool.Return(sourceRows[sourceIndex]);
                continue;
            }

            targetRows[targetIndex] = sourceRows[sourceIndex];
        }

        for (int targetIndex = 0; targetIndex < targetRows.Length; targetIndex++)
        {
            if (targetRows[targetIndex] is not null)
            {
                continue;
            }

            Type runtimeType = layouts.RefAt(targetIndex).RuntimeType;
            targetRows[targetIndex] = _componentRowArrayPool.Rent(
                runtimeType,
                rowOperations.RefAt(targetIndex),
                Capacity);
        }

        _componentRowArrayPool.ReturnRowReferences(sourceRows);
        RawComponentRows = targetRows;
        _rowOperations = rowOperations;
    }

    internal void AdoptLayoutFromEmptyChunk(
        Chunk donor,
        ComponentLayout[] layouts,
        ComponentRowOperations[] rowOperations,
        ReadOnlySpan<int> sourceToTarget)
    {
        if (!donor.IsEmpty
            || sourceToTarget.Length != RawComponentRows.Length
            || donor.RawComponentRows.Length != layouts.Length)
        {
            ThrowHelper.ThrowChunkRowOperationsMismatch(nameof(sourceToTarget));
        }

        Array[] targetRows = donor.RawComponentRows;
        for (int sourceIndex = 0; sourceIndex < sourceToTarget.Length; sourceIndex++)
        {
            int targetIndex = sourceToTarget.RefAt(sourceIndex);
            if (targetIndex >= 0)
            {
                _componentRowArrayPool.Return(targetRows[targetIndex]);
                targetRows[targetIndex] = RawComponentRows.RefAt(sourceIndex);
            }
            else
            {
                _componentRowArrayPool.Return(RawComponentRows.RefAt(sourceIndex));
            }
        }

        _componentRowArrayPool.ReturnRowReferences(RawComponentRows);
        donor.RawComponentRows = [];
        RawComponentRows = targetRows;
        _rowOperations = rowOperations;
    }

    internal void TrimTail(int count)
    {
        if (count < 0 || count > Count)
        {
            ThrowHelper.ThrowChunkCountOutOfRange(nameof(count));
        }

        if (count == 0)
        {
            return;
        }

        int start = Count - count;
        for (int slotIndex = start; slotIndex < Count; slotIndex++)
        {
            ClearReferenceRows(slotIndex);
        }

        _tagMasks?.ClearRange(start, count);
        _entities.Span.Slice(start, count).Clear();
        Count = start;
    }

    internal void CopySlotTo(Chunk target, int sourceSlotIndex, int targetSlotIndex, int sourceComponentIndex, int targetComponentIndex)
    {
        Array.Copy(
            GetRawComponentRowTrusted(sourceComponentIndex),
            sourceSlotIndex,
            target.GetRawComponentRowTrusted(targetComponentIndex),
            targetSlotIndex,
            1);
    }

    internal void CopyTagsTo(Chunk target, int sourceSlotIndex, int targetSlotIndex)
    {
        target.ClearTagRange(targetSlotIndex, 1);
        if (_tagMasks is { } source)
        {
            target.EnsureTagCapacity(source.TagCapacity);
            source.CopySlotTo(target._tagMasks!, sourceSlotIndex, targetSlotIndex);
        }
    }

    internal void CopyTagsRangeTo(Chunk target, int sourceSlotIndex, int targetSlotIndex, int count)
    {
        target.ClearTagRange(targetSlotIndex, count);
        if (_tagMasks is { } source)
        {
            target.EnsureTagCapacity(source.TagCapacity);
            source.CopyRangeTo(target._tagMasks!, sourceSlotIndex, targetSlotIndex, count);
        }
    }

    internal void CopyTagsWithin(int sourceSlotIndex, int targetSlotIndex, int count)
        => _tagMasks?.CopyRangeTo(_tagMasks, sourceSlotIndex, targetSlotIndex, count);

    internal void ClearTagRange(int slotIndex, int count) => _tagMasks?.ClearRange(slotIndex, count);

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

    internal void InitializeSlotRange(int slotIndex, int count)
    {
        if (count == 0)
        {
            return;
        }

        for (int componentIndex = 0; componentIndex < RawComponentRows.Length; componentIndex++)
        {
            Array.Clear(RawComponentRows.RefAt(componentIndex), slotIndex, count);
        }
    }

    internal void InitializeRows(int slotIndex, ReadOnlySpan<int> componentIndices)
    {
        for (int index = 0; index < componentIndices.Length; index++)
        {
            int componentIndex = componentIndices.RefAt(index);
            Array.Clear(RawComponentRows.RefAt(componentIndex), slotIndex, 1);
        }
    }

    internal void InitializeRowsRange(int slotIndex, int count, ReadOnlySpan<int> componentIndices)
    {
        for (int index = 0; index < componentIndices.Length; index++)
        {
            Array.Clear(RawComponentRows.RefAt(componentIndices.RefAt(index)), slotIndex, count);
        }
    }

    internal void ClearAll()
    {
        for (int componentIndex = 0; componentIndex < RawComponentRows.Length; componentIndex++)
        {
            if (_rowOperations.RefAt(componentIndex).ContainsReferences)
            {
                Array.Clear(RawComponentRows.RefAt(componentIndex), 0, Count);
            }
        }

        _tagMasks?.ClearAll();
        Count = 0;
    }

    internal void BeginFreeRecordBlock(int count)
    {
        if (count <= 0 || count > Capacity || HasFreeRecordBlock)
        {
            ThrowHelper.ThrowChunkCountOutOfRange(nameof(count));
        }

        _freeRecordBlockCount = count;
        _freeRecordBlockNextSlot = 0;
    }

    internal int TakeFreeRecordIndex()
    {
        if (!HasFreeRecordBlock)
        {
            ThrowHelper.ThrowChunkCountOutOfRange(nameof(_freeRecordBlockCount));
        }

        int recordIndex = _entities.RefAt(_freeRecordBlockNextSlot++).Index;
        _freeRecordBlockCount--;
        return recordIndex;
    }

    internal bool TryTakeFreeRecordForSlot(int slotIndex, out int recordIndex)
    {
        if (!HasFreeRecordBlock || slotIndex != _freeRecordBlockNextSlot)
        {
            recordIndex = -1;
            return false;
        }

        recordIndex = _entities.RefAt(_freeRecordBlockNextSlot++).Index;
        _freeRecordBlockCount--;
        return true;
    }

    internal void Dispose()
    {
        for (int componentIndex = 0; componentIndex < RawComponentRows.Length; componentIndex++)
        {
            _componentRowArrayPool.Return(RawComponentRows.RefAt(componentIndex));
        }

        _componentRowArrayPool.ReturnRowReferences(RawComponentRows);
        RawComponentRows = [];
        _entities.Dispose();
    }

    private void CopySlot(int sourceSlotIndex, int destinationSlotIndex)
    {
        for (int componentIndex = 0; componentIndex < RawComponentRows.Length; componentIndex++)
        {
            CopySlot(sourceSlotIndex, destinationSlotIndex, componentIndex);
        }
    }

    private void ClearReferenceRows(int slotIndex)
    {
        for (int componentIndex = 0; componentIndex < RawComponentRows.Length; componentIndex++)
        {
            ref readonly var operations = ref _rowOperations.RefAt(componentIndex);
            if (operations.ContainsReferences)
            {
                Array.Clear(RawComponentRows.RefAt(componentIndex), slotIndex, 1);
            }
        }
    }
}
