namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>A borrowed view of the current entity in an entity-aware iteration callback.</summary>
/// <remarks>
/// Use this value only during the callback that received it. It refers directly
/// to the current chunk and slot, so it becomes invalid after the callback or a
/// structural change. Store <see cref="Handle"/> when an entity handle must be
/// retained.
/// </remarks>
public readonly ref struct EntityRef
{
    private readonly World _world;
    private readonly Chunk _chunk;
    private readonly Archetype _archetype;
    private readonly int _slotIndex;
    private readonly QueryPlan? _queryPlan;
    private readonly int[]? _entityRefComponentIndices;
    private readonly EntityRefStampState? _entityRefStampState;
    private readonly int _planEntityIndex;
    private readonly EntityRefStampBatch? _stampBatch;
    private readonly int _stampBatchGeneration;

    internal EntityRef(
        World world,
        Chunk chunk,
        Archetype archetype,
        int slotIndex,
        QueryPlan? queryPlan,
        int[]? entityRefComponentIndices,
        EntityRefStampState? entityRefStampState,
        int planEntityIndex,
        EntityRefStampBatch? stampBatch,
        int stampBatchGeneration)
    {
        _world = world;
        _chunk = chunk;
        _archetype = archetype;
        _slotIndex = slotIndex;
        _queryPlan = queryPlan;
        _entityRefComponentIndices = entityRefComponentIndices;
        _entityRefStampState = entityRefStampState;
        _planEntityIndex = planEntityIndex;
        _stampBatch = stampBatch;
        _stampBatchGeneration = stampBatchGeneration;
    }

    /// <summary>Gets the stable handle for the current entity.</summary>
    public Entity Handle => _chunk.RawEntities[_slotIndex];

    /// <summary>Converts this borrowed view to its stable entity handle.</summary>
    public static implicit operator Entity(EntityRef entity) => entity.Handle;

    /// <summary>Gets the current entity's index.</summary>
    public int Index => Handle.Index;

    /// <summary>Gets the current entity's lifetime generation.</summary>
    public int Generation => Handle.Generation;

    /// <summary>Reports whether the current entity has the specified component registration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has(ComponentId componentId)
        => _world.Has(
            _chunk,
            _archetype,
            _slotIndex,
            componentId,
            _queryPlan,
            _entityRefComponentIndices);

    /// <summary>Attempts to read a component by registration, returning its default value when absent or mismatched.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>(ComponentId componentId, out T value)
        => _world.TryGet(
            _chunk,
            _archetype,
            _slotIndex,
            componentId,
            _queryPlan,
            _entityRefComponentIndices,
            out value);

    /// <summary>Gets a writable component reference and marks its change stamp.</summary>
    /// <remarks>For a tag, this returns the shared default placeholder; writes through it are not stored.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef<T>(ComponentId componentId)
        => ref _world.GetRef<T>(
            _chunk,
            _archetype,
            _slotIndex,
            componentId,
            _queryPlan,
            _entityRefComponentIndices,
            _entityRefStampState,
            _planEntityIndex,
            _stampBatch,
            _stampBatchGeneration);
}

public sealed partial class World
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal EntityRef CreateEntityRef(
        Chunk chunk,
        Archetype archetype,
        int slotIndex,
        QueryPlan? queryPlan,
        int[]? entityRefComponentIndices,
        EntityRefStampState? entityRefStampState,
        int planEntityIndex,
        EntityRefStampBatch? stampBatch,
        int stampBatchGeneration)
        => new(
            this,
            chunk,
            archetype,
            slotIndex,
            queryPlan,
            entityRefComponentIndices,
            entityRefStampState,
            planEntityIndex,
            stampBatch,
            stampBatchGeneration);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Has(
        Chunk chunk,
        Archetype archetype,
        int slotIndex,
        ComponentId componentId,
        QueryPlan? queryPlan,
        int[]? entityRefComponentIndices)
    {
        EnsureExecutionAccess();
        if (TryResolveEntityRefAccess(
                queryPlan,
                entityRefComponentIndices,
                componentId,
                out int route,
                out int componentIndex,
                out _))
        {
            return route >= 0
                ? queryPlan!.IsRequiredEntityRefComponent(componentId) || componentIndex >= 0
                : queryPlan!.IsRequiredEntityRefComponent(componentId)
                    || chunk.HasTag(QueryPlan.GetEntityRefTagIndex(route), slotIndex);
        }

        return _layouts.TryGetTagIndex(componentId, out int tagIndex)
            ? chunk.HasTag(tagIndex, slotIndex)
            : archetype.Contains(componentId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGet<T>(
        Chunk chunk,
        Archetype archetype,
        int slotIndex,
        ComponentId componentId,
        QueryPlan? queryPlan,
        int[]? entityRefComponentIndices,
        out T value)
    {
        EnsureExecutionAccess();
        if (TryResolveEntityRefAccess(
                queryPlan,
                entityRefComponentIndices,
                componentId,
                out int route,
                out int componentIndex,
                out Type? runtimeType))
        {
            if (!ReferenceEquals(runtimeType, typeof(T)))
            {
                value = default!;
                return false;
            }

            if (route >= 0)
            {
                if (componentIndex < 0)
                {
                    value = default!;
                    return false;
                }

                value = chunk.GetComponentRef<T>(componentIndex, slotIndex);
                return true;
            }

            value = default!;
            return queryPlan!.IsRequiredEntityRefComponent(componentId)
                || chunk.HasTag(QueryPlan.GetEntityRefTagIndex(route), slotIndex);
        }

        if (!_layouts.TryGet(componentId, out ComponentLayout layout) || !IsCompatibleComponentType<T>(layout))
        {
            value = default!;
            return false;
        }

        if (_layouts.TryGetTagIndex(componentId, out int tagIndex))
        {
            value = default!;
            return chunk.HasTag(tagIndex, slotIndex);
        }

        if (!archetype.TryGetComponentIndex(componentId, out int fallbackComponentIndex))
        {
            value = default!;
            return false;
        }

        value = chunk.GetComponentRef<T>(fallbackComponentIndex, slotIndex);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref T GetRef<T>(
        Chunk chunk,
        Archetype archetype,
        int slotIndex,
        ComponentId componentId,
        QueryPlan? queryPlan,
        int[]? entityRefComponentIndices,
        EntityRefStampState? entityRefStampState,
        int planEntityIndex,
        EntityRefStampBatch? stampBatch,
        int stampBatchGeneration)
    {
        EnsureExecutionAccess();
        Entity entity = chunk.RawEntities[slotIndex];
        if (TryResolveEntityRefAccess(
                queryPlan,
                entityRefComponentIndices,
                componentId,
                out int route,
                out int componentIndex,
                out Type? runtimeType))
        {
            if (!ReferenceEquals(runtimeType, typeof(T)))
            {
                ThrowHelper.ThrowComponentTypeMismatch(componentId, typeof(T));
            }

            if (route < 0)
            {
                if (!queryPlan!.IsRequiredEntityRefComponent(componentId)
                    && !chunk.HasTag(QueryPlan.GetEntityRefTagIndex(route), slotIndex))
                {
                    ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
                }

                return ref GeneratedTagRows.GetReference<T>(0);
            }

            if (componentIndex < 0)
            {
                ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
            }

            if (stampBatch?.Mark(
                    stampBatchGeneration,
                    entityRefStampState,
                    planEntityIndex,
                    route,
                    componentIndex) == true)
            {
                if (stampBatch.TryPinRow(stampBatchGeneration, route, chunk, componentIndex))
                {
                    return ref stampBatch.GetPinnedReference<T>(route, slotIndex);
                }

                return ref chunk.GetComponentRef<T>(componentIndex, slotIndex);
            }

            Stamp queryStamp = chunk.IncrementComponentStamp(componentIndex, slotIndex);
            CreateEntityComponentStampWriter(chunk, componentIndex, slotIndex, queryStamp).MarkPoint();
            return ref chunk.GetComponentRef<T>(componentIndex, slotIndex);
        }

        EnsureRegisteredType<T>(componentId);
        if (_layouts.TryGetTagIndex(componentId, out int tagIndex))
        {
            if (!chunk.HasTag(tagIndex, slotIndex))
            {
                ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
            }

            return ref GeneratedTagRows.GetReference<T>(0);
        }

        if (!archetype.TryGetComponentIndex(componentId, out int fallbackComponentIndex))
        {
            ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
        }

        Stamp stamp = chunk.IncrementComponentStamp(fallbackComponentIndex, slotIndex);
        CreateEntityComponentStampWriter(chunk, fallbackComponentIndex, slotIndex, stamp).MarkPoint();
        return ref chunk.GetComponentRef<T>(fallbackComponentIndex, slotIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryResolveEntityRefAccess(
        QueryPlan? queryPlan,
        int[]? entityRefComponentIndices,
        ComponentId componentId,
        out int route,
        out int componentIndex,
        out Type? runtimeType)
    {
        if (queryPlan is not null
            && queryPlan.TryGetEntityRefRoute(componentId, out route, out runtimeType))
        {
            if (route < 0)
            {
                componentIndex = -1;
                return true;
            }

            if (entityRefComponentIndices is not null
                && (uint)route < (uint)entityRefComponentIndices.Length)
            {
                componentIndex = entityRefComponentIndices.RefAt(route);
                return true;
            }
        }

        route = -1;
        componentIndex = -1;
        runtimeType = null;
        return false;
    }
}
