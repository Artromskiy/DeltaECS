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

    public QueryArchetypes Archetypes
    {
        get
        {
            EnsureActive();
            return new QueryArchetypes(_plans, _query, _writeTick);
        }
    }

    public ReadAccess BindRead(AccessRequest access)
    {
        EnsureActive();
        Validate(access.Query);
        if (access.IsWrite)
        {
            QueryThrowHelper.ThrowAccessModeMismatch();
        }

        return new ReadAccess(_query, access.QueryComponentIndex);
    }

    /// <summary>Compatibility generic call; the returned access token remains non-generic.</summary>
    [Obsolete("Use BindRead(AccessRequest); the generic argument is compatibility-only.")]
    public ReadAccess BindRead<T>(AccessRequest access) => BindRead(access);

    public ReadAccess Bind(ReadAccess access)
    {
        EnsureActive();
        Validate(access.Query);
        return access;
    }

    [Obsolete("Use non-generic AccessRequest with BindRead.")]
    public ReadAccess Bind(ReadRequest request)
    {
        EnsureActive();
        Validate(request.Query);
        return new ReadAccess(_query, request.QueryComponentIndex);
    }

    public WriteAccess BindWrite(AccessRequest access)
    {
        EnsureActive();
        Validate(access.Query);
        if (!access.IsWrite)
        {
            QueryThrowHelper.ThrowAccessModeMismatch();
        }

        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        return new WriteAccess(_query, access.QueryComponentIndex);
    }

    /// <summary>Compatibility generic call; the returned access token remains non-generic.</summary>
    [Obsolete("Use BindWrite(AccessRequest); the generic argument is compatibility-only.")]
    public WriteAccess BindWrite<T>(AccessRequest access) => BindWrite(access);

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

    [Obsolete("Use non-generic AccessRequest with BindWrite.")]
    public WriteAccess Bind(WriteRequest request)
    {
        EnsureActive();
        Validate(request.Query);
        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        return new WriteAccess(_query, request.QueryComponentIndex);
    }

    // Obsolete source-compatibility path. The returned token remains non-generic.
    [Obsolete("Use BindRead(AccessRequest); the generic argument is compatibility-only.")]
    public ReadAccess Bind<T>(ReadRequest<T> request)
    {
        EnsureActive();
        Validate(request.Query);
        return new ReadAccess(_query, request.QueryComponentIndex);
    }

    // Obsolete source-compatibility path. The returned token remains non-generic.
    [Obsolete("Use BindWrite(AccessRequest); the generic argument is compatibility-only.")]
    public WriteAccess Bind<T>(WriteRequest<T> request)
    {
        EnsureActive();
        Validate(request.Query);
        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        return new WriteAccess(_query, request.QueryComponentIndex);
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
