namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>Independent iterator over the active chunks of one dense archetype.</summary>
public ref struct DenseChunkIterator
{
    private readonly DenseArchetypePlan _plan;
    private readonly Chunk[] _chunks;
    private readonly int _count;
    private readonly uint _writeTick;
    private int _index;

    internal DenseChunkIterator(DenseArchetypePlan plan, uint writeTick)
    {
        _plan = plan;
        _chunks = plan.Archetype.ActiveChunks;
        _count = plan.Archetype.ActiveChunkCount;
        _writeTick = writeTick;
        _index = -1;
    }

    public DenseQueryChunk Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)_index >= (uint)_count)
            {
                QueryThrowHelper.ThrowChunkIteratorNotPositioned();
            }

            return new DenseQueryChunk(_plan, _chunks.Element(_index), _writeTick);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        var next = _index + 1;
        if ((uint)next >= (uint)_count)
        {
            _index = _count;
            return false;
        }

        _index = next;
        return true;
    }
}

/// <summary>Current dense chunk without archetype- or query-iterator state.</summary>
public readonly ref struct DenseQueryChunk
{
    private readonly DenseArchetypePlan _plan;
    private readonly Chunk _chunk;
    private readonly uint _writeTick;

    internal DenseQueryChunk(DenseArchetypePlan plan, Chunk chunk, uint writeTick)
    {
        _plan = plan;
        _chunk = chunk;
        _writeTick = writeTick;
    }

    public int ArchetypeId => _plan.Archetype.Id;

    public int GlobalChunkId => _chunk.GlobalId;

    public int SlotCount => _chunk.Count;

    public ReadOnlySpan<Entity> Entities => _chunk.Entities;

    public DenseSlotIterator Slots => new(_plan, _chunk, _writeTick);
}
