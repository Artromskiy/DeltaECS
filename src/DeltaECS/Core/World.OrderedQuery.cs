namespace Delta.ECS;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public sealed partial class World
{
    private Entity[] _orderedEntityBuffer = [];
    private Entity[] _orderedEntityMergeBuffer = [];
    private int _orderedEntityCount;
    private bool _orderedQueryOperationActive;
    private object?[] _orderedQueryKeyRoutes = [];

    /// <summary>Validates a key registration selected by a generated ordered-query comparer.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void ValidateGeneratedOrderedQueryKey<T>(in Query query, ComponentId componentId)
        => ValidateOrderedQueryKey<T>(in query, componentId);

    /// <summary>Reads a component value selected by a generated ordered-query comparer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref readonly T GetGeneratedOrderedQueryKey<T>(Entity entity, ComponentId componentId)
    {
        if ((uint)componentId.Value < (uint)_orderedQueryKeyRoutes.Length
            && _orderedQueryKeyRoutes[componentId.Value] is OrderedQueryKeyRoute<T> route)
        {
            if (!TryResolve(entity, out _, out Chunk chunk, out int slotIndex))
            {
                ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
            }

            return ref route.GetKey(this, entity, componentId, chunk, slotIndex);
        }

        return ref GetUnpreparedOrderedQueryKey<T>(entity, componentId);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ref readonly T GetUnpreparedOrderedQueryKey<T>(Entity entity, ComponentId componentId)
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

        if (_orderedQueryKeyRoutes.Length <= componentId.Value)
        {
            Array.Resize(ref _orderedQueryKeyRoutes, Math.Max(_layouts.Count, _orderedQueryKeyRoutes.Length * 2));
        }

        if (_orderedQueryKeyRoutes[componentId.Value] is null)
        {
            int tagIndex = _layouts.TryGetTagIndex(componentId, out int registeredTagIndex) ? registeredTagIndex : -1;
            _orderedQueryKeyRoutes[componentId.Value] = new OrderedQueryKeyRoute<T>(tagIndex);
        }
    }

    internal void BeginOrderedQueryOperation()
    {
        EnsureExecutionAccess();
        ThrowHelper.ThrowIfDisposed(IsDisposed, this);
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

    private sealed class OrderedQueryKeyRoute<T>(int tagIndex)
    {
        private OrderedQueryKeyRow[] _rows = [];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref readonly T GetKey(World world, Entity entity, ComponentId componentId, Chunk chunk, int slotIndex)
        {
            if (tagIndex >= 0)
            {
                if (!chunk.HasTag(tagIndex, slotIndex))
                {
                    ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
                }

                return ref GeneratedTagRows.GetReference<T>(0);
            }

            int chunkId = chunk.GlobalId;
            if ((uint)chunkId >= (uint)_rows.Length)
            {
                GrowRows(chunkId);
            }

            ref OrderedQueryKeyRow row = ref _rows[chunkId];
            // Layout adoption and chunk reuse replace this array. Values themselves
            // remain live so a comparer can mutate keys between comparisons.
            if (!ReferenceEquals(row.Owner, chunk) || !ReferenceEquals(row.Identity, chunk.RawComponentRows))
            {
                RefreshRow(world, entity, componentId, chunk, ref row);
            }

            return ref row.Values!.RefAt(slotIndex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void GrowRows(int chunkId)
            => Array.Resize(ref _rows, Math.Max(chunkId + 1, Math.Max(4, _rows.Length * 2)));

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RefreshRow(World world, Entity entity, ComponentId componentId, Chunk chunk, ref OrderedQueryKeyRow row)
        {
            if (!world.Archetypes[chunk.ArchetypeId].TryGetComponentIndex(componentId, out int componentIndex))
            {
                ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
            }

            row.Values = Unsafe.As<T[]>(chunk.GetRawComponentRowTrusted(componentIndex));
            row.Identity = chunk.RawComponentRows;
            row.Owner = chunk;
        }

        private struct OrderedQueryKeyRow
        {
            internal Chunk? Owner;
            internal Array[]? Identity;
            internal T[]? Values;
        }
    }
}
