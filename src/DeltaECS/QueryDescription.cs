namespace DVG.ECS;

using System;
using System.Collections.Generic;

public readonly struct QueryDescription : IEquatable<QueryDescription>
{
    private readonly ComponentId[] _allComponents;
    private readonly ComponentId[] _anyComponents;
    private readonly ComponentId[] _noneComponents;
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
        _allComponents = Normalize(allComponents, out _);
        _anyComponents = Normalize(anyComponents, out _);
        _noneComponents = Normalize(noneComponents, out _);
        AllMask = ComponentMask.From(AllComponents);
        AnyMask = ComponentMask.From(AnyComponents);
        NoneMask = ComponentMask.From(NoneComponents);
        _allTags = Normalize(allTags);
        _anyTags = Normalize(anyTags);
        _noneTags = Normalize(noneTags);
        Hash = ComputeHash();
    }

    public ReadOnlySpan<ComponentId> AllComponents => _allComponents;

    public ReadOnlySpan<ComponentId> AnyComponents => _anyComponents;

    public ReadOnlySpan<ComponentId> NoneComponents => _noneComponents;

    internal ComponentMask AllMask { get; }

    internal ComponentMask AnyMask { get; }

    internal ComponentMask NoneMask { get; }

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

    private static ComponentId[] Normalize(ComponentId[] ids, out bool hasValid)
    {
        if (ids.Length == 0)
        {
            hasValid = false;
            return Array.Empty<ComponentId>();
        }

        var set = new HashSet<ComponentId>(ids);
        var normalized = new ComponentId[set.Count];
        var index = 0;
        var valid = false;

        foreach (var id in set)
        {
            if (!id.IsValid)
            {
                continue;
            }

            normalized[index++] = id;
            valid = true;
        }

        if (index < normalized.Length)
        {
            Array.Resize(ref normalized, index);
        }

        Array.Sort(normalized, (x, y) => x.Value.CompareTo(y.Value));
        hasValid = valid;
        return normalized;
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

        if (AllComponents.Length != other.AllComponents.Length
            || AnyComponents.Length != other.AnyComponents.Length
            || NoneComponents.Length != other.NoneComponents.Length
            || AllTags.Length != other.AllTags.Length
            || AnyTags.Length != other.AnyTags.Length
            || NoneTags.Length != other.NoneTags.Length)
        {
            return false;
        }

        for (var i = 0; i < AllComponents.Length; i++)
        {
            if (!AllComponents[i].Equals(other.AllComponents[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < AnyComponents.Length; i++)
        {
            if (!AnyComponents[i].Equals(other.AnyComponents[i]))
            {
                return false;
            }
        }

        for (var i = 0; i < NoneComponents.Length; i++)
        {
            if (!NoneComponents[i].Equals(other.NoneComponents[i]))
            {
                return false;
            }
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
