namespace Delta.ECS;

using System.Collections.Generic;
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
}

/// <summary>Reports changes to one component registration across the world.</summary>
/// <remarks>The first invocation reports every existing component as <see cref="ChangeKind.Added"/>. Later invocations report current state changes.</remarks>
public sealed class ComponentChangeOperation<T> : IOperation
{
    private readonly World _world;
    private readonly ComponentChangeAction<T> _action;
    private HashSet<Entity> _entities = new();
    private HashSet<Entity> _currentEntities = new();
    private HashSet<Entity> _modifiedEntities = new();
    private HashSet<Entity> _processingModifiedEntities = new();
    private readonly object _modifiedGate = new();
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
        lock (_modifiedGate)
        {
            (_modifiedEntities, _processingModifiedEntities) = (_processingModifiedEntities, _modifiedEntities);
            _modifiedEntities.Clear();
        }

        _world.BeginQueryLease();
        bool completed = false;
        try
        {
            _currentEntities.Clear();
            VisitCurrentEntities();
            VisitRemovedEntities();
            (_entities, _currentEntities) = (_currentEntities, _entities);
            _currentEntities.Clear();
            _processingModifiedEntities.Clear();
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                _currentEntities.Clear();
                lock (_modifiedGate)
                {
                    foreach (Entity entity in _processingModifiedEntities)
                    {
                        _modifiedEntities.Add(entity);
                    }

                    _processingModifiedEntities.Clear();
                }
            }

            _world.EndQueryLease();
            Volatile.Write(ref _invokingThreadId, 0);
            _invoking = false;
        }
    }

    private void Record(Entity entity)
    {
        if (Volatile.Read(ref _invokingThreadId) != Environment.CurrentManagedThreadId)
        {
            lock (_modifiedGate)
            {
                _modifiedEntities.Add(entity);
            }
        }
    }

    private void VisitCurrentEntities()
    {
        bool isTag = _world.Layouts.TryGetTagIndex(_observer.ComponentId, out int tagIndex);
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
                    bool added = !_entities.Contains(entity);
                    bool modified = !added && _processingModifiedEntities.Contains(entity);
                    _currentEntities.Add(entity);
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

    private void VisitRemovedEntities()
    {
        foreach (Entity entity in _entities)
        {
            if (_currentEntities.Contains(entity))
            {
                continue;
            }

            ref T component = ref Unsafe.NullRef<T>();
            _action(entity, ChangeKind.Removed, ref component);
        }
    }

    private sealed class Observer : ComponentChangeObserver
    {
        private readonly ComponentChangeOperation<T> _operation;

        internal Observer(ComponentChangeOperation<T> operation, ComponentId componentId)
            : base(componentId) => _operation = operation;

        internal override void Record(Entity entity) => _operation.Record(entity);
    }
}
