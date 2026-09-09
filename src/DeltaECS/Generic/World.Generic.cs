namespace Delta.ECS;

public sealed partial class World
{
    /// <summary>Creates entities with the primary component for <typeparamref name="T"/> into caller-owned storage.</summary>
    public int Create<T>(int count, Span<Entity> output)
        => Create<T>(_layouts.GetPrimary<T>(), count, output);

    /// <summary>
    /// Creates an entity containing one component and initializes its value.
    /// </summary>
    public Entity Create<T>(ComponentId componentId, in T value)
    {
        EnsureRegisteredType<T>(componentId);
        Entity entity = Create(componentId);
        InitializeComponentValue(entity, componentId, in value);

        return entity;
    }

    /// <summary>Creates typed component entities into caller-owned storage.</summary>
    public int Create<T>(ComponentId componentId, int count, Span<Entity> output)
    {
        EnsureRegisteredType<T>(componentId);
        return Create(stackalloc[] { componentId }, count, output);
    }

    /// <summary>Adds and initializes the primary component for <typeparamref name="T"/> on one entity.</summary>
    public bool Add<T>(Entity entity, in T value)
        => Add(entity, _layouts.GetPrimary<T>(), in value);

    /// <summary>Adds and initializes the primary component for <typeparamref name="T"/> on every eligible entity.</summary>
    public int Add<T>(ReadOnlySpan<Entity> entities, in T value)
        => Add(entities, _layouts.GetPrimary<T>(), in value);

    /// <summary>Adds one typed component to an alive entity and initializes its value.</summary>
    public bool Add<T>(Entity entity, ComponentId componentId, in T value)
    {
        if (!IsRegisteredType<T>(componentId)
            || !IsAlive(entity)
            || TryGetCore<T>(entity, componentId, out _))
        {
            return false;
        }

        return AddComponentBatch(stackalloc[] { entity }, componentId, in value) == 1;
    }

    /// <summary>Adds and initializes one typed component on every eligible entity in a batch.</summary>
    public int Add<T>(ReadOnlySpan<Entity> entities, ComponentId componentId, in T value)
    {
        if (!IsRegisteredType<T>(componentId) || entities.Length == 0)
        {
            return 0;
        }

        return AddComponentBatch(entities, componentId, in value);
    }

    /// <summary>Removes the primary component for <typeparamref name="T"/> from one entity.</summary>
    public bool Remove<T>(Entity entity)
        => Remove<T>(entity, _layouts.GetPrimary<T>());

    /// <summary>Removes the primary component for <typeparamref name="T"/> from every eligible entity.</summary>
    public int Remove<T>(ReadOnlySpan<Entity> entities)
        => Remove<T>(entities, _layouts.GetPrimary<T>());

    /// <summary>Removes one typed component from an alive entity.</summary>
    public bool Remove<T>(Entity entity, ComponentId componentId)
    {
        if (!IsRegisteredType<T>(componentId)
            || !IsAlive(entity)
            || !TryGetCore<T>(entity, componentId, out _))
        {
            return false;
        }

        return RemoveComponentBatch<T>(stackalloc[] { entity }, componentId) == 1;
    }

    /// <summary>Removes one typed component from every eligible entity in a batch.</summary>
    public int Remove<T>(ReadOnlySpan<Entity> entities, ComponentId componentId)
    {
        if (!IsRegisteredType<T>(componentId) || entities.Length == 0)
        {
            return 0;
        }

        return RemoveComponentBatch<T>(entities, componentId);
    }

    /// <summary>Reads the primary component for <typeparamref name="T"/> when present.</summary>
    public bool TryGet<T>(Entity entity, out T value)
    {
        if (!_layouts.TryGetPrimary<T>(out ComponentId componentId))
        {
            value = default!;
            return false;
        }

        return TryGet(entity, componentId, out value);
    }

    /// <summary>Reads one component when the entity owns a matching component row.</summary>
    public bool TryGet<T>(Entity entity, ComponentId componentId, out T value)
        => TryGetCore(entity, componentId, out value);

    /// <summary>
    /// Reads one component, throwing when the entity is stale, missing the row,
    /// or the requested type does not match the registered component type.
    /// </summary>
    public T Get<T>(Entity entity, ComponentId componentId)
    {
        EnsureRegisteredType<T>(componentId);
        if (!TryGet(entity, componentId, out T value))
        {
            return ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
        }

        return value;
    }

    /// <summary>Reads the primary component for <typeparamref name="T"/> or throws when it is missing.</summary>
    public T Get<T>(Entity entity)
        => Get<T>(entity, _layouts.GetPrimary<T>());

    /// <summary>Writes one component value and reports whether the row was updated.</summary>
    public bool Set<T>(Entity entity, ComponentId componentId, in T value)
        => SetCore(entity, componentId, in value);

    /// <summary>Writes the primary component for <typeparamref name="T"/>.</summary>
    public bool Set<T>(Entity entity, in T value)
        => Set(entity, _layouts.GetPrimary<T>(), in value);

    private bool IsRegisteredType<T>(ComponentId componentId)
    {
        return _layouts.TryGet(componentId, out var layout)
            && layout.RuntimeType == typeof(T);
    }

    private int AddComponentBatch<T>(ReadOnlySpan<Entity> entities, ComponentId componentId, in T value)
    {
        EnsureNoActiveLease("add components");
        ComponentMask changeMask = ComponentMask.From(stackalloc[] { componentId });
        int edgeStamp = entities.Length == 1 ? 0 : BeginBatchEdgeCache();
        int changed = 0;
        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            Entity entity = entities.RefAt(entityIndex);
            if (!TryResolve(entity, out int recordIndex))
            {
                continue;
            }

            ref readonly var record = ref RecordAt(recordIndex);
            var sourceArchetype = _archetypes[record.Archetype];
            if (sourceArchetype.Contains(componentId))
            {
                continue;
            }

            ComponentMask targetMask = sourceArchetype.Mask.Or(changeMask);
            var edge = edgeStamp == 0
                ? GetTransitionEdge(sourceArchetype.Id, changeMask, true, targetMask)
                : GetBatchTransitionEdge(sourceArchetype.Id, changeMask, true, targetMask, edgeStamp);
            MoveEntity(recordIndex, edge, out int targetChunkIndex, out int targetSlotIndex);

            ref readonly var targetRecord = ref RecordAt(recordIndex);
            var targetArchetype = _archetypes[targetRecord.Archetype];
            int targetComponentIndex = targetArchetype.Mask.Rank(componentId);
            targetArchetype.GetChunk(targetChunkIndex)
                .GetComponentRow<T>(targetComponentIndex)
                .RefAt(targetSlotIndex) = value;
            changed++;
        }

        return changed;
    }

    private int RemoveComponentBatch<T>(ReadOnlySpan<Entity> entities, ComponentId componentId)
    {
        EnsureNoActiveLease("remove components");
        ComponentMask changeMask = ComponentMask.From(stackalloc[] { componentId });
        int edgeStamp = entities.Length == 1 ? 0 : BeginBatchEdgeCache();
        int changed = 0;
        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            Entity entity = entities.RefAt(entityIndex);
            if (!TryResolve(entity, out int recordIndex))
            {
                continue;
            }

            ref readonly var record = ref RecordAt(recordIndex);
            var sourceArchetype = _archetypes[record.Archetype];
            if (!sourceArchetype.Contains(componentId))
            {
                continue;
            }

            ComponentMask targetMask = sourceArchetype.Mask.Except(changeMask);
            var edge = edgeStamp == 0
                ? GetTransitionEdge(sourceArchetype.Id, changeMask, false, targetMask)
                : GetBatchTransitionEdge(sourceArchetype.Id, changeMask, false, targetMask, edgeStamp);
            MoveEntity(recordIndex, edge);
            changed++;
        }

        return changed;
    }

    private void EnsureRegisteredType<T>(ComponentId componentId)
    {
        if (!_layouts.TryGet(componentId, out var layout))
        {
            ThrowHelper.ThrowComponentNotRegistered(componentId);
        }

        if (layout.RuntimeType != typeof(T))
        {
            ThrowHelper.ThrowGenericComponentTypeMismatch<T>(componentId, layout.RuntimeType!);
        }
    }

    private void InitializeComponentValue<T>(Entity entity, ComponentId componentId, in T value)
    {
        if (!TryResolve(entity, out int recordIndex))
        {
            ThrowHelper.ThrowStructuralCreateFailed();
        }

        ref readonly var record = ref RecordAt(recordIndex);
        var archetype = _archetypes[record.Archetype];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            ThrowHelper.ThrowStructuralComponentMissing();
        }

        archetype.GetChunk(record.Chunk).GetComponentRow<T>(componentIndex).RefAt(record.SlotIndex) = value;
    }
}
