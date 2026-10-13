namespace Delta.ECS.Systems;

using System;

using Delta.ECS;

/// <summary>Describes component access restricted to entities matching one query.</summary>
public readonly struct SystemQueryAccess
{
    private static readonly ComponentId[] Empty = [];
    private readonly ComponentId[]? _reads;
    private readonly ComponentId[]? _writes;

    /// <summary>Gets the query limiting this access.</summary>
    public Query Query { get; }

    /// <summary>Gets components read for matching entities.</summary>
    public ReadOnlySpan<ComponentId> Reads => _reads ?? Empty;

    /// <summary>Gets components written for matching entities.</summary>
    public ReadOnlySpan<ComponentId> Writes => _writes ?? Empty;

    internal QueryPlan QueryPlan => Query.Cached;

    /// <summary>Creates query-scoped access metadata from world-local component ids.</summary>
    /// <param name="query">The query that limits the entities accessed.</param>
    /// <param name="reads">Components read for matching entities.</param>
    /// <param name="writes">Components written for matching entities.</param>
    public SystemQueryAccess(
        in Query query,
        ReadOnlySpan<ComponentId> reads = default,
        ReadOnlySpan<ComponentId> writes = default)
    {
        if (!query.IsValid)
        {
            ThrowHelper.ThrowInvalidSystemQueryAccess(nameof(query));
        }

        Query = query;
        _reads = SystemAccess.Copy(reads, nameof(reads));
        _writes = SystemAccess.Copy(writes, nameof(writes));
    }
}
