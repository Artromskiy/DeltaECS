namespace Delta.ECS;

using System;
using System.Collections.Generic;

public readonly struct QuerySpec : IEquatable<QuerySpec>
{
    private readonly ComponentMask _allMask;
    private readonly ComponentMask _anyMask;
    private readonly ComponentMask _noneMask;

    public QuerySpec(
        ReadOnlySpan<ComponentId> allComponents,
        ReadOnlySpan<ComponentId> anyComponents,
        ReadOnlySpan<ComponentId> noneComponents)
    {
        _allMask = BuildMask(allComponents);
        _anyMask = BuildMask(anyComponents);
        _noneMask = BuildMask(noneComponents);
        Hash = ComputeHash();
    }

    public ComponentMask AllMask => _allMask;

    public ComponentMask AnyMask => _anyMask;

    public ComponentMask NoneMask => _noneMask;

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
    {
        var mask = default(ComponentMask);
        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i].IsValid)
            {
                mask = mask.Set(ids[i]);
            }
        }

        return mask;
    }

    public bool Equals(QuerySpec other) => Hash == other.Hash
        && _allMask == other._allMask
        && _anyMask == other._anyMask
        && _noneMask == other._noneMask;

    public override bool Equals(object? obj) => obj is QuerySpec other && Equals(other);

    public override int GetHashCode() => Hash;

    private QuerySpec(ReadOnlySpan<ComponentId> components)
        : this(components, ReadOnlySpan<ComponentId>.Empty, ReadOnlySpan<ComponentId>.Empty)
    {
    }

    public static QuerySpec ForComponents(params ReadOnlySpan<ComponentId> components) => new(components);

    public static QuerySpec ForComponents(ComponentId first, ComponentId second)
        => ForComponents(stackalloc[] { first, second });

    public static QuerySpec ForComponents(ComponentId first, ComponentId second, ComponentId third)
        => ForComponents(stackalloc[] { first, second, third });

    public static QuerySpec ForComponents(ComponentId first, ComponentId second, ComponentId third, ComponentId fourth)
        => ForComponents(stackalloc[] { first, second, third, fourth });

    public static IEqualityComparer<QuerySpec> Comparer { get; } = new QuerySpecComparer();
}

internal sealed class QuerySpecComparer : IEqualityComparer<QuerySpec>
{
    public bool Equals(QuerySpec x, QuerySpec y) => x.Equals(y);

    public int GetHashCode(QuerySpec obj) => obj.GetHashCode();
}
