namespace Delta.ECS;

using System.Runtime.CompilerServices;

public sealed partial class World
{
    /// <summary>Creates one entity with the primary component for <typeparamref name="T"/>.</summary>
    public Entity Create<T>()
        => Create(GetPrimaryComponentId<T>());

    /// <summary>Creates entities with the primary component for <typeparamref name="T"/> into caller-owned storage.</summary>
    public int Create<T>(int count, Span<Entity> output)
        => Create<T>(GetPrimaryComponentId<T>(), count, output);

    /// <summary>Creates entities with the primary component for <typeparamref name="T"/> without retaining handles.</summary>
    public int Create<T>(int count)
    {
        ThrowHelper.ThrowIfNegative(count, nameof(count));
        return Create(stackalloc[] { GetPrimaryComponentId<T>() }, count);
    }

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

    /// <summary>Creates typed entities with the specified component registration and returns their handles.</summary>
    public int Create<T>(ComponentId componentId, int count)
    {
        EnsureRegisteredType<T>(componentId);
        return Create(stackalloc[] { componentId }, count);
    }

    /// <summary>
    /// Adds and initializes the primary component for <typeparamref name="T"/> on one entity.
    /// </summary>
    /// <example>
    /// <para>For one component:</para>
    /// <code>world.Add(entity, new Health { Value = 100 });</code>
    /// <para>For multiple components, the generator emits a one-transition overload:</para>
    /// <code>
    /// world.Add(entity,
    ///     new Weapon(equipped),
    ///     new Damage(damage),
    ///     new Attack(0f),
    ///     Target.None);
    /// </code>
    /// </example>
    public bool Add<T>(Entity entity, in T value)
        => AddComponentBatch(
            stackalloc[] { entity },
            GetPrimaryComponentId<T>(),
            in value) == 1;

    /// <summary>Adds and initializes the primary component for <typeparamref name="T"/> on every eligible entity.</summary>
    public int Add<T>(ReadOnlySpan<Entity> entities, in T value)
        => AddComponentBatch(entities, GetPrimaryComponentId<T>(), in value);

    /// <summary>Adds one typed component to an alive entity and initializes its value.</summary>
    public bool Add<T>(Entity entity, ComponentId componentId, in T value)
    {
        EnsureExecutionAccess();
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
        EnsureExecutionAccess();
        if (!IsRegisteredType<T>(componentId) || entities.Length == 0)
        {
            return 0;
        }

        return AddComponentBatch(entities, componentId, in value);
    }

    /// <summary>Removes the primary component for <typeparamref name="T"/> from one entity.</summary>
    public bool Remove<T>(Entity entity)
        => RemoveComponentBatch(
            stackalloc[] { entity },
            GetPrimaryComponentId<T>()) == 1;

    /// <summary>Removes the primary component for <typeparamref name="T"/> from every eligible entity.</summary>
    public int Remove<T>(ReadOnlySpan<Entity> entities)
        => RemoveComponentBatch(entities, GetPrimaryComponentId<T>());

    /// <summary>Removes one typed component from an alive entity.</summary>
    public bool Remove<T>(Entity entity, ComponentId componentId)
    {
        EnsureExecutionAccess();
        if (!IsRegisteredType<T>(componentId)
            || !IsAlive(entity)
            || !TryGetCore<T>(entity, componentId, out _))
        {
            return false;
        }

        return RemoveComponentBatch(stackalloc[] { entity }, componentId) == 1;
    }

    /// <summary>Removes one typed component from every eligible entity in a batch.</summary>
    public int Remove<T>(ReadOnlySpan<Entity> entities, ComponentId componentId)
    {
        EnsureExecutionAccess();
        if (!IsRegisteredType<T>(componentId) || entities.Length == 0)
        {
            return 0;
        }

        return RemoveComponentBatch(entities, componentId);
    }

    /// <summary>Reads the primary component for <typeparamref name="T"/> when present.</summary>
    public bool TryGet<T>(Entity entity, out T value)
    {
        EnsureExecutionAccess();
        if (!TryGetPrimaryComponentId<T>(out ComponentId componentId))
        {
            value = default!;
            return false;
        }

        return TryGetRegisteredCore(entity, componentId, out value);
    }

    /// <summary>Reads one component when the entity owns a matching component row.</summary>
    public bool TryGet<T>(Entity entity, ComponentId componentId, out T value)
    {
        EnsureExecutionAccess();
        return TryGetCore(entity, componentId, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetRegisteredCore<T>(Entity entity, ComponentId componentId, out T value)
    {
        if (!TryResolve(entity, out _, out Chunk chunk, out int slotIndex))
        {
            value = default!;
            return false;
        }

        Archetype archetype = _archetypes[chunk.ArchetypeId];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            value = default!;
            return false;
        }

        value = chunk.GetComponentRow<T>(componentIndex).RefAt(slotIndex);
        return true;
    }

    /// <summary>Reports whether an alive entity owns the primary component for <typeparamref name="T"/>.</summary>
    public bool Has<T>(Entity entity)
    {
        EnsureExecutionAccess();
        if (!TryGetPrimaryComponentId<T>(out ComponentId componentId))
        {
            return false;
        }

        return Has(entity, componentId);
    }

    /// <summary>Reports whether an alive entity owns a typed component registration.</summary>
    public bool Has<T>(Entity entity, ComponentId componentId)
    {
        EnsureExecutionAccess();
        return IsRegisteredType<T>(componentId) && Has(entity, componentId);
    }

    /// <summary>Reads the primary component stamp when present.</summary>
    public bool TryGetComponentStamp<T>(Entity entity, out Stamp stamp)
    {
        EnsureExecutionAccess();
        if (!TryGetPrimaryComponentId<T>(out ComponentId componentId))
        {
            stamp = default;
            return false;
        }

        return TryGetComponentStamp(entity, componentId, out stamp);
    }

    /// <summary>Reads a typed component stamp when the entity owns the matching registration.</summary>
    public bool TryGetComponentStamp<T>(Entity entity, ComponentId componentId, out Stamp stamp)
    {
        EnsureExecutionAccess();
        if (!IsRegisteredType<T>(componentId))
        {
            stamp = default;
            return false;
        }

        return TryGetComponentStamp(entity, componentId, out stamp);
    }

    /// <summary>
    /// Reads one component, throwing when the entity is stale, missing the row,
    /// or the requested type does not match the registered component type.
    /// </summary>
    public T Get<T>(Entity entity, ComponentId componentId)
    {
        EnsureRegisteredType<T>(componentId);
        if (!TryGetRegisteredCore(entity, componentId, out T value))
        {
            return ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
        }

        return value;
    }

    /// <summary>Reads the primary component for <typeparamref name="T"/> or throws when it is missing.</summary>
    public T Get<T>(Entity entity)
    {
        ComponentId componentId = GetPrimaryComponentId<T>();
        if (TryGetRegisteredCore(entity, componentId, out T value))
        {
            return value;
        }

        return ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
    }

    /// <summary>
    /// Returns a writable reference to one component row.
    /// </summary>
    /// <remarks>
    /// The reference is invalid after a structural operation moves the entity
    /// or changes its component rows.
    /// </remarks>
    public ref T GetRef<T>(Entity entity, ComponentId componentId)
    {
        EnsureExecutionAccess();
        EnsureRegisteredType<T>(componentId);
        return ref GetRefUnchecked<T>(entity, componentId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref T GetRefUnchecked<T>(Entity entity, ComponentId componentId)
    {
        if (!TryResolve(entity, out _, out Chunk chunk, out int slotIndex))
        {
            ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
        }

        var archetype = _archetypes[chunk.ArchetypeId];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
        }

        Stamp stamp = chunk.IncrementComponentStamp(componentIndex, slotIndex);
        CreateEntityComponentStampWriter(
            chunk,
            componentIndex,
            slotIndex,
            stamp).MarkPoint();
        return ref chunk.GetComponentRow<T>(componentIndex).RefAt(slotIndex);
    }

    /// <summary>Returns a writable reference to the primary component row.</summary>
    public ref T GetRef<T>(Entity entity)
        => ref GetRefUnchecked<T>(entity, GetPrimaryComponentId<T>());

    /// <summary>Returns a read-only reference to one component row.</summary>
    /// <remarks>
    /// The reference is invalid after a structural operation moves the entity
    /// or changes its component rows.
    /// </remarks>
    public ref readonly T GetReadRef<T>(Entity entity, ComponentId componentId)
    {
        EnsureExecutionAccess();
        EnsureRegisteredType<T>(componentId);
        return ref GetReadRefUnchecked<T>(entity, componentId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref readonly T GetReadRefUnchecked<T>(Entity entity, ComponentId componentId)
    {
        if (!TryResolve(entity, out _, out Chunk chunk, out int slotIndex))
        {
            ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
        }

        var archetype = _archetypes[chunk.ArchetypeId];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
        }

        return ref chunk
            .GetComponentRow<T>(componentIndex)
            .RefAt(slotIndex);
    }

    /// <summary>Returns a read-only reference to the primary component row.</summary>
    public ref readonly T GetReadRef<T>(Entity entity)
        => ref GetReadRefUnchecked<T>(entity, GetPrimaryComponentId<T>());

    /// <summary>Writes one component value and throws when the entity lacks the component.</summary>
    public bool Set<T>(Entity entity, ComponentId componentId, in T value)
    {
        EnsureExecutionAccess();
        EnsureRegisteredType<T>(componentId);
        return SetCore(entity, componentId, in value);
    }

    /// <summary>Writes the primary component for <typeparamref name="T"/> and throws when it is missing.</summary>
    public bool Set<T>(Entity entity, in T value)
        => SetCore(entity, GetPrimaryComponentId<T>(), in value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsRegisteredType<T>(ComponentId componentId)
    {
        return _layouts.TryGet(componentId, out var layout)
            && layout.RuntimeType == typeof(T);
    }

    private int AddComponentBatch<T>(ReadOnlySpan<Entity> entities, ComponentId componentId, in T value)
    {
        if (entities.Length == 0)
        {
            return 0;
        }

        EnsureNoActiveLease("add components");
        ComponentSet changeSet = GetOrCreateComponentSet(stackalloc[] { componentId });
        int edgeStamp = entities.Length == 1 ? 0 : BeginBatchEdgeCache();
        int changed = 0;
        Chunk? pendingChunk = null;
        int pendingComponentIndex = -1;
        int pendingSlotIndex = 0;
        int pendingCount = 0;
        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            Entity entity = entities.RefAt(entityIndex);
            if (!TryResolve(entity, out int recordIndex, out Chunk sourceChunk, out _))
            {
                continue;
            }

            var sourceArchetype = _archetypes[sourceChunk.ArchetypeId];
            var edge = edgeStamp == 0
                ? GetTransitionEdge(sourceArchetype.Id, changeSet, true)
                : GetBatchTransitionEdge(sourceArchetype.Id, changeSet, true, edgeStamp);
            if (edge.IsNoOp)
            {
                continue;
            }

            MoveEntity(recordIndex, edge, out Chunk targetChunk, out int targetSlotIndex);
            var targetArchetype = _archetypes[targetChunk.ArchetypeId];
            int targetComponentIndex = targetArchetype.Mask.Rank(componentId);
            if (pendingChunk is not null
                && ReferenceEquals(pendingChunk, targetChunk)
                && pendingComponentIndex == targetComponentIndex
                && targetSlotIndex == pendingSlotIndex + pendingCount)
            {
                pendingCount++;
            }
            else
            {
                FillComponentRange(pendingChunk, pendingComponentIndex, pendingSlotIndex, pendingCount, in value);
                pendingChunk = targetChunk;
                pendingComponentIndex = targetComponentIndex;
                pendingSlotIndex = targetSlotIndex;
                pendingCount = 1;
            }

            changed++;
        }

        FillComponentRange(pendingChunk, pendingComponentIndex, pendingSlotIndex, pendingCount, in value);
        return changed;
    }

    internal bool AddGeneratedComponentValues<TInitializer>(
        Entity entity,
        ReadOnlySpan<ComponentId> componentIds,
        ref TInitializer initializer)
        where TInitializer : struct, IGeneratedComponentValueInitializer
    {
        EnsureNoActiveLease("add components");
        if (componentIds.Length == 0 || !TryResolve(entity, out int recordIndex, out Chunk sourceChunk, out _))
        {
            return false;
        }

        Archetype sourceArchetype = _archetypes[sourceChunk.ArchetypeId];
        ComponentSet changeSet = GetOrCreateComponentSet(componentIds);
        TransitionEdge edge = GetTransitionEdge(sourceArchetype.Id, changeSet, true);
        if (edge.IsNoOp)
        {
            return false;
        }

        MoveEntity(recordIndex, edge, out Chunk targetChunk, out int targetSlotIndex);
        Archetype targetArchetype = _archetypes[targetChunk.ArchetypeId];
        var writer = new GeneratedComponentValueWriter(
            targetChunk,
            targetArchetype,
            targetSlotIndex,
            edge.AddedTargetRowIndices);
        initializer.Initialize(ref writer);
        return true;
    }

    internal bool SetGeneratedComponentValues<TInitializer>(
        Entity entity,
        ReadOnlySpan<ComponentId> componentIds,
        ref TInitializer initializer)
        where TInitializer : struct, IGeneratedComponentValueInitializer
    {
        EnsureNoActiveLease("set components");
        if (componentIds.Length == 0)
        {
            ThrowHelper.ThrowInvalidComponentList();
        }

        if (!TryResolve(entity, out _, out Chunk chunk, out int slotIndex))
        {
            ThrowHelper.ThrowMissingComponent(entity, componentIds[0]);
        }

        Archetype archetype = _archetypes[chunk.ArchetypeId];
        for (int index = 0; index < componentIds.Length; index++)
        {
            if (!archetype.Contains(componentIds[index]))
            {
                ThrowHelper.ThrowMissingComponent(entity, componentIds[index]);
            }
        }

        var writer = new GeneratedComponentValueWriter(chunk, archetype, slotIndex);
        initializer.Initialize(ref writer);
        return true;
    }

    private static void FillComponentRange<T>(
        Chunk? chunk,
        int componentIndex,
        int slotIndex,
        int count,
        in T value)
    {
        if (chunk is null || count == 0)
        {
            return;
        }

        chunk
            .GetComponentRow<T>(componentIndex)
            .Slice(slotIndex, count)
            .Fill(value);
    }

    private int RemoveComponentBatch(ReadOnlySpan<Entity> entities, ComponentId componentId)
    {
        if (entities.Length == 0)
        {
            return 0;
        }

        EnsureNoActiveLease("remove components");
        ComponentSet changeSet = GetOrCreateComponentSet(stackalloc[] { componentId });
        int edgeStamp = entities.Length == 1 ? 0 : BeginBatchEdgeCache();
        int changed = 0;
        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            Entity entity = entities.RefAt(entityIndex);
            if (!TryResolve(entity, out int recordIndex, out Chunk sourceChunk, out _))
            {
                continue;
            }

            var sourceArchetype = _archetypes[sourceChunk.ArchetypeId];
            var edge = edgeStamp == 0
                ? GetTransitionEdge(sourceArchetype.Id, changeSet, false)
                : GetBatchTransitionEdge(sourceArchetype.Id, changeSet, false, edgeStamp);
            if (edge.IsNoOp)
            {
                continue;
            }

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
        if (!TryResolve(entity, out _, out Chunk chunk, out int slotIndex))
        {
            ThrowHelper.ThrowStructuralCreateFailed();
        }

        var archetype = _archetypes[chunk.ArchetypeId];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            ThrowHelper.ThrowStructuralComponentMissing();
        }

        chunk.GetComponentRow<T>(componentIndex).RefAt(slotIndex) = value;
    }
}
