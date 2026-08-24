namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>Independent iterator over every active chunk in a query scope.</summary>
public ref struct QueryChunks
{
    private readonly ReadOnlySpan<ArchetypePlan> _plans;
    private readonly QueryPlan _query;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;
    private ReadOnlySpan<ChunkPlan> _chunks;
    private int _planIndex;
    private int _chunkIndex;

    internal QueryChunks(
        ArchetypePlan[] plans,
        QueryPlan query,
        QueryWriteSession writeSession,
        int sessionGeneration)
    {
        _plans = plans;
        _query = query;
        _writeSession = writeSession;
        _sessionGeneration = sessionGeneration;
        _chunks = default;
        _planIndex = -1;
        _chunkIndex = -1;
    }

    public QueryChunk Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)_planIndex >= (uint)_plans.Length
                || (uint)_chunkIndex >= (uint)_chunks.Length)
            {
                QueryThrowHelper.ThrowChunkIteratorNotPositioned();
            }

            return new QueryChunk(
                _plans.Ref(_planIndex),
                _chunks.Ref(_chunkIndex),
                _query,
                _writeSession,
                _sessionGeneration);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        _writeSession.EnsureActive(_sessionGeneration);
        int nextChunk = _chunkIndex + 1;
        if ((uint)nextChunk < (uint)_chunks.Length)
        {
            _chunkIndex = nextChunk;
            return true;
        }

        return MoveNextArchetype();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool MoveNextArchetype()
    {
        while ((uint)++_planIndex < (uint)_plans.Length)
        {
            _chunks = _plans.Ref(_planIndex).Chunks;
            if (!_chunks.IsEmpty)
            {
                _chunkIndex = 0;
                return true;
            }
        }

        _chunks = default;
        _chunkIndex = -1;
        return false;
    }
}

/// <summary>Independent iterator over the active chunks of one selected archetype.</summary>
public ref struct QueryArchetypeChunks
{
    private readonly ArchetypePlan _plan;
    private readonly QueryPlan _query;
    private readonly ReadOnlySpan<ChunkPlan> _chunks;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;
    private int _index;

    internal QueryArchetypeChunks(
        ArchetypePlan plan,
        QueryPlan query,
        QueryWriteSession writeSession,
        int sessionGeneration)
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
