namespace Delta.ECS;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>Trusted compiler-support slot iterator for one validated query chunk.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedQuerySlots
{
    private readonly Chunk _chunk;
    private readonly Array[] _resolvedRowsByQuery;
    private readonly int _count;
    private readonly int _offset;

    internal GeneratedQuerySlots(in ChunkPlan chunkPlan)
        : this(in chunkPlan, chunkPlan.Chunk.Count)
    {
    }

    internal GeneratedQuerySlots(in ChunkPlan chunkPlan, int count)
        : this(in chunkPlan, count, 0)
    {
    }

    internal GeneratedQuerySlots(in ChunkPlan chunkPlan, int count, int offset)
    {
        _chunk = chunkPlan.Chunk;
        _resolvedRowsByQuery = chunkPlan.ComponentRows;
        _count = count;
        _offset = offset;
    }

    /// <summary>Gets the number of entities in the validated chunk.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>Gets the stable identity of the current chunk.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int ChunkId => _chunk.GlobalId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Entity EntityAt(int index)
        => _chunk.RawEntities.RefAt(_offset + index);

    /// <summary>Gets the trusted first element of a validated read row.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref T GetGeneratedReadReference<T>(int queryComponentIndex)
        => ref Unsafe.Add(
            ref Unsafe.As<T[]>(_resolvedRowsByQuery.RefAt(queryComponentIndex)).GetRefAtZero(),
            _offset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref T GetGeneratedReadReference<T>(ReadAccess access)
        => ref GetGeneratedReadReference<T>(access.QueryComponentIndex);

    /// <summary>Marks and gets the trusted first element of a validated write row.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref T GetGeneratedWriteReference<T>(int queryComponentIndex)
        => ref Unsafe.Add(
            ref Unsafe.As<T[]>(_resolvedRowsByQuery.RefAt(queryComponentIndex)).GetRefAtZero(),
            _offset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref T GetGeneratedWriteReference<T>(WriteAccess access)
        => ref GetGeneratedWriteReference<T>(access.QueryComponentIndex);
}

/// <summary>Trusted compiler-support slot iterator for a read-only query chunk.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedReadQuerySlots
{
    private readonly Chunk _chunk;
    private readonly Array[] _resolvedRowsByQuery;
    private readonly int _count;

    internal GeneratedReadQuerySlots(in ChunkPlan chunkPlan)
    {
        _chunk = chunkPlan.Chunk;
        _resolvedRowsByQuery = chunkPlan.ComponentRows;
        _count = chunkPlan.Chunk.Count;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity EntityAt(int index)
        => _chunk.RawEntities.RefAt(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetGeneratedReadReference<T>(int queryComponentIndex)
        => ref Unsafe.As<T[]>(_resolvedRowsByQuery.RefAt(queryComponentIndex)).GetRefAtZero();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetGeneratedReadReference<T>(ReadAccess access)
        => ref GetGeneratedReadReference<T>(access.QueryComponentIndex);
}
