namespace Delta.ECS;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>Short-lived chunk access used only by <see cref="World.Execute{TContext}"/>.</summary>
public ref struct QueryChunkCursor
{
    private readonly QueryPlan _query;
    private readonly Chunk _chunk;
    private readonly ReadOnlySpan<int> _componentRows;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;
    private readonly int _count;
    private int _index;

    internal QueryChunkCursor(
        QueryPlan query,
        int archetypeId,
        Chunk chunk,
        ReadOnlySpan<int> componentRows,
        QueryWriteSession writeSession,
        int sessionGeneration)
    {
        _query = query;
        ArchetypeId = archetypeId;
        _chunk = chunk;
        _componentRows = componentRows;
        _writeSession = writeSession;
        _sessionGeneration = sessionGeneration;
        _count = chunk.Count;
        _index = -1;
    }

    public int SlotCount => _count;
    public int CurrentIndex => _index;
    public int ArchetypeId { get; }
    public int GlobalChunkId => _chunk.GlobalId;
    public ReadOnlySpan<Entity> Entities => _chunk.Entities;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        int next = _index + 1;
        if (next >= _count)
        {
            _index = _count;
            return false;
        }

        _index = next;
        return true;
    }

    public ReadRow GetRow(ReadAccess access)
    {
        _writeSession.EnsureActive(_sessionGeneration);
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        return new ReadRow(_chunk.GetRawComponentRow(physicalRow));
    }

    public WriteRow GetRow(WriteAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        AcquireWrite(out uint writeTick, out Stamp writeStamp);
        _chunk.MarkComponentWritten(physicalRow, writeTick, writeStamp);
        return new WriteRow(_chunk.GetRawComponentRow(physicalRow));
    }

    public ObjectReadValues GetObject(ReadAccess access)
    {
        _writeSession.EnsureActive(_sessionGeneration);
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        return new ObjectReadValues(_chunk.GetRawComponentRow(physicalRow));
    }

    public ObjectWriteValues GetObject(WriteAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        AcquireWrite(out uint writeTick, out Stamp writeStamp);
        _chunk.MarkComponentWritten(physicalRow, writeTick, writeStamp);
        return new ObjectWriteValues(_chunk.GetRawComponentRow(physicalRow));
    }

    private void AcquireWrite(out uint writeTick, out Stamp writeStamp)
    {
        _writeSession.Acquire(_sessionGeneration, out writeTick, out writeStamp);
    }
}

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
    private int _version = -1;
    private NativeMemory<int> _matchingArchetypes = new(0);
    private ArchetypePlan[] _matchingPlans = Array.Empty<ArchetypePlan>();
    private int[] _planIndicesByArchetype = Array.Empty<int>();
    private bool _hasWriteAccess;

    public QueryPlan(QuerySpec spec) => _description = spec;

    public bool HasWriteAccess => _hasWriteAccess;
    public void RegisterWriteAccess() => _hasWriteAccess = true;

    public ReadOnlySpan<int> MatchingArchetypes(World world)
    {
        if (_version == world.ArchetypeVersion)
        {
            return _matchingArchetypes.ReadOnlySpan;
        }

        var matches = new List<int>(world.Archetypes.Count);
        var plans = new List<ArchetypePlan>(world.Archetypes.Count);
        for (int archetypeId = 0; archetypeId < world.Archetypes.Count; archetypeId++)
        {
            var archetype = world.Archetypes[archetypeId];
            if (!Matches(archetype))
            {
                continue;
            }

            int[] indices = new int[_description.AllMask.Count];
            int componentIndex = 0;
            foreach (var componentId in _description.AllMask)
            {
                indices[componentIndex++] = archetype.Mask.Rank(componentId);
            }

            matches.Add(archetypeId);
            plans.Add(new ArchetypePlan(archetype, indices));
        }

        _matchingArchetypes.Dispose();
        _matchingArchetypes = new NativeMemory<int>(CollectionsMarshal.AsSpan(matches));
        _matchingPlans = plans.ToArray();
        _planIndicesByArchetype = new int[world.Archetypes.Count];
        Array.Fill(_planIndicesByArchetype, -1);
        for (int planIndex = 0; planIndex < _matchingPlans.Length; planIndex++)
        {
            _planIndicesByArchetype[_matchingPlans[planIndex].Archetype.Id] = planIndex;
        }

        _version = world.ArchetypeVersion;
        return _matchingArchetypes.ReadOnlySpan;
    }

    public ArchetypePlan[] MatchingPlans(World world)
    {
        MatchingArchetypes(world);
        for (int index = 0; index < _matchingPlans.Length; index++)
        {
            _matchingPlans[index].RefreshChunks();
        }

        return _matchingPlans;
    }

    public ReadOnlySpan<int> ComponentRowIndices(int matchingIndex) => _matchingPlans[matchingIndex].ComponentRows;

    public bool TryGetPlan(int archetypeId, out ArchetypePlan plan)
    {
        if ((uint)archetypeId < (uint)_planIndicesByArchetype.Length)
        {
            int planIndex = _planIndicesByArchetype[archetypeId];
            if (planIndex >= 0)
            {
                plan = _matchingPlans[planIndex];
                return true;
            }
        }

        plan = default;
        return false;
    }

    internal void Dispose() => _matchingArchetypes.Dispose();

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
        Chunks = Array.Empty<ChunkPlan>();
    }

    public Archetype Archetype { get; }
    public int[] ComponentRows { get; }
    public ChunkPlan[] Chunks { get; private set; }

    public void RefreshChunks()
    {
        var activeChunks = Archetype.ActiveChunks;
        if (Chunks.Length == activeChunks.Length)
        {
            int index = 0;
            for (; index < activeChunks.Length; index++)
            {
                if (!ReferenceEquals(Chunks[index].Chunk, activeChunks[index]))
                {
                    break;
                }
            }

            if (index == activeChunks.Length)
            {
                return;
            }
        }

        var chunks = new ChunkPlan[activeChunks.Length];
        for (int chunkIndex = 0; chunkIndex < activeChunks.Length; chunkIndex++)
        {
            var chunk = activeChunks[chunkIndex];
            var sourceRows = chunk.RawComponentRows;
            var resolvedRows = new Array[ComponentRows.Length];
            for (int queryRow = 0; queryRow < ComponentRows.Length; queryRow++)
            {
                resolvedRows[queryRow] = sourceRows[ComponentRows[queryRow]];
            }

            chunks[chunkIndex] = new ChunkPlan(chunk, resolvedRows);
        }

        Chunks = chunks;
    }
}

internal readonly record struct ChunkPlan(Chunk Chunk, Array[] ComponentRows);

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
