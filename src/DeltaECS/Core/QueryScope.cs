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
        _plans = _query.MatchingPlans(owner);
        _writeTick = owner.GetQueryWriteTick(_query);
        _disposed = false;
        _owner.BeginQueryLease();
    }

    public QueryArchetypes Archetypes
    {
        get
        {
            EnsureActive();
            return new QueryArchetypes(_plans, _query, _writeTick);
        }
    }

    public ReadAccess Bind(ReadAccess access)
    {
        EnsureActive();
        Validate(access.Query);
        return access;
    }

    public WriteAccess Bind(WriteAccess access)
    {
        EnsureActive();
        Validate(access.Query);
        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        return access;
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

    private void Validate(QueryPlan? query)
    {
        if (!ReferenceEquals(query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }
    }

    private void EnsureActive()
    {
        if (_disposed || _owner is null)
        {
            throw new InvalidOperationException("The dense query scope has been disposed.");
        }
    }
}
