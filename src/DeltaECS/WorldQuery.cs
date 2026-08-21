namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public sealed class DenseChunkScope : IDisposable
{
    private readonly World _owner;
    private readonly CachedQuery _query;
    private readonly Archetype _archetype;
    private readonly Chunk _chunk;
    private readonly int _globalChunkId;
    private readonly int _slotCount;
    private readonly int[] _componentRows;
    private readonly uint _writeTick;
    private ulong[]? _overlayMask;
    private readonly bool _fullMask;
    private bool _disposed;

    internal DenseChunkScope(
        World owner,
        CachedQuery query,
        Archetype archetype,
        Chunk chunk,
        int globalChunkId,
        int[] componentRows,
        ulong[]? overlayMask,
        OverlayMaskResult overlayResult,
        uint writeTick)
    {
        _owner = owner;
        _query = query;
        _archetype = archetype;
        _chunk = chunk;
        _globalChunkId = globalChunkId;
        _slotCount = chunk.Count;
        _componentRows = componentRows;
        _writeTick = writeTick;
        _overlayMask = overlayResult == OverlayMaskResult.Partial ? overlayMask : null;
        _fullMask = overlayResult == OverlayMaskResult.Full;
    }

    public int ArchetypeId => _archetype.Id;

    public int GlobalChunkId => _globalChunkId;

    public int SlotCount => _slotCount;

    public ReadOnlySpan<Entity> Entities => _chunk.Entities;

    public bool IsActiveSlot(int slotIndex)
    {
        if (_fullMask)
        {
            return (uint)slotIndex < (uint)_slotCount;
        }

        return _overlayMask is not null
            && (_overlayMask[slotIndex >> 6] & (1UL << (slotIndex & 63))) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetRow<T>(ReadRowBinding<T> binding)
    {
        var index = ResolveBinding(binding.Data);
        return _chunk.GetComponentRow<T>(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetRow<T>(WriteRowBinding<T> binding)
    {
        var index = ResolveWriteBinding(binding.Data);
        return _chunk.GetComponentRow<T>(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ResolveBinding(RowBindingData binding)
    {
        if (_disposed)
        {
            QueryThrowHelper.ThrowDisposedScope();
        }

        if (!ReferenceEquals(binding.Query, _query))
        {
            QueryThrowHelper.ThrowBindingMismatch();
        }

        return _componentRows[binding.QueryComponentIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ResolveWriteBinding(RowBindingData binding)
    {
        var index = ResolveBinding(binding);
        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        MarkWritten(index);
        return index;
    }

    private void MarkWritten(int componentIndex)
    {
        _chunk.MarkComponentWritten(componentIndex, _writeTick);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _owner.CompleteChunkScope();
        _disposed = true;
    }
}

public ref struct DenseChunkAccessor
{
    private readonly CachedQuery _query;
    private readonly Chunk _chunk;
    private readonly int _archetypeId;
    private readonly int _globalChunkId;
    private readonly int _slotCount;
    private readonly int[] _componentRows;
    private readonly ulong[]? _overlayMask;
    private readonly bool _fullMask;
    private readonly World _owner;
    private readonly int _viewId;
    private readonly uint _writeTick;
    private bool _disposed;

    internal DenseChunkAccessor(
        World owner,
        CachedQuery query,
        Archetype archetype,
        Chunk chunk,
        int globalChunkId,
        int[] componentRows,
        ulong[]? overlayMask,
        OverlayMaskResult overlayResult,
        int viewId,
        uint writeTick)
    {
        _query = query;
        _chunk = chunk;
        _archetypeId = archetype.Id;
        _globalChunkId = globalChunkId;
        _slotCount = chunk.Count;
        _componentRows = componentRows;
        _overlayMask = overlayResult == OverlayMaskResult.Partial ? overlayMask : null;
        _fullMask = overlayResult == OverlayMaskResult.Full;
        _owner = owner;
        _viewId = viewId;
        _writeTick = writeTick;
        _disposed = false;
    }

    public int ArchetypeId => _archetypeId;

    public int GlobalChunkId => _globalChunkId;

    public int SlotCount => _slotCount;

    /// <summary>
    /// Gets a value indicating whether every slot in this view is active.
    /// </summary>
    /// <remarks>
    /// This is a chunk-level fast-path selector for views created from queries
    /// that contain overlay/tag predicates. If it is <see langword="true"/>,
    /// the slot loop may process every index from <c>SlotCount - 1</c>
    /// down to zero without calling <see cref="IsActiveSlot(int)"/>. If it is
    /// <see langword="false"/>, the view can contain overlay holes and each
    /// slot must be checked with <see cref="IsActiveSlot(int)"/>. Component
    /// matching is resolved at archetype level and is not represented by this
    /// mask.
    /// </remarks>
    public bool IsAllSlotsActive
    {
        get
        {
            EnsureCurrent();
            return _fullMask;
        }
    }

    public ReadOnlySpan<Entity> Entities
    {
        get
        {
            EnsureCurrent();
            return _chunk.Entities;
        }
    }

    /// <summary>
    /// Gets whether a slot passes the overlay/tag predicates of this view.
    /// </summary>
    /// <remarks>
    /// Call this inside the slot loop only after <see cref="IsAllSlotsActive"/>
    /// returned <see langword="false"/> for the current chunk.
    /// </remarks>
    public bool IsActiveSlot(int slotIndex)
    {
        EnsureCurrent();
        if (_fullMask)
        {
            return (uint)slotIndex < (uint)_slotCount;
        }

        return _overlayMask is not null
            && (_overlayMask[slotIndex >> 6] & (1UL << (slotIndex & 63))) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetRow<T>(ReadRowBinding<T> binding)
    {
        var index = ResolveBinding(binding.Data);
        return _chunk.GetComponentRow<T>(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetRow<T>(WriteRowBinding<T> binding)
    {
        var index = ResolveWriteBinding(binding.Data);
        return _chunk.GetComponentRow<T>(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ResolveBinding(RowBindingData binding)
    {
        EnsureCurrent();
        if (!ReferenceEquals(binding.Query, _query))
        {
            QueryThrowHelper.ThrowBindingMismatch();
        }

        return _componentRows[binding.QueryComponentIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ResolveWriteBinding(RowBindingData binding)
    {
        var index = ResolveBinding(binding);
        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        MarkWritten(index);
        return index;
    }

    private void MarkWritten(int componentIndex)
    {
        _chunk.MarkComponentWritten(componentIndex, _writeTick);
    }

    internal void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCurrent()
    {
        if (_disposed)
        {
            QueryThrowHelper.ThrowDisposedAccessor();
        }

        if (!_owner.IsChunkAccessorIdValid(_viewId))
        {
            QueryThrowHelper.ThrowStaleAccessor();
        }
    }
}

/// <summary>Short-lived dense chunk access used only by <see cref="World.QueryCursor{TContext}"/>.</summary>
public ref struct DenseChunkCursor
{
    private readonly CachedQuery _query;
    private readonly Chunk _chunk;
    private readonly int[] _componentRows;
    private readonly uint _writeTick;
    private int _index;

    internal DenseChunkCursor(CachedQuery query, Chunk chunk, int[] componentRows, uint writeTick)
    {
        _query = query;
        _chunk = chunk;
        _componentRows = componentRows;
        _writeTick = writeTick;
        _index = -1;
    }

    public int SlotCount => _chunk.Count;

    public int CurrentIndex => _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        var next = _index + 1;
        if ((uint)next >= (uint)_chunk.Count)
        {
            _index = _chunk.Count;
            return false;
        }

        _index = next;
        return true;
    }

    public ResolvedReadRow<T> Resolve<T>(CursorReadBinding<T> binding)
    {
        return new ResolvedReadRow<T>(GetRow(binding));
    }

    public ResolvedWriteRow<T> Resolve<T>(CursorWriteBinding<T> binding)
    {
        return new ResolvedWriteRow<T>(GetRow(binding));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetRow<T>(CursorReadBinding<T> binding)
    {
        if (!ReferenceEquals(binding.Query, _query))
        {
            QueryThrowHelper.ThrowBindingMismatch();
        }

        return _chunk.GetComponentRow<T>(_componentRows[binding.QueryComponentIndex]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetRow<T>(CursorWriteBinding<T> binding)
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
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), cursor.CurrentIndex);
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
        get => ref Unsafe.Add(ref MemoryMarshal.GetReference(_row), cursor.CurrentIndex);
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
    private List<RowBindingData>? _bindings;
    private bool _hasWriteBindings;

    public CachedQuery(QueryDescription description)
    {
        _description = description;
    }

    public bool HasTags => !_description.AllTags.IsEmpty
        || !_description.AnyTags.IsEmpty
        || !_description.NoneTags.IsEmpty;

    public bool HasWriteBindings => _hasWriteBindings;

    public void RegisterBinding(RowBindingData binding)
    {
        (_bindings ??= new List<RowBindingData>(4)).Add(binding);
    }

    public void RegisterWriteBinding() => _hasWriteBindings = true;

    public void ValidateBindings()
    {
        if (_bindings is null)
        {
            return;
        }

        for (var i = 0; i < _bindings.Count; i++)
        {
            var binding = _bindings[i];
            if (!ReferenceEquals(binding.Query, this)
                || !binding.IsValid
                || (uint)binding.QueryComponentIndex >= (uint)_description.AllMask.Count
                || _description.AllMask.Rank(binding.ComponentId) != binding.QueryComponentIndex)
            {
                QueryThrowHelper.ThrowInvalidBindingRegistration();
            }
        }
    }

    public int[] MatchingArchetypes(World world)
    {
        ValidateBindings();
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
    public static void ThrowDisposedScope() => throw new ObjectDisposedException(nameof(DenseChunkScope));

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDisposedAccessor() => throw new ObjectDisposedException(nameof(DenseChunkAccessor));

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowStaleAccessor() => throw new InvalidOperationException("Chunk accessor is stale.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowBindingMismatch() => throw new InvalidOperationException("The row binding does not belong to this query or world.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowMissingWriteIntent() => throw new InvalidOperationException("The query did not register its write row binding.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidBindingRegistration() => throw new InvalidOperationException("The query contains an invalid row binding registration.");
}
