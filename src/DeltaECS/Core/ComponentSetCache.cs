namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

internal readonly struct ComponentSetId : IEquatable<ComponentSetId>
{
    internal ComponentSetId(int value)
    {
        Value = value;
    }

    internal int Value { get; }

    internal bool IsValid => Value > 0;

    public bool Equals(ComponentSetId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is ComponentSetId other && Equals(other);

    public override int GetHashCode() => Value;

    public static bool operator ==(ComponentSetId left, ComponentSetId right) => left.Equals(right);

    public static bool operator !=(ComponentSetId left, ComponentSetId right) => !left.Equals(right);
}

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

    internal ComponentSetId Id { get; private set; }

    internal bool Matches(ReadOnlySpan<ComponentId> componentIds)
        => _componentIds.AsSpan().SequenceEqual(componentIds);

    internal void AssignId(ComponentSetId id) => Id = id;

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
    private readonly Dictionary<ComponentMask, ComponentSetId> _idsByMask = new();
    private ComponentSet?[] _setsById = new ComponentSet?[4];
    private int _nextId = 1;

    internal bool TryGet(ComponentSetId id, [NotNullWhen(true)] out ComponentSet? set)
    {
        int value = id.Value;
        if ((uint)value < (uint)_setsById.Length && (set = _setsById[value]) is not null)
        {
            return true;
        }

        set = null;
        return false;
    }

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
    {
        Register(set);
        _typed[key] = set;
    }

    internal void Add(ComponentSet set)
    {
        Register(set);
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
        _idsByMask.Clear();
        _setsById = Array.Empty<ComponentSet?>();
        _nextId = 1;
    }

    private void Register(ComponentSet set)
    {
        if (set.Id.IsValid)
        {
            return;
        }

        if (_idsByMask.TryGetValue(set.Mask, out ComponentSetId existingId))
        {
            set.AssignId(existingId);
            return;
        }

        int value = _nextId++;
        if (value >= _setsById.Length)
        {
            Array.Resize(ref _setsById, Math.Max(value + 1, _setsById.Length * 2));
        }

        ComponentSetId id = new(value);
        set.AssignId(id);
        _idsByMask.Add(set.Mask, id);
        _setsById[value] = set;
    }
}
