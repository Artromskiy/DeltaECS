namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>Short-lived dense chunk access used only by <see cref="World.Query{TContext}"/>.</summary>
public ref struct QueryChunkCursor
{
    private readonly QueryPlan _query;
    private readonly Chunk _chunk;
    private readonly int[] _componentRows;
    private readonly uint _writeTick;
    private readonly ulong[]? _overlayMask;
    private readonly bool _fullMask;
    private int _index;

    internal QueryChunkCursor(QueryPlan query, int archetypeId, Chunk chunk, int[] componentRows, uint writeTick, ulong[]? overlayMask, OverlayMaskResult overlayResult)
    {
        _query = query;
        ArchetypeId = archetypeId;
        _chunk = chunk;
        _componentRows = componentRows;
        _writeTick = writeTick;
        _overlayMask = overlayResult == OverlayMaskResult.Partial ? overlayMask : null;
        _fullMask = overlayResult == OverlayMaskResult.Full;
        _index = chunk.Count;
    }

    public int SlotCount => _chunk.Count;
    public int CurrentIndex => _index;
    public int ArchetypeId { get; }
    public int GlobalChunkId => _chunk.GlobalId;
    public ReadOnlySpan<Entity> Entities => _chunk.Entities;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsActiveSlot(int slotIndex)
    {
        if ((uint)slotIndex >= (uint)_chunk.Count)
        {
            return false;
        }

        return _fullMask || (_overlayMask is not null && (_overlayMask[slotIndex >> 6] & (1UL << (slotIndex & 63))) != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        var next = _index - 1;
        if (next < 0)
        {
            _index = -1;
            return false;
        }

        _index = next;
        return true;
    }

    public ReadValues Get(ReadAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var physicalRow = _componentRows[access.QueryComponentIndex];
        return new ReadValues(_chunk.GetRawComponentRow(physicalRow));
    }

    public WriteValues Get(WriteAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var physicalRow = _componentRows[access.QueryComponentIndex];
        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new WriteValues(_chunk.GetRawComponentRow(physicalRow));
    }

    [Obsolete("Use AccessRequest with BindRead and non-generic values.")]
    public ReadValues Get(ReadRequest request)
    {
        if (!ReferenceEquals(request.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        return new ReadValues(_chunk.GetRawComponentRow(_componentRows[request.QueryComponentIndex]));
    }

    [Obsolete("Use AccessRequest with BindWrite and non-generic values.")]
    public WriteValues Get(WriteRequest request)
    {
        if (!ReferenceEquals(request.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var physicalRow = _componentRows[request.QueryComponentIndex];
        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new WriteValues(_chunk.GetRawComponentRow(physicalRow));
    }

    // Obsolete source-compatibility path. The L4 path above is non-generic.
    [Obsolete("Use non-generic ReadAccess and values.Ref<T>(cursor).")]
    public ReadValues<T> Get<T>(ReadRequest<T> request)
    {
        if (!ReferenceEquals(request.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var physicalRow = _componentRows[request.QueryComponentIndex];
        return new ReadValues<T>(_chunk.GetComponentRow<T>(physicalRow));
    }

    // Obsolete source-compatibility path. The L4 path above is non-generic.
    [Obsolete("Use non-generic WriteAccess and values.Ref<T>(cursor).")]
    public WriteValues<T> Get<T>(WriteRequest<T> request)
    {
        if (!ReferenceEquals(request.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var physicalRow = _componentRows[request.QueryComponentIndex];
        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new WriteValues<T>(_chunk.GetComponentRow<T>(physicalRow));
    }

    /// <summary>Compatibility generic call; the returned values object remains non-generic.</summary>
    [Obsolete("Use Get(ReadAccess); the generic argument is compatibility-only.")]
    public ReadValues Get<T>(ReadAccess access) => Get(access);

    /// <summary>Compatibility generic call; the returned values object remains non-generic.</summary>
    [Obsolete("Use Get(WriteAccess); the generic argument is compatibility-only.")]
    public WriteValues Get<T>(WriteAccess access) => Get(access);

    public ReadValues GetRead(AccessRequest access)
    {
        if (access.IsWrite || !ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var physicalRow = _componentRows[access.QueryComponentIndex];
        return new ReadValues(_chunk.GetRawComponentRow(physicalRow));
    }

    public WriteValues GetWrite(AccessRequest access)
    {
        if (!access.IsWrite || !ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var physicalRow = _componentRows[access.QueryComponentIndex];
        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new WriteValues(_chunk.GetRawComponentRow(physicalRow));
    }
}

/// <summary>Prepared read-only values for one component row in one current chunk.</summary>
public ref struct ReadValues
{
    private readonly Array _row;

    internal ReadValues(Array row) => _row = row;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Ref<T>(QueryChunkCursor cursor)
    {
        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Unsafe.As<T[]>(_row)), cursor.CurrentIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Ref<T>(QuerySlots slots)
    {
        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Unsafe.As<T[]>(_row)), slots.CurrentIndex);
    }
}

/// <summary>Prepared writable values for one component row in one current chunk.</summary>
public ref struct WriteValues
{
    private readonly Array _row;

    internal WriteValues(Array row) => _row = row;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(QueryChunkCursor cursor)
    {
        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Unsafe.As<T[]>(_row)), cursor.CurrentIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(QuerySlots slots)
    {
        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Unsafe.As<T[]>(_row)), slots.CurrentIndex);
    }
}

// Obsolete source-compatibility values. New L4 code uses non-generic values.Ref<T>(cursor).
[Obsolete("Use non-generic ReadValues and values.Ref<T>(cursor).")]
public ref struct ReadValues<T>
{
    private readonly ReadOnlySpan<T> _row;

    internal ReadValues(ReadOnlySpan<T> row) => _row = row;

    public ref readonly T this[QueryChunkCursor cursor]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), cursor.CurrentIndex);
    }

    public ref readonly T this[QuerySlots slots]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), slots.CurrentIndex);
    }
}

// Obsolete source-compatibility values. New L4 code uses non-generic values.Ref<T>(cursor).
[Obsolete("Use non-generic WriteValues and values.Ref<T>(cursor).")]
public ref struct WriteValues<T>
{
    private readonly Span<T> _row;

    internal WriteValues(Span<T> row) => _row = row;

    public ref T this[QueryChunkCursor cursor]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), cursor.CurrentIndex);
    }

    public ref T this[QuerySlots slots]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), slots.CurrentIndex);
    }
}

/// <summary>Non-generic access request used by the type-erased query core.</summary>
public readonly struct AccessRequest
{
    internal AccessRequest(QueryPlan query, int queryComponentIndex, bool write, Type runtimeType)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
        IsWrite = write;
        RuntimeType = runtimeType;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }
    internal bool IsWrite { get; }
    internal Type RuntimeType { get; }

}

// Obsolete source-compatibility requests. New code uses AccessRequest.
[Obsolete("Use non-generic AccessRequest.")]
public readonly struct ReadRequest
{
    internal ReadRequest(AccessRequest request)
    {
        Query = request.Query;
        QueryComponentIndex = request.QueryComponentIndex;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }

    public static implicit operator ReadRequest(AccessRequest request) => new(request);
}

// Obsolete source-compatibility requests. New code uses AccessRequest.
[Obsolete("Use non-generic AccessRequest.")]
public readonly struct WriteRequest
{
    internal WriteRequest(AccessRequest request)
    {
        Query = request.Query;
        QueryComponentIndex = request.QueryComponentIndex;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }

    public static implicit operator WriteRequest(AccessRequest request) => new(request);
}

/// <summary>Non-generic query access token for a read row.</summary>
public readonly struct ReadAccess
{
    internal ReadAccess(QueryPlan query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }
}

/// <summary>Non-generic query access token for a write row.</summary>
public readonly struct WriteAccess
{
    internal WriteAccess(QueryPlan query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }
}

// Obsolete source-compatibility requests. The L4 API returns AccessRequest;
// the second conversion keeps the older request-based callers compiling.
[Obsolete("Use non-generic WriteRequest.")]
public readonly struct ReadRequest<T>
{
    internal ReadRequest(AccessRequest request)
    {
        Query = request.Query;
        QueryComponentIndex = request.QueryComponentIndex;
    }

    internal ReadRequest(QueryPlan query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }

    public static implicit operator ReadRequest<T>(AccessRequest request)
        => new(request);

    public static implicit operator ReadRequest<T>(ReadRequest request)
        => new(request.Query!, request.QueryComponentIndex);
}

[Obsolete("Use non-generic WriteRequest.")]
public readonly struct WriteRequest<T>
{
    internal WriteRequest(AccessRequest request)
    {
        Query = request.Query;
        QueryComponentIndex = request.QueryComponentIndex;
    }

    internal WriteRequest(QueryPlan query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }

    public static implicit operator WriteRequest<T>(AccessRequest request)
        => new(request);

    public static implicit operator WriteRequest<T>(WriteRequest request)
        => new(request.Query!, request.QueryComponentIndex);
}


internal sealed class QueryPlan
{
    private readonly QuerySpec _description;
    private int _version = -1;
    private int[] _matchingArchetypes = Array.Empty<int>();
    private DenseArchetypePlan[] _matchingPlans = Array.Empty<DenseArchetypePlan>();
    private bool _hasWriteAccess;

    public QueryPlan(QuerySpec spec) => _description = spec;

    public bool HasTags => !_description.AllTags.IsEmpty || !_description.AnyTags.IsEmpty || !_description.NoneTags.IsEmpty;
    public bool HasWriteAccess => _hasWriteAccess;
    public void RegisterWriteAccess() => _hasWriteAccess = true;

    public int[] MatchingArchetypes(World world)
    {
        if (_version == world.ArchetypeVersion)
        {
            return _matchingArchetypes;
        }

        var matches = new List<int>(world.Archetypes.Count);
        var plans = new List<DenseArchetypePlan>(world.Archetypes.Count);
        for (var archetypeId = 0; archetypeId < world.Archetypes.Count; archetypeId++)
        {
            var archetype = world.Archetypes[archetypeId];
            if (!Matches(archetype))
            {
                continue;
            }

            var indices = new int[_description.AllMask.Count];
            var componentIndex = 0;
            foreach (var componentId in _description.AllMask)
            {
                indices[componentIndex++] = archetype.Mask.Rank(componentId);
            }

            matches.Add(archetypeId);
            plans.Add(new DenseArchetypePlan(archetype, indices));
        }

        _matchingArchetypes = matches.ToArray();
        _matchingPlans = plans.ToArray();
        _version = world.ArchetypeVersion;
        return _matchingArchetypes;
    }

    public DenseArchetypePlan[] MatchingPlans(World world)
    {
        MatchingArchetypes(world);
        return _matchingPlans;
    }

    public int[] ComponentRowIndices(int matchingIndex) => _matchingPlans[matchingIndex].ComponentRows;

    private bool Matches(Archetype archetype) => archetype.Mask.ContainsAll(_description.AllMask)
        && (_description.AnyMask.IsEmpty || archetype.Mask.Intersects(_description.AnyMask))
        && !archetype.Mask.Intersects(_description.NoneMask);
}

internal readonly struct DenseArchetypePlan
{
    public DenseArchetypePlan(Archetype archetype, int[] componentRows)
    {
        Archetype = archetype;
        ComponentRows = componentRows;
    }

    public Archetype Archetype { get; }
    public int[] ComponentRows { get; }
}

internal static class QueryThrowHelper
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowAccessMismatch() => throw new InvalidOperationException("The row access does not belong to this query or world.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowAccessTypeMismatch() => throw new InvalidOperationException("The row access type does not match the registered component type.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowMissingWriteIntent() => throw new InvalidOperationException("The query did not register its write row access.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowAccessModeMismatch() => throw new InvalidOperationException("The access mode does not match the requested row operation.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArchetypeIteratorNotPositioned() => throw new InvalidOperationException("The archetype iterator is not positioned on an archetype.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowChunkIteratorNotPositioned() => throw new InvalidOperationException("The chunk iterator is not positioned on a chunk.");
}
