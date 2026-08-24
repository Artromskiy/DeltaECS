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
    private readonly uint _writeTick;
    private readonly Stamp _writeStamp;
    private readonly int _count;
    private int _index;

    internal QuerySlots(ArchetypePlan plan, ChunkPlan chunkPlan, QueryPlan query, uint writeTick, Stamp writeStamp)
    {
        _componentRowsByQuery = plan.ComponentRows;
        _chunk = chunkPlan.Chunk;
        _resolvedRowsByQuery = chunkPlan.ComponentRows;
        _query = query;
        _writeTick = writeTick;
        _writeStamp = writeStamp;
        _count = chunkPlan.Chunk.Count;
        _index = -1;
    }

    public int CurrentIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return ++_index < _count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadValues Get(ReadAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        return new ReadValues(_resolvedRowsByQuery.Ref(access.QueryComponentIndex));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WriteValues Get(WriteAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRowsByQuery.Ref(access.QueryComponentIndex);
        _chunk.MarkComponentWritten(physicalRow, _writeTick, _writeStamp);
        return new WriteValues(_resolvedRowsByQuery.Ref(access.QueryComponentIndex));
    }

    public ObjectReadValues GetObject(ReadAccess access)
    {
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

        int physicalRow = _componentRowsByQuery.Ref(access.QueryComponentIndex);
        _chunk.MarkComponentWritten(physicalRow, _writeTick, _writeStamp);
        return new ObjectWriteValues(_resolvedRowsByQuery.Ref(access.QueryComponentIndex));
    }

}
