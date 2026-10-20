namespace Delta.ECS;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

/// <summary>Stores typed component values and tags under explicit component IDs.</summary>
/// <remarks>This data is independent of a world. Component IDs are validated when a world consumes it.</remarks>
public sealed class EntityData
{
    private static readonly TagEntry Tag = new();
    private readonly Dictionary<ComponentId, IEntry> _components = new();
    private readonly List<ComponentId> _componentIds = new();
    private int _shapeVersion;

    /// <summary>Gets the number of components and tags stored in this data.</summary>
    public int Count => _components.Count;

    /// <summary>Gets the IDs of the stored component values and tags in insertion order.</summary>
    /// <remarks>The span borrows this instance's storage and must not be retained across modifications.</remarks>
    public ReadOnlySpan<ComponentId> ComponentIds => ArrayAccess.AsSpan(_componentIds);

    internal int ShapeVersion => _shapeVersion;

    /// <summary>Tries to add a component value without replacing an existing value.</summary>
    /// <param name="componentId">The explicit component registration ID.</param>
    /// <param name="value">The component value to store.</param>
    /// <returns><see langword="true"/> when the value was added; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="componentId"/> is invalid.</exception>
    /// <exception cref="InvalidOperationException">The ID is already stored with a different type or as a tag.</exception>
    public bool Add<T>(ComponentId componentId, in T value)
    {
        EnsureValid(componentId);
        if (_components.TryGetValue(componentId, out IEntry? existing))
        {
            if (existing is not ComponentEntry<T>)
            {
                ThrowHelper.ThrowEntityDataTypeMismatch<T>(componentId);
            }

            return false;
        }

        AddEntry(componentId, new ComponentEntry<T>(value));
        return true;
    }

    /// <summary>Adds a component value or replaces the value stored under its ID.</summary>
    /// <param name="componentId">The explicit component registration ID.</param>
    /// <param name="value">The component value to store.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="componentId"/> is invalid.</exception>
    /// <exception cref="InvalidOperationException">The ID is already stored with a different type or as a tag.</exception>
    public void AddOrSet<T>(ComponentId componentId, in T value)
    {
        EnsureValid(componentId);
        if (_components.TryGetValue(componentId, out IEntry? existing))
        {
            if (existing is ComponentEntry<T> component)
            {
                component.Value = value;
                return;
            }

            ThrowHelper.ThrowEntityDataTypeMismatch<T>(componentId);
        }

        AddEntry(componentId, new ComponentEntry<T>(value));
    }

    /// <summary>Adds a tag and returns whether its ID was new.</summary>
    /// <param name="componentId">The explicit tag registration ID.</param>
    /// <returns><see langword="true"/> when the tag was added; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="componentId"/> is invalid.</exception>
    /// <exception cref="InvalidOperationException">The ID is already stored as a component value.</exception>
    public bool AddTag(ComponentId componentId)
    {
        EnsureValid(componentId);
        if (_components.TryGetValue(componentId, out IEntry? existing))
        {
            if (existing is not TagEntry)
            {
                ThrowHelper.ThrowEntityDataTagValueMismatch(componentId);
            }

            return false;
        }

        AddEntry(componentId, Tag);
        return true;
    }

    /// <summary>Reports whether a component value or tag is stored under the ID.</summary>
    public bool Has(ComponentId componentId) => _components.ContainsKey(componentId);

    /// <summary>Tries to copy a stored component value of the requested CLR type.</summary>
    /// <remarks>Tags have no value and return <see langword="false"/>.</remarks>
    public bool TryGet<T>(ComponentId componentId, out T value)
    {
        if (_components.TryGetValue(componentId, out IEntry? entry) && entry is ComponentEntry<T> component)
        {
            value = component.Value;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>Gets a stored component value of the requested CLR type.</summary>
    /// <exception cref="KeyNotFoundException">No component value is stored under the ID.</exception>
    /// <exception cref="InvalidOperationException">The stored value has a different CLR type or is a tag.</exception>
    public T Get<T>(ComponentId componentId)
    {
        if (!_components.TryGetValue(componentId, out IEntry? entry))
        {
            ThrowHelper.ThrowEntityDataMissingValue(componentId);
        }

        if (entry is ComponentEntry<T> component)
        {
            return component.Value;
        }

        return ThrowHelper.ThrowEntityDataTypeMismatch<T>(componentId);
    }

    /// <summary>Tries to replace a stored component value without adding a new entry.</summary>
    /// <returns><see langword="true"/> when the value was replaced; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">The ID is stored with a different type or as a tag.</exception>
    public bool Set<T>(ComponentId componentId, in T value)
    {
        if (!_components.TryGetValue(componentId, out IEntry? entry))
        {
            return false;
        }

        if (entry is ComponentEntry<T> component)
        {
            component.Value = value;
            return true;
        }

        return ThrowHelper.ThrowEntityDataTypeMismatch<bool>(componentId);
    }

    /// <summary>Gets a writable reference to a stored component value.</summary>
    /// <exception cref="KeyNotFoundException">No component value is stored under the ID.</exception>
    /// <exception cref="InvalidOperationException">The stored value has a different CLR type or is a tag.</exception>
    public ref T GetRef<T>(ComponentId componentId)
    {
        if (!_components.TryGetValue(componentId, out IEntry? entry))
        {
            ThrowHelper.ThrowEntityDataMissingValue(componentId);
        }

        if (entry is ComponentEntry<T> component)
        {
            return ref component.Value;
        }

        return ref ThrowHelper.ThrowEntityDataRefTypeMismatch<T>(componentId);
    }

    /// <summary>Gets a read-only reference to a stored component value.</summary>
    public ref readonly T GetReadRef<T>(ComponentId componentId) => ref GetRef<T>(componentId);

    /// <summary>Removes a stored component value or tag.</summary>
    /// <returns><see langword="true"/> when an entry was removed.</returns>
    public bool Remove(ComponentId componentId)
    {
        if (!_components.Remove(componentId))
        {
            return false;
        }

        for (int index = 0; index < _componentIds.Count; index++)
        {
            if (_componentIds[index] == componentId)
            {
                _componentIds.RemoveAt(index);
                break;
            }
        }

        _shapeVersion++;
        return true;
    }

    /// <summary>Removes all stored component values and tags.</summary>
    public void Clear()
    {
        if (_componentIds.Count == 0)
        {
            return;
        }

        _components.Clear();
        _componentIds.Clear();
        _shapeVersion++;
    }

    internal void Validate(ComponentRegistry layouts)
    {
        for (int index = 0; index < _componentIds.Count; index++)
        {
            ComponentId componentId = _componentIds[index];
            Type registeredType = layouts.GetComponentType(componentId);
            IEntry entry = _components[componentId];
            if (entry.IsTag)
            {
                if (!layouts.IsTag(componentId))
                {
                    ThrowHelper.ThrowEntityDataUnregisteredTag(componentId);
                }

                continue;
            }

            if (entry.ComponentType != registeredType)
            {
                ThrowHelper.ThrowComponentTypeMismatch(componentId, entry.ComponentType!);
            }
        }
    }

    internal void InitializeAdded(ReadOnlySpan<int> componentRows, ref GeneratedComponentValueWriter writer)
    {
        for (int index = 0; index < _componentIds.Count; index++)
        {
            ComponentId componentId = _componentIds[index];
            _components[componentId].InitializeAdded(componentRows.RefAt(index), ref writer);
        }
    }

    internal void InitializeCreated(ReadOnlySpan<int> componentRows, ref GeneratedComponentValueWriter writer)
    {
        for (int index = 0; index < _componentIds.Count; index++)
        {
            ComponentId componentId = _componentIds[index];
            _components[componentId].InitializeCreated(componentRows.RefAt(index), ref writer);
        }
    }

    private static void EnsureValid(ComponentId componentId)
    {
        if (!componentId.IsValid)
        {
            ThrowHelper.ThrowComponentIdOutOfRange();
        }
    }

    private void AddEntry(ComponentId componentId, IEntry entry)
    {
        _components.Add(componentId, entry);
        _componentIds.Add(componentId);
        _shapeVersion++;
    }

    private interface IEntry
    {
        Type? ComponentType { get; }
        bool IsTag { get; }

        void InitializeAdded(int componentRow, ref GeneratedComponentValueWriter writer);

        void InitializeCreated(int componentRow, ref GeneratedComponentValueWriter writer);
    }

    private sealed class TagEntry : IEntry
    {
        Type? IEntry.ComponentType => null;
        bool IEntry.IsTag => true;

        void IEntry.InitializeAdded(int componentRow, ref GeneratedComponentValueWriter writer) { }

        void IEntry.InitializeCreated(int componentRow, ref GeneratedComponentValueWriter writer) { }
    }

    private sealed class ComponentEntry<T>(T value) : IEntry
    {
        internal T Value = value;

        Type? IEntry.ComponentType => typeof(T);
        bool IEntry.IsTag => false;

        void IEntry.InitializeAdded(int componentRow, ref GeneratedComponentValueWriter writer)
            => writer.InitializeAdded(componentRow, in Value);

        void IEntry.InitializeCreated(int componentRow, ref GeneratedComponentValueWriter writer)
            => writer.InitializeCreated(componentRow, in Value);
    }
}

internal sealed class EntityDataPlan
{
    internal const int SkippedComponentRow = int.MinValue;

    private readonly ConcurrentDictionary<EntityDataRouteKey, int[]> _componentRows = new();

    internal EntityDataPlan(int shapeVersion, ComponentSet componentSet)
    {
        ShapeVersion = shapeVersion;
        ComponentSet = componentSet;
    }

    internal ComponentSet ComponentSet { get; }

    internal Archetype? TargetArchetype { get; set; }

    internal int ShapeVersion { get; }

    internal int[] GetComponentRows(
        ComponentRegistry layouts,
        Archetype targetArchetype,
        ReadOnlySpan<ComponentId> componentIds,
        int sourceArchetypeId = -1,
        ReadOnlySpan<int> addedTargetRows = default,
        bool allowMissing = false)
    {
        var key = new EntityDataRouteKey(targetArchetype.Id, sourceArchetypeId, allowMissing);
        if (_componentRows.TryGetValue(key, out int[]? componentRows))
        {
            return componentRows;
        }

        componentRows = BuildComponentRows(
            layouts,
            targetArchetype,
            componentIds,
            sourceArchetypeId >= 0,
            addedTargetRows,
            allowMissing);
        return _componentRows.GetOrAdd(key, componentRows);
    }

    private static int[] BuildComponentRows(
        ComponentRegistry layouts,
        Archetype targetArchetype,
        ReadOnlySpan<ComponentId> componentIds,
        bool skipExistingRows,
        ReadOnlySpan<int> addedTargetRows,
        bool allowMissing)
    {
        var componentRows = new int[componentIds.Length];
        for (int index = 0; index < componentIds.Length; index++)
        {
            ComponentId componentId = componentIds.RefAt(index);
            if (layouts.TryGetTagIndex(componentId, out int tagIndex))
            {
                componentRows[index] = -tagIndex - 1;
                continue;
            }

            if (!targetArchetype.TryGetComponentIndex(componentId, out int componentRow))
            {
                if (!allowMissing)
                {
                    ThrowHelper.ThrowStructuralComponentMissing();
                }

                componentRows[index] = SkippedComponentRow;
                continue;
            }

            if (skipExistingRows && !Contains(addedTargetRows, componentRow))
            {
                componentRows[index] = SkippedComponentRow;
                continue;
            }

            componentRows[index] = componentRow;
        }

        return componentRows;
    }

    internal static bool HasAll(Chunk chunk, int slotIndex, ReadOnlySpan<int> componentRows)
    {
        for (int index = 0; index < componentRows.Length; index++)
        {
            int componentRow = componentRows.RefAt(index);
            if (componentRow == SkippedComponentRow
                || (componentRow < 0 && !chunk.HasTag(-componentRow - 1, slotIndex)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(ReadOnlySpan<int> rows, int componentRow)
    {
        for (int index = 0; index < rows.Length; index++)
        {
            if (rows[index] == componentRow)
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct EntityDataRouteKey(int TargetArchetypeId, int SourceArchetypeId, bool AllowMissing);
}
