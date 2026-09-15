namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>Immutable component registrations and their matching mask.</summary>
internal sealed class ComponentSet
{
    internal static readonly ComponentSet Empty =
        new(Array.Empty<ComponentId>(), default);

    private readonly ComponentId[] _componentIds;

    internal ComponentSet(ComponentId[] componentIds, ComponentMask mask)
    {
        _componentIds = componentIds;
        Mask = mask;
        Hash = ComputeHash(componentIds);
    }

    internal ReadOnlySpan<ComponentId> ComponentIds => _componentIds;

    internal ComponentMask Mask { get; }

    internal int Hash { get; }

    internal bool Matches(ReadOnlySpan<ComponentId> componentIds)
        => _componentIds.AsSpan().SequenceEqual(componentIds);

    internal static int ComputeHash(ReadOnlySpan<ComponentId> componentIds)
    {
        unchecked
        {
            uint hash = 2_166_136_261u;
            hash = (hash ^ (uint)componentIds.Length) * 16_777_619u;
            for (int index = 0; index < componentIds.Length; index++)
            {
                hash = (hash ^ (uint)componentIds[index].Value) * 16_777_619u;
            }

            return (int)hash;
        }
    }
}

/// <summary>World-owned cache for immutable component sets.</summary>
internal sealed class ComponentSetCache
{
    private readonly Dictionary<RuntimeTypeHandle, ComponentSet> _typed = new();
    private readonly Dictionary<int, List<ComponentSet>> _dynamic = new();

    internal bool TryGet(
        RuntimeTypeHandle key,
        [NotNullWhen(true)] out ComponentSet? set)
        => _typed.TryGetValue(key, out set);

    internal bool TryGet(
        ReadOnlySpan<ComponentId> componentIds,
        [NotNullWhen(true)] out ComponentSet? set)
    {
        int hash = ComponentSet.ComputeHash(componentIds);
        if (_dynamic.TryGetValue(hash, out List<ComponentSet>? candidates))
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                ComponentSet candidate = candidates[index];
                if (candidate.Matches(componentIds))
                {
                    set = candidate;
                    return true;
                }
            }
        }

        set = null;
        return false;
    }

    internal void Add(RuntimeTypeHandle key, ComponentSet set)
        => _typed[key] = set;

    internal void Add(ComponentSet set)
    {
        if (!_dynamic.TryGetValue(set.Hash, out List<ComponentSet>? candidates))
        {
            candidates = new List<ComponentSet>(1);
            _dynamic.Add(set.Hash, candidates);
        }

        candidates!.Add(set);
    }

    internal void Clear()
    {
        _typed.Clear();
        _dynamic.Clear();
    }

}
