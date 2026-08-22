namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

/// <summary>Reverse dense slot iterator for one already-selected chunk.</summary>
public ref struct QuerySlots
{
    private readonly DenseArchetypePlan _plan;
    private readonly Chunk _chunk;
    private readonly QueryPlan _query;
    private readonly uint _writeTick;
    private int _index;

    internal QuerySlots(DenseArchetypePlan plan, Chunk chunk, QueryPlan query, uint writeTick)
    {
        _plan = plan;
        _chunk = chunk;
        _query = query;
        _writeTick = writeTick;
        _index = chunk.Count;
    }

    public int CurrentIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (--_index < 0)
        {
            _index = -1;
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadValues Get(ReadAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var physicalRow = _plan.ComponentRows.Element(access.QueryComponentIndex);
        return new ReadValues(_chunk.GetRawComponentRow(physicalRow), access.RuntimeType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WriteValues Get(WriteAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var physicalRow = _plan.ComponentRows.Element(access.QueryComponentIndex);
        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new WriteValues(_chunk.GetRawComponentRow(physicalRow), access.RuntimeType);
    }
}
