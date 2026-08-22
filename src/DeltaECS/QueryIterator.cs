namespace Delta.ECS;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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
///         ref var cursor = ref iterator.CurrentChunk;
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
    private QueryChunkIterator _chunks;
    private readonly bool _hasTags;
    private readonly uint _writeTick;
    private ulong[]? _overlayScratch;
    private int _archetypePosition;
    private bool _hasArchetype;
    private bool _hasChunks;
    private bool _disposed;

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
        _chunks = default;
        _hasArchetype = false;
        _hasChunks = false;
        _disposed = false;
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

    /// <summary>Advances the middle loop to the next matching active chunk.</summary>
    public bool MoveNextChunk()
    {
        EnsureNotDisposed();
        if (!_hasArchetype || !_hasChunks)
        {
            return false;
        }

        return _chunks.MoveNext();
    }

    /// <summary>Gets the cursor for the chunk selected by the middle loop.</summary>
    [UnscopedRef]
    public ref DenseChunkCursor CurrentChunk
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            EnsureNotDisposed();
            return ref _chunks.Current;
        }
    }

    /// <summary>Creates an independent chunk iterator for the selected archetype.</summary>
    public QueryChunkIterator CreateChunkIterator()
    {
        EnsureNotDisposed();
        if (!_hasArchetype)
        {
            throw new InvalidOperationException("The iterator is not positioned on an archetype.");
        }

        return new QueryChunkIterator(
            _owner,
            _description,
            _cached,
            _plans[_archetypePosition],
            _hasTags,
            _writeTick,
            _overlayScratch);
    }

    /// <summary>Advances the outer loop to the next matching archetype.</summary>
    public bool MoveNextArchetype()
    {
        EnsureNotDisposed();
        _chunks.Dispose();
        _archetypePosition++;
        _hasArchetype = _archetypePosition < _plans.Length;
        _hasChunks = _hasArchetype;
        if (_hasArchetype)
        {
            _chunks = new QueryChunkIterator(
                _owner,
                _description,
                _cached,
                _plans[_archetypePosition],
                _hasTags,
                _writeTick,
                _overlayScratch);
        }

        return _hasArchetype;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _hasArchetype = false;
        _hasChunks = false;
        _chunks.Dispose();
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
