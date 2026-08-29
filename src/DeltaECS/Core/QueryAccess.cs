namespace Delta.ECS;

using System;
using System.Diagnostics.CodeAnalysis;
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
    private readonly QuerySpec _description;
    private readonly WeakReference<QueryPlan> _weakReference;
    private int[] _matchingArchetypes = Array.Empty<int>();
    private ArchetypePlan[] _matchingPlans = Array.Empty<ArchetypePlan>();
    private int[] _planIndicesByArchetype = Array.Empty<int>();
    private readonly int[] _readRoutesByComponent;
    private readonly Type?[] _readRouteTypesByComponent;
    private readonly Dictionary<Type, int> _primaryReadRoutesByType;
    private readonly ReadAccess[] _preparedReadAccessesByComponent;
    private readonly WriteAccess[] _preparedWriteAccessesByComponent;
    private readonly Dictionary<Type, ReadAccess> _preparedPrimaryReadAccessesByType;
    private readonly Dictionary<Type, WriteAccess> _preparedPrimaryWriteAccessesByType;
    private int _matchingCount;
    private bool _hasWriteAccess;

    internal QueryPlan(World world, QuerySpec spec)
    {
        _description = spec;
        _weakReference = new WeakReference<QueryPlan>(this);
        _readRoutesByComponent = new int[world.Layouts.Count];
        _readRouteTypesByComponent = new Type?[world.Layouts.Count];
        _preparedReadAccessesByComponent = new ReadAccess[world.Layouts.Count];
        _preparedWriteAccessesByComponent = new WriteAccess[world.Layouts.Count];
        _primaryReadRoutesByType = new Dictionary<Type, int>(_description.AllMask.Count);
        _preparedPrimaryReadAccessesByType = new Dictionary<Type, ReadAccess>(_description.AllMask.Count);
        _preparedPrimaryWriteAccessesByType = new Dictionary<Type, WriteAccess>(_description.AllMask.Count);
        Array.Fill(_readRoutesByComponent, -1);
        PrepareReadRoutes(world, spec);
        for (int archetypeId = 0; archetypeId < world.Archetypes.Count; archetypeId++)
        {
            OnArchetypeCreated(world.Archetypes[archetypeId]);
        }
    }

    internal bool HasWriteAccess => _hasWriteAccess;
    internal WeakReference<QueryPlan> WeakReference => _weakReference;
    internal int PreparedPrimaryReadRouteCount { get; private set; }

    internal int ResolveReadRoute(ComponentId component)
    {
        if (component.IsValid
            && (uint)component.Value < (uint)_readRoutesByComponent.Length)
        {
            int route = _readRoutesByComponent[component.Value];
            if (route >= 0)
            {
                return route;
            }
        }

        throw new ArgumentException(
            "A row access must target a registered component guaranteed by the query All mask.",
            nameof(component));
    }

    internal int ResolveReadRoute(ComponentId component, Type runtimeType)
    {
        int route = ResolveReadRoute(component);
        if (!ReferenceEquals(_readRouteTypesByComponent[component.Value], runtimeType))
        {
            throw new ArgumentException(
                $"Component {component} is not registered as {runtimeType}.",
                nameof(component));
        }

        return route;
    }

    internal int ResolvePrimaryReadRoute(Type runtimeType)
    {
        if (_primaryReadRoutesByType.TryGetValue(runtimeType, out int route))
        {
            return route;
        }

        throw new ArgumentException(
            $"The primary component for {runtimeType} is not guaranteed by the query All mask.",
            nameof(runtimeType));
    }

    internal int UpgradeReadRouteToWrite(int route)
    {
        _hasWriteAccess = true;
        return route;
    }

    internal ReadAccess GetPreparedPrimaryReadAccess(Type runtimeType)
    {
        if (_preparedPrimaryReadAccessesByType.TryGetValue(runtimeType, out ReadAccess access))
        {
            return access;
        }

        throw new ArgumentException(
            $"The primary component for {runtimeType} is not guaranteed by the query All mask.",
            nameof(runtimeType));
    }

    internal WriteAccess GetPreparedPrimaryWriteAccess(Type runtimeType)
    {
        _hasWriteAccess = true;
        if (_preparedPrimaryWriteAccessesByType.TryGetValue(runtimeType, out WriteAccess access))
        {
            return access;
        }

        throw new ArgumentException(
            $"The primary component for {runtimeType} is not guaranteed by the query All mask.",
            nameof(runtimeType));
    }

    internal ReadAccess GetPreparedReadAccess(ComponentId component, Type runtimeType)
    {
        ResolveReadRoute(component);
        ValidatePreparedRuntimeType(component, runtimeType);
        return _preparedReadAccessesByComponent[component.Value];
    }

    internal WriteAccess GetPreparedWriteAccess(ComponentId component, Type runtimeType)
    {
        ResolveReadRoute(component);
        ValidatePreparedRuntimeType(component, runtimeType);
        _hasWriteAccess = true;
        return _preparedWriteAccessesByComponent[component.Value];
    }

    internal ReadOnlySpan<int> MatchingArchetypes() => _matchingArchetypes.AsSpan(0, _matchingCount);

    internal ReadOnlySpan<ArchetypePlan> MatchingPlans() => _matchingPlans.AsSpan(0, _matchingCount);

    internal ArchetypePlan[] MatchingPlanArray => _matchingPlans;

    internal int MatchingPlanCount => _matchingCount;

    internal ReadOnlySpan<int> ComponentRowIndices(int matchingIndex) => _matchingPlans[matchingIndex].ComponentRows;

    internal bool TryGetPlan(int archetypeId, out ArchetypePlan plan)
    {
        if ((uint)archetypeId < (uint)_planIndicesByArchetype.Length)
        {
            int planIndex = _planIndicesByArchetype[archetypeId];
            if ((uint)planIndex < (uint)_matchingCount)
            {
                plan = _matchingPlans[planIndex];
                return true;
            }
        }

        plan = default;
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
            indices[componentIndex++] = archetype.Mask.Rank(componentId);
        }

        EnsureMatchingCapacity(_matchingCount + 1);
        int planIndex = _matchingCount;
        var plan = new ArchetypePlan(archetype, indices);
        _matchingArchetypes[_matchingCount] = archetype.Id;
        _matchingPlans[_matchingCount] = plan;
        _planIndicesByArchetype[archetype.Id] = _matchingCount++;
        archetype.Attach(this, planIndex);
    }

    internal void OnChunkActivated(int planIndex, Chunk chunk, int activePosition)
        => _matchingPlans[planIndex].OnChunkActivated(chunk, activePosition);

    internal void OnChunkDeactivated(int planIndex, int activePosition, int lastPosition)
        => _matchingPlans[planIndex].OnChunkDeactivated(activePosition, lastPosition);

    internal void Dispose()
    {
        _matchingArchetypes = Array.Empty<int>();
        _matchingPlans = Array.Empty<ArchetypePlan>();
        _planIndicesByArchetype = Array.Empty<int>();
        _matchingCount = 0;
        _primaryReadRoutesByType.Clear();
        _preparedPrimaryReadAccessesByType.Clear();
        _preparedPrimaryWriteAccessesByType.Clear();
        Array.Clear(_preparedReadAccessesByComponent);
        Array.Clear(_preparedWriteAccessesByComponent);
        Array.Fill(_readRoutesByComponent, -1);
        Array.Clear(_readRouteTypesByComponent);
    }

    private void ValidatePreparedRuntimeType(ComponentId component, Type runtimeType)
    {
        if (!ReferenceEquals(_readRouteTypesByComponent[component.Value], runtimeType))
        {
            throw new ArgumentException(
                $"Component {component} is not registered as {runtimeType}.",
                nameof(component));
        }
    }

    private void PrepareReadRoutes(World world, QuerySpec spec)
    {
        int route = 0;
        foreach (ComponentId component in spec.AllMask)
        {
            if (!world.Layouts.TryGet(component, out ComponentLayout layout))
            {
                throw new ArgumentException(
                    $"Query component {component} is not registered in the query's world.",
                    nameof(spec));
            }

            _readRoutesByComponent[component.Value] = route;
            _preparedReadAccessesByComponent[component.Value] = new ReadAccess(this, route);
            _preparedWriteAccessesByComponent[component.Value] = new WriteAccess(this, route);
            if (layout.RuntimeType is { } runtimeType)
            {
                _readRouteTypesByComponent[component.Value] = runtimeType;
                if (world.Layouts.TryGetPrimary(runtimeType, out ComponentId primary)
                    && primary == component)
                {
                    _primaryReadRoutesByType.Add(runtimeType, route);
                    _preparedPrimaryReadAccessesByType.Add(runtimeType, _preparedReadAccessesByComponent[component.Value]);
                    _preparedPrimaryWriteAccessesByType.Add(runtimeType, _preparedWriteAccessesByComponent[component.Value]);
                    PreparedPrimaryReadRouteCount++;
                }
            }

            route++;
        }
    }

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

    private bool Matches(Archetype archetype) => archetype.Mask.ContainsAll(_description.AllMask)
        && (_description.AnyMask.IsEmpty || archetype.Mask.Intersects(_description.AnyMask))
        && !archetype.Mask.Intersects(_description.NoneMask);
}

internal struct ArchetypePlan
{
    internal ArchetypePlan(Archetype archetype, int[] componentRows)
    {
        Archetype = archetype;
        ComponentRows = componentRows;
        _chunks = Array.Empty<ChunkPlan>();
        for (int chunkIndex = 0; chunkIndex < archetype.ActiveChunkCount; chunkIndex++)
        {
            OnChunkActivated(archetype.GetActiveChunk(chunkIndex), chunkIndex);
        }
    }

    internal Archetype Archetype { get; }
    internal int[] ComponentRows { get; }
    internal ReadOnlySpan<ChunkPlan> Chunks => _chunks.AsSpan(0, _chunkCount);
    internal ChunkPlan[] ChunkArray => _chunks;
    internal int ChunkCount => _chunkCount;

    private ChunkPlan[] _chunks;
    private int _chunkCount;

    internal void OnChunkActivated(Chunk chunk, int activePosition)
    {
        if (activePosition != _chunkCount)
        {
            throw new InvalidOperationException("Chunk plan activation order is out of sync with its archetype.");
        }

        if (_chunkCount == _chunks.Length)
        {
            Array.Resize(ref _chunks, Math.Max(4, _chunks.Length * 2));
        }

        var sourceRows = chunk.RawComponentRows;
        var resolvedRows = new Array[ComponentRows.Length];
        for (int queryRow = 0; queryRow < ComponentRows.Length; queryRow++)
        {
            resolvedRows[queryRow] = sourceRows[ComponentRows[queryRow]];
        }

        _chunks[_chunkCount++] = new ChunkPlan(chunk, resolvedRows);
    }

    internal void OnChunkDeactivated(int activePosition, int lastPosition)
    {
        if ((uint)activePosition >= (uint)_chunkCount || lastPosition != _chunkCount - 1)
        {
            throw new InvalidOperationException("Chunk plan deactivation order is out of sync with its archetype.");
        }

        if (activePosition != lastPosition)
        {
            _chunks[activePosition] = _chunks[lastPosition];
        }

        _chunks[lastPosition] = default;
        _chunkCount--;
    }
}

internal readonly struct ChunkPlan
{
    internal ChunkPlan(Chunk chunk, Array[] componentRows)
    {
        Chunk = chunk;
        ComponentRows = componentRows;
    }

    internal Chunk Chunk { get; }
    internal Array[] ComponentRows { get; }
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

internal static class QueryThrowHelper
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowAccessMismatch() => throw new InvalidOperationException("The row access does not belong to this query or world.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowAccessTypeMismatch() => throw new InvalidOperationException("The row access type does not match the registered component type.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    internal static void ThrowMissingWriteIntent() => throw new InvalidOperationException("The query did not register its write row access.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    internal static void ThrowDisposedQueryExecution() => throw new InvalidOperationException("The query execution has ended.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowAccessModeMismatch() => throw new InvalidOperationException("The access mode does not match the requested row operation.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArchetypeIteratorNotPositioned() => throw new InvalidOperationException("The archetype iterator is not positioned on an archetype.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowChunkIteratorNotPositioned() => throw new InvalidOperationException("The chunk iterator is not positioned on a chunk.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowSlotIteratorNotPositioned() => throw new InvalidOperationException("The slot iterator is not positioned on a slot.");
}
