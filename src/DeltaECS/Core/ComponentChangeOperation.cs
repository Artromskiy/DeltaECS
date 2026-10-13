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

    internal abstract void RecordChunk(Chunk chunk, QueryPlan query);

    internal abstract void RecordTagChunk(Chunk chunk, int tagIndex, QueryPlan query);

    internal abstract void EnsureCapacity(int capacity);
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

        _world.BeginQueryLease();
        bool completed = false;
        try
        {
            VisitCurrentEntities();
            VisitRemovedEntities();
            (_entities, _currentEntities) = (_currentEntities, _entities);
            (_entityPresence, _currentEntityPresence) = (_currentEntityPresence, _entityPresence);
            Array.Clear(_processingModifiedEntityMarkers, 0, _processingModifiedEntityMarkers.Length);
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

    private void RecordChunk(Chunk chunk, QueryPlan query)
    {
        if (Volatile.Read(ref _invokingThreadId) == Environment.CurrentManagedThreadId)
        {
            return;
        }

        Span<Entity> entities = chunk.RawEntities;
        if (query.TryGetTagSlots(chunk, out ReadOnlySpan<int> selectedSlots))
        {
            for (int index = 0; index < selectedSlots.Length; index++)
            {
                MarkEntity(_modifiedEntityMarkers, entities.RefAt(selectedSlots.RefAt(index)).Index);
            }

            return;
        }

        MarkEntityRange(_modifiedEntityMarkers, entities);
    }

    private void RecordTagChunk(Chunk chunk, int tagIndex, QueryPlan query)
    {
        if (Volatile.Read(ref _invokingThreadId) == Environment.CurrentManagedThreadId)
        {
            return;
        }

        int wordCount = (chunk.Count + 63) >> 6;
        bool hasSelection = query.TryGetTagSlots(chunk, out ReadOnlySpan<int> selectedSlots);
        int selectedIndex = 0;
        for (int wordIndex = 0; wordIndex < wordCount; wordIndex++)
        {
            ulong writeMask;
            if (!hasSelection)
            {
                writeMask = wordIndex == wordCount - 1 && (chunk.Count & 63) != 0
                    ? (1UL << (chunk.Count & 63)) - 1
                    : ulong.MaxValue;
            }
            else
            {
                writeMask = 0;
                int firstSlot = wordIndex << 6;
                int endSlot = firstSlot + 64;
                while (selectedIndex < selectedSlots.Length && selectedSlots.RefAt(selectedIndex) < endSlot)
                {
                    int slot = selectedSlots.RefAt(selectedIndex++);
                    if (slot >= firstSlot)
                    {
                        writeMask |= 1UL << (slot & 63);
                    }
                }
            }

            ulong changedSlots = writeMask & chunk.GetTagWord(tagIndex, wordIndex);
            MarkChunkWord(chunk, wordIndex, changedSlots);
        }
    }

    private void MarkChunkWord(Chunk chunk, int wordIndex, ulong slots)
    {
        Span<Entity> entities = chunk.RawEntities;
        while (slots != 0)
        {
            int bit = BitOperationsCompat.TrailingZeroCount(slots);
            MarkEntity(_modifiedEntityMarkers, entities.RefAt((wordIndex << 6) + bit).Index);
            slots &= slots - 1;
        }
    }

    private static void MarkEntityRange(ulong[] markers, Span<Entity> entities)
    {
        int count = entities.Length;
        if (count == 0)
        {
            return;
        }

        int firstIndex = entities.GetRefAtZero().Index;
        for (int index = 1; index < count; index++)
        {
            if (entities.RefAt(index).Index != firstIndex + index)
            {
                for (int entityIndex = 0; entityIndex < count; entityIndex++)
                {
                    MarkEntity(markers, entities.RefAt(entityIndex).Index);
                }

                return;
            }
        }

        int end = firstIndex + count;
        int firstWord = firstIndex >> 6;
        int lastWord = (end - 1) >> 6;
        for (int word = firstWord; word <= lastWord; word++)
        {
            int startBit = word == firstWord ? firstIndex & 63 : 0;
            int endBit = word == lastWord ? ((end - 1) & 63) + 1 : 64;
            ulong mask = (ulong.MaxValue << startBit) & (ulong.MaxValue >> (64 - endBit));
            markers[word] |= mask;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MarkEntity(ulong[] markers, int entityIndex)
        => markers[entityIndex >> 6] |= 1UL << (entityIndex & 63);

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
                for (int slot = 0; slot < chunk.Count; slot++)
                {
                    if (isTag && !chunk.HasTag(tagIndex, slot))
                    {
                        continue;
                    }

                    Entity entity = entities.RefAt(slot);
                    bool added = !IsMarked(_entityPresence, entity.Index) || _entities[entity.Index] != entity;
                    bool modified = !added && IsMarked(_processingModifiedEntityMarkers, entity.Index);
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

    private void VisitRemovedEntities()
    {
        for (int wordIndex = 0; wordIndex < _entityPresence.Length; wordIndex++)
        {
            ulong previous = _entityPresence[wordIndex];
            ulong current = _currentEntityPresence[wordIndex];
            ulong removed = previous & ~current;
            while (removed != 0)
            {
                int bit = BitOperationsCompat.TrailingZeroCount(removed);
                InvokeRemoved(_entities[(wordIndex << 6) + bit]);
                removed &= removed - 1;
            }

            ulong shared = previous & current;
            while (shared != 0)
            {
                int bit = BitOperationsCompat.TrailingZeroCount(shared);
                int entityIndex = (wordIndex << 6) + bit;
                Entity previousEntity = _entities[entityIndex];
                if (previousEntity != _currentEntities[entityIndex])
                {
                    InvokeRemoved(previousEntity);
                }

                shared &= shared - 1;
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

        internal override void RecordChunk(Chunk chunk, QueryPlan query) => _operation.RecordChunk(chunk, query);

        internal override void RecordTagChunk(Chunk chunk, int tagIndex, QueryPlan query)
            => _operation.RecordTagChunk(chunk, tagIndex, query);

        internal override void EnsureCapacity(int capacity) => _operation.EnsureCapacity(capacity);
    }
}
