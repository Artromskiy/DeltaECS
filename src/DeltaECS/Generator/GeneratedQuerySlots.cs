namespace Delta.ECS;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>Trusted compiler-support slot iterator for one validated query chunk.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedQuerySlots
{
    private readonly World _world;
    private readonly Chunk _chunk;
    private readonly Span<Entity> _entities;
    private readonly Array[] _resolvedRowsByQuery;
    private readonly int[] _componentIndices;
    private readonly int _count;
    private readonly int _offset;
    private readonly ReadOnlySpan<int> _tagSlots;
    private readonly bool _hasTagSlots;
    private readonly QueryPlan? _queryPlan;

    internal GeneratedQuerySlots(World world, in ChunkPlan chunkPlan, QueryPlan? queryPlan = null)
        : this(world, in chunkPlan, chunkPlan.Chunk.Count, 0, queryPlan)
    {
    }

    internal GeneratedQuerySlots(World world, in ChunkPlan chunkPlan, int count, QueryPlan? queryPlan = null)
        : this(world, in chunkPlan, count, 0, queryPlan)
    {
    }

    internal GeneratedQuerySlots(World world, in ChunkPlan chunkPlan, int count, int offset, QueryPlan? queryPlan = null)
    {
        _world = world;
        _chunk = chunkPlan.Chunk;
        _entities = _chunk.RawEntities;
        _resolvedRowsByQuery = chunkPlan.ComponentRows;
        _componentIndices = chunkPlan.ComponentIndices;
        _count = count;
        _offset = offset;
        _queryPlan = queryPlan;
        _hasTagSlots = queryPlan is not null && offset == 0 && queryPlan.TryGetTagSlots(_chunk, out _tagSlots);
        if (!_hasTagSlots)
        {
            _tagSlots = default;
        }
    }

    /// <summary>Gets the number of entities in the validated chunk.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _hasTagSlots ? _tagSlots.Length : _count;
    }

    /// <summary>Gets the physical chunk population before any tag filter is applied.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int PhysicalCount => _count;

    /// <summary>Reports whether the owning query applies tag filters.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool HasTagFilters => _queryPlan?.HasTagFilters ?? false;

    /// <summary>Tests whether a physical chunk slot satisfies the query's tag filters.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTagSelected(int slotIndex)
        => _queryPlan is null || !_queryPlan.HasTagFilters || _queryPlan.MatchesTagSlot(_chunk, slotIndex);

    /// <summary>Gets the stable identity of the current chunk.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int ChunkId => _chunk.GlobalId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Entity EntityAt(int index)
        => _entities.RefAt(_hasTagSlots ? _tagSlots.RefAt(index) : _offset + index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryGetTagSlots(out ReadOnlySpan<int> slots)
    {
        slots = _tagSlots;
        return _hasTagSlots;
    }

    /// <summary>Maps a logical query index to its physical slot within the chunk.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int GetGeneratedSlotIndex(int index)
        => _hasTagSlots ? _tagSlots.RefAt(index) : _offset + index;

    /// <summary>Gets the first entity reference for the current validated slot range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref Entity GetGeneratedEntityReference()
        => ref Unsafe.Add(ref _entities.GetRefAtZero(), _offset);

    /// <summary>Gets the trusted first element of a validated read row.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref T GetGeneratedReadReference<T>(int queryComponentIndex)
        => ref Unsafe.Add(
            ref Unsafe.As<T[]>(_resolvedRowsByQuery.RefAt(queryComponentIndex)).GetRefAtZero(),
            _offset);

    /// <summary>Gets the validated component array for generated chunk-row binding.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public T[] GetGeneratedArray<T>(int queryComponentIndex)
        => Unsafe.As<T[]>(_resolvedRowsByQuery.RefAt(queryComponentIndex));

    /// <summary>Gets the validated component array for generated chunk-row binding.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public T[] GetGeneratedArray<T>(ReadAccess access)
        => GetGeneratedArray<T>(access.QueryComponentIndex);

    /// <summary>Gets the validated component array for generated chunk-row binding.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public T[] GetGeneratedArray<T>(WriteAccess access)
        => GetGeneratedArray<T>(access.QueryComponentIndex);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Stamp GetGeneratedStamp(int queryComponentIndex, int index)
        => _world.GetComponentStamp(
            _chunk.ArchetypeId,
            _chunk,
            _componentIndices.RefAt(queryComponentIndex),
            _hasTagSlots ? _tagSlots.RefAt(index) : _offset + index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Stamp GetGeneratedStamp(ReadAccess access, int index)
        => GetGeneratedStamp(access.QueryComponentIndex, index);
}

/// <summary>Trusted compiler-support slot iterator for a read-only query chunk.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedReadQuerySlots
{
    private readonly World _world;
    private readonly Chunk _chunk;
    private readonly ReadOnlySpan<Entity> _entities;
    private readonly Array[] _resolvedRowsByQuery;
    private readonly int[] _componentIndices;
    private readonly int _count;
    private readonly ReadOnlySpan<int> _tagSlots;
    private readonly bool _hasTagSlots;

    internal GeneratedReadQuerySlots(World world, in ChunkPlan chunkPlan, QueryPlan? queryPlan = null)
    {
        _world = world;
        _chunk = chunkPlan.Chunk;
        _entities = _chunk.RawEntities;
        _resolvedRowsByQuery = chunkPlan.ComponentRows;
        _componentIndices = chunkPlan.ComponentIndices;
        _hasTagSlots = queryPlan is not null && queryPlan.TryGetTagSlots(_chunk, out _tagSlots);
        if (!_hasTagSlots)
        {
            _tagSlots = default;
        }

        _count = _hasTagSlots ? _tagSlots.Length : chunkPlan.Chunk.Count;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity EntityAt(int index)
        => _entities.RefAt(_hasTagSlots ? _tagSlots.RefAt(index) : index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryGetTagSlots(out ReadOnlySpan<int> slots)
    {
        slots = _tagSlots;
        return _hasTagSlots;
    }

    /// <summary>Maps a logical query index to its physical slot within the chunk.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int GetGeneratedSlotIndex(int index)
        => _hasTagSlots ? _tagSlots.RefAt(index) : index;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref readonly Entity GetGeneratedEntityReference()
        => ref _entities.GetRefAtZero();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetGeneratedReadReference<T>(int queryComponentIndex)
        => ref Unsafe.As<T[]>(_resolvedRowsByQuery.RefAt(queryComponentIndex)).GetRefAtZero();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetGeneratedReadReference<T>(ReadAccess access)
        => ref GetGeneratedReadReference<T>(access.QueryComponentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Stamp GetGeneratedStamp(int queryComponentIndex, int index)
        => _world.GetComponentStamp(
            _chunk.ArchetypeId,
            _chunk,
            _componentIndices.RefAt(queryComponentIndex),
            _hasTagSlots ? _tagSlots.RefAt(index) : index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Stamp GetGeneratedStamp(ReadAccess access, int index)
        => GetGeneratedStamp(access.QueryComponentIndex, index);
}
