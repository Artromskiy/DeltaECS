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
        ThrowHelper.ThrowIfNegative(componentCount, nameof(componentCount));
        ThrowHelper.ThrowIfNegativeOrZero(capacity, nameof(capacity));
        _capacity = capacity;
        _componentCount = componentCount;
        _values = new NativeMemory<Stamp>(0);
        _uniformStamps = new NativeMemory<Stamp>(componentCount);
        _uniformCounts = new NativeMemory<int>(componentCount);
    }

    internal readonly Stamp Get(int componentIndex, int slotIndex)
    {
        int offset = Offset(componentIndex, slotIndex);
        if (slotIndex < _uniformCounts.ReadOnlySpan.RefAt(componentIndex))
        {
            return _uniformStamps.ReadOnlySpan.RefAt(componentIndex);
        }

        return _values.Length == 0
            ? default
            : _values.ReadOnlySpan.RefAt(offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Stamp GetTrusted(int componentIndex, int slotIndex)
    {
        int offset = (componentIndex * _capacity) + slotIndex;
        if (slotIndex < _uniformCounts.ReadOnlySpan.RefAt(componentIndex))
        {
            return _uniformStamps.ReadOnlySpan.RefAt(componentIndex);
        }

        return _values.Length == 0
            ? default
            : _values.ReadOnlySpan.RefAt(offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp Increment(int componentIndex, int slotIndex)
    {
        int offset = Offset(componentIndex, slotIndex);
        Materialize(componentIndex);
        ref Stamp value = ref _values.RefAt(offset);
        Stamp stamp = value.Next();
        value = stamp;
        return stamp;
    }

    internal void Set(int componentIndex, int slotIndex, Stamp stamp)
    {
        int offset = Offset(componentIndex, slotIndex);
        Materialize(componentIndex);
        _values.RefAt(offset) = stamp;
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
        _uniformStamps.RefAt(componentIndex) = stamp;
        _uniformCounts.RefAt(componentIndex) = count;
    }

    internal void SetSlotRange(int slotIndex, int count, Stamp stamp)
    {
        if (slotIndex < 0 || count < 0 || slotIndex > _capacity - count)
        {
            ThrowHelper.ThrowStampRange(nameof(slotIndex));
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
            SetComponentRange(componentIndices.RefAt(index), slotIndex, count, stamp);
        }
    }

    internal ComponentStampStorage Remap(ReadOnlySpan<int> sourceToTarget, int targetComponentCount)
    {
        if (sourceToTarget.Length != _componentCount)
        {
            ThrowHelper.ThrowStampRange(nameof(sourceToTarget));
        }

        var replacement = new ComponentStampStorage(targetComponentCount, _capacity);
        for (int sourceComponentIndex = 0; sourceComponentIndex < sourceToTarget.Length; sourceComponentIndex++)
        {
            int targetComponentIndex = sourceToTarget.RefAt(sourceComponentIndex);
            if (targetComponentIndex < 0)
            {
                continue;
            }

            int uniformCount = _uniformCounts.RefAt(sourceComponentIndex);
            int copiedTail = _capacity - uniformCount;
            replacement._uniformStamps.RefAt(targetComponentIndex) = _uniformStamps.RefAt(sourceComponentIndex);
            replacement._uniformCounts.RefAt(targetComponentIndex) = uniformCount;
            if (copiedTail != 0 && _values.Length != 0)
            {
                replacement.EnsureValues();
                _values.ReadOnlySpan
                    .Slice((sourceComponentIndex * _capacity) + uniformCount, copiedTail)
                    .CopyTo(replacement._values.Span.Slice(
                        (targetComponentIndex * _capacity) + uniformCount,
                        copiedTail));
            }
        }

        return replacement;
    }

    internal void CopyRemappedTo(
        ref ComponentStampStorage target,
        ReadOnlySpan<int> sourceToTarget,
        int activeCount)
    {
        if (sourceToTarget.Length != _componentCount
            || target._capacity != _capacity
            || activeCount < 0
            || activeCount > _capacity)
        {
            ThrowHelper.ThrowStampRange(nameof(sourceToTarget));
        }

        target._uniformStamps.Clear();
        target._uniformCounts.Clear();
        for (int sourceComponentIndex = 0; sourceComponentIndex < sourceToTarget.Length; sourceComponentIndex++)
        {
            int targetComponentIndex = sourceToTarget.RefAt(sourceComponentIndex);
            if (targetComponentIndex < 0)
            {
                continue;
            }

            int uniformCount = Math.Min(_uniformCounts.RefAt(sourceComponentIndex), activeCount);
            target._uniformStamps.RefAt(targetComponentIndex) = _uniformStamps.RefAt(sourceComponentIndex);
            target._uniformCounts.RefAt(targetComponentIndex) = uniformCount;
            if (uniformCount == activeCount)
            {
                continue;
            }

            int copiedCount = activeCount - uniformCount;
            if (uniformCount == 0 && _values.Length == 0)
            {
                target._uniformStamps.RefAt(targetComponentIndex) = default;
                target._uniformCounts.RefAt(targetComponentIndex) = activeCount;
                continue;
            }

            target.EnsureValues();
            if (_values.Length != 0)
            {
                _values.ReadOnlySpan
                    .Slice((sourceComponentIndex * _capacity) + uniformCount, copiedCount)
                    .CopyTo(target._values.Span.Slice(
                        (targetComponentIndex * _capacity) + uniformCount,
                        copiedCount));
            }
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

    internal void CopyComponentRangeTo(
        ref ComponentStampStorage target,
        int sourceSlotIndex,
        int targetSlotIndex,
        int count,
        int sourceComponentIndex,
        int targetComponentIndex)
    {
        ValidateRange(sourceComponentIndex, sourceSlotIndex, count);
        target.ValidateRange(targetComponentIndex, targetSlotIndex, count);
        if (count == 0)
        {
            return;
        }

        int sourceUniformCount = _uniformCounts.RefAt(sourceComponentIndex);
        int sourceEnd = sourceSlotIndex + count;
        int uniformStart = sourceSlotIndex;
        int uniformEnd = Math.Min(sourceEnd, sourceUniformCount);
        if (uniformEnd > uniformStart)
        {
            target.SetComponentRange(
                targetComponentIndex,
                targetSlotIndex + (uniformStart - sourceSlotIndex),
                uniformEnd - uniformStart,
                _uniformStamps.RefAt(sourceComponentIndex));
        }

        int copiedTailStart = Math.Max(sourceSlotIndex, sourceUniformCount);
        int copiedTailCount = sourceEnd - copiedTailStart;
        if (copiedTailCount <= 0)
        {
            return;
        }

        int targetTailStart = targetSlotIndex + (copiedTailStart - sourceSlotIndex);
        if (_values.Length == 0)
        {
            target.Materialize(targetComponentIndex);
            target._values.Span
                .Slice(
                    (targetComponentIndex * target._capacity) + targetTailStart,
                    copiedTailCount)
                .Clear();
        }
        else
        {
            target.Materialize(targetComponentIndex);
            int sourceOffset = (sourceComponentIndex * _capacity) + copiedTailStart;
            int targetOffset = (targetComponentIndex * target._capacity) + targetTailStart;
            _values.ReadOnlySpan
                .Slice(sourceOffset, copiedTailCount)
                .CopyTo(target._values.Span.Slice(
                    targetOffset,
                    copiedTailCount));
        }
        target._uniformCounts.RefAt(targetComponentIndex) = 0;
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
        int count = _uniformCounts.RefAt(componentIndex);
        EnsureValues();
        if (count == 0)
        {
            return;
        }

        int offset = checked(componentIndex * _capacity);
        _values.Span.Slice(offset, count).Fill(_uniformStamps.RefAt(componentIndex));
        _uniformCounts.RefAt(componentIndex) = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureValues()
    {
        if (_values.Length == 0)
        {
            _values = new NativeMemory<Stamp>(checked(_componentCount * _capacity));
        }
    }

    private readonly int Offset(int componentIndex, int slotIndex)
    {
        if ((uint)componentIndex >= (uint)_componentCount)
        {
            ThrowHelper.ThrowStampRange(nameof(componentIndex));
        }

        ValidateSlot(slotIndex);
        return checked((componentIndex * _capacity) + slotIndex);
    }

    private readonly void ValidateRange(int componentIndex, int slotIndex, int count)
    {
        if ((uint)componentIndex >= (uint)_componentCount)
        {
            ThrowHelper.ThrowStampRange(nameof(componentIndex));
        }

        if (slotIndex < 0 || count < 0 || slotIndex > _capacity - count)
        {
            ThrowHelper.ThrowStampRange(nameof(slotIndex));
        }
    }

    private readonly void ValidateSlot(int slotIndex)
    {
        if ((uint)slotIndex >= (uint)_capacity)
        {
            ThrowHelper.ThrowStampRange(nameof(slotIndex));
        }
    }
}
