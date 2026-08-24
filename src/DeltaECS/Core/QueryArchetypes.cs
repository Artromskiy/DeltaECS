namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>Independent iterator over the query's matching archetypes.</summary>
public ref struct QueryArchetypes
{
    private readonly ReadOnlySpan<ArchetypePlan> _plans;
    private readonly QueryPlan _query;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;
    private int _index;

    internal QueryArchetypes(
        ArchetypePlan[] plans,
        QueryPlan query,
        QueryWriteSession writeSession,
        int sessionGeneration)
    {
        _plans = plans;
        _query = query;
        _writeSession = writeSession;
        _sessionGeneration = sessionGeneration;
        _index = -1;
    }

    public QueryArchetype Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)_index >= (uint)_plans.Length)
            {
                QueryThrowHelper.ThrowArchetypeIteratorNotPositioned();
            }

            return new QueryArchetype(_plans.Ref(_index), _query, _writeSession, _sessionGeneration);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        _writeSession.EnsureActive(_sessionGeneration);
        if ((uint)++_index >= (uint)_plans.Length)
        {
            return false;
        }

        return true;
    }
}

/// <summary>Current matching archetype and its row plan.</summary>
public readonly ref struct QueryArchetype
{
    private readonly ArchetypePlan _plan;
    private readonly QueryPlan _query;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;

    internal QueryArchetype(ArchetypePlan plan, QueryPlan query, QueryWriteSession writeSession, int sessionGeneration)
    {
        _plan = plan;
        _query = query;
        _writeSession = writeSession;
        _sessionGeneration = sessionGeneration;
    }

    public int ArchetypeId => _plan.Archetype.Id;

    public int EntityCount => _plan.Archetype.EntityCount;

    public int ChunkCount => _plan.Archetype.ActiveChunkCount;

    public QueryArchetypeChunks Chunks => new(_plan, _query, _writeSession, _sessionGeneration);
}
