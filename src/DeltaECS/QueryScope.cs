namespace Delta.ECS;

/// <summary>
/// Owns one validated dense query execution and its structural lease.
/// Child iterators are trusted stack-only views and do not own the lease.
/// </summary>
public ref struct QueryScope
{
    private readonly World _owner;
    private readonly QueryPlan _query;
    private readonly DenseArchetypePlan[] _plans;
    private readonly uint _writeTick;
    private bool _disposed;

    internal QueryScope(World owner, in Query handle)
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
    public QueryArchetypes Archetypes
    {
        get
        {
            EnsureActive();
            return new QueryArchetypes(_plans, _query, _writeTick);
        }
    }

    /// <summary>Validates a read binding once for this dense execution.</summary>
    public ReadAccess<T> Bind<T>(ReadRequest<T> binding)
    {
        EnsureActive();
        if (!ReferenceEquals(binding.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        return new ReadAccess<T>(_query, binding.QueryComponentIndex);
    }

    /// <summary>Validates a write binding once for this dense execution.</summary>
    public WriteAccess<T> Bind<T>(WriteRequest<T> binding)
    {
        EnsureActive();
        if (!ReferenceEquals(binding.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        return new WriteAccess<T>(_query, binding.QueryComponentIndex);
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
public readonly struct ReadAccess<T>
{
    internal ReadAccess(QueryPlan query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal QueryPlan? Query { get; }

    internal int QueryComponentIndex { get; }
}

/// <summary>Scope-validated write row token for dense iteration.</summary>
public readonly struct WriteAccess<T>
{
    internal WriteAccess(QueryPlan query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal QueryPlan? Query { get; }

    internal int QueryComponentIndex { get; }
}
