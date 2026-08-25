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
    private int _matchingCount;
    private Dictionary<Type, ComponentId>? _primaryComponentIdsByType;
    private bool _hasWriteAccess;

    public QueryPlan(World world, QuerySpec spec)
    {
        _description = spec;
        _weakReference = new WeakReference<QueryPlan>(this);
        for (int archetypeId = 0; archetypeId < world.Archetypes.Count; archetypeId++)
        {
            OnArchetypeCreated(world.Archetypes[archetypeId]);
        }
    }

    public bool HasWriteAccess => _hasWriteAccess;
    internal WeakReference<QueryPlan> WeakReference => _weakReference;
    public void RegisterWriteAccess() => _hasWriteAccess = true;

    internal int PrimaryRouteResolutionCount { get; private set; }

    internal ComponentId ResolvePrimaryComponent(World world, Type runtimeType)
    {
        if (_primaryComponentIdsByType is { } cached
            && cached.TryGetValue(runtimeType, out ComponentId componentId))
        {
            return componentId;
        }

        componentId = world.Layouts.GetPrimary(runtimeType);
        (_primaryComponentIdsByType ??= new Dictionary<Type, ComponentId>()).Add(runtimeType, componentId);
        PrimaryRouteResolutionCount++;
        return componentId;
    }

    public ReadOnlySpan<int> MatchingArchetypes() => _matchingArchetypes.AsSpan(0, _matchingCount);

    public ReadOnlySpan<ArchetypePlan> MatchingPlans() => _matchingPlans.AsSpan(0, _matchingCount);

    public ReadOnlySpan<int> ComponentRowIndices(int matchingIndex) => _matchingPlans[matchingIndex].ComponentRows;

    public bool TryGetPlan(int archetypeId, out ArchetypePlan plan)
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
        _primaryComponentIdsByType?.Clear();
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
    public ArchetypePlan(Archetype archetype, int[] componentRows)
    {
        Archetype = archetype;
        ComponentRows = componentRows;
        _chunks = Array.Empty<ChunkPlan>();
        for (int chunkIndex = 0; chunkIndex < archetype.ActiveChunkCount; chunkIndex++)
        {
            OnChunkActivated(archetype.GetActiveChunk(chunkIndex), chunkIndex);
        }
    }

    public Archetype Archetype { get; }
    public int[] ComponentRows { get; }
    public ReadOnlySpan<ChunkPlan> Chunks => _chunks.AsSpan(0, _chunkCount);

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

internal readonly record struct ChunkPlan(Chunk Chunk, Array[] ComponentRows);

internal readonly record struct QueryPlanLink(WeakReference<QueryPlan> Query, int PlanIndex);

internal static class QueryThrowHelper
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowAccessMismatch() => throw new InvalidOperationException("The row access does not belong to this query or world.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowAccessTypeMismatch() => throw new InvalidOperationException("The row access type does not match the registered component type.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    public static void ThrowMissingWriteIntent() => throw new InvalidOperationException("The query did not register its write row access.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    public static void ThrowDisposedQueryExecution() => throw new InvalidOperationException("The query execution has ended.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowAccessModeMismatch() => throw new InvalidOperationException("The access mode does not match the requested row operation.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArchetypeIteratorNotPositioned() => throw new InvalidOperationException("The archetype iterator is not positioned on an archetype.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowChunkIteratorNotPositioned() => throw new InvalidOperationException("The chunk iterator is not positioned on a chunk.");
}
