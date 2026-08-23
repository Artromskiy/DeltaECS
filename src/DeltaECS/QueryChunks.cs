namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>Independent iterator over the active chunks of one dense archetype.</summary>
public ref struct QueryChunks
{
    private readonly DenseArchetypePlan _plan;
    private readonly QueryPlan _query;
    private readonly Chunk[] _chunks;
    private readonly int _count;
    private readonly uint _writeTick;
    private int _index;

    internal QueryChunks(DenseArchetypePlan plan, QueryPlan query, uint writeTick)
    {
        _plan = plan;
        _query = query;
        _chunks = plan.Archetype.ActiveChunks;
        _count = plan.Archetype.ActiveChunkCount;
        _writeTick = writeTick;
        _index = -1;
    }

    public QueryChunk Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)_index >= (uint)_count)
            {
                QueryThrowHelper.ThrowChunkIteratorNotPositioned();
            }

            return new QueryChunk(_plan, _chunks.Ref(_index), _query, _writeTick);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if ((uint)++_index >= (uint)_count)
        {
            return false;
        }

        return true;
    }
}

/// <summary>Current dense chunk without archetype- or query-iterator state.</summary>
public readonly ref struct QueryChunk
{
    private readonly DenseArchetypePlan _plan;
    private readonly Chunk _chunk;
    private readonly QueryPlan _query;
    private readonly uint _writeTick;

    internal QueryChunk(DenseArchetypePlan plan, Chunk chunk, QueryPlan query, uint writeTick)
    {
        _plan = plan;
        _chunk = chunk;
        _query = query;
        _writeTick = writeTick;
    }

    public int ArchetypeId => _plan.Archetype.Id;

    public int GlobalChunkId => _chunk.GlobalId;

    public int SlotCount => _chunk.Count;

    public ReadOnlySpan<Entity> Entities => _chunk.Entities;

    public QuerySlots Slots => new(_plan, _chunk, _query, _writeTick);
}
