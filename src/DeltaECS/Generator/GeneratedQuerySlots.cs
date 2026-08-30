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
    private int _index;

    internal GeneratedQuerySlots(
        ArchetypePlan plan,
        ChunkPlan chunkPlan)
    {
        _chunk = chunkPlan.Chunk;
        _resolvedRowsByQuery = chunkPlan.ComponentRows;
        _count = chunkPlan.Chunk.Count;
        _index = -1;
    }

    public int CurrentIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _index;
    }

    /// <summary>Gets the number of entities in the validated chunk.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    public Entity CurrentEntity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunk.RawEntities.Ref(_index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Entity EntityAt(int index)
        => _chunk.RawEntities.Ref(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => ++_index < _count;

    /// <summary>Gets the trusted first element of a validated read row.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref T GetGeneratedReadReference<T>(int queryComponentIndex)
        => ref Unsafe.As<byte, T>(ref ArrayAccess.DataReference(_resolvedRowsByQuery.Ref(queryComponentIndex)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref T GetGeneratedReadReference<T>(ReadAccess access)
        => ref GetGeneratedReadReference<T>(access.QueryComponentIndex);

    /// <summary>Marks and gets the trusted first element of a validated write row.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref T GetGeneratedWriteReference<T>(int queryComponentIndex)
        => ref Unsafe.As<byte, T>(ref ArrayAccess.DataReference(_resolvedRowsByQuery.Ref(queryComponentIndex)));

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
    private int _index;

    internal GeneratedReadQuerySlots(ChunkPlan chunkPlan)
    {
        _chunk = chunkPlan.Chunk;
        _resolvedRowsByQuery = chunkPlan.ComponentRows;
        _count = chunkPlan.Chunk.Count;
        _index = -1;
    }

    public int CurrentIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _index;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    public Entity CurrentEntity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunk.RawEntities.Ref(_index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity EntityAt(int index)
        => _chunk.RawEntities.Ref(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => ++_index < _count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetGeneratedReadReference<T>(int queryComponentIndex)
        => ref Unsafe.As<byte, T>(ref ArrayAccess.DataReference(_resolvedRowsByQuery.Ref(queryComponentIndex)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetGeneratedReadReference<T>(ReadAccess access)
        => ref GetGeneratedReadReference<T>(access.QueryComponentIndex);
}
