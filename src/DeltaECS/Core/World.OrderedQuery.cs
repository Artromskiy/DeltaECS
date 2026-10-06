namespace Delta.ECS;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public sealed partial class World
{
    private Entity[] _orderedEntityBuffer = Array.Empty<Entity>();
    private Entity[] _orderedEntityMergeBuffer = Array.Empty<Entity>();
    private int _orderedEntityCount;
    private bool _orderedQueryOperationActive;

    /// <summary>Validates a key registration selected by a generated ordered-query comparer.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void ValidateGeneratedOrderedQueryKey<T>(in Query query, ComponentId componentId)
        => ValidateOrderedQueryKey<T>(in query, componentId);

    /// <summary>Reads a component value selected by a generated ordered-query comparer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref readonly T GetGeneratedOrderedQueryKey<T>(Entity entity, ComponentId componentId)
        => ref GetReadRefUnchecked<T>(entity, componentId);

    internal void ValidateOrderedQueryKey<T>(in Query query, ComponentId componentId)
    {
        EnsureExecutionAccess();
        if (!query.IsValid || query.Owner != this)
        {
            ThrowHelper.ThrowInvalidEntityQueryHandle();
        }

        EnsureRegisteredType<T>(componentId);
        if (!query.Description.AllMask.Contains(componentId))
        {
            ThrowHelper.ThrowOrderedQueryKeyMustBeRequired(componentId);
        }
    }

    internal void BeginOrderedQueryOperation()
    {
        EnsureExecutionAccess();
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        if (_orderedQueryOperationActive)
        {
            ThrowHelper.ThrowOrderedQueryAlreadyActive();
        }

        _orderedQueryOperationActive = true;
    }

    internal void EndOrderedQueryOperation() => _orderedQueryOperationActive = false;

    internal ReadOnlySpan<Entity> CollectOrderedQueryEntities(in Query query)
    {
        _orderedEntityCount = 0;
        using GeneratedDenseExecution execution = GeneratedForEachRuntime.OpenDense(this, in query);
        while (execution.MoveNextTrusted(out GeneratedQuerySlots slots))
        {
            for (int index = 0; index < slots.Count; index++)
            {
                AppendOrderedQueryEntity(slots.EntityAt(index));
            }
        }

        return _orderedEntityBuffer.AsSpan(0, _orderedEntityCount);
    }

    internal void ClearOrderedQueryEntities() => _orderedEntityCount = 0;

    internal ReadOnlySpan<Entity> SortOrderedQueryEntities(OrderedQueryState state)
    {
        int count = _orderedEntityCount;
        if (count < 2)
        {
            return _orderedEntityBuffer.AsSpan(0, count);
        }

        Entity[] source = _orderedEntityBuffer;
        Entity[] destination = _orderedEntityMergeBuffer;
        for (int width = 1; width < count; width = width > count / 2 ? count : width * 2)
        {
            int start = 0;
            while (start < count)
            {
                int middle = Math.Min(start + width, count);
                int end = Math.Min(middle + width, count);
                int left = start;
                int right = middle;
                int output = start;
                while (left < middle && right < end)
                {
                    // Taking the left item on equality preserves the source query order.
                    destination[output++] = state.Compare(source[left], source[right]) <= 0
                        ? source[left++]
                        : source[right++];
                }

                while (left < middle)
                {
                    destination[output++] = source[left++];
                }

                while (right < end)
                {
                    destination[output++] = source[right++];
                }

                start = end;
            }

            (source, destination) = (destination, source);
        }

        if (!ReferenceEquals(source, _orderedEntityBuffer))
        {
            Array.Copy(source, 0, _orderedEntityBuffer, 0, count);
        }

        return _orderedEntityBuffer.AsSpan(0, count);
    }

    internal void AppendOrderedQueryEntity(Entity entity)
    {
        if (_orderedEntityCount == _orderedEntityBuffer.Length)
        {
            int capacity = _orderedEntityBuffer.Length == 0 ? 16 : _orderedEntityBuffer.Length * 2;
            Array.Resize(ref _orderedEntityBuffer, capacity);
            Array.Resize(ref _orderedEntityMergeBuffer, capacity);
        }

        _orderedEntityBuffer[_orderedEntityCount++] = entity;
    }
}
