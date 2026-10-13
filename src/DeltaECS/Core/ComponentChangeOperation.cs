namespace Delta.ECS;

using System.Runtime.CompilerServices;
using static ComponentChangeMasks;

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

    internal abstract void RecordMembership(Entity entity, bool present);

    internal abstract void RecordMembershipRange(ReadOnlySpan<Entity> entities, bool present);

    internal abstract void MergeParallel();

    internal abstract void RecordChunkSlot(Chunk chunk, int slotIndex);

    internal abstract void RecordChunk(Chunk chunk);

    internal abstract void PrepareChunk(Chunk chunk);

    internal abstract void RecordChunkMask(Chunk chunk, ReadOnlySpan<ulong> mask, DirtyKind kind);

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

internal struct DirtyChunkState
{
    internal Chunk? Chunk;
    internal DirtyKind Kind;
    internal ulong[]? Mask;
    internal int ActiveIndex;
    internal bool HasMaskWrites;
}

internal sealed class DirtyChunkSet
{
    private DirtyChunkState[] _states = Array.Empty<DirtyChunkState>();
    private int[] _activeChunkIds = Array.Empty<int>();
    private int _activeCount;

    internal int ActiveCount => _activeCount;

    internal bool HasDirty => _activeCount != 0;

    private ref DirtyChunkState GetState(Chunk chunk)
    {
        int chunkId = chunk.GlobalId;
        if ((uint)chunkId >= (uint)_states.Length)
        {
            Array.Resize(ref _states, Math.Max(chunkId + 1, Math.Max(4, _states.Length * 2)));
        }

        ref DirtyChunkState state = ref _states[chunkId];
        if (!ReferenceEquals(state.Chunk, chunk))
        {
            if (state.Chunk is not null && state.ActiveIndex >= 0)
            {
                Remove(state.Chunk);
            }

            state = new DirtyChunkState { Chunk = chunk, Mask = state.Mask, ActiveIndex = -1 };
        }

        return ref state;
    }

    internal ref DirtyChunkState GetOrAdd(Chunk chunk)
    {
        int chunkId = chunk.GlobalId;
        ref DirtyChunkState state = ref GetState(chunk);
        if (state.ActiveIndex < 0)
        {
            if (_activeCount == _activeChunkIds.Length)
            {
                Array.Resize(ref _activeChunkIds, Math.Max(4, _activeChunkIds.Length * 2));
            }

            state.ActiveIndex = _activeCount;
            _activeChunkIds[_activeCount++] = chunkId;
        }

        return ref state;
    }

    internal bool TryGet(Chunk chunk, out int chunkId)
    {
        chunkId = chunk.GlobalId;
        return (uint)chunkId < (uint)_states.Length
            && ReferenceEquals(_states[chunkId].Chunk, chunk)
            && _states[chunkId].ActiveIndex >= 0;
    }

    internal ref DirtyChunkState GetAt(int activeIndex)
        => ref _states[_activeChunkIds[activeIndex]];

    internal ref DirtyChunkState GetById(int chunkId) => ref _states[chunkId];

    internal void Prepare(Chunk chunk)
    {
        ref DirtyChunkState state = ref GetState(chunk);
        state.Mask ??= new ulong[Chunk.WordCount];
        if (_activeChunkIds.Length < _states.Length)
        {
            Array.Resize(ref _activeChunkIds, _states.Length);
        }
    }

    internal void Remove(Chunk chunk)
    {
        if (!TryGet(chunk, out int chunkId))
        {
            return;
        }

        ref DirtyChunkState state = ref _states[chunkId];
        int activeIndex = state.ActiveIndex;
        int lastActiveIndex = --_activeCount;
        if (activeIndex != lastActiveIndex)
        {
            int movedChunkId = _activeChunkIds[lastActiveIndex];
            _activeChunkIds[activeIndex] = movedChunkId;
            _states[movedChunkId].ActiveIndex = activeIndex;
        }

        _activeChunkIds[lastActiveIndex] = 0;
        if (state.HasMaskWrites && state.Mask is { } mask)
        {
            Array.Clear(mask, 0, mask.Length);
        }

        state = new DirtyChunkState { Mask = state.Mask, ActiveIndex = -1 };
    }

    internal void Clear()
    {
        for (int index = 0; index < _activeCount; index++)
        {
            ref DirtyChunkState state = ref _states[_activeChunkIds[index]];
            if (state.HasMaskWrites && state.Mask is { } mask)
            {
                Array.Clear(mask, 0, mask.Length);
            }

            state.HasMaskWrites = false;
            state.Kind = DirtyKind.None;
            state.ActiveIndex = -1;
        }

        _activeCount = 0;
    }


}

internal struct ComponentMembershipChange
{
    internal Entity Entity;
    internal bool Present;
}

internal sealed class ComponentMembershipChanges
{
    private Entity[] _entities = Array.Empty<Entity>();
    private bool[] _present = Array.Empty<bool>();
    private int[] _indices = Array.Empty<int>();
    internal int Count { get; private set; }
    internal ComponentMembershipChange this[int index] => new() { Entity = _entities[index], Present = _present[index] };

    internal void Record(Entity entity, bool present)
    {
        EnsureIndexCapacity(entity.Index + 1);
        int index = Find(entity);
        if (index < 0)
        {
            EnsureChangeCapacity(Count + 1);
            index = Count++;
            _indices[entity.Index] = index + 1;
            _entities[index] = entity;
        }

        _present[index] = present;
    }

    internal void RecordRange(ReadOnlySpan<Entity> entities, bool present)
    {
        int requiredIndices = _indices.Length;
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            if (Find(entity) >= 0)
            {
                for (int item = 0; item < entities.Length; item++)
                {
                    Record(entities[item], present);
                }

                return;
            }

            requiredIndices = Math.Max(requiredIndices, entity.Index + 1);
        }

        EnsureIndexCapacity(requiredIndices);
        EnsureChangeCapacity(Count + entities.Length);
        entities.CopyTo(_entities.AsSpan(Count));
        _present.AsSpan(Count, entities.Length).Fill(present);
        for (int index = 0; index < entities.Length; index++)
        {
            _indices[entities[index].Index] = Count + index + 1;
        }

        Count += entities.Length;
    }

    internal bool Contains(Entity entity) => Find(entity) >= 0;

    internal void Clear()
    {
        for (int index = 0; index < Count; index++)
        {
            _indices[_entities[index].Index] = 0;
        }

        Count = 0;
    }

    private int Find(Entity entity)
    {
        int index = entity.Index < _indices.Length ? _indices[entity.Index] - 1 : -1;
        return index >= 0 && index < Count && _entities[index] == entity ? index : -1;
    }

    private void EnsureIndexCapacity(int capacity)
    {
        if (capacity > _indices.Length)
        {
            Array.Resize(ref _indices, Math.Max(capacity, Math.Max(4, _indices.Length * 2)));
        }
    }

    private void EnsureChangeCapacity(int capacity)
    {
        if (capacity > _entities.Length)
        {
            capacity = Math.Max(capacity, Math.Max(4, _entities.Length * 2));
            Array.Resize(ref _entities, capacity);
            Array.Resize(ref _present, capacity);
        }
    }
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
    private DirtyChunkSet _dirtyChunks = new();
    private DirtyChunkSet _processingDirtyChunks = new();
    private readonly DirtyChunkSet[] _workerDirtyChunks = new DirtyChunkSet[Environment.ProcessorCount];
    private ComponentMembershipChanges _membershipChanges = new();
    private ComponentMembershipChanges _processingMembershipChanges = new();
    private readonly Observer _observer;
    private bool _invoking;
    private bool _hasSnapshot;
    [ThreadStatic]
    private static ComponentChangeOperation<T>? s_invoking;

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

        if (_hasSnapshot && _membershipChanges.Count == 0 && !_dirtyChunks.HasDirty)
        {
            return;
        }

        _invoking = true;
        (_membershipChanges, _processingMembershipChanges) = (_processingMembershipChanges, _membershipChanges);
        (_dirtyChunks, _processingDirtyChunks) = (_processingDirtyChunks, _dirtyChunks);

        _world.BeginQueryLease();
        ComponentChangeOperation<T>? previousInvoking = s_invoking;
        s_invoking = this;
        bool completed = false;
        bool initializing = !_hasSnapshot;
        try
        {
            if (!_hasSnapshot)
            {
                VisitCurrentEntities();
                (_entities, _currentEntities) = (_currentEntities, _entities);
                (_entityPresence, _currentEntityPresence) = (_currentEntityPresence, _entityPresence);
                _hasSnapshot = true;
                _currentEntities = Array.Empty<Entity>();
                _currentEntityPresence = Array.Empty<ulong>();
            }
            else
            {
                VisitMembershipChanges();
                VisitDirtyChunks();
                CommitMembershipChanges();
            }

            _processingMembershipChanges.Clear();
            _processingDirtyChunks.Clear();
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                for (int index = 0; index < _membershipChanges.Count; index++)
                {
                    ComponentMembershipChange change = _membershipChanges[index];
                    _processingMembershipChanges.Record(change.Entity, change.Present);
                }

                _membershipChanges.Clear();
                (_membershipChanges, _processingMembershipChanges) = (_processingMembershipChanges, _membershipChanges);
                MergeDirtyChunks(_processingDirtyChunks, _dirtyChunks);
                _processingDirtyChunks.Clear();
            }

            if (initializing && !completed)
            {
                Array.Clear(_currentEntityPresence, 0, _currentEntityPresence.Length);
            }
            _world.EndQueryLease();
            s_invoking = previousInvoking;
            _invoking = false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldRecord() => !ReferenceEquals(s_invoking, this);

    private void RecordMembership(Entity entity, bool present) => _membershipChanges.Record(entity, present);

    private void MergeParallel()
    {
        for (int index = 0; index < _workerDirtyChunks.Length; index++)
        {
            if (_workerDirtyChunks[index] is { } worker)
            {
                MergeDirtyChunks(worker, _dirtyChunks);
                worker.Clear();
            }
        }
    }

    private void RecordChunk(Chunk chunk)
    {
        if (!ShouldRecord())
        {
            return;
        }

        if (chunk.Count != 0)
        {
            SetAll(_dirtyChunks, chunk);
        }
    }

    private void PrepareChunk(Chunk chunk)
    {
        if (ShouldRecord())
        {
            _dirtyChunks.Prepare(chunk);
            for (int index = 0; index < _workerDirtyChunks.Length; index++)
            {
                (_workerDirtyChunks[index] ??= new DirtyChunkSet()).Prepare(chunk);
            }
        }
    }

    private void RecordChunkSlot(Chunk chunk, int slotIndex)
    {
        if (ShouldRecord())
        {
            DirtyChunkSet chunks = _world.IsParallelExecutionActive
                ? _workerDirtyChunks[World.ChangeTrackingWorkerIndex]
                : _dirtyChunks;
            SetSlot(chunks, chunk, slotIndex);
        }
    }

    private void RecordChunkMask(Chunk chunk, ReadOnlySpan<ulong> mask, DirtyKind kind)
    {
        if (!ShouldRecord())
        {
            return;
        }

        RecordMask(_dirtyChunks, chunk, mask, kind);
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

    private void ClearChunk(Chunk chunk)
    {
        _dirtyChunks.Remove(chunk);
        _processingDirtyChunks.Remove(chunk);
    }

    private static void SetAll(DirtyChunkSet chunks, Chunk chunk)
        => chunks.GetOrAdd(chunk).Kind = DirtyKind.All;

    private static void RecordMask(DirtyChunkSet chunks, Chunk chunk, ReadOnlySpan<ulong> mask, DirtyKind kind)
    {
        if (kind == DirtyKind.None)
        {
            return;
        }

        ref DirtyChunkState state = ref chunks.GetOrAdd(chunk);
        if (state.Kind == DirtyKind.All)
        {
            return;
        }

        if (kind == DirtyKind.All)
        {
            state.Kind = DirtyKind.All;
            return;
        }

        EnsureMask(ref state);
        Union(state.Mask!, mask, chunk.Count);
        state.Kind = DirtyKind.Mask;
    }

    private static void PrepareAddRange(DirtyChunkSet chunks, Chunk chunk, int slotIndex, int count)
    {
        if (!chunks.TryGet(chunk, out int chunkId))
        {
            return;
        }

        ref DirtyChunkState state = ref chunks.GetById(chunkId);
        if (state.Kind == DirtyKind.All)
        {
            if (slotIndex == 0)
            {
                chunks.Remove(chunk);
                return;
            }

            EnsureMask(ref state);
            Array.Clear(state.Mask!, 0, state.Mask!.Length);
            SetRange(state.Mask!, 0, slotIndex, true);
            state.Kind = DirtyKind.Mask;
        }

        if (state.Kind == DirtyKind.Mask)
        {
            SetRange(state.Mask!, slotIndex, count, false);
            RemoveEmpty(chunks, chunk);
        }
    }

    private static void MoveRange(DirtyChunkSet chunks, Chunk source, int sourceSlot, Chunk target, int targetSlot, int count)
    {
        if (count == 0 || !chunks.TryGet(source, out int sourceId))
        {
            return;
        }

        DirtyChunkState sourceState = chunks.GetById(sourceId);
        if (sourceState.Kind == DirtyKind.All)
        {
            MarkRange(chunks, target, targetSlot, count);
            return;
        }

        if (sourceState.Kind != DirtyKind.Mask)
        {
            return;
        }

        ref DirtyChunkState targetState = ref chunks.GetOrAdd(target);
        if (targetState.Kind != DirtyKind.All)
        {
            EnsureMask(ref targetState);
            MergeRange(sourceState.Mask!, sourceSlot, targetState.Mask!, targetSlot, count);

            targetState.Kind = DirtyKind.Mask;
        }

        SetRange(sourceState.Mask!, sourceSlot, count, false);
        RemoveEmpty(chunks, source);
    }

    private static void CopyRangeWithin(DirtyChunkSet chunks, Chunk chunk, int sourceSlot, int targetSlot, int count)
    {
        if (sourceSlot == targetSlot || count == 0 || !chunks.TryGet(chunk, out int chunkId))
        {
            return;
        }

        ref DirtyChunkState state = ref chunks.GetById(chunkId);
        if (state.Kind != DirtyKind.Mask)
        {
            return;
        }

        CopyBits(state.Mask!, sourceSlot, state.Mask!, targetSlot, count);
    }

    private static void RemoveSwapBack(DirtyChunkSet chunks, Chunk chunk, int slotIndex, int lastSlotIndex)
    {
        if (!chunks.TryGet(chunk, out int chunkId))
        {
            return;
        }

        ref DirtyChunkState state = ref chunks.GetById(chunkId);
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

        ComponentChangeMasks.SetSlot(state.Mask!, slotIndex, GetSlot(state.Mask!, lastSlotIndex));
        ComponentChangeMasks.SetSlot(state.Mask!, lastSlotIndex, false);
        RemoveEmpty(chunks, chunk);
    }

    private static void TrimTail(DirtyChunkSet chunks, Chunk chunk, int start, int count)
    {
        if (count == 0 || !chunks.TryGet(chunk, out int chunkId))
        {
            return;
        }

        ref DirtyChunkState state = ref chunks.GetById(chunkId);
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
            RemoveEmpty(chunks, chunk);
        }
    }

    private static void MarkRange(DirtyChunkSet chunks, Chunk chunk, int slotIndex, int count)
    {
        if (count == 0)
        {
            return;
        }

        ref DirtyChunkState state = ref chunks.GetOrAdd(chunk);
        if (state.Kind == DirtyKind.All)
        {
            return;
        }

        if (slotIndex == 0 && count == chunk.Count)
        {
            state.Kind = DirtyKind.All;
            return;
        }

        EnsureMask(ref state);
        SetRange(state.Mask!, slotIndex, count, true);
        state.Kind = DirtyKind.Mask;
    }

    private static void SetSlot(DirtyChunkSet chunks, Chunk chunk, int slotIndex)
    {
        ref DirtyChunkState state = ref chunks.GetOrAdd(chunk);
        if (state.Kind == DirtyKind.All)
        {
            return;
        }

        EnsureMask(ref state);
        ComponentChangeMasks.SetSlot(state.Mask!, slotIndex, true);
        state.Kind = DirtyKind.Mask;
    }

    private static void MergeDirtyChunks(DirtyChunkSet source, DirtyChunkSet target)
    {
        for (int index = 0; index < source.ActiveCount; index++)
        {
            ref DirtyChunkState state = ref source.GetAt(index);
            if (state.Chunk is not { } chunk)
            {
                continue;
            }

            if (state.Kind == DirtyKind.All)
            {
                SetAll(target, chunk);
            }
            else if (state.Kind == DirtyKind.Mask)
            {
                RecordMask(target, chunk, state.Mask!, DirtyKind.Mask);
            }
        }
    }

    private static void EnsureMask(ref DirtyChunkState state)
    {
        state.Mask ??= new ulong[Chunk.WordCount];
        state.HasMaskWrites = true;
    }

    private static void RemoveEmpty(DirtyChunkSet chunks, Chunk chunk)
    {
        if (!chunks.TryGet(chunk, out int chunkId))
        {
            return;
        }

        ref DirtyChunkState state = ref chunks.GetById(chunkId);
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
            if (!_hasSnapshot)
            {
                Array.Resize(ref _currentEntities, capacity);
            }
        }

        int requiredWords = WordCount(capacity);
        if (requiredWords > _entityPresence.Length)
        {
            Array.Resize(ref _entityPresence, requiredWords);
            if (!_hasSnapshot)
            {
                Array.Resize(ref _currentEntityPresence, requiredWords);
            }
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
                if (isTag)
                {
                    ref T component = ref GeneratedTagRows.GetReference<T>(0);
                    int wordCount = WordCount(chunk.Count);
                    for (int wordIndex = 0; wordIndex < wordCount; wordIndex++)
                    {
                        ulong candidates = Intersect(chunk.GetTagWord(tagIndex, wordIndex), GetValidWord(chunk.Count, wordIndex));
                        if (IsFullWord(candidates, chunk.Count, wordIndex))
                        {
                            ref Entity entity = ref entities.RefAt(WordStart(wordIndex));
                            int remaining = WordLength(chunk.Count, wordIndex);
                            do
                            {
                                VisitCurrentEntity(entity, ref component);
                                entity = ref Unsafe.Add(ref entity, 1);
                            } while (--remaining != 0);
                            continue;
                        }

                        while (candidates != 0)
                        {
                            int slot = TakeFirstSlot(ref candidates, wordIndex);
                            Entity entity = entities.RefAt(slot);
                            VisitCurrentEntity(entity, ref component);
                        }
                    }

                    continue;
                }

                Span<T> components = chunk.GetComponentRow<T>(componentIndex);
                for (int slot = 0; slot < chunk.Count; slot++)
                {
                    Entity entity = entities.RefAt(slot);
                    ref T component = ref components.RefAt(slot);
                    VisitCurrentEntity(entity, ref component);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void VisitCurrentEntity(Entity entity, ref T component)
    {
        _currentEntities[entity.Index] = entity;
        MarkEntity(_currentEntityPresence, entity.Index);
        _action(entity, ChangeKind.Added, ref component);
    }

    private void VisitMembershipChanges()
    {
        for (int index = 0; index < _processingMembershipChanges.Count; index++)
        {
            ComponentMembershipChange change = _processingMembershipChanges[index];
            Entity entity = change.Entity;
            bool existed = IsMarked(_entityPresence, entity.Index) && _entities[entity.Index] == entity;
            if (!change.Present)
            {
                if (existed)
                {
                    InvokeRemoved(entity);
                }

                continue;
            }

            if (_world.TryResolveEntityLocation(entity, out Chunk chunk, out int slot))
            {
                ref T component = ref _world.Components.TryGetTagIndex(_observer.ComponentId, out _)
                    ? ref GeneratedTagRows.GetReference<T>(0)
                    : ref chunk.GetComponentRow<T>(GetComponentIndex(chunk)).RefAt(slot);
                _action(entity, existed ? ChangeKind.Modified : ChangeKind.Added, ref component);
            }
        }
    }

    private int GetComponentIndex(Chunk chunk)
    {
        _world.Archetypes[chunk.ArchetypeId].TryGetComponentIndex(_observer.ComponentId, out int componentIndex);
        return componentIndex;
    }

    private void CommitMembershipChanges()
    {
        for (int index = 0; index < _processingMembershipChanges.Count; index++)
        {
            ComponentMembershipChange change = _processingMembershipChanges[index];
            int entityIndex = change.Entity.Index;
            if (change.Present)
            {
                _entities[entityIndex] = change.Entity;
                MarkEntity(_entityPresence, entityIndex);
            }
            else if (_entities[entityIndex] == change.Entity)
            {
                ComponentChangeMasks.SetSlot(_entityPresence, entityIndex, false);
            }
        }
    }

    private interface IChangeFilter
    {
        bool Accept(Entity entity);
    }

    private readonly struct ModifiedOnly : IChangeFilter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IChangeFilter.Accept(Entity entity) => true;
    }

    private readonly struct ExcludeMembershipChanges : IChangeFilter
    {
        private readonly ComponentMembershipChanges _changes;

        internal ExcludeMembershipChanges(ComponentMembershipChanges changes) => _changes = changes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IChangeFilter.Accept(Entity entity) => !_changes.Contains(entity);
    }

    private void VisitDirtyChunks()
    {
        if (_processingMembershipChanges.Count == 0)
        {
            VisitDirtyChunks(default(ModifiedOnly));
        }
        else
        {
            VisitDirtyChunks(new ExcludeMembershipChanges(_processingMembershipChanges));
        }
    }

    private void VisitDirtyChunks<TFilter>(TFilter filter) where TFilter : struct, IChangeFilter
    {
        if (_world.Components.TryGetTagIndex(_observer.ComponentId, out int tagIndex))
        {
            // Keep localloc outside the hot kernel so tiered JIT can use dynamic PGO.
            Span<ulong> selected = stackalloc ulong[Chunk.WordCount];
            VisitDirtyTagChunks(tagIndex, filter, selected);
        }
        else
        {
            VisitDirtyDataChunks(filter);
        }
    }

    private void VisitDirtyDataChunks<TFilter>(TFilter filter) where TFilter : struct, IChangeFilter
    {
        for (int dirtyIndex = 0; dirtyIndex < _processingDirtyChunks.ActiveCount; dirtyIndex++)
        {
            ref DirtyChunkState state = ref _processingDirtyChunks.GetAt(dirtyIndex);
            if (state.Kind == DirtyKind.None || state.Chunk is not { } chunk)
            {
                continue;
            }

            int count = chunk.Count;
            DirtyKind kind = state.Kind == DirtyKind.All ? DirtyKind.All : Classify(state.Mask!, count);
            if (kind == DirtyKind.None)
            {
                continue;
            }

            Archetype archetype = _world.Archetypes[chunk.ArchetypeId];
            if (!archetype.TryGetComponentIndex(_observer.ComponentId, out int componentIndex))
            {
                continue;
            }
            Span<T> components = chunk.GetComponentRow<T>(componentIndex);
            Span<Entity> entities = chunk.RawEntities;
            if (kind == DirtyKind.All)
            {
                ref Entity entity = ref entities.GetRefAtZero();
                ref T component = ref components.GetRefAtZero();
                int remaining = count;
                while (remaining != 0)
                {
                    if (filter.Accept(entity))
                    {
                        _action(entity, ChangeKind.Modified, ref component);
                    }
                    entity = ref Unsafe.Add(ref entity, 1);
                    component = ref Unsafe.Add(ref component, 1);
                    remaining--;
                }
                continue;
            }

            int wordCount = WordCount(count);
            for (int wordIndex = 0; wordIndex < wordCount; wordIndex++)
            {
                ulong candidates = Intersect(state.Mask![wordIndex], GetValidWord(count, wordIndex));
                if (IsFullWord(candidates, count, wordIndex))
                {
                    int start = WordStart(wordIndex);
                    ref Entity entity = ref entities.RefAt(start);
                    ref T component = ref components.RefAt(start);
                    int remaining = WordLength(count, wordIndex);
                    do
                    {
                        if (filter.Accept(entity))
                        {
                            _action(entity, ChangeKind.Modified, ref component);
                        }
                        entity = ref Unsafe.Add(ref entity, 1);
                        component = ref Unsafe.Add(ref component, 1);
                    } while (--remaining != 0);
                    continue;
                }

                while (candidates != 0)
                {
                    int slot = TakeFirstSlot(ref candidates, wordIndex);
                    Entity entity = entities.RefAt(slot);
                    if (filter.Accept(entity))
                    {
                        _action(entity, ChangeKind.Modified, ref components.RefAt(slot));
                    }
                }
            }
        }
    }

    private void VisitDirtyTagChunks<TFilter>(int tagIndex, TFilter filter, Span<ulong> selected) where TFilter : struct, IChangeFilter
    {
        ref T component = ref GeneratedTagRows.GetReference<T>(0);
        for (int dirtyIndex = 0; dirtyIndex < _processingDirtyChunks.ActiveCount; dirtyIndex++)
        {
            ref DirtyChunkState state = ref _processingDirtyChunks.GetAt(dirtyIndex);
            if (state.Kind == DirtyKind.None || state.Chunk is not { } chunk)
            {
                continue;
            }

            int count = chunk.Count;
            DirtyKind kind = SelectTagChanges(chunk, tagIndex, state.Kind, state.Mask, selected);
            if (kind == DirtyKind.None)
            {
                continue;
            }
            Span<Entity> entities = chunk.RawEntities;
            if (kind == DirtyKind.All)
            {
                ref Entity entity = ref entities.GetRefAtZero();
                int remaining = count;
                while (remaining != 0)
                {
                    if (filter.Accept(entity))
                    {
                        _action(entity, ChangeKind.Modified, ref component);
                    }
                    entity = ref Unsafe.Add(ref entity, 1);
                    remaining--;
                }
                continue;
            }

            int wordCount = WordCount(count);
            for (int wordIndex = 0; wordIndex < wordCount; wordIndex++)
            {
                ulong candidates = selected[wordIndex];
                if (IsFullWord(candidates, count, wordIndex))
                {
                    int start = WordStart(wordIndex);
                    ref Entity entity = ref entities.RefAt(start);
                    int remaining = WordLength(count, wordIndex);
                    do
                    {
                        if (filter.Accept(entity))
                        {
                            _action(entity, ChangeKind.Modified, ref component);
                        }
                        entity = ref Unsafe.Add(ref entity, 1);
                    } while (--remaining != 0);
                    continue;
                }

                while (candidates != 0)
                {
                    int slot = TakeFirstSlot(ref candidates, wordIndex);
                    Entity entity = entities.RefAt(slot);
                    if (filter.Accept(entity))
                    {
                        _action(entity, ChangeKind.Modified, ref component);
                    }
                }
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

        internal override void RecordMembership(Entity entity, bool present) => _operation.RecordMembership(entity, present);

        internal override void RecordMembershipRange(ReadOnlySpan<Entity> entities, bool present)
            => _operation._membershipChanges.RecordRange(entities, present);

        internal override void MergeParallel() => _operation.MergeParallel();

        internal override void RecordChunkSlot(Chunk chunk, int slotIndex) => _operation.RecordChunkSlot(chunk, slotIndex);

        internal override void RecordChunk(Chunk chunk) => _operation.RecordChunk(chunk);

        internal override void PrepareChunk(Chunk chunk) => _operation.PrepareChunk(chunk);

        internal override void RecordChunkMask(Chunk chunk, ReadOnlySpan<ulong> mask, DirtyKind kind)
            => _operation.RecordChunkMask(chunk, mask, kind);

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
