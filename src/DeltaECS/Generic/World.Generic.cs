namespace Delta.ECS;

using System.Runtime.CompilerServices;

public sealed partial class World
{
    /// <summary>Creates one entity with the primary component for <typeparamref name="T"/>.</summary>
    public Entity Create<T>() => Create(GetPrimaryComponentId<T>());

    /// <summary>Creates entities with the primary component for <typeparamref name="T"/> into caller-owned storage.</summary>
    public int Create<T>(int count, Span<Entity> output) => Create<T>(GetPrimaryComponentId<T>(), count, output);

    /// <summary>Creates entities with the primary component for <typeparamref name="T"/> without retaining handles.</summary>
    public int Create<T>(int count) => Create<T>(GetPrimaryComponentId<T>(), count, Span<Entity>.Empty);

    /// <summary>Creates typed component entities into caller-owned storage.</summary>
    public int Create<T>(ComponentId componentId, int count, Span<Entity> output)
    {
        EnsureRegisteredType<T>(componentId);
        return Create(componentId, count, output);
    }

    /// <summary>Creates typed entities with the specified component registration and returns their handles.</summary>
    public int Create<T>(ComponentId componentId, int count)
    {
        EnsureRegisteredType<T>(componentId);
        return Create<T>(componentId, count, Span<Entity>.Empty);
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
    {
        ComponentId componentId = GetPrimaryComponentId<T>();
        return _layouts.IsTag(componentId)
            ? Add(entity, componentId)
            : AddComponentBatch(stackalloc[] { entity }, componentId, in value) == 1;
    }

    /// <summary>Adds and initializes the primary component for <typeparamref name="T"/> on every eligible entity.</summary>
    public int Add<T>(ReadOnlySpan<Entity> entities, in T value)
    {
        ComponentId componentId = GetPrimaryComponentId<T>();
        return _layouts.IsTag(componentId)
            ? Add(entities, stackalloc[] { componentId })
            : AddComponentBatch(entities, componentId, in value);
    }

    /// <summary>Removes the primary component for <typeparamref name="T"/> from one entity.</summary>
    public bool Remove<T>(Entity entity)
        => RemoveComponentBatch(
            stackalloc[] { entity },
            GetPrimaryComponentId<T>()) == 1;

    /// <summary>Removes the primary component for <typeparamref name="T"/> from every eligible entity.</summary>
    public int Remove<T>(ReadOnlySpan<Entity> entities) => RemoveComponentBatch(entities, GetPrimaryComponentId<T>());

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

    /// <summary>Reads the primary component or reports tag presence, returning default for a tag.</summary>
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

    /// <summary>Reads one component or reports tag presence, returning default for a tag.</summary>
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

        return TryGetRegisteredCore<T>(chunk, slotIndex, componentId, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetRegisteredCoreTrusted<T>(Entity entity, ComponentId componentId, out T value)
    {
        Chunk chunk = GetEntityRefLocationTrusted(entity, out int slotIndex);
        return TryGetRegisteredCore<T>(chunk, slotIndex, componentId, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetRegisteredCore<T>(Chunk chunk, int slotIndex, ComponentId componentId, out T value)
    {
        if (_layouts.TryGetTagIndex(componentId, out int tagIndex))
        {
            value = default!;
            return chunk.HasTag(tagIndex, slotIndex);
        }

        Archetype archetype = _archetypes[chunk.ArchetypeId];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            value = default!;
            return false;
        }

        value = chunk.GetComponentRef<T>(componentIndex, slotIndex);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetTrusted<T>(Entity entity, out T value)
    {
        if (!TryGetPrimaryComponentId<T>(out ComponentId componentId))
        {
            value = default!;
            return false;
        }

        return TryGetRegisteredCoreTrusted(entity, componentId, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetTrusted<T>(ref GeneratedEntityRefView view, out T value)
    {
        if (!TryGetPrimaryComponentId<T>(out ComponentId componentId))
        {
            value = default!;
            return false;
        }

        return TryGetRegisteredCore<T>(view.Chunk!, view.SlotIndex, componentId, out value);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool HasTrusted<T>(Entity entity) => TryGetPrimaryComponentId<T>(out ComponentId componentId)
        && HasTrusted(entity, componentId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool HasTrusted<T>(Entity entity, ComponentId componentId)
        => IsRegisteredType<T>(componentId) && HasTrusted(entity, componentId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool HasTrusted<T>(ref GeneratedEntityRefView view)
        => TryGetPrimaryComponentId<T>(out ComponentId componentId) && HasTrusted<T>(ref view, componentId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool HasTrusted<T>(ref GeneratedEntityRefView view, ComponentId componentId)
        => IsRegisteredType<T>(componentId) && HasTrusted(ref view, componentId);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetComponentStampTrusted<T>(Entity entity, out Stamp stamp)
    {
        if (!TryGetPrimaryComponentId<T>(out ComponentId componentId))
        {
            stamp = default;
            return false;
        }

        return TryGetComponentStampTrusted(entity, componentId, out stamp);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetComponentStampTrusted<T>(Entity entity, ComponentId componentId, out Stamp stamp)
    {
        if (!IsRegisteredType<T>(componentId))
        {
            stamp = default;
            return false;
        }

        return TryGetComponentStampTrusted(entity, componentId, out stamp);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetComponentStampTrusted<T>(ref GeneratedEntityRefView view, out Stamp stamp)
    {
        if (!TryGetPrimaryComponentId<T>(out ComponentId componentId))
        {
            stamp = default;
            return false;
        }

        return TryGetComponentStampTrusted<T>(ref view, componentId, out stamp);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetComponentStampTrusted<T>(ref GeneratedEntityRefView view, ComponentId componentId, out Stamp stamp)
    {
        if (!IsRegisteredType<T>(componentId))
        {
            stamp = default;
            return false;
        }

        return TryGetComponentStampTrusted(ref view, componentId, out stamp);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T GetTrusted<T>(Entity entity, ComponentId componentId)
    {
        EnsureRegisteredType<T>(componentId);
        return TryGetRegisteredCoreTrusted(entity, componentId, out T value)
            ? value
            : ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T GetTrusted<T>(ref GeneratedEntityRefView view, ComponentId componentId)
    {
        EnsureRegisteredType<T>(componentId);
        return TryGetRegisteredCore<T>(view.Chunk!, view.SlotIndex, componentId, out T value)
            ? value
            : ThrowHelper.ThrowMissingComponent<T>(view.CurrentEntity, componentId);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T GetTrusted<T>(Entity entity)
    {
        ComponentId componentId = GetPrimaryComponentId<T>();
        return TryGetRegisteredCoreTrusted(entity, componentId, out T value)
            ? value
            : ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T GetTrusted<T>(ref GeneratedEntityRefView view)
    {
        ComponentId componentId = GetPrimaryComponentId<T>();
        return TryGetRegisteredCore<T>(view.Chunk!, view.SlotIndex, componentId, out T value)
            ? value
            : ThrowHelper.ThrowMissingComponent<T>(view.CurrentEntity, componentId);
    }

    /// <summary>
    /// Returns a writable reference to one component row.
    /// </summary>
    /// <remarks>
    /// The reference is invalid after a structural operation moves the entity
    /// or changes its component rows. For a tag, this is a shared default
    /// placeholder; writes through it are not stored.
    /// </remarks>
    public ref T GetRef<T>(Entity entity, ComponentId componentId)
    {
        EnsureExecutionAccess();
        EnsureRegisteredType<T>(componentId);
        return ref GetRefUnchecked<T>(entity, componentId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref T GetRefTrusted<T>(Entity entity, ComponentId componentId)
    {
        EnsureRegisteredType<T>(componentId);
        return ref GetRefTrustedUnchecked<T>(entity, componentId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref T GetRefTrusted<T>(ref GeneratedEntityRefView view, ComponentId componentId)
    {
        EnsureRegisteredType<T>(componentId);
        return ref GetRefUnchecked<T>(ref view, componentId, view.Chunk!);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref T GetRefUnchecked<T>(Entity entity, ComponentId componentId)
    {
        if (!TryResolve(entity, out _, out Chunk chunk, out int slotIndex))
        {
            ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
        }

        return ref GetRefUnchecked<T>(entity, componentId, chunk, slotIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref T GetRefTrustedUnchecked<T>(Entity entity, ComponentId componentId)
    {
        Chunk chunk = GetEntityRefLocationTrusted(entity, out int slotIndex);
        return ref GetRefUnchecked<T>(entity, componentId, chunk, slotIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref T GetRefUnchecked<T>(Entity entity, ComponentId componentId, Chunk chunk, int slotIndex)
    {
        if (_layouts.TryGetTagIndex(componentId, out int tagIndex))
        {
            if (!chunk.HasTag(tagIndex, slotIndex))
            {
                ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
            }

            return ref GeneratedTagRows.GetReference<T>(0);
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
        return ref chunk.GetComponentRef<T>(componentIndex, slotIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref T GetRefUnchecked<T>(ref GeneratedEntityRefView view, ComponentId componentId, Chunk chunk)
    {
        int slotIndex = view.SlotIndex;
        if (_layouts.TryGetTagIndex(componentId, out int tagIndex))
        {
            if (!chunk.HasTag(tagIndex, slotIndex))
            {
                ThrowHelper.ThrowMissingComponent<T>(view.CurrentEntity, componentId);
            }

            return ref GeneratedTagRows.GetReference<T>(0);
        }

        var archetype = _archetypes[chunk.ArchetypeId];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            ThrowHelper.ThrowMissingComponent<T>(view.CurrentEntity, componentId);
        }

        Stamp stamp = chunk.IncrementComponentStamp(componentIndex, slotIndex);
        CreateEntityComponentStampWriter(
            chunk,
            componentIndex,
            slotIndex,
            stamp).MarkPoint();
        return ref chunk.GetComponentRef<T>(componentIndex, slotIndex);
    }

    /// <summary>Returns a writable reference to the primary component row.</summary>
    public ref T GetRef<T>(Entity entity) => ref GetRefUnchecked<T>(entity, GetPrimaryComponentId<T>());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref T GetRefTrusted<T>(Entity entity) => ref GetRefTrustedUnchecked<T>(entity, GetPrimaryComponentId<T>());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref T GetRefTrusted<T>(ref GeneratedEntityRefView view)
    {
        ComponentId componentId = GetPrimaryComponentId<T>();
        return ref GetRefUnchecked<T>(ref view, componentId, view.Chunk!);
    }

    /// <summary>Returns a read-only reference to one component row.</summary>
    /// <remarks>
    /// The reference is invalid after a structural operation moves the entity
    /// or changes its component rows. For a tag, this refers to the shared
    /// default placeholder because tags have no per-entity value.
    /// </remarks>
    public ref readonly T GetReadRef<T>(Entity entity, ComponentId componentId)
    {
        EnsureExecutionAccess();
        EnsureRegisteredType<T>(componentId);
        return ref GetReadRefUnchecked<T>(entity, componentId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref readonly T GetReadRefTrusted<T>(Entity entity, ComponentId componentId)
    {
        EnsureRegisteredType<T>(componentId);
        return ref GetReadRefTrustedUnchecked<T>(entity, componentId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref readonly T GetReadRefTrusted<T>(ref GeneratedEntityRefView view, ComponentId componentId)
    {
        EnsureRegisteredType<T>(componentId);
        return ref GetReadRefUnchecked<T>(ref view, componentId, view.Chunk!);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref readonly T GetReadRefUnchecked<T>(Entity entity, ComponentId componentId)
    {
        if (!TryResolve(entity, out _, out Chunk chunk, out int slotIndex))
        {
            ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
        }

        return ref GetReadRefUnchecked<T>(entity, componentId, chunk, slotIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref readonly T GetReadRefTrustedUnchecked<T>(Entity entity, ComponentId componentId)
    {
        Chunk chunk = GetEntityRefLocationTrusted(entity, out int slotIndex);
        return ref GetReadRefUnchecked<T>(entity, componentId, chunk, slotIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref readonly T GetReadRefUnchecked<T>(Entity entity, ComponentId componentId, Chunk chunk, int slotIndex)
    {
        if (_layouts.TryGetTagIndex(componentId, out int tagIndex))
        {
            if (!chunk.HasTag(tagIndex, slotIndex))
            {
                ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
            }

            return ref GeneratedTagRows.GetReference<T>(0);
        }

        var archetype = _archetypes[chunk.ArchetypeId];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
        }

        return ref chunk.GetComponentRef<T>(componentIndex, slotIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref readonly T GetReadRefUnchecked<T>(ref GeneratedEntityRefView view, ComponentId componentId, Chunk chunk)
    {
        int slotIndex = view.SlotIndex;
        if (_layouts.TryGetTagIndex(componentId, out int tagIndex))
        {
            if (!chunk.HasTag(tagIndex, slotIndex))
            {
                ThrowHelper.ThrowMissingComponent<T>(view.CurrentEntity, componentId);
            }

            return ref GeneratedTagRows.GetReference<T>(0);
        }

        var archetype = _archetypes[chunk.ArchetypeId];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            ThrowHelper.ThrowMissingComponent<T>(view.CurrentEntity, componentId);
        }

        return ref chunk.GetComponentRef<T>(componentIndex, slotIndex);
    }

    /// <summary>Returns a read-only reference to the primary component row.</summary>
    public ref readonly T GetReadRef<T>(Entity entity)
        => ref GetReadRefUnchecked<T>(entity, GetPrimaryComponentId<T>());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref readonly T GetReadRefTrusted<T>(Entity entity)
        => ref GetReadRefTrustedUnchecked<T>(entity, GetPrimaryComponentId<T>());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref readonly T GetReadRefTrusted<T>(ref GeneratedEntityRefView view)
    {
        ComponentId componentId = GetPrimaryComponentId<T>();
        return ref GetReadRefUnchecked<T>(ref view, componentId, view.Chunk!);
    }

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
            if (pendingChunk is null
                || !ReferenceEquals(pendingChunk, targetChunk)
                || pendingComponentIndex != targetComponentIndex
                || targetSlotIndex != pendingSlotIndex + pendingCount)
            {
                FillComponentRange(pendingChunk, pendingComponentIndex, pendingSlotIndex, pendingCount, in value);
                pendingChunk = targetChunk;
                pendingComponentIndex = targetComponentIndex;
                pendingSlotIndex = targetSlotIndex;
                pendingCount = 0;
            }

            pendingCount++;
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
        if (componentIds.Length == 0 || !TryResolve(entity, out int recordIndex, out Chunk sourceChunk, out int sourceSlotIndex))
        {
            return false;
        }

        Archetype sourceArchetype = _archetypes[sourceChunk.ArchetypeId];
        ComponentSet changeSet = GetOrCreateComponentSet(componentIds);
        TransitionEdge edge = GetTransitionEdge(sourceArchetype.Id, changeSet, true);
        bool archetypeChanged = !edge.IsNoOp;
        if (!archetypeChanged && changeSet.TagIndices.Length == 0)
        {
            return false;
        }

        Chunk targetChunk = sourceChunk;
        int targetSlotIndex = sourceSlotIndex;
        if (archetypeChanged)
        {
            MoveEntity(recordIndex, edge, out targetChunk, out targetSlotIndex);
        }

        bool tagsChanged = ApplyTags(targetChunk, targetSlotIndex, changeSet.TagIndices, isAdd: true);
        if (tagsChanged)
        {
            _tagVersion++;
        }

        Archetype targetArchetype = _archetypes[targetChunk.ArchetypeId];
        var writer = new GeneratedComponentValueWriter(
            _layouts,
            targetChunk,
            targetArchetype,
            targetSlotIndex,
            edge.AddedTargetRowIndices);
        initializer.Initialize(ref writer);
        return archetypeChanged || tagsChanged;
    }

    internal int AddGeneratedComponentValues<TInitializer>(
        ReadOnlySpan<Entity> entities,
        ReadOnlySpan<ComponentId> componentIds,
        ref TInitializer initializer)
        where TInitializer : struct, IGeneratedComponentValueInitializer
    {
        EnsureNoActiveLease("add components");
        if (componentIds.Length == 0)
        {
            ThrowHelper.ThrowInvalidComponentList();
        }

        if (entities.IsEmpty)
        {
            return 0;
        }

        ComponentSet changeSet = GetOrCreateComponentSet(componentIds);
        int edgeStamp = entities.Length == 1 ? 0 : BeginBatchEdgeCache();
        int changed = 0;
        Chunk? pendingChunk = null;
        Archetype? pendingArchetype = null;
        int[]? pendingAddedRows = null;
        int pendingSlot = 0;
        int pendingCount = 0;
        bool tagsChanged = false;

        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            Entity entity = entities.RefAt(entityIndex);
            if (!TryResolve(entity, out int recordIndex, out Chunk sourceChunk, out int sourceSlotIndex))
            {
                continue;
            }

            Archetype sourceArchetype = _archetypes[sourceChunk.ArchetypeId];
            TransitionEdge edge = edgeStamp == 0
                ? GetTransitionEdge(sourceArchetype.Id, changeSet, true)
                : GetBatchTransitionEdge(sourceArchetype.Id, changeSet, true, edgeStamp);
            bool archetypeChanged = !edge.IsNoOp;
            if (!archetypeChanged && changeSet.TagIndices.Length == 0)
            {
                continue;
            }

            Chunk targetChunk = sourceChunk;
            int targetSlot = sourceSlotIndex;
            if (archetypeChanged)
            {
                MoveEntity(recordIndex, edge, out targetChunk, out targetSlot);
            }

            bool entityTagsChanged = ApplyTags(targetChunk, targetSlot, changeSet.TagIndices, isAdd: true);
            tagsChanged |= entityTagsChanged;
            if (!archetypeChanged)
            {
                changed += entityTagsChanged ? 1 : 0;
                continue;
            }

            Archetype targetArchetype = _archetypes[targetChunk.ArchetypeId];
            if (!ReferenceEquals(pendingChunk, targetChunk)
                || !ReferenceEquals(pendingArchetype, targetArchetype)
                || !ReferenceEquals(pendingAddedRows, edge.AddedTargetRowIndices)
                || targetSlot != pendingSlot + pendingCount)
            {
                InitializeGeneratedComponentRange(
                    pendingChunk,
                    pendingArchetype,
                    pendingAddedRows,
                    pendingSlot,
                    pendingCount,
                    _layouts,
                    ref initializer);
                pendingChunk = targetChunk;
                pendingArchetype = targetArchetype;
                pendingAddedRows = edge.AddedTargetRowIndices;
                pendingSlot = targetSlot;
                pendingCount = 0;
            }

            pendingCount++;
            changed++;
        }

        if (tagsChanged)
        {
            _tagVersion++;
        }

        InitializeGeneratedComponentRange(
            pendingChunk,
            pendingArchetype,
            pendingAddedRows,
            pendingSlot,
            pendingCount,
            _layouts,
            ref initializer);
        return changed;
    }

    internal Entity CreateGeneratedComponentValues<TInitializer>(
        ReadOnlySpan<ComponentId> componentIds,
        ref TInitializer initializer)
        where TInitializer : struct, IGeneratedComponentValueInitializer
    {
        EnsureNoActiveLease("create entities");
        Entity entity = Create(componentIds);
        if (!TryResolve(entity, out _, out Chunk chunk, out int slotIndex))
        {
            ThrowHelper.ThrowStructuralCreateFailed();
        }

        Archetype archetype = _archetypes[chunk.ArchetypeId];
        var writer = new GeneratedComponentValueWriter(_layouts, chunk, archetype, slotIndex);
        initializer.Initialize(ref writer);
        return entity;
    }

    private static void InitializeGeneratedComponentRange<TInitializer>(
        Chunk? chunk,
        Archetype? archetype,
        int[]? addedRows,
        int slotIndex,
        int count,
        ComponentLayoutRegistry layouts,
        ref TInitializer initializer)
        where TInitializer : struct, IGeneratedComponentValueInitializer
    {
        if (chunk is null || archetype is null || addedRows is null || count == 0)
        {
            return;
        }

        var writer = new GeneratedComponentValueWriter(layouts, chunk, archetype, slotIndex, addedRows, count);
        initializer.Initialize(ref writer);
    }

    private static void FillComponentRange<T>(Chunk? chunk, int componentIndex, int slotIndex, int count, in T value)
    {
        if (chunk is null || count == 0)
        {
            return;
        }

        chunk.GetComponentRow<T>(componentIndex).Slice(slotIndex, count).Fill(value);
    }

    private int RemoveComponentBatch(ReadOnlySpan<Entity> entities, ComponentId componentId)
        => ApplyComponents(false, stackalloc[] { componentId }, entities);

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

    internal void ValidateGeneratedComponentType<T>(ComponentId componentId) => EnsureRegisteredType<T>(componentId);

    internal bool IsGeneratedComponentType<T>(ComponentId componentId) => IsRegisteredType<T>(componentId);

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

        chunk.GetComponentRef<T>(componentIndex, slotIndex) = value;
    }
}
