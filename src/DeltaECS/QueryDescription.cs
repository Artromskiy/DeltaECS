namespace DVG.ECS;

using System;
using System.Collections.Generic;

public readonly struct QueryDescription : IEquatable<QueryDescription>
{
    private readonly ComponentMask _allMask;
    private readonly ComponentMask _anyMask;
    private readonly ComponentMask _noneMask;
    private readonly TagId[] _allTags;
    private readonly TagId[] _anyTags;
    private readonly TagId[] _noneTags;

    public QueryDescription(
        ComponentId[] allComponents,
        ComponentId[] anyComponents,
        ComponentId[] noneComponents,
        TagId[] allTags,
        TagId[] anyTags,
        TagId[] noneTags)
    {
        _allMask = BuildMask(allComponents);
        _anyMask = BuildMask(anyComponents);
        _noneMask = BuildMask(noneComponents);
        _allTags = Normalize(allTags);
        _anyTags = Normalize(anyTags);
        _noneTags = Normalize(noneTags);
        Hash = ComputeHash();
    }

    public ComponentMask AllMask => _allMask;

    public ComponentMask AnyMask => _anyMask;

    public ComponentMask NoneMask => _noneMask;

    public ReadOnlySpan<TagId> AllTags => _allTags;

    public ReadOnlySpan<TagId> AnyTags => _anyTags;

    public ReadOnlySpan<TagId> NoneTags => _noneTags;

    internal int Hash { get; }

    private int ComputeHash()
    {
        var hash = new HashCode();
        hash.Add(AllMask);
        hash.Add(AnyMask);
        hash.Add(NoneMask);

        for (var i = 0; i < AllTags.Length; i++)
        {
            hash.Add(AllTags[i].Value);
        }

        for (var i = 0; i < AnyTags.Length; i++)
        {
            hash.Add(AnyTags[i].Value);
        }

        for (var i = 0; i < NoneTags.Length; i++)
        {
            hash.Add(NoneTags[i].Value);
        }

        return hash.ToHashCode();
    }

    private static ComponentMask BuildMask(ComponentId[] ids)
    {
        var mask = default(ComponentMask);
        for (var i = 0; i < ids.Length; i++)
        {
            if (!ids[i].IsValid)
            {
                continue;
            }

            mask = mask.Set(ids[i]);
        }

        return mask;
    }

    private static TagId[] Normalize(TagId[] ids)
    {
        if (ids.Length == 0)
        {
            return Array.Empty<TagId>();
        }

        for (var i = 0; i < ids.Length; i++)
        {
            if (!ids[i].IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(ids), "TagId must be non-negative.");
            }
        }

        var set = new HashSet<TagId>(ids);
        var normalized = new TagId[set.Count];
        var index = 0;
        foreach (var tag in set)
        {
            normalized[index++] = tag;
        }

        if (index < normalized.Length)
        {
            Array.Resize(ref normalized, index);
        }

        Array.Sort(normalized, (x, y) => x.Value.CompareTo(y.Value));
        return normalized;
    }

    public bool Equals(QueryDescription other)
    {
        if (Hash != other.Hash)
        {
            return false;
        }

        if (_allMask != other._allMask
            || _anyMask != other._anyMask
            || _noneMask != other._noneMask
            || AllTags.Length != other.AllTags.Length
            || AnyTags.Length != other.AnyTags.Length
            || NoneTags.Length != other.NoneTags.Length)
        {
            return false;
        }

        for (var i = 0; i < AllTags.Length; i++)
        {
            if (!AllTags[i].Equals(other.AllTags[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < AnyTags.Length; i++)
        {
            if (!AnyTags[i].Equals(other.AnyTags[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < NoneTags.Length; i++)
        {
            if (!NoneTags[i].Equals(other.NoneTags[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is QueryDescription other && Equals(other);

    public override int GetHashCode() => Hash;

    public static QueryDescription ForComponents(params ComponentId[] components) =>
        new(components, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(), Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>());

    public static IEqualityComparer<QueryDescription> Comparer { get; } = new QueryDescriptionComparer();
}

internal sealed class QueryDescriptionComparer : IEqualityComparer<QueryDescription>
{
    public bool Equals(QueryDescription x, QueryDescription y) => x.Equals(y);

    public int GetHashCode(QueryDescription obj) => obj.GetHashCode();
}
