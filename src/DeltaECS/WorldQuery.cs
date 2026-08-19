namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

public sealed class DenseChunkScope : IDisposable
{
    private readonly World _owner;
    private readonly Archetype _archetype;
    private readonly Chunk _chunk;
    private readonly int _globalChunkId;
    private readonly int _slotCount;
    private ulong[]? _overlayMask;
    private readonly bool _fullMask;
    private bool _disposed;

    internal DenseChunkScope(World owner, Archetype archetype, Chunk chunk, int globalChunkId, ulong[]? overlayMask, OverlayMaskResult overlayResult)
    {
        _owner = owner;
        _archetype = archetype;
        _chunk = chunk;
        _globalChunkId = globalChunkId;
        _slotCount = chunk.Count;
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

    public Span<T> GetComponentRow<T>(ComponentId componentId)
    {
        if (!_archetype.TryGetComponentIndex(componentId, out var index))
        {
            throw new ArgumentException("Component is not part of this chunk archetype.", nameof(componentId));
        }

        return _chunk.GetComponentRow<T>(index);
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
    private readonly Archetype _archetype;
    private readonly Chunk _chunk;
    private readonly int _archetypeId;
    private readonly int _globalChunkId;
    private readonly int _slotCount;
    private readonly int[]? _queryComponentRowIndices;
    private ulong[]? _overlayMask;
    private readonly bool _fullMask;
    private readonly World _owner;
    private readonly int _viewId;
    private bool _disposed;

    internal DenseChunkAccessor(
        World owner,
        Archetype archetype,
        Chunk chunk,
        int globalChunkId,
        int[]? queryComponentRowIndices,
        ulong[]? overlayMask,
        OverlayMaskResult overlayResult,
        int viewId)
    {
        _archetype = archetype;
        _chunk = chunk;
        _archetypeId = archetype.Id;
        _globalChunkId = globalChunkId;
        _slotCount = chunk.Count;
        _queryComponentRowIndices = queryComponentRowIndices;
        _overlayMask = overlayResult == OverlayMaskResult.Partial ? overlayMask : null;
        _fullMask = overlayResult == OverlayMaskResult.Full;
        _owner = owner;
        _viewId = viewId;
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

    public Span<T> GetComponentRow<T>(ComponentId componentId)
    {
        EnsureCurrent();
        if (!_archetype.TryGetComponentIndex(componentId, out var index))
        {
            throw new ArgumentException("Component is not part of this chunk archetype.", nameof(componentId));
        }

        return _chunk.GetComponentRow<T>(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetComponentRow<T>(int queryComponentIndex)
    {
        EnsureCurrent();
        Debug.Assert(_queryComponentRowIndices is not null);
        Debug.Assert((uint)queryComponentIndex < (uint)_queryComponentRowIndices!.Length);
        return _chunk.GetComponentRow<T>(_queryComponentRowIndices[queryComponentIndex]);
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
            throw new ObjectDisposedException(nameof(DenseChunkAccessor));
        }

        if (!_owner.IsChunkAccessorIdValid(_viewId))
        {
            throw new InvalidOperationException("Chunk accessor is stale.");
        }
    }
}

internal sealed class CachedQuery
{
    private readonly QueryDescription _description;
    private int _version = -1;
    private int[] _matchingArchetypes = Array.Empty<int>();
    private int[][] _matchingComponentRowIndices = Array.Empty<int[]>();

    public CachedQuery(QueryDescription description)
    {
        _description = description;
    }

    public bool HasTags => !_description.AllTags.IsEmpty
        || !_description.AnyTags.IsEmpty
        || !_description.NoneTags.IsEmpty;

    public int[] MatchingArchetypes(World world)
    {
        if (_version == world.ArchetypeVersion)
        {
            return _matchingArchetypes;
        }

        var matches = new List<int>(world.Archetypes.Count);
        var plans = new List<int[]>(world.Archetypes.Count);
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
            plans.Add(indices);
        }

        _matchingArchetypes = matches.ToArray();
        _matchingComponentRowIndices = plans.ToArray();
        _version = world.ArchetypeVersion;
        return _matchingArchetypes;
    }

    public int[] ComponentRowIndices(int matchingIndex) => _matchingComponentRowIndices[matchingIndex];

    private bool Matches(Archetype archetype)
    {
        return archetype.Mask.ContainsAll(_description.AllMask)
            && (_description.AnyMask.IsEmpty || archetype.Mask.Intersects(_description.AnyMask))
            && !archetype.Mask.Intersects(_description.NoneMask);
    }
}
