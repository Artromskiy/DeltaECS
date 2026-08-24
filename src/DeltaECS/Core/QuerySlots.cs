namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

/// <summary>Forward slot iterator for one already-selected chunk.</summary>
public ref struct QuerySlots
{
    private readonly int[] _componentRowsByQuery;
    private readonly Chunk _chunk;
    private readonly Array[] _resolvedRowsByQuery;
    private readonly QueryPlan _query;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;
    private readonly int _count;
    private int _index;

    internal QuerySlots(
        ArchetypePlan plan,
        ChunkPlan chunkPlan,
        QueryPlan query,
        QueryWriteSession writeSession,
        int sessionGeneration)
    {
        _componentRowsByQuery = plan.ComponentRows;
        _chunk = chunkPlan.Chunk;
        _resolvedRowsByQuery = chunkPlan.ComponentRows;
        _query = query;
        _writeSession = writeSession;
        _sessionGeneration = sessionGeneration;
        _count = chunkPlan.Chunk.Count;
        _index = -1;
    }

    internal QuerySlots(ArchetypePlan plan, ChunkPlan chunkPlan, QueryPlan query, uint writeTick, Stamp writeStamp)
    {
        _componentRowsByQuery = plan.ComponentRows;
        _chunk = chunkPlan.Chunk;
        _resolvedRowsByQuery = chunkPlan.ComponentRows;
        _query = query;
        _writeSession = new QueryWriteSession();
        _sessionGeneration = _writeSession.Reset(writeTick, writeStamp);
        _count = chunkPlan.Chunk.Count;
        _index = -1;
    }

    public int CurrentIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _index;
    }

    public Entity CurrentEntity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunk.Entities[_index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return ++_index < _count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadRow GetRow(ReadAccess access)
    {
        _writeSession.EnsureActive(_sessionGeneration);
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        return new ReadRow(_resolvedRowsByQuery.Ref(access.QueryComponentIndex));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WriteRow GetRow(WriteAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        _writeSession.Acquire(_sessionGeneration, out uint writeTick, out Stamp writeStamp);
        int physicalRow = _componentRowsByQuery.Ref(access.QueryComponentIndex);
        _chunk.MarkComponentWritten(physicalRow, writeTick, writeStamp);
        return new WriteRow(_resolvedRowsByQuery.Ref(access.QueryComponentIndex));
    }

    public ObjectReadValues GetObject(ReadAccess access)
    {
        _writeSession.EnsureActive(_sessionGeneration);
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        return new ObjectReadValues(_resolvedRowsByQuery.Ref(access.QueryComponentIndex));
    }

    public ObjectWriteValues GetObject(WriteAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        _writeSession.Acquire(_sessionGeneration, out uint writeTick, out Stamp writeStamp);
        int physicalRow = _componentRowsByQuery.Ref(access.QueryComponentIndex);
        _chunk.MarkComponentWritten(physicalRow, writeTick, writeStamp);
        return new ObjectWriteValues(_resolvedRowsByQuery.Ref(access.QueryComponentIndex));
    }

}
