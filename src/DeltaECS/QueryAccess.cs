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

    internal QueryChunkCursor(
        QueryPlan query,
        int archetypeId,
        Chunk chunk,
        int[] componentRows,
        uint writeTick,
        ulong[]? overlayMask,
        OverlayMaskResult overlayResult)
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

        return _fullMask
            || (_overlayMask is not null && (_overlayMask[slotIndex >> 6] & (1UL << (slotIndex & 63))) != 0);
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

    public ReadValues<T> Get<T>(ReadRequest<T> binding)
    {
        return new ReadValues<T>(GetReadValues(binding));
    }

    public WriteValues<T> Get<T>(WriteRequest<T> binding)
    {
        return new WriteValues<T>(GetWriteValues(binding));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<T> GetReadValues<T>(ReadRequest<T> binding)
    {
        if (!ReferenceEquals(binding.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        return _chunk.GetComponentRow<T>(_componentRows[binding.QueryComponentIndex]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<T> GetWriteValues<T>(WriteRequest<T> binding)
    {
        if (!ReferenceEquals(binding.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        var index = _componentRows[binding.QueryComponentIndex];
        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        _chunk.MarkComponentWritten(index, _writeTick);
        return _chunk.GetComponentRow<T>(index);
    }
}

public ref struct ReadValues<T>
{
    private readonly ReadOnlySpan<T> _row;

    internal ReadValues(ReadOnlySpan<T> row)
    {
        _row = row;
    }

    public ref readonly T this[QueryChunkCursor cursor]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), cursor.CurrentIndex);
    }

    public ref readonly T this[QuerySlots iterator]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), iterator.CurrentIndex);
    }
}

public ref struct WriteValues<T>
{
    private readonly Span<T> _row;

    internal WriteValues(Span<T> row)
    {
        _row = row;
    }

    public ref T this[QueryChunkCursor cursor]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), cursor.CurrentIndex);
    }

    public ref T this[QuerySlots iterator]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), iterator.CurrentIndex);
    }

}

public readonly struct ReadRequest<T>
{
    internal ReadRequest(QueryPlan query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }
}

public readonly struct WriteRequest<T>
{
    internal WriteRequest(QueryPlan query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }
}

internal sealed class QueryPlan
{
    private readonly QuerySpec _description;
    private int _version = -1;
    private int[] _matchingArchetypes = Array.Empty<int>();
    private DenseArchetypePlan[] _matchingPlans = Array.Empty<DenseArchetypePlan>();
    private bool _hasWriteAccess;

    public QueryPlan(QuerySpec spec)
    {
        _description = spec;
    }

    public bool HasTags => !_description.AllTags.IsEmpty
        || !_description.AnyTags.IsEmpty
        || !_description.NoneTags.IsEmpty;

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

    private bool Matches(Archetype archetype)
    {
        return archetype.Mask.ContainsAll(_description.AllMask)
            && (_description.AnyMask.IsEmpty || archetype.Mask.Intersects(_description.AnyMask))
            && !archetype.Mask.Intersects(_description.NoneMask);
    }
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
    public static void ThrowMissingWriteIntent() => throw new InvalidOperationException("The query did not register its write row access.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArchetypeIteratorNotPositioned() => throw new InvalidOperationException("The archetype iterator is not positioned on an archetype.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowChunkIteratorNotPositioned() => throw new InvalidOperationException("The chunk iterator is not positioned on a chunk.");

}
