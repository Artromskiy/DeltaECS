namespace Delta.ECS;

using System.Runtime.CompilerServices;
using System.Threading;

/// <summary>Describes a component addition, modification, or removal.</summary>
public enum ChangeKind : byte
{
    /// <summary>The component is present but was absent at the previous invocation.</summary>
    Added,

    /// <summary>The component was opened for writing since the previous invocation.</summary>
    Modified,

    /// <summary>The component or its entity is no longer present.</summary>
    Removed
}

/// <summary>Handles a change to one component registration.</summary>
/// <remarks>For <see cref="ChangeKind.Removed"/>, <paramref name="component"/> is a null reference and must not be dereferenced.</remarks>
public delegate void ComponentChangeAction<T>(Entity entity, ChangeKind change, ref T component);

internal abstract class ComponentChangeObserver
{
    internal ComponentChangeObserver(ComponentId componentId) => ComponentId = componentId;

    internal ComponentId ComponentId { get; }

    internal abstract void Record(Entity entity);

    internal abstract void RecordChunkSlot(Chunk chunk, int slotIndex);

    internal abstract void RecordChunk(Chunk chunk);

    internal abstract void RecordChunkMask(Chunk chunk, ReadOnlySpan<ulong> mask);

    internal abstract void EnsureCapacity(int capacity);

    internal abstract void PrepareAddRange(Chunk chunk, int slotIndex, int count);

    internal abstract void MoveRange(Chunk source, int sourceSlot, Chunk target, int targetSlot, int count);

    internal abstract void CopyRangeWithin(Chunk chunk, int sourceSlot, int targetSlot, int count);

    internal abstract void RemoveSwapBack(Chunk chunk, int slotIndex, int lastSlotIndex);

    internal abstract void TrimTail(Chunk chunk, int count);

    internal abstract void ClearChunk(Chunk chunk);
}

internal enum DirtyKind : byte
{
    None,
    All,
    Mask
}

internal sealed class DirtyChunkState
{
    internal DirtyKind Kind;
    internal ulong[]? Mask;
}

internal sealed class ComponentChangeObserverRoute
{
    internal readonly WeakReference<ComponentChangeObserver>[] Observers;

    internal ComponentChangeObserverRoute(WeakReference<ComponentChangeObserver>[] observers) => Observers = observers;
}

/// <summary>Reports changes to one component registration across the world.</summary>
/// <remarks>The first invocation reports every existing component as <see cref="ChangeKind.Added"/>. Later invocations report current state changes.</remarks>
public sealed class ComponentChangeOperation<T> : IOperation
{
    private readonly World _world;
    private readonly ComponentChangeAction<T> _action;
    private Entity[] _entities = Array.Empty<Entity>();
    private Entity[] _currentEntities = Array.Empty<Entity>();
    private ulong[] _entityPresence = Array.Empty<ulong>();
    private ulong[] _currentEntityPresence = Array.Empty<ulong>();
    private ulong[] _modifiedEntityMarkers = Array.Empty<ulong>();
    private ulong[] _processingModifiedEntityMarkers = Array.Empty<ulong>();
    private ulong[] _replacedEntityMarkers = Array.Empty<ulong>();
    private Dictionary<Chunk, DirtyChunkState> _dirtyChunks = new();
    private Dictionary<Chunk, DirtyChunkState> _processingDirtyChunks = new();
    private readonly Observer _observer;
    private bool _invoking;
    private int _invokingThreadId;

    internal ComponentChangeOperation(World world, ComponentId componentId, ComponentChangeAction<T> action)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        world.ValidateGeneratedComponentType<T>(componentId);
        _world = world;
        _action = action;
        _observer = new Observer(this, componentId);
        world.RegisterComponentChangeObserver(_observer);
    }

    /// <summary>Reports additions, modifications, and removals since the previous invocation.</summary>
    public void Invoke()
    {
        _ = _world.AliveEntityCount;
        if (_invoking)
        {
            ThrowHelper.ThrowArchetypeOperationAlreadyRunning();
        }

        _invoking = true;
        Volatile.Write(ref _invokingThreadId, Environment.CurrentManagedThreadId);
        (_modifiedEntityMarkers, _processingModifiedEntityMarkers) =
            (_processingModifiedEntityMarkers, _modifiedEntityMarkers);
        (_dirtyChunks, _processingDirtyChunks) = (_processingDirtyChunks, _dirtyChunks);
        _dirtyChunks.Clear();

        _world.BeginQueryLease();
        bool completed = false;
        try
        {
            VisitCurrentEntities();
            VisitRemovedEntities();
            (_entities, _currentEntities) = (_currentEntities, _entities);
            (_entityPresence, _currentEntityPresence) = (_currentEntityPresence, _entityPresence);
            Array.Clear(_processingModifiedEntityMarkers, 0, _processingModifiedEntityMarkers.Length);
            _processingDirtyChunks.Clear();
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                for (int index = 0; index < _processingModifiedEntityMarkers.Length; index++)
                {
                    _modifiedEntityMarkers[index] |= _processingModifiedEntityMarkers[index];
                }

                Array.Clear(_processingModifiedEntityMarkers, 0, _processingModifiedEntityMarkers.Length);
                MergeDirtyChunks(_processingDirtyChunks, _dirtyChunks);
                _processingDirtyChunks.Clear();
                Array.Clear(_replacedEntityMarkers, 0, _replacedEntityMarkers.Length);
            }

            Array.Clear(_currentEntityPresence, 0, _currentEntityPresence.Length);
            _world.EndQueryLease();
            Volatile.Write(ref _invokingThreadId, 0);
            _invoking = false;
        }
    }

    private void Record(Entity entity)
    {
        if (Volatile.Read(ref _invokingThreadId) != Environment.CurrentManagedThreadId)
        {
            MarkEntity(_modifiedEntityMarkers, entity.Index);
        }
    }

    private void RecordChunk(Chunk chunk)
    {
        if (Volatile.Read(ref _invokingThreadId) == Environment.CurrentManagedThreadId)
        {
            return;
        }

        if (chunk.Count != 0)
        {
            SetAll(_dirtyChunks, chunk);
        }
    }

    private void RecordChunkSlot(Chunk chunk, int slotIndex)
    {
        if (Volatile.Read(ref _invokingThreadId) != Environment.CurrentManagedThreadId)
        {
            SetSlot(_dirtyChunks, chunk, slotIndex, true);
        }
    }

    private void RecordChunkMask(Chunk chunk, ReadOnlySpan<ulong> mask)
    {
        if (Volatile.Read(ref _invokingThreadId) == Environment.CurrentManagedThreadId)
        {
            return;
        }

        RecordMask(_dirtyChunks, chunk, mask);
    }

    private void PrepareAddRange(Chunk chunk, int slotIndex, int count)
    {
        PrepareAddRange(_dirtyChunks, chunk, slotIndex, count);
    }

    private void MoveRange(Chunk source, int sourceSlot, Chunk target, int targetSlot, int count)
    {
        MoveRange(_dirtyChunks, source, sourceSlot, target, targetSlot, count);
    }

    private void CopyRangeWithin(Chunk chunk, int sourceSlot, int targetSlot, int count)
        => CopyRangeWithin(_dirtyChunks, chunk, sourceSlot, targetSlot, count);

    private void RemoveSwapBack(Chunk chunk, int slotIndex, int lastSlotIndex)
        => RemoveSwapBack(_dirtyChunks, chunk, slotIndex, lastSlotIndex);

    private void TrimTail(Chunk chunk, int count)
        => TrimTail(_dirtyChunks, chunk, chunk.Count - count, count);

    private void ClearChunk(Chunk chunk) => _dirtyChunks.Remove(chunk);

    private static void SetAll(Dictionary<Chunk, DirtyChunkState> chunks, Chunk chunk)
    {
        if (!chunks.TryGetValue(chunk, out DirtyChunkState? state))
        {
            state = new DirtyChunkState();
            chunks.Add(chunk, state);
        }

        state.Kind = DirtyKind.All;
    }

    private static void RecordMask(Dictionary<Chunk, DirtyChunkState> chunks, Chunk chunk, ReadOnlySpan<ulong> mask)
    {
        int wordCount = (chunk.Count + 63) >> 6;
        bool any = false;
        bool all = wordCount != 0;
        for (int wordIndex = 0; wordIndex < wordCount; wordIndex++)
        {
            ulong word = mask[wordIndex];
            ulong valid = GetValidWord(chunk.Count, wordIndex);
            any |= word != 0;
            all &= word == valid;
        }

        if (!any)
        {
            return;
        }

        if (all)
        {
            SetAll(chunks, chunk);
            return;
        }

        if (!chunks.TryGetValue(chunk, out DirtyChunkState? state))
        {
            state = new DirtyChunkState();
            chunks.Add(chunk, state);
        }

        if (state.Kind == DirtyKind.All)
        {
            return;
        }

        EnsureMask(state);
        for (int wordIndex = 0; wordIndex < wordCount; wordIndex++)
        {
            state.Mask![wordIndex] |= mask[wordIndex];
        }
        state.Kind = DirtyKind.Mask;
        if (IsAll(state.Mask!, chunk.Count))
        {
            state.Kind = DirtyKind.All;
        }
    }

    private static void PrepareAddRange(Dictionary<Chunk, DirtyChunkState> chunks, Chunk chunk, int slotIndex, int count)
    {
        if (!chunks.TryGetValue(chunk, out DirtyChunkState? state))
        {
            return;
        }

        if (state.Kind == DirtyKind.All)
        {
            if (slotIndex == 0)
            {
                chunks.Remove(chunk);
                return;
            }

            EnsureMask(state);
            Array.Clear(state.Mask!, 0, state.Mask!.Length);
            SetRange(state.Mask!, 0, slotIndex, true);
            state.Kind = DirtyKind.Mask;
        }

        if (state.Kind == DirtyKind.Mask)
        {
            SetRange(state.Mask!, slotIndex, count, false);
            RemoveEmpty(chunks, chunk, state);
        }
    }

    private static void MoveRange(
        Dictionary<Chunk, DirtyChunkState> chunks,
        Chunk source,
        int sourceSlot,
        Chunk target,
        int targetSlot,
        int count)
    {
        if (count == 0 || !chunks.TryGetValue(source, out DirtyChunkState? sourceState))
        {
            return;
        }

        if (sourceState.Kind == DirtyKind.All)
        {
            MarkRange(chunks, target, targetSlot, count);
            return;
        }

        if (sourceState.Kind != DirtyKind.Mask)
        {
            return;
        }

        for (int index = 0; index < count; index++)
        {
            bool dirty = GetSlot(sourceState.Mask!, sourceSlot + index);
            SetSlot(chunks, target, targetSlot + index, dirty);
        }

        SetRange(sourceState.Mask!, sourceSlot, count, false);
        RemoveEmpty(chunks, source, sourceState);
    }

    private static void CopyRangeWithin(
        Dictionary<Chunk, DirtyChunkState> chunks,
        Chunk chunk,
        int sourceSlot,
        int targetSlot,
        int count)
    {
        if (sourceSlot == targetSlot || count == 0
            || !chunks.TryGetValue(chunk, out DirtyChunkState? state)
            || state.Kind != DirtyKind.Mask)
        {
            return;
        }

        int start = targetSlot > sourceSlot ? count - 1 : 0;
        int end = targetSlot > sourceSlot ? -1 : count;
        int step = targetSlot > sourceSlot ? -1 : 1;
        for (int index = start; index != end; index += step)
        {
            SetSlot(state.Mask!, targetSlot + index, GetSlot(state.Mask!, sourceSlot + index));
        }
    }

    private static void RemoveSwapBack(
        Dictionary<Chunk, DirtyChunkState> chunks,
        Chunk chunk,
        int slotIndex,
        int lastSlotIndex)
    {
        if (!chunks.TryGetValue(chunk, out DirtyChunkState? state))
        {
            return;
        }

        if (state.Kind == DirtyKind.All)
        {
            if (lastSlotIndex == 0)
            {
                chunks.Remove(chunk);
            }

            return;
        }

        if (state.Kind != DirtyKind.Mask)
        {
            return;
        }

        SetSlot(state.Mask!, slotIndex, GetSlot(state.Mask!, lastSlotIndex));
        SetSlot(state.Mask!, lastSlotIndex, false);
        RemoveEmpty(chunks, chunk, state);
    }

    private static void TrimTail(
        Dictionary<Chunk, DirtyChunkState> chunks,
        Chunk chunk,
        int start,
        int count)
    {
        if (count == 0 || !chunks.TryGetValue(chunk, out DirtyChunkState? state))
        {
            return;
        }

        if (state.Kind == DirtyKind.All)
        {
            if (start == 0)
            {
                chunks.Remove(chunk);
            }

            return;
        }

        if (state.Kind == DirtyKind.Mask)
        {
            SetRange(state.Mask!, start, count, false);
            RemoveEmpty(chunks, chunk, state);
        }
    }

    private static void MarkRange(Dictionary<Chunk, DirtyChunkState> chunks, Chunk chunk, int slotIndex, int count)
    {
        if (count == 0)
        {
            return;
        }

        if (!chunks.TryGetValue(chunk, out DirtyChunkState? state))
        {
            state = new DirtyChunkState();
            chunks.Add(chunk, state);
        }

        if (state.Kind == DirtyKind.All)
        {
            return;
        }

        if (slotIndex == 0 && count == chunk.Count)
        {
            state.Kind = DirtyKind.All;
            return;
        }

        EnsureMask(state);
        SetRange(state.Mask!, slotIndex, count, true);
        state.Kind = IsAll(state.Mask!, chunk.Count) ? DirtyKind.All : DirtyKind.Mask;
    }

    private static void SetSlot(Dictionary<Chunk, DirtyChunkState> chunks, Chunk chunk, int slotIndex, bool dirty)
    {
        if (dirty)
        {
            if (!chunks.TryGetValue(chunk, out DirtyChunkState? state))
            {
                state = new DirtyChunkState();
                chunks.Add(chunk, state);
            }

            if (state.Kind == DirtyKind.All)
            {
                return;
            }

            EnsureMask(state);
            SetSlot(state.Mask!, slotIndex, true);
            state.Kind = IsAll(state.Mask!, chunk.Count) ? DirtyKind.All : DirtyKind.Mask;
            return;
        }

        if (!chunks.TryGetValue(chunk, out DirtyChunkState? existing) || existing.Kind != DirtyKind.Mask)
        {
            return;
        }

        SetSlot(existing.Mask!, slotIndex, false);
        RemoveEmpty(chunks, chunk, existing);
    }

    private static void MergeDirtyChunks(
        Dictionary<Chunk, DirtyChunkState> source,
        Dictionary<Chunk, DirtyChunkState> target)
    {
        foreach (KeyValuePair<Chunk, DirtyChunkState> pair in source)
        {
            if (pair.Value.Kind == DirtyKind.All)
            {
                SetAll(target, pair.Key);
            }
            else if (pair.Value.Kind == DirtyKind.Mask)
            {
                RecordMask(target, pair.Key, pair.Value.Mask!);
            }
        }
    }

    private static void EnsureMask(DirtyChunkState state)
    {
        if (state.Mask is null)
        {
            state.Mask = new ulong[Chunk.WordCount];
        }
    }

    private static bool IsAll(ulong[] mask, int count)
    {
        int wordCount = (count + 63) >> 6;
        for (int wordIndex = 0; wordIndex < wordCount; wordIndex++)
        {
            if (mask[wordIndex] != GetValidWord(count, wordIndex))
            {
                return false;
            }
        }

        return wordCount != 0;
    }

    private static ulong GetValidWord(int count, int wordIndex)
    {
        int remaining = count - (wordIndex << 6);
        return remaining >= 64 ? ulong.MaxValue : (1UL << remaining) - 1;
    }

    private static bool GetSlot(ulong[] mask, int slotIndex)
        => (mask[slotIndex >> 6] & (1UL << (slotIndex & 63))) != 0;

    private static void SetSlot(ulong[] mask, int slotIndex, bool value)
    {
        ulong bit = 1UL << (slotIndex & 63);
        ref ulong word = ref mask[slotIndex >> 6];
        if (value)
        {
            word |= bit;
        }
        else
        {
            word &= ~bit;
        }
    }

    private static void SetRange(ulong[] mask, int start, int count, bool value)
    {
        for (int index = 0; index < count; index++)
        {
            SetSlot(mask, start + index, value);
        }
    }

    private static void RemoveEmpty(Dictionary<Chunk, DirtyChunkState> chunks, Chunk chunk, DirtyChunkState state)
    {
        if (state.Kind != DirtyKind.Mask)
        {
            return;
        }

        for (int index = 0; index < state.Mask!.Length; index++)
        {
            if (state.Mask[index] != 0)
            {
                return;
            }
        }

        chunks.Remove(chunk);
    }

    private void EnsureCapacity(int capacity)
    {
        if (capacity > _entities.Length)
        {
            Array.Resize(ref _entities, capacity);
            Array.Resize(ref _currentEntities, capacity);
        }

        int requiredWords = (capacity + 63) >> 6;
        if (requiredWords > _entityPresence.Length)
        {
            Array.Resize(ref _entityPresence, requiredWords);
            Array.Resize(ref _currentEntityPresence, requiredWords);
            Array.Resize(ref _replacedEntityMarkers, requiredWords);
        }

        if (requiredWords > _modifiedEntityMarkers.Length)
        {
            int newCapacity = Math.Max(requiredWords, _modifiedEntityMarkers.Length == 0 ? 4 : _modifiedEntityMarkers.Length * 2);
            Array.Resize(ref _modifiedEntityMarkers, newCapacity);
            Array.Resize(ref _processingModifiedEntityMarkers, newCapacity);
        }
    }

    private void VisitCurrentEntities()
    {
        bool isTag = _world.Components.TryGetTagIndex(_observer.ComponentId, out int tagIndex);
        List<Archetype> archetypes = _world.Archetypes;
        for (int archetypeIndex = 0; archetypeIndex < archetypes.Count; archetypeIndex++)
        {
            Archetype archetype = archetypes[archetypeIndex];
            int componentIndex = -1;
            if (!isTag && !archetype.TryGetComponentIndex(_observer.ComponentId, out componentIndex))
            {
                continue;
            }

            for (int chunkIndex = 0; chunkIndex < archetype.ActiveChunkCount; chunkIndex++)
            {
                Chunk chunk = archetype.GetActiveChunk(chunkIndex);
                Span<Entity> entities = chunk.RawEntities;
                Span<T> components = isTag ? default : chunk.GetComponentRow<T>(componentIndex);
                _processingDirtyChunks.TryGetValue(chunk, out DirtyChunkState? dirtyState);
                for (int slot = 0; slot < chunk.Count; slot++)
                {
                    if (isTag && !chunk.HasTag(tagIndex, slot))
                    {
                        continue;
                    }

                    Entity entity = entities.RefAt(slot);
                    bool wasPresent = IsMarked(_entityPresence, entity.Index);
                    bool replaced = wasPresent && _entities[entity.Index] != entity;
                    bool added = !wasPresent || replaced;
                    if (replaced)
                    {
                        MarkEntity(_replacedEntityMarkers, entity.Index);
                    }

                    bool modified = !added && (IsMarked(_processingModifiedEntityMarkers, entity.Index)
                        || IsDirty(dirtyState, slot));
                    _currentEntities[entity.Index] = entity;
                    MarkEntity(_currentEntityPresence, entity.Index);
                    if (!added && !modified)
                    {
                        continue;
                    }

                    ref T component = ref isTag
                        ? ref GeneratedTagRows.GetReference<T>(0)
                        : ref components.RefAt(slot);
                    _action(entity, added ? ChangeKind.Added : ChangeKind.Modified, ref component);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMarked(ulong[] markers, int entityIndex)
        => (markers[entityIndex >> 6] & (1UL << (entityIndex & 63))) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MarkEntity(ulong[] markers, int entityIndex)
        => markers[entityIndex >> 6] |= 1UL << (entityIndex & 63);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDirty(DirtyChunkState? state, int slotIndex)
        => state is not null && (state.Kind == DirtyKind.All
            || (state.Kind == DirtyKind.Mask && GetSlot(state.Mask!, slotIndex)));

    private void VisitRemovedEntities()
    {
        for (int wordIndex = 0; wordIndex < _entityPresence.Length; wordIndex++)
        {
            ulong previous = _entityPresence[wordIndex];
            ulong current = _currentEntityPresence[wordIndex];
            ulong replaced = _replacedEntityMarkers[wordIndex];
            _replacedEntityMarkers[wordIndex] = 0;
            ulong removed = (previous & ~current) | replaced;
            while (removed != 0)
            {
                int bit = BitOperationsCompat.TrailingZeroCount(removed);
                InvokeRemoved(_entities[(wordIndex << 6) + bit]);
                removed &= removed - 1;
            }

        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InvokeRemoved(Entity entity)
    {
        ref T component = ref Unsafe.NullRef<T>();
        _action(entity, ChangeKind.Removed, ref component);
    }

    private sealed class Observer : ComponentChangeObserver
    {
        private readonly ComponentChangeOperation<T> _operation;

        internal Observer(ComponentChangeOperation<T> operation, ComponentId componentId)
            : base(componentId) => _operation = operation;

        internal override void Record(Entity entity) => _operation.Record(entity);

        internal override void RecordChunkSlot(Chunk chunk, int slotIndex) => _operation.RecordChunkSlot(chunk, slotIndex);

        internal override void RecordChunk(Chunk chunk) => _operation.RecordChunk(chunk);

        internal override void RecordChunkMask(Chunk chunk, ReadOnlySpan<ulong> mask)
            => _operation.RecordChunkMask(chunk, mask);

        internal override void EnsureCapacity(int capacity) => _operation.EnsureCapacity(capacity);

        internal override void PrepareAddRange(Chunk chunk, int slotIndex, int count)
            => _operation.PrepareAddRange(chunk, slotIndex, count);

        internal override void MoveRange(Chunk source, int sourceSlot, Chunk target, int targetSlot, int count)
            => _operation.MoveRange(source, sourceSlot, target, targetSlot, count);

        internal override void CopyRangeWithin(Chunk chunk, int sourceSlot, int targetSlot, int count)
            => _operation.CopyRangeWithin(chunk, sourceSlot, targetSlot, count);

        internal override void RemoveSwapBack(Chunk chunk, int slotIndex, int lastSlotIndex)
            => _operation.RemoveSwapBack(chunk, slotIndex, lastSlotIndex);

        internal override void TrimTail(Chunk chunk, int count) => _operation.TrimTail(chunk, count);

        internal override void ClearChunk(Chunk chunk) => _operation.ClearChunk(chunk);
    }
}
