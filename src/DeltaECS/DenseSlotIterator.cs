namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>Reverse dense slot iterator for one already-selected chunk.</summary>
public ref struct DenseSlotIterator
{
    private readonly DenseArchetypePlan _plan;
    private readonly Chunk _chunk;
    private readonly uint _writeTick;
    private int _index;

    internal DenseSlotIterator(DenseArchetypePlan plan, Chunk chunk, uint writeTick)
    {
        _plan = plan;
        _chunk = chunk;
        _writeTick = writeTick;
        _index = chunk.Count;
    }

    public int CurrentIndex => _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        var next = _index - 1;
        if (next < 0)
        {
            _index = -1;
            return false;
        }

        _index = next;
        return true;
    }

    public ResolvedReadRow<T> Resolve<T>(DenseReadBinding<T> binding)
    {
        if (!ReferenceEquals(binding.Query, _plan.Query))
        {
            QueryThrowHelper.ThrowBindingMismatch();
        }

        var physicalRow = _plan.ComponentRows[binding.QueryComponentIndex];
        return new ResolvedReadRow<T>(_chunk.GetComponentRow<T>(physicalRow));
    }

    public ResolvedWriteRow<T> Resolve<T>(DenseWriteBinding<T> binding)
    {
        if (!ReferenceEquals(binding.Query, _plan.Query))
        {
            QueryThrowHelper.ThrowBindingMismatch();
        }

        var physicalRow = _plan.ComponentRows[binding.QueryComponentIndex];
        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new ResolvedWriteRow<T>(_chunk.GetComponentRow<T>(physicalRow));
    }
}
