namespace Delta.ECS;

/// <summary>
/// Owns one validated query execution and its structural lease.
/// Child iterators are trusted stack-only views and do not own the lease.
/// </summary>
public ref struct QueryScope
{
    private readonly World _owner;
    private readonly QueryPlan _query;
    private readonly ArchetypePlan[] _plans;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;

    internal QueryScope(World owner, in Query handle)
    {
        if (!ReferenceEquals(handle.Owner, owner) || !handle.IsValid)
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(handle));
        }

        _owner = owner;
        _query = handle.Cached;
        _plans = _query.MatchingPlans(owner);
        _writeSession = owner.RentQueryWriteSession(_query, out _sessionGeneration);
        _owner.BeginQueryLease();
    }

    public QueryArchetypes Archetypes
    {
        get
        {
            EnsureActive();
            return new QueryArchetypes(_plans, _query, _writeSession, _sessionGeneration);
        }
    }

    /// <summary>Iterates every active chunk across all matching archetypes.</summary>
    public QueryChunks Chunks
    {
        get
        {
            EnsureActive();
            return new QueryChunks(_plans, _query, _writeSession, _sessionGeneration);
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
        return access;
    }

    public void Dispose()
    {
        if (_owner is null)
        {
            return;
        }

        _owner.ReturnQueryWriteSession(_writeSession, _sessionGeneration);
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
        if (_owner is null)
        {
            throw new InvalidOperationException("The query scope has been disposed.");
        }

        _writeSession.EnsureActive(_sessionGeneration);
    }
}
