namespace Delta.ECS;

/// <summary>
/// Short-lived query traversal with explicit archetype, chunk and slot loops.
/// </summary>
/// <remarks>
/// The intended shape is:
/// <code>
/// using var iterator = world.Iterate(in query);
/// while (iterator.MoveNextArchetype())
/// {
///     while (iterator.MoveNextChunk())
///     {
///         var cursor = iterator.Current;
///         while (cursor.MoveNext())
///         {
///             // read rows through the cursor's resolved bindings
///         }
///     }
/// }
/// </code>
/// Tagged queries keep their existing mask semantics: use
/// <see cref="DenseChunkCursor.IsActiveSlot(int)"/> for partial chunks.
/// </remarks>
public ref struct QueryIterator
{
    private readonly World _owner;
    private readonly QueryDescription _description;
    private readonly CachedQuery _cached;
    private readonly DenseArchetypePlan[] _plans;
    private readonly bool _hasTags;
    private readonly uint _writeTick;
    private ulong[]? _overlayScratch;
    private int _archetypePosition;
    private int _chunkPosition;
    private bool _hasArchetype;
    private bool _hasCurrent;
    private bool _disposed;
    private DenseChunkCursor _current;

    internal QueryIterator(World owner, in QueryHandle handle)
    {
        if (!ReferenceEquals(handle.Owner, owner) || !handle.IsValid)
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(handle));
        }

        _owner = owner;
        _description = handle.Description;
        _cached = handle.Cached;
        _plans = _cached.MatchingPlans(owner);
        _hasTags = _cached.HasTags;
        _writeTick = owner.GetQueryWriteTick(_cached);
        _overlayScratch = _hasTags ? owner.RentChunkOverlayScratch() : null;
        _archetypePosition = -1;
        _chunkPosition = 0;
        _hasArchetype = false;
        _hasCurrent = false;
        _disposed = false;
        _current = default;
        _owner.BeginQueryLease();
    }

    /// <summary>Gets the matching archetype currently selected by the outer loop.</summary>
    public ArchetypeHandle CurrentArchetype
    {
        get
        {
            EnsureNotDisposed();
            if (!_hasArchetype)
            {
                throw new InvalidOperationException("The iterator is not positioned on an archetype.");
            }

            return new ArchetypeHandle(_owner, _plans[_archetypePosition].Archetype.Id);
        }
    }

    public int ArchetypeId => CurrentArchetype.ArchetypeId;

    /// <summary>Gets the cursor for the chunk selected by the middle loop.</summary>
    public DenseChunkCursor Current
    {
        get
        {
            EnsureNotDisposed();
            if (!_hasCurrent)
            {
                throw new InvalidOperationException("The iterator is not positioned on a chunk.");
            }

            return _current;
        }
    }

    /// <summary>Advances the outer loop to the next matching archetype.</summary>
    public bool MoveNextArchetype()
    {
        EnsureNotDisposed();
        _archetypePosition++;
        _chunkPosition = 0;
        _hasArchetype = _archetypePosition < _plans.Length;
        _hasCurrent = false;
        return _hasArchetype;
    }

    /// <summary>Advances the middle loop to the next matching active chunk.</summary>
    public bool MoveNextChunk()
    {
        EnsureNotDisposed();
        if (!_hasArchetype)
        {
            return false;
        }

        var plan = _plans[_archetypePosition];
        var archetype = plan.Archetype;
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
                plan.ComponentRows,
                _writeTick,
                _overlayScratch,
                overlayResult);
            _hasCurrent = true;
            return true;
        }

        _hasCurrent = false;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _hasArchetype = false;
        _hasCurrent = false;
        _owner.EndQueryLease();
        if (_overlayScratch is not null)
        {
            _owner.ReturnChunkOverlayScratch(_overlayScratch);
            _overlayScratch = null;
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new InvalidOperationException("The query iterator has been disposed.");
        }
    }
}
