namespace Delta.ECS;

/// <summary>
/// Owns one validated query execution and its structural lease.
/// Child iterators are trusted stack-only views and do not own the lease.
/// </summary>
public ref struct QueryScope
{
    private readonly World _owner;
    private readonly QueryPlan _query;
    private readonly ReadOnlySpan<ArchetypePlan> _plans;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;

    internal QueryScope(World owner, Query handle)
    {
        if (!ReferenceEquals(handle.Owner, owner) || !handle.IsValid)
        {
            ThrowHelper.ThrowInvalidQueryScopeHandle(nameof(handle));
        }

        _owner = owner;
        _query = handle.Cached;
        _plans = _query.MatchingPlans();
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

    public void Dispose()
    {
        if (_owner is null)
        {
            return;
        }

        _owner.ReturnQueryWriteSession(_writeSession, _sessionGeneration);
    }

    private void EnsureActive()
    {
        if (_owner is null)
        {
            ThrowHelper.ThrowDisposedQueryScope();
        }

        _writeSession.EnsureActive(_sessionGeneration);
    }
}
