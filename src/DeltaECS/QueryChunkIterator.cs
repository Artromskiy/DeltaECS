namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>
/// Short-lived iterator over active chunks of one archetype selected by a
/// <see cref="QueryIterator"/>. The parent query iterator owns the structural
/// lease and overlay scratch; this iterator only owns its current position.
/// </summary>
public ref struct QueryChunkIterator
{
    private readonly World _owner;
    private readonly QueryDescription _description;
    private readonly CachedQuery _cached;
    private readonly DenseArchetypePlan _plan;
    private readonly bool _hasTags;
    private readonly uint _writeTick;
    private readonly ulong[]? _overlayScratch;
    private int _chunkPosition;
    private DenseChunkCursor _current;
    private bool _hasCurrent;
    private bool _disposed;

    internal QueryChunkIterator(
        World owner,
        QueryDescription description,
        CachedQuery cached,
        DenseArchetypePlan plan,
        bool hasTags,
        uint writeTick,
        ulong[]? overlayScratch)
    {
        _owner = owner;
        _description = description;
        _cached = cached;
        _plan = plan;
        _hasTags = hasTags;
        _writeTick = writeTick;
        _overlayScratch = overlayScratch;
        _chunkPosition = 0;
        _current = default;
        _hasCurrent = false;
        _disposed = false;
    }

    /// <summary>Gets the cursor for the currently selected chunk.</summary>
    public DenseChunkCursor Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            EnsureNotDisposed();
            if (!_hasCurrent)
            {
                throw new InvalidOperationException("The chunk iterator is not positioned on a chunk.");
            }

            return _current;
        }
    }

    /// <summary>Advances to the next active chunk of the selected archetype.</summary>
    public bool MoveNext()
    {
        EnsureNotDisposed();
        var archetype = _plan.Archetype;
        while (_chunkPosition < archetype.ActiveChunkCount)
        {
            var chunk = archetype.GetActiveChunk(_chunkPosition++);
            var overlayResult = _hasTags
                ? _owner.OverlayTags.BuildMask(_description, chunk.GlobalId, chunk.Count, _overlayScratch!)
                : OverlayMaskResult.Full;
            if (overlayResult == OverlayMaskResult.None)
            {
                continue;
            }

            _current = new DenseChunkCursor(
                _cached,
                archetype.Id,
                chunk,
                _plan.ComponentRows,
                _writeTick,
                _overlayScratch,
                overlayResult);
            _hasCurrent = true;
            return true;
        }

        _hasCurrent = false;
        return false;
    }

    /// <summary>Invalidates this chunk iterator without ending the parent query lease.</summary>
    public void Dispose()
    {
        _disposed = true;
        _hasCurrent = false;
        _current = default;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new InvalidOperationException("The chunk iterator has been disposed.");
        }
    }
}
