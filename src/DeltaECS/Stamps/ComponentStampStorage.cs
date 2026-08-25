namespace Delta.ECS;

using System.Runtime.CompilerServices;

internal struct ComponentStampStorage : IDisposable
{
    private readonly int _capacity;
    private readonly int _componentCount;
    private NativeMemory<Stamp> _values;
    private NativeMemory<Stamp> _uniformStamps;
    private NativeMemory<int> _uniformCounts;

    internal ComponentStampStorage(int componentCount, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(componentCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _componentCount = componentCount;
        _values = new NativeMemory<Stamp>(checked(componentCount * capacity));
        _uniformStamps = new NativeMemory<Stamp>(componentCount);
        _uniformCounts = new NativeMemory<int>(componentCount);
    }

    internal readonly Stamp Get(int componentIndex, int slotIndex)
    {
        int offset = Offset(componentIndex, slotIndex);
        return slotIndex < _uniformCounts.ReadOnlySpan[componentIndex]
            ? _uniformStamps.ReadOnlySpan[componentIndex]
            : _values.ReadOnlySpan[offset];
    }

    internal void Set(int componentIndex, int slotIndex, Stamp stamp)
    {
        int offset = Offset(componentIndex, slotIndex);
        Materialize(componentIndex);
        _values[offset] = stamp;
    }

    internal void SetComponentRange(int componentIndex, int slotIndex, int count, Stamp stamp)
    {
        ValidateRange(componentIndex, slotIndex, count);
        if (slotIndex == 0)
        {
            SetComponentPrefixTrusted(componentIndex, count, stamp);
            return;
        }

        Materialize(componentIndex);
        _values.Span.Slice(Offset(componentIndex, slotIndex), count).Fill(stamp);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetComponentPrefixTrusted(int componentIndex, int count, Stamp stamp)
    {
        _uniformStamps[componentIndex] = stamp;
        _uniformCounts[componentIndex] = count;
    }

    internal void SetSlot(int slotIndex, Stamp stamp)
    {
        ValidateSlot(slotIndex);
        for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
        {
            Set(componentIndex, slotIndex, stamp);
        }
    }

    internal void SetSlotRange(int slotIndex, int count, Stamp stamp)
    {
        if (slotIndex < 0 || count < 0 || slotIndex > _capacity - count)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
        {
            SetComponentRange(componentIndex, slotIndex, count, stamp);
        }
    }

    internal void SetRowsRange(int slotIndex, int count, ReadOnlySpan<int> componentIndices, Stamp stamp)
    {
        for (int index = 0; index < componentIndices.Length; index++)
        {
            SetComponentRange(componentIndices[index], slotIndex, count, stamp);
        }
    }

    internal readonly void CopyComponentSlotTo(
        ref ComponentStampStorage target,
        int sourceSlotIndex,
        int targetSlotIndex,
        int sourceComponentIndex,
        int targetComponentIndex)
    {
        target.Set(targetComponentIndex, targetSlotIndex, Get(sourceComponentIndex, sourceSlotIndex));
    }

    internal readonly void CopyComponentRangeTo(
        ref ComponentStampStorage target,
        int sourceSlotIndex,
        int targetSlotIndex,
        int count,
        int sourceComponentIndex,
        int targetComponentIndex)
    {
        ValidateRange(sourceComponentIndex, sourceSlotIndex, count);
        target.ValidateRange(targetComponentIndex, targetSlotIndex, count);
        for (int index = 0; index < count; index++)
        {
            target.Set(
                targetComponentIndex,
                targetSlotIndex + index,
                Get(sourceComponentIndex, sourceSlotIndex + index));
        }
    }

    internal void CopySlot(int sourceSlotIndex, int targetSlotIndex)
    {
        ValidateSlot(sourceSlotIndex);
        ValidateSlot(targetSlotIndex);
        for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
        {
            Set(componentIndex, targetSlotIndex, Get(componentIndex, sourceSlotIndex));
        }
    }

    internal void ClearSlot(int slotIndex)
    {
        ValidateSlot(slotIndex);
        for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
        {
            Set(componentIndex, slotIndex, default);
        }
    }

    internal void ClearRange(int slotIndex, int count)
    {
        if (count == 0)
        {
            return;
        }

        for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
        {
            SetComponentRange(componentIndex, slotIndex, count, default);
        }
    }

    internal void Dispose()
    {
        _values.Dispose();
        _uniformStamps.Dispose();
        _uniformCounts.Dispose();
    }

    void IDisposable.Dispose() => Dispose();

    private void Materialize(int componentIndex)
    {
        int count = _uniformCounts[componentIndex];
        if (count == 0)
        {
            return;
        }

        int offset = checked(componentIndex * _capacity);
        _values.Span.Slice(offset, count).Fill(_uniformStamps[componentIndex]);
        _uniformCounts[componentIndex] = 0;
    }

    private readonly int Offset(int componentIndex, int slotIndex)
    {
        if ((uint)componentIndex >= (uint)_componentCount)
        {
            throw new ArgumentOutOfRangeException(nameof(componentIndex));
        }

        ValidateSlot(slotIndex);
        return checked((componentIndex * _capacity) + slotIndex);
    }

    private readonly void ValidateRange(int componentIndex, int slotIndex, int count)
    {
        if ((uint)componentIndex >= (uint)_componentCount)
        {
            throw new ArgumentOutOfRangeException(nameof(componentIndex));
        }

        if (slotIndex < 0 || count < 0 || slotIndex > _capacity - count)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }
    }

    private readonly void ValidateSlot(int slotIndex)
    {
        if ((uint)slotIndex >= (uint)_capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }
    }
}
