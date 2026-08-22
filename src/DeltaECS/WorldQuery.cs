namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>Short-lived dense chunk access used only by <see cref="World.QueryCursor{TContext}"/>.</summary>
public ref struct DenseChunkCursor
{
    private readonly CachedQuery _query;
    private readonly Chunk _chunk;
    private readonly int[] _componentRows;
    private readonly uint _writeTick;
    private readonly ulong[]? _overlayMask;
    private readonly bool _fullMask;
    private int _index;

    internal DenseChunkCursor(
        CachedQuery query,
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

    public ResolvedReadRow<T> Resolve<T>(CursorReadBinding<T> binding)
    {
        return new ResolvedReadRow<T>(ResolveReadRow(binding));
    }

    public ResolvedWriteRow<T> Resolve<T>(CursorWriteBinding<T> binding)
    {
        return new ResolvedWriteRow<T>(ResolveWriteRow(binding));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<T> ResolveReadRow<T>(CursorReadBinding<T> binding)
    {
        if (!ReferenceEquals(binding.Query, _query))
        {
            QueryThrowHelper.ThrowBindingMismatch();
        }

        return _chunk.GetComponentRow<T>(_componentRows[binding.QueryComponentIndex]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<T> ResolveWriteRow<T>(CursorWriteBinding<T> binding)
    {
        if (!ReferenceEquals(binding.Query, _query))
        {
            QueryThrowHelper.ThrowBindingMismatch();
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

public ref struct ResolvedReadRow<T>
{
    private readonly ReadOnlySpan<T> _row;

    internal ResolvedReadRow(ReadOnlySpan<T> row)
    {
        _row = row;
    }

    public ref readonly T this[DenseChunkCursor cursor]
    {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), cursor.CurrentIndex);
    }

    public ref readonly T this[DenseSlotIterator iterator]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), iterator.CurrentIndex);
    }
}

public ref struct ResolvedWriteRow<T>
{
    private readonly Span<T> _row;

    internal ResolvedWriteRow(Span<T> row)
    {
        _row = row;
    }

    public ref T this[DenseChunkCursor cursor]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), cursor.CurrentIndex);
    }

    public ref T this[DenseSlotIterator iterator]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), iterator.CurrentIndex);
    }

}

public readonly struct CursorReadBinding<T>
{
    internal CursorReadBinding(CachedQuery query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal CachedQuery? Query { get; }
    internal int QueryComponentIndex { get; }
}

public readonly struct CursorWriteBinding<T>
{
    internal CursorWriteBinding(CachedQuery query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal CachedQuery? Query { get; }
    internal int QueryComponentIndex { get; }
}

internal sealed class CachedQuery
{
    private readonly QueryDescription _description;
    private int _version = -1;
    private int[] _matchingArchetypes = Array.Empty<int>();
    private DenseArchetypePlan[] _matchingPlans = Array.Empty<DenseArchetypePlan>();
    private bool _hasWriteBindings;

    public CachedQuery(QueryDescription description)
    {
        _description = description;
    }

    public bool HasTags => !_description.AllTags.IsEmpty
        || !_description.AnyTags.IsEmpty
        || !_description.NoneTags.IsEmpty;

    public bool HasWriteBindings => _hasWriteBindings;

    public void RegisterWriteBinding() => _hasWriteBindings = true;

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
    public static void ThrowBindingMismatch() => throw new InvalidOperationException("The row binding does not belong to this query or world.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowMissingWriteIntent() => throw new InvalidOperationException("The query did not register its write row binding.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArchetypeIteratorNotPositioned() => throw new InvalidOperationException("The archetype iterator is not positioned on an archetype.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowChunkIteratorNotPositioned() => throw new InvalidOperationException("The chunk iterator is not positioned on a chunk.");

}
