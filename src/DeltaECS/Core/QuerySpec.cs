namespace Delta.ECS;

using System;

public readonly struct QuerySpec : IEquatable<QuerySpec>
{
    private readonly ComponentMask _allMask;
    private readonly ComponentMask _anyMask;
    private readonly ComponentMask _noneMask;

    internal QuerySpec(
        ReadOnlySpan<ComponentId> allComponents,
        ReadOnlySpan<ComponentId> anyComponents,
        ReadOnlySpan<ComponentId> noneComponents)
    {
        _allMask = BuildMask(allComponents);
        _anyMask = BuildMask(anyComponents);
        _noneMask = BuildMask(noneComponents);
        Hash = ComputeHash();
    }

    internal ComponentMask AllMask => _allMask;

    internal ComponentMask AnyMask => _anyMask;

    internal ComponentMask NoneMask => _noneMask;

    internal int Hash { get; }

    private int ComputeHash()
    {
        var hash = new HashCode();
        hash.Add(AllMask);
        hash.Add(AnyMask);
        hash.Add(NoneMask);
        return hash.ToHashCode();
    }

    private static ComponentMask BuildMask(ReadOnlySpan<ComponentId> ids)
        => ComponentMask.From(ids);

    public bool Equals(QuerySpec other) => Hash == other.Hash
        && _allMask == other._allMask
        && _anyMask == other._anyMask
        && _noneMask == other._noneMask;

    public override bool Equals(object? obj) => obj is QuerySpec other && Equals(other);

    public override int GetHashCode() => Hash;

    private QuerySpec(ComponentMask allMask, ComponentMask anyMask, ComponentMask noneMask)
    {
        _allMask = allMask;
        _anyMask = anyMask;
        _noneMask = noneMask;
        Hash = ComputeHash();
    }

    internal QuerySpec Compose(in QuerySpec additions)
        => new(
            _allMask.Or(additions._allMask),
            _anyMask.Or(additions._anyMask),
            _noneMask.Or(additions._noneMask));

    public static QuerySpec WhereAll(ReadOnlySpan<ComponentId> components)
        => new(components, ReadOnlySpan<ComponentId>.Empty, ReadOnlySpan<ComponentId>.Empty);

    public static QuerySpec WhereAny(ReadOnlySpan<ComponentId> components)
        => new(ReadOnlySpan<ComponentId>.Empty, components, ReadOnlySpan<ComponentId>.Empty);

    public static QuerySpec WhereNone(ReadOnlySpan<ComponentId> components)
        => new(ReadOnlySpan<ComponentId>.Empty, ReadOnlySpan<ComponentId>.Empty, components);

    public static QuerySpec WhereAll(ComponentId component0)
        => WhereAll(stackalloc ComponentId[1] { component0 });

    public static QuerySpec WhereAny(ComponentId component0)
        => WhereAny(stackalloc ComponentId[1] { component0 });

    public static QuerySpec WhereNone(ComponentId component0)
        => WhereNone(stackalloc ComponentId[1] { component0 });

    /// <summary>Gets an empty query specification that can be extended with generated filters.</summary>
    public static QuerySpec Empty => new(
        default(ComponentMask),
        default(ComponentMask),
        default(ComponentMask));

    /// <summary>Adds an all-components filter to this specification.</summary>
    public QuerySpec WithAll(ReadOnlySpan<ComponentId> components)
        => Compose(WhereAll(components));

    /// <summary>Adds an any-components filter to this specification.</summary>
    public QuerySpec WithAny(ReadOnlySpan<ComponentId> components)
        => Compose(WhereAny(components));

    /// <summary>Adds a none-components filter to this specification.</summary>
    public QuerySpec WithNone(ReadOnlySpan<ComponentId> components)
        => Compose(WhereNone(components));

}
