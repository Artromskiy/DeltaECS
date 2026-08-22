namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>Independent dense iterator over the query's matching archetypes.</summary>
public ref struct DenseArchetypeIterator
{
    private readonly DenseArchetypePlan[] _plans;
    private readonly CachedQuery _query;
    private readonly uint _writeTick;
    private int _index;

    internal DenseArchetypeIterator(DenseArchetypePlan[] plans, CachedQuery query, uint writeTick)
    {
        _plans = plans;
        _query = query;
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

            return new DenseQueryArchetype(_plans.Element(_index), _query, _writeTick);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if ((uint)++_index >= (uint)_plans.Length)
        {
            return false;
        }

        return true;
    }
}

/// <summary>Current matching archetype and its dense row plan.</summary>
public readonly ref struct DenseQueryArchetype
{
    private readonly DenseArchetypePlan _plan;
    private readonly CachedQuery _query;
    private readonly uint _writeTick;

    internal DenseQueryArchetype(DenseArchetypePlan plan, CachedQuery query, uint writeTick)
    {
        _plan = plan;
        _query = query;
        _writeTick = writeTick;
    }

    public int ArchetypeId => _plan.Archetype.Id;

    public int EntityCount => _plan.Archetype.EntityCount;

    public int ChunkCount => _plan.Archetype.ActiveChunkCount;

    public DenseChunkIterator Chunks => new(_plan, _query, _writeTick);
}
