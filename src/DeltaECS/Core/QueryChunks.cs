namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>Independent iterator over the active chunks of one archetype.</summary>
public ref struct QueryChunks
{
    private readonly ArchetypePlan _plan;
    private readonly QueryPlan _query;
    private readonly ReadOnlySpan<ChunkPlan> _chunks;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;
    private int _index;

    internal QueryChunks(ArchetypePlan plan, QueryPlan query, QueryWriteSession writeSession, int sessionGeneration)
    {
        _plan = plan;
        _query = query;
        _chunks = plan.Chunks;
        _writeSession = writeSession;
        _sessionGeneration = sessionGeneration;
        _index = -1;
    }

    public QueryChunk Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)_index >= (uint)_chunks.Length)
            {
                QueryThrowHelper.ThrowChunkIteratorNotPositioned();
            }

            return new QueryChunk(_plan, _chunks.Ref(_index), _query, _writeSession, _sessionGeneration);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        _writeSession.EnsureActive(_sessionGeneration);
        if ((uint)++_index >= (uint)_chunks.Length)
        {
            return false;
        }

        return true;
    }
}

/// <summary>Current chunk without archetype- or query-iterator state.</summary>
public readonly ref struct QueryChunk
{
    private readonly ArchetypePlan _plan;
    private readonly ChunkPlan _chunk;
    private readonly QueryPlan _query;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;

    internal QueryChunk(
        ArchetypePlan plan,
        ChunkPlan chunk,
        QueryPlan query,
        QueryWriteSession writeSession,
        int sessionGeneration)
    {
        _plan = plan;
        _chunk = chunk;
        _query = query;
        _writeSession = writeSession;
        _sessionGeneration = sessionGeneration;
    }

    public int ArchetypeId => _plan.Archetype.Id;

    public int GlobalChunkId => _chunk.Chunk.GlobalId;

    public int SlotCount => _chunk.Chunk.Count;

    public ReadOnlySpan<Entity> Entities => _chunk.Chunk.Entities;

    public QuerySlots Slots => new(_plan, _chunk, _query, _writeSession, _sessionGeneration);
}
