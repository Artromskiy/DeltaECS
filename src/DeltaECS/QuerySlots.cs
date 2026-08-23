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
        return new ReadValues(_chunk.GetRawComponentRow(physicalRow));
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
        return new WriteValues(_chunk.GetRawComponentRow(physicalRow));
    }

    [Obsolete("Use AccessRequest with BindRead and non-generic values.")]
    public ReadValues Get(ReadRequest request)
    {
        if (!ReferenceEquals(request.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var physicalRow = _plan.ComponentRows.Element(request.QueryComponentIndex);
        return new ReadValues(_chunk.GetRawComponentRow(physicalRow));
    }

    [Obsolete("Use AccessRequest with BindWrite and non-generic values.")]
    public WriteValues Get(WriteRequest request)
    {
        if (!ReferenceEquals(request.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var physicalRow = _plan.ComponentRows.Element(request.QueryComponentIndex);
        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new WriteValues(_chunk.GetRawComponentRow(physicalRow));
    }

    // Obsolete source-compatibility path. The L4 path above is non-generic.
    [Obsolete("Use non-generic ReadAccess and values.Ref<T>(slots).")]
    public ReadValues<T> Get<T>(ReadRequest<T> request)
    {
        if (!ReferenceEquals(request.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var physicalRow = _plan.ComponentRows.Element(request.QueryComponentIndex);
        return new ReadValues<T>(_chunk.GetComponentRow<T>(physicalRow));
    }

    // Obsolete source-compatibility path. The L4 path above is non-generic.
    [Obsolete("Use non-generic WriteAccess and values.Ref<T>(slots).")]
    public WriteValues<T> Get<T>(WriteRequest<T> request)
    {
        if (!ReferenceEquals(request.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var physicalRow = _plan.ComponentRows.Element(request.QueryComponentIndex);
        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new WriteValues<T>(_chunk.GetComponentRow<T>(physicalRow));
    }

    /// <summary>Compatibility generic call; the returned values object remains non-generic.</summary>
    [Obsolete("Use Get(ReadAccess); the generic argument is compatibility-only.")]
    public ReadValues Get<T>(ReadAccess access) => Get(access);

    /// <summary>Compatibility generic call; the returned values object remains non-generic.</summary>
    [Obsolete("Use Get(WriteAccess); the generic argument is compatibility-only.")]
    public WriteValues Get<T>(WriteAccess access) => Get(access);
}
