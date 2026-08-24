namespace Delta.ECS;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>Trusted compiler-support slot iterator for one validated query chunk.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedQuerySlots
{
    private readonly int[] _componentRowsByQuery;
    private readonly Chunk _chunk;
    private readonly Array[] _resolvedRowsByQuery;
    private readonly uint _writeTick;
    private readonly Stamp _writeStamp;
    private readonly int _count;
    private int _index;

    internal GeneratedQuerySlots(
        ArchetypePlan plan,
        ChunkPlan chunkPlan,
        uint writeTick,
        Stamp writeStamp)
    {
        _componentRowsByQuery = plan.ComponentRows;
        _chunk = chunkPlan.Chunk;
        _resolvedRowsByQuery = chunkPlan.ComponentRows;
        _writeTick = writeTick;
        _writeStamp = writeStamp;
        _count = chunkPlan.Chunk.Count;
        _index = -1;
    }

    public int CurrentIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _index;
    }

    public Entity CurrentEntity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunk.Entities[_index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => ++_index < _count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ReadRow GetGeneratedReadRow(int queryComponentIndex)
        => new(_resolvedRowsByQuery.Ref(queryComponentIndex));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public WriteRow GetGeneratedWriteRow(int queryComponentIndex)
    {
        int physicalRow = _componentRowsByQuery.Ref(queryComponentIndex);
        _chunk.MarkComponentWrittenTrusted(physicalRow, _writeTick, _writeStamp);
        return new WriteRow(_resolvedRowsByQuery.Ref(queryComponentIndex));
    }
}
