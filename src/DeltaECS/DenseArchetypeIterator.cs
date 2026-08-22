namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>Independent dense iterator over the query's matching archetypes.</summary>
public ref struct DenseArchetypeIterator
{
    private readonly DenseArchetypePlan[] _plans;
    private readonly uint _writeTick;
    private int _index;

    internal DenseArchetypeIterator(DenseArchetypePlan[] plans, uint writeTick)
    {
        _plans = plans;
        _writeTick = writeTick;
        _index = -1;
    }

    public DenseQueryArchetype Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)_index >= (uint)_plans.Length)
            {
                QueryThrowHelper.ThrowArchetypeIteratorNotPositioned();
            }

            return new DenseQueryArchetype(_plans.Element(_index), _writeTick);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        var next = _index + 1;
        if ((uint)next >= (uint)_plans.Length)
        {
            _index = _plans.Length;
            return false;
        }

        _index = next;
        return true;
    }
}

/// <summary>Current matching archetype and its dense row plan.</summary>
public readonly ref struct DenseQueryArchetype
{
    private readonly DenseArchetypePlan _plan;
    private readonly uint _writeTick;

    internal DenseQueryArchetype(DenseArchetypePlan plan, uint writeTick)
    {
        _plan = plan;
        _writeTick = writeTick;
    }

    public int ArchetypeId => _plan.Archetype.Id;

    public int EntityCount => _plan.Archetype.EntityCount;

    public int ChunkCount => _plan.Archetype.ActiveChunkCount;

    public DenseChunkIterator Chunks => new(_plan, _writeTick);
}
