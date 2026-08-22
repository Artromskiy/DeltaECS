namespace Delta.ECS;

/// <summary>
/// Owns one validated dense query execution and its structural lease.
/// Child iterators are trusted stack-only views and do not own the lease.
/// </summary>
public ref struct DenseQueryScope
{
    private readonly World _owner;
    private readonly CachedQuery _query;
    private readonly DenseArchetypePlan[] _plans;
    private readonly uint _writeTick;
    private bool _disposed;

    internal DenseQueryScope(World owner, in QueryHandle handle)
    {
        if (!ReferenceEquals(handle.Owner, owner) || !handle.IsValid)
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(handle));
        }

        _owner = owner;
        _query = handle.Cached;
        if (_query.HasTags)
        {
            throw new ArgumentException(
                "Dense query iteration does not accept tag predicates. Use the tagged query path.",
                nameof(handle));
        }

        _plans = _query.MatchingPlans(owner);
        _writeTick = owner.GetQueryWriteTick(_query);
        _disposed = false;
        _owner.BeginQueryLease();
    }

    /// <summary>Creates the independent outer iterator over matching archetypes.</summary>
    public DenseArchetypeIterator Archetypes
    {
        get
        {
            EnsureActive();
            return new DenseArchetypeIterator(_plans, _query, _writeTick);
        }
    }

    /// <summary>Validates a read binding once for this dense execution.</summary>
    public DenseReadBinding<T> Prepare<T>(CursorReadBinding<T> binding)
    {
        EnsureActive();
        if (!ReferenceEquals(binding.Query, _query))
        {
            QueryThrowHelper.ThrowBindingMismatch();
        }

        return new DenseReadBinding<T>(_query, binding.QueryComponentIndex);
    }

    /// <summary>Validates a write binding once for this dense execution.</summary>
    public DenseWriteBinding<T> Prepare<T>(CursorWriteBinding<T> binding)
    {
        EnsureActive();
        if (!ReferenceEquals(binding.Query, _query))
        {
            QueryThrowHelper.ThrowBindingMismatch();
        }

        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        return new DenseWriteBinding<T>(_query, binding.QueryComponentIndex);
    }

    public void Dispose()
    {
        if (_disposed || _owner is null)
        {
            return;
        }

        _disposed = true;
        _owner.EndQueryLease();
    }

    private void EnsureActive()
    {
        if (_disposed || _owner is null)
        {
            throw new InvalidOperationException("The dense query scope has been disposed.");
        }
    }
}

/// <summary>Scope-validated read row token for dense iteration.</summary>
public readonly struct DenseReadBinding<T>
{
    internal DenseReadBinding(CachedQuery query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal CachedQuery? Query { get; }

    internal int QueryComponentIndex { get; }
}

/// <summary>Scope-validated write row token for dense iteration.</summary>
public readonly struct DenseWriteBinding<T>
{
    internal DenseWriteBinding(CachedQuery query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal CachedQuery? Query { get; }

    internal int QueryComponentIndex { get; }
}
