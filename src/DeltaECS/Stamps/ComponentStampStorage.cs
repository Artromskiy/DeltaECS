namespace Delta.ECS;

internal struct ComponentStampStorage : IDisposable
{
    private readonly int _capacity;
    private readonly int _componentCount;
    private NativeMemory<Stamp> _values;

    public ComponentStampStorage(int componentCount, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(componentCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _componentCount = componentCount;
        _values = new NativeMemory<Stamp>(checked(componentCount * capacity));
    }

    public readonly Stamp Get(int componentIndex, int slotIndex)
        => _values[Offset(componentIndex, slotIndex)];

    public void Set(int componentIndex, int slotIndex, Stamp stamp)
        => _values[Offset(componentIndex, slotIndex)] = stamp;

    public void SetComponentRange(int componentIndex, int slotIndex, int count, Stamp stamp)
    {
        ValidateRange(componentIndex, slotIndex, count);
        _values.Span.Slice(Offset(componentIndex, slotIndex), count).Fill(stamp);
    }

    public void SetSlot(int slotIndex, Stamp stamp)
    {
        ValidateSlot(slotIndex);
        for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
        {
            _values[Offset(componentIndex, slotIndex)] = stamp;
        }
    }

    public void SetSlotRange(int slotIndex, int count, Stamp stamp)
    {
        if (slotIndex < 0 || count < 0 || slotIndex > _capacity - count)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
        {
            _values.Span.Slice(Offset(componentIndex, slotIndex), count).Fill(stamp);
        }
    }

    public void SetRowsRange(int slotIndex, int count, ReadOnlySpan<int> componentIndices, Stamp stamp)
    {
        for (int index = 0; index < componentIndices.Length; index++)
        {
            SetComponentRange(componentIndices[index], slotIndex, count, stamp);
        }
    }

    public readonly void CopyComponentSlotTo(
        ref ComponentStampStorage target,
        int sourceSlotIndex,
        int targetSlotIndex,
        int sourceComponentIndex,
        int targetComponentIndex)
    {
        target.Set(targetComponentIndex, targetSlotIndex, Get(sourceComponentIndex, sourceSlotIndex));
    }

    public readonly void CopyComponentRangeTo(
        ref ComponentStampStorage target,
        int sourceSlotIndex,
        int targetSlotIndex,
        int count,
        int sourceComponentIndex,
        int targetComponentIndex)
    {
        ValidateRange(sourceComponentIndex, sourceSlotIndex, count);
        target.ValidateRange(targetComponentIndex, targetSlotIndex, count);
        _values.ReadOnlySpan.Slice(Offset(sourceComponentIndex, sourceSlotIndex), count)
            .CopyTo(target._values.Span.Slice(target.Offset(targetComponentIndex, targetSlotIndex), count));
    }

    public void CopySlot(int sourceSlotIndex, int targetSlotIndex)
    {
        ValidateSlot(sourceSlotIndex);
        ValidateSlot(targetSlotIndex);
        for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
        {
            Set(componentIndex, targetSlotIndex, Get(componentIndex, sourceSlotIndex));
        }
    }

    public void ClearSlot(int slotIndex)
    {
        ValidateSlot(slotIndex);
        for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
        {
            _values[Offset(componentIndex, slotIndex)] = default;
        }
    }

    public void ClearRange(int slotIndex, int count)
    {
        if (count == 0)
        {
            return;
        }

        for (int componentIndex = 0; componentIndex < _componentCount; componentIndex++)
        {
            ValidateRange(componentIndex, slotIndex, count);
            _values.Span.Slice(Offset(componentIndex, slotIndex), count).Clear();
        }
    }

    public void Dispose() => _values.Dispose();

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
