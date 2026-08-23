namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

/// <summary>Forward dense slot iterator for one already-selected chunk.</summary>
public ref struct QuerySlots
{
    private readonly int[] _componentRowsByQuery;
    private readonly Chunk _chunk;
    private readonly Array[] _componentRows;
    private readonly QueryPlan _query;
    private readonly uint _writeTick;
    private readonly int _count;
    private int _index;

    internal QuerySlots(DenseArchetypePlan plan, Chunk chunk, QueryPlan query, uint writeTick)
    {
        _componentRowsByQuery = plan.ComponentRows;
        _chunk = chunk;
        _componentRows = chunk.RawComponentRows;
        _query = query;
        _writeTick = writeTick;
        _count = chunk.Count;
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

        int physicalRow = _componentRowsByQuery.Ref(access.QueryComponentIndex);
        return new ReadValues(_componentRows.Ref(physicalRow));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WriteValues Get(WriteAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRowsByQuery.Ref(access.QueryComponentIndex);
        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new WriteValues(_componentRows.Ref(physicalRow));
    }

    public ObjectReadValues GetObject(ReadAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRowsByQuery.Ref(access.QueryComponentIndex);
        return new ObjectReadValues(_componentRows.Ref(physicalRow));
    }

    public ObjectWriteValues GetObject(WriteAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRowsByQuery.Ref(access.QueryComponentIndex);
        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new ObjectWriteValues(_componentRows.Ref(physicalRow));
    }

}
