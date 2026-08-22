namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

/// <summary>Reverse dense slot iterator for one already-selected chunk.</summary>
public ref struct DenseSlotIterator
{
    private readonly DenseArchetypePlan _plan;
    private readonly Chunk _chunk;
    private readonly CachedQuery _query;
    private readonly Array[] _componentRows;
    private readonly uint _writeTick;
    private int _index;

    internal DenseSlotIterator(DenseArchetypePlan plan, Chunk chunk, CachedQuery query, uint writeTick)
    {
        _plan = plan;
        _chunk = chunk;
        _query = query;
        _componentRows = chunk.RawComponentRows;
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
    public ResolvedReadRow<T> Resolve<T>(DenseReadBinding<T> binding)
    {
        if (!ReferenceEquals(binding.Query, _query))
        {
            QueryThrowHelper.ThrowBindingMismatch();
        }

        var physicalRow = _plan.ComponentRows.Element(binding.QueryComponentIndex);
        return new ResolvedReadRow<T>(_chunk.GetComponentRow<T>(_componentRows, physicalRow));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ResolvedWriteRow<T> Resolve<T>(DenseWriteBinding<T> binding)
    {
        if (!ReferenceEquals(binding.Query, _query))
        {
            QueryThrowHelper.ThrowBindingMismatch();
        }

        var physicalRow = _plan.ComponentRows.Element(binding.QueryComponentIndex);
        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new ResolvedWriteRow<T>(_chunk.GetComponentRow<T>(_componentRows, physicalRow));
    }
}
