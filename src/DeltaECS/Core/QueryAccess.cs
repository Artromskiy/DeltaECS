namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>Non-generic query access token for a read row.</summary>
public readonly struct ReadAccess
{
    internal ReadAccess(QueryPlan query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }
}

/// <summary>Non-generic query access token for a write row.</summary>
public readonly struct WriteAccess
{
    internal WriteAccess(QueryPlan query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }
}

internal sealed class QueryPlan
{
    private readonly World _owner;
    private IGeneratedDenseBinding? _lastDenseBinding;
    private Dictionary<RuntimeTypeHandle, IGeneratedDenseBinding>? _denseBindings;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TBinding GetDenseBinding<TBinding, TRows>(in Query query)
        where TBinding : GeneratedDenseBinding<TRows>, new()
        where TRows : struct
    {
        if (_lastDenseBinding is TBinding binding)
        {
            return binding;
        }
        return ResolveDenseBinding<TBinding, TRows>(in query);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private TBinding ResolveDenseBinding<TBinding, TRows>(in Query query)
        where TBinding : GeneratedDenseBinding<TRows>, new()
        where TRows : struct
    {
        _denseBindings ??= new Dictionary<RuntimeTypeHandle, IGeneratedDenseBinding>(RuntimeTypeHandleComparer.Instance);
        RuntimeTypeHandle key = typeof(TBinding).TypeHandle;
        if (!_denseBindings.TryGetValue(key, out IGeneratedDenseBinding? existing))
        {
            var created = new TBinding();
            created.Initialize(in query);
            existing = created;
            _denseBindings.Add(key, created);
        }
        _lastDenseBinding = existing;
        return (TBinding)existing;
    }

    private readonly QuerySpec _description;
    private readonly WeakReference<QueryPlan> _weakReference;
    private int[] _matchingArchetypes = Array.Empty<int>();
    private ArchetypePlan[] _matchingPlans = Array.Empty<ArchetypePlan>();
    private ChunkPlan[] _matchingChunkPlans = Array.Empty<ChunkPlan>();
    private int[] _matchingChunkPlanIndices = Array.Empty<int>();
    private int[] _planIndicesByArchetype = Array.Empty<int>();
    private readonly int[] _readRoutesByComponent;
    private readonly Type?[] _readRouteTypesByComponent;
    private readonly Dictionary<RuntimeTypeHandle, int> _primaryReadRoutesByType;
    private int _matchingChunkCount;
    private readonly ReadAccess[] _preparedReadAccessesByComponent;
    private readonly WriteAccess[] _preparedWriteAccessesByComponent;
    private int _matchingCount;
    private int _matchingVersion;
    private bool _matchingChunkPlansDirty;
    internal QueryPlan(World world, QuerySpec spec)
    {
        _owner = world;
        _description = spec;
        _weakReference = new WeakReference<QueryPlan>(this);
        _readRoutesByComponent = new int[world.Layouts.Count];
        _readRouteTypesByComponent = new Type?[world.Layouts.Count];
        _preparedReadAccessesByComponent = new ReadAccess[world.Layouts.Count];
        _preparedWriteAccessesByComponent = new WriteAccess[world.Layouts.Count];
        _primaryReadRoutesByType = new Dictionary<RuntimeTypeHandle, int>(
            _description.AllMask.Count,
            RuntimeTypeHandleComparer.Instance);
        Array.Fill(_readRoutesByComponent, -1);
        PrepareReadRoutes(world, spec);
        for (int archetypeId = 0; archetypeId < world.Archetypes.Count; archetypeId++)
        {
            OnArchetypeCreated(world.Archetypes[archetypeId]);
        }
    }

    internal World Owner => _owner;
    internal WeakReference<QueryPlan> WeakReference => _weakReference;
    internal int MatchingVersion => _matchingVersion;

    internal int ResolveReadRoute(ComponentId component)
    {
        if (component.IsValid
            && (uint)component.Value < (uint)_readRoutesByComponent.Length)
        {
            int route = _readRoutesByComponent.RefAt(component.Value);
            if (route >= 0)
            {
                return route;
            }
        }

        return ThrowHelper.ThrowInvalidReadRoute(component);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadAccess GetPreparedPrimaryReadAccess<T>()
    {
        if (TryGetPreparedPrimaryRoute<T>(out int route))
        {
            return new ReadAccess(this, route);
        }

        return ThrowHelper.ThrowMissingPrimaryReadAccess(typeof(T));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal WriteAccess GetPreparedPrimaryWriteAccess<T>()
    {
        if (TryGetPreparedPrimaryRoute<T>(out int route))
        {
            return new WriteAccess(this, route);
        }

        return ThrowHelper.ThrowMissingPrimaryWriteAccess(typeof(T));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetPreparedPrimaryReadRoute<T>()
        => TryGetPreparedPrimaryRoute<T>(out int route)
            ? route
            : ThrowHelper.ThrowMissingPrimaryReadAccess(typeof(T)).QueryComponentIndex;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetPreparedPrimaryWriteRoute<T>()
    {
        return TryGetPreparedPrimaryRoute<T>(out int route)
            ? route
            : ThrowHelper.ThrowMissingPrimaryWriteAccess(typeof(T)).QueryComponentIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetPreparedPrimaryRoute<T>(out int route)
        => TryGetPrimaryRoute(typeof(T).TypeHandle, out route);

    internal ReadAccess GetPreparedReadAccess(ComponentId component, Type runtimeType)
    {
        ResolveReadRoute(component);
        ValidatePreparedRuntimeType(component, runtimeType);
        return _preparedReadAccessesByComponent.RefAt(component.Value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadAccess GetPreparedStampAccess(ComponentId component)
    {
        ResolveReadRoute(component);
        return _preparedReadAccessesByComponent.RefAt(component.Value);
    }

    internal WriteAccess GetPreparedWriteAccess(ComponentId component, Type runtimeType)
    {
        ResolveReadRoute(component);
        ValidatePreparedRuntimeType(component, runtimeType);
        return _preparedWriteAccessesByComponent.RefAt(component.Value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<int> MatchingArchetypes() => _matchingArchetypes.AsSpan(0, _matchingCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<ArchetypePlan> MatchingPlans() => _matchingPlans.AsSpan(0, _matchingCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<ChunkPlan> MatchingChunkPlans() => _matchingChunkPlans.AsSpan(0, _matchingChunkCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<int> MatchingChunkPlanIndices() => _matchingChunkPlanIndices.AsSpan(0, _matchingChunkCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int MatchingPlanIndex(int archetypeId)
        => (uint)archetypeId < (uint)_planIndicesByArchetype.Length
            ? _planIndicesByArchetype.RefAt(archetypeId)
            : -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetChunkPlan(int archetypeId, int globalChunkId, out ChunkPlan chunkPlan)
    {
        int planIndex = MatchingPlanIndex(archetypeId);
        if (planIndex >= 0)
        {
            ArchetypePlan plan = _matchingPlans.RefAt(planIndex);
            int chunkIndex = plan.FindChunkIndex(globalChunkId);
            if (chunkIndex >= 0)
            {
                chunkPlan = plan.ChunkArray.RefAt(chunkIndex);
                return true;
            }
        }

        chunkPlan = default;
        return false;
    }

    internal void OnArchetypeCreated(Archetype archetype)
    {
        EnsureArchetypeCapacity(archetype.Id + 1);
        if (!Matches(archetype))
        {
            return;
        }

        int[] indices = new int[_description.AllMask.Count];
        int componentIndex = 0;
        foreach (var componentId in _description.AllMask)
        {
            indices.RefAt(componentIndex++) = archetype.Mask.Rank(componentId);
        }

        EnsureMatchingCapacity(_matchingCount + 1);
        int planIndex = _matchingCount;
        var plan = new ArchetypePlan(
            archetype,
            indices,
            _owner.GetArchetypeComponentStamps(archetype.Id));
        _matchingArchetypes.RefAt(_matchingCount) = archetype.Id;
        _matchingPlans.RefAt(_matchingCount) = plan;
        _planIndicesByArchetype.RefAt(archetype.Id) = _matchingCount++;
        AppendMatchingChunkPlans(planIndex, plan);
        _matchingVersion = _matchingVersion == int.MaxValue ? 1 : _matchingVersion + 1;
        archetype.Attach(this, planIndex);
    }

    internal void OnChunkActivated(int planIndex, Chunk chunk, int activePosition)
    {
        ref ArchetypePlan plan = ref _matchingPlans.RefAt(planIndex);
        int previousChunkCount = plan.ChunkCount;
        plan.OnChunkActivated(chunk, activePosition);
        plan.SetChunkTopologyVersion(plan.Archetype.ChunkTopologyVersion);
        SynchronizeMatchingChunkPlans(planIndex, previousChunkCount);
        _matchingVersion = _matchingVersion == int.MaxValue ? 1 : _matchingVersion + 1;
    }

    internal void OnChunkDeactivated(int planIndex, int activePosition, int lastPosition)
    {
        ref ArchetypePlan plan = ref _matchingPlans.RefAt(planIndex);
        int previousChunkCount = plan.ChunkCount;
        plan.OnChunkDeactivated(activePosition, lastPosition);
        plan.SetChunkTopologyVersion(plan.Archetype.ChunkTopologyVersion);
        SynchronizeMatchingChunkPlans(planIndex, previousChunkCount);
        _matchingVersion = _matchingVersion == int.MaxValue ? 1 : _matchingVersion + 1;
    }

    internal void RefreshArchetype(
        int planIndex,
        Archetype archetype,
        List<QueryPlan>? dirtyPlans = null)
    {
        ref ArchetypePlan plan = ref _matchingPlans.RefAt(planIndex);
        int previousChunkCount = plan.ChunkCount;
        if (!plan.RefreshChunks(archetype))
        {
            return;
        }

        SynchronizeMatchingChunkPlans(planIndex, previousChunkCount);
        if (dirtyPlans is null)
        {
            IncrementMatchingVersion();
            return;
        }

        if (!_matchingChunkPlansDirty)
        {
            _matchingChunkPlansDirty = true;
            dirtyPlans.Add(this);
        }
    }

    internal void CompleteChunkPlanRefresh()
    {
        if (!_matchingChunkPlansDirty)
        {
            return;
        }

        _matchingChunkPlansDirty = false;
        IncrementMatchingVersion();
    }

    internal void Dispose()
    {
        if (_denseBindings is not null)
        {
            foreach (IGeneratedDenseBinding binding in _denseBindings.Values)
            {
                binding.Clear();
            }
            _denseBindings.Clear();
        }
        _lastDenseBinding = null;
        _matchingArchetypes = Array.Empty<int>();
        _matchingPlans = Array.Empty<ArchetypePlan>();
        _matchingChunkPlans = Array.Empty<ChunkPlan>();
        _matchingChunkPlanIndices = Array.Empty<int>();
        _planIndicesByArchetype = Array.Empty<int>();
        _matchingCount = 0;
        _matchingChunkCount = 0;
        _matchingVersion = 0;
        _matchingChunkPlansDirty = false;
        _primaryReadRoutesByType.Clear();
        _preparedReadAccessesByComponent.AsSpan().Clear();
        _preparedWriteAccessesByComponent.AsSpan().Clear();
        Array.Fill(_readRoutesByComponent, -1);
        _readRouteTypesByComponent.AsSpan().Clear();
    }

    private void ValidatePreparedRuntimeType(ComponentId component, Type runtimeType)
    {
        if (!ReferenceEquals(_readRouteTypesByComponent.RefAt(component.Value), runtimeType))
        {
            ThrowHelper.ThrowComponentTypeMismatch(component, runtimeType);
        }
    }

    private void PrepareReadRoutes(World world, QuerySpec spec)
    {
        int route = 0;
        foreach (ComponentId component in spec.AllMask)
        {
            if (!world.Layouts.TryGet(component, out ComponentLayout layout))
            {
                ThrowHelper.ThrowUnregisteredQueryComponent(component, spec);
            }

            _readRoutesByComponent.RefAt(component.Value) = route;
            _preparedReadAccessesByComponent.RefAt(component.Value) = new ReadAccess(this, route);
            _preparedWriteAccessesByComponent.RefAt(component.Value) = new WriteAccess(this, route);
            Type runtimeType = layout.RuntimeType;
            _readRouteTypesByComponent.RefAt(component.Value) = runtimeType;
            if (world.Layouts.TryGetPrimary(runtimeType, out ComponentId primary)
                && primary == component)
            {
                _primaryReadRoutesByType.Add(runtimeType.TypeHandle, route);
            }

            route++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetPrimaryRoute(RuntimeTypeHandle runtimeType, out int route)
        => _primaryReadRoutesByType.TryGetValue(runtimeType, out route);

    private void EnsureArchetypeCapacity(int required)
    {
        if (required <= _planIndicesByArchetype.Length)
        {
            return;
        }

        int previousLength = _planIndicesByArchetype.Length;
        int capacity = Math.Max(required, previousLength == 0 ? 4 : previousLength * 2);
        Array.Resize(ref _planIndicesByArchetype, capacity);
        Array.Fill(_planIndicesByArchetype, -1, previousLength, capacity - previousLength);
    }

    private void EnsureMatchingCapacity(int required)
    {
        if (required <= _matchingPlans.Length)
        {
            return;
        }

        int capacity = Math.Max(required, _matchingPlans.Length == 0 ? 4 : _matchingPlans.Length * 2);
        Array.Resize(ref _matchingArchetypes, capacity);
        Array.Resize(ref _matchingPlans, capacity);
    }

    private void AppendMatchingChunkPlans(int planIndex, in ArchetypePlan plan)
    {
        int count = plan.ChunkCount;
        EnsureMatchingChunkPlanCapacity(_matchingChunkCount + count);
        if (count == 0)
        {
            return;
        }

        plan.ChunkArray.AsSpan(0, count).CopyTo(_matchingChunkPlans.AsSpan(_matchingChunkCount, count));
        _matchingChunkPlanIndices.AsSpan(_matchingChunkCount, count).Fill(planIndex);
        _matchingChunkCount += count;
    }

    private void SynchronizeMatchingChunkPlans(int planIndex, int previousChunkCount)
    {
        ref ArchetypePlan plan = ref _matchingPlans.RefAt(planIndex);
        int currentChunkCount = plan.ChunkCount;
        int start = 0;
        for (int index = 0; index < planIndex; index++)
        {
            start += _matchingPlans.RefAt(index).ChunkCount;
        }

        int delta = currentChunkCount - previousChunkCount;
        if (delta > 0)
        {
            EnsureMatchingChunkPlanCapacity(_matchingChunkCount + delta);
            int tailCount = _matchingChunkCount - start - previousChunkCount;
            _matchingChunkPlans.AsSpan(start + previousChunkCount, tailCount)
                .CopyTo(_matchingChunkPlans.AsSpan(start + currentChunkCount, tailCount));
            _matchingChunkPlanIndices.AsSpan(start + previousChunkCount, tailCount)
                .CopyTo(_matchingChunkPlanIndices.AsSpan(start + currentChunkCount, tailCount));
            _matchingChunkCount += delta;
        }
        else if (delta < 0)
        {
            int tailCount = _matchingChunkCount - start - previousChunkCount;
            _matchingChunkPlans.AsSpan(start + previousChunkCount, tailCount)
                .CopyTo(_matchingChunkPlans.AsSpan(start + currentChunkCount, tailCount));
            _matchingChunkPlanIndices.AsSpan(start + previousChunkCount, tailCount)
                .CopyTo(_matchingChunkPlanIndices.AsSpan(start + currentChunkCount, tailCount));
            _matchingChunkCount += delta;
            _matchingChunkPlans.AsSpan(_matchingChunkCount, -delta).Clear();
        }

        if (currentChunkCount == 0)
        {
            return;
        }

        ReadOnlySpan<ChunkPlan> refreshed = plan.ChunkArray.AsSpan(0, currentChunkCount);
        Span<ChunkPlan> current = _matchingChunkPlans.AsSpan(start, currentChunkCount);
        for (int index = 0; index < currentChunkCount; index++)
        {
            ChunkPlan next = refreshed.RefAt(index);
            ChunkPlan previous = current.RefAt(index);
            if (!ReferenceEquals(previous.Chunk, next.Chunk)
                || !ReferenceEquals(previous.ComponentRows, next.ComponentRows))
            {
                current.RefAt(index) = next;
            }
        }

        _matchingChunkPlanIndices.AsSpan(start, currentChunkCount).Fill(planIndex);
    }

    private void EnsureMatchingChunkPlanCapacity(int required)
    {
        if (required <= _matchingChunkPlans.Length)
        {
            return;
        }

        int capacity = Math.Max(required, _matchingChunkPlans.Length == 0 ? 4 : _matchingChunkPlans.Length * 2);
        Array.Resize(ref _matchingChunkPlans, capacity);
        Array.Resize(ref _matchingChunkPlanIndices, capacity);
    }

    private bool Matches(Archetype archetype) => archetype.Mask.ContainsAll(_description.AllMask)
        && (_description.AnyMask.IsEmpty || archetype.Mask.Intersects(_description.AnyMask))
        && !archetype.Mask.Intersects(_description.NoneMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncrementMatchingVersion()
        => _matchingVersion = _matchingVersion == int.MaxValue ? 1 : _matchingVersion + 1;

}

internal struct ArchetypePlan
{
    internal ArchetypePlan(
        Archetype archetype,
        int[] componentRows,
        Stamp[]? archetypeStamps = null)
    {
        Archetype = archetype;
        ComponentRows = componentRows;
        ArchetypeStamps = archetypeStamps ?? Array.Empty<Stamp>();
        _chunks = Array.Empty<ChunkPlan>();
        _chunkTopologyVersion = archetype.ChunkTopologyVersion;
        for (int chunkIndex = 0; chunkIndex < archetype.ActiveChunkCount; chunkIndex++)
        {
            OnChunkActivated(archetype.GetActiveChunk(chunkIndex), chunkIndex);
        }
    }

    internal Archetype Archetype { get; }
    internal int[] ComponentRows { get; }
    internal Stamp[] ArchetypeStamps { get; }
    internal int FindChunkIndex(int globalChunkId, int startIndex = 0, int endIndex = -1)
    {
        int end = endIndex < 0 ? _chunkCount : Math.Min(endIndex, _chunkCount);
        for (int index = startIndex; index < end; index++)
        {
            if (_chunks.RefAt(index).Chunk.GlobalId == globalChunkId)
            {
                return index;
            }
        }

        return -1;
    }
    internal ChunkPlan[] ChunkArray => _chunks;
    internal int ChunkCount => _chunkCount;

    private ChunkPlan[] _chunks;
    private int _chunkCount;
    private int _chunkTopologyVersion;

    internal void SetChunkTopologyVersion(int version) => _chunkTopologyVersion = version;

    internal void OnChunkActivated(Chunk chunk, int activePosition)
    {
        if (activePosition != _chunkCount)
        {
            ThrowHelper.ThrowPlanActivationOutOfSync();
        }

        if (_chunkCount == _chunks.Length)
        {
            Array.Resize(ref _chunks, Math.Max(4, _chunks.Length * 2));
        }

        _chunks.RefAt(_chunkCount++) = CreateChunkPlan(chunk);
    }

    internal void OnChunkDeactivated(int activePosition, int lastPosition)
    {
        if ((uint)activePosition >= (uint)_chunkCount || lastPosition != _chunkCount - 1)
        {
            ThrowHelper.ThrowPlanDeactivationOutOfSync();
        }

        if (activePosition != lastPosition)
        {
            _chunks.RefAt(activePosition) = _chunks.RefAt(lastPosition);
        }

        _chunkCount--;
    }

    internal bool RefreshChunks(Archetype archetype)
    {
        if (_chunkTopologyVersion == archetype.ChunkTopologyVersion)
        {
            return false;
        }

        int activeCount = archetype.ActiveChunkCount;
        if (activeCount > _chunks.Length)
        {
            Array.Resize(ref _chunks, Math.Max(activeCount, _chunks.Length == 0 ? 4 : _chunks.Length * 2));
        }

        int previousCount = _chunkCount;
        for (int chunkIndex = 0; chunkIndex < activeCount; chunkIndex++)
        {
            Chunk chunk = archetype.GetActiveChunk(chunkIndex);
            if (chunkIndex < previousCount
                && ReferenceEquals(_chunks.RefAt(chunkIndex).Chunk, chunk))
            {
                continue;
            }

            int existingIndex = FindChunkIndex(chunk.GlobalId, chunkIndex + 1, previousCount);
            if (existingIndex >= 0)
            {
                ChunkPlan moved = _chunks.RefAt(existingIndex);
                _chunks.RefAt(existingIndex) = _chunks.RefAt(chunkIndex);
                _chunks.RefAt(chunkIndex) = moved;
            }
            else
            {
                _chunks.RefAt(chunkIndex) = CreateChunkPlan(chunk);
            }
        }

        _chunkCount = activeCount;
        for (int index = activeCount; index < previousCount; index++)
        {
            _chunks.RefAt(index) = default;
        }

        _chunkTopologyVersion = archetype.ChunkTopologyVersion;
        return true;
    }

    private ChunkPlan CreateChunkPlan(Chunk chunk)
    {
        Array[] resolvedRows = new Array[ComponentRows.Length];
        var sourceRows = chunk.RawComponentRows;
        for (int queryRow = 0; queryRow < ComponentRows.Length; queryRow++)
        {
            resolvedRows.RefAt(queryRow) = sourceRows.RefAt(ComponentRows.RefAt(queryRow));
        }

        return new ChunkPlan(chunk, resolvedRows, ComponentRows);
    }
}

internal readonly struct ChunkPlan
{
    internal ChunkPlan(Chunk chunk, Array[] componentRows, int[] componentIndices)
    {
        Chunk = chunk;
        ComponentRows = componentRows;
        ComponentIndices = componentIndices;
    }

    internal Chunk Chunk { get; }
    internal Array[] ComponentRows { get; }
    internal int[] ComponentIndices { get; }
}

internal readonly struct QueryPlanLink
{
    internal QueryPlanLink(WeakReference<QueryPlan> query, int planIndex)
    {
        Query = query;
        PlanIndex = planIndex;
    }

    internal WeakReference<QueryPlan> Query { get; }
    internal int PlanIndex { get; }
}
