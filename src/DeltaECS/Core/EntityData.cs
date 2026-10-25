namespace Delta.ECS;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

/// <summary>Stores component values and component IDs under explicit component IDs.</summary>
/// <remarks>This data is independent of a world. Component IDs are validated when a world consumes it.</remarks>
public sealed class EntityData
{
    private readonly Dictionary<ComponentId, IEntry> _components = new();
    private readonly List<ComponentId> _componentIds = new();
    private int _shapeVersion;

    /// <summary>Gets the number of component IDs stored in this data.</summary>
    public int Count => _components.Count;

    /// <summary>Gets the stored component IDs in insertion order.</summary>
    /// <remarks>The span borrows this instance's storage and must not be retained across modifications.</remarks>
    public ReadOnlySpan<ComponentId> ComponentIds => ArrayAccess.AsSpan(_componentIds);

    internal int ShapeVersion => _shapeVersion;

    /// <summary>Copies missing component values and IDs from an entity, preserving existing entries.</summary>
    /// <returns>The number of entries added.</returns>
    public int Add(World world, Entity entity) => CopyFrom(world, entity, add: true, set: false);

    /// <summary>Copies component values and IDs from an entity into existing entries only.</summary>
    /// <returns>The number of matching entries applied.</returns>
    public int Set(World world, Entity entity) => CopyFrom(world, entity, add: false, set: true);

    /// <summary>Copies component values and IDs from an entity, adding or updating entries.</summary>
    /// <remarks>Entries absent from the entity are preserved. Values are shallow copies.</remarks>
    public void AddOrSet(World world, Entity entity) => CopyFrom(world, entity, add: true, set: true);

    /// <summary>Adds a component ID without a CLR type or value.</summary>
    /// <remarks>The consuming world resolves the ID; data components receive their default value when added, and tags receive membership.</remarks>
    /// <returns><see langword="true"/> when the ID was added; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="componentId"/> is invalid.</exception>
    public bool Add(ComponentId componentId)
    {
        EnsureValid(componentId);
        if (_components.ContainsKey(componentId))
        {
            return false;
        }

        AddEntry(componentId, new ComponentPresenceEntry(null));
        return true;
    }

    /// <summary>Adds a component value, or only presence when <typeparamref name="T"/> is a tag, without replacing an existing entry.</summary>
    /// <param name="componentId">The explicit component registration ID.</param>
    /// <param name="value">The component value to store.</param>
    /// <returns><see langword="true"/> when the value was added; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="componentId"/> is invalid.</exception>
    /// <exception cref="InvalidOperationException">The ID is already stored with a different CLR type.</exception>
    public bool Add<T>(ComponentId componentId, in T value)
    {
        EnsureValid(componentId);
        if (ComponentType<T>.IsTag)
        {
            return AddTypedPresence<T>(componentId);
        }

        if (_components.TryGetValue(componentId, out IEntry? existing))
        {
            EnsureType<T>(componentId, existing);
            return false;
        }

        AddEntry(componentId, new ComponentEntry<T>(value));
        return true;
    }

    /// <summary>Adds a component value or presence, or replaces the stored value under its ID.</summary>
    /// <param name="componentId">The explicit component registration ID.</param>
    /// <param name="value">The component value to store.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="componentId"/> is invalid.</exception>
    /// <exception cref="InvalidOperationException">The ID is already stored with a different CLR type.</exception>
    public void AddOrSet<T>(ComponentId componentId, in T value)
    {
        EnsureValid(componentId);
        if (ComponentType<T>.IsTag)
        {
            AddOrSetTypedPresence<T>(componentId);
            return;
        }

        if (_components.TryGetValue(componentId, out IEntry? existing))
        {
            EnsureType<T>(componentId, existing);
            if (existing is ComponentEntry<T> component)
            {
                component.Value = value;
                return;
            }

            _components[componentId] = new ComponentEntry<T>(value);
            return;
        }

        AddEntry(componentId, new ComponentEntry<T>(value));
    }

    /// <summary>Reports whether the component ID is stored.</summary>
    public bool Has(ComponentId componentId) => _components.ContainsKey(componentId);

    /// <summary>Tries to copy a stored component value of the requested CLR type.</summary>
    /// <remarks>Entries without a stored value return <see langword="false"/>.</remarks>
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
    /// <exception cref="InvalidOperationException">No value of the requested CLR type is stored under the ID.</exception>
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
    /// <exception cref="InvalidOperationException">The ID is stored with a different CLR type.</exception>
    public bool Set<T>(ComponentId componentId, in T value)
    {
        if (!_components.TryGetValue(componentId, out IEntry? entry))
        {
            return false;
        }

        EnsureType<T>(componentId, entry);
        if (ComponentType<T>.IsTag)
        {
            _components[componentId] = new ComponentPresenceEntry(typeof(T));
            return true;
        }

        if (entry is ComponentEntry<T> component)
        {
            component.Value = value;
            return true;
        }

        _components[componentId] = new ComponentEntry<T>(value);
        return true;
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

    /// <summary>Removes a stored component ID.</summary>
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

    /// <summary>Removes all stored component IDs and values.</summary>
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

    internal static ComponentReader GetComponentReader<T>() => TypedComponentReader<T>.Instance;

    internal bool HasValue(ComponentId componentId) => _components[componentId].HasValue;

    internal void Validate(ComponentRegistry layouts)
    {
        for (int index = 0; index < _componentIds.Count; index++)
        {
            ComponentId componentId = _componentIds[index];
            Type registeredType = layouts.GetComponentType(componentId);
            IEntry entry = _components[componentId];
            if (entry.ComponentType is Type componentType && componentType != registeredType)
            {
                ThrowHelper.ThrowComponentTypeMismatch(componentId, componentType);
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

    private static void EnsureType<T>(ComponentId componentId, IEntry entry)
    {
        if (entry.ComponentType is Type componentType && componentType != typeof(T))
        {
            ThrowHelper.ThrowEntityDataTypeMismatch<T>(componentId);
        }
    }

    private int CopyFrom(World world, Entity entity, bool add, bool set)
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        return world.CopyEntityData(entity, this, add, set);
    }

    private bool AddTypedPresence<T>(ComponentId componentId)
    {
        if (_components.TryGetValue(componentId, out IEntry? existing))
        {
            EnsureType<T>(componentId, existing);
            return false;
        }

        AddEntry(componentId, new ComponentPresenceEntry(typeof(T)));
        return true;
    }

    private void AddOrSetTypedPresence<T>(ComponentId componentId)
    {
        if (_components.TryGetValue(componentId, out IEntry? existing))
        {
            EnsureType<T>(componentId, existing);
            _components[componentId] = new ComponentPresenceEntry(typeof(T));
            return;
        }

        AddEntry(componentId, new ComponentPresenceEntry(typeof(T)));
    }

    private void AddEntry(ComponentId componentId, IEntry entry)
    {
        _components.Add(componentId, entry);
        _componentIds.Add(componentId);
        _shapeVersion++;
    }

    internal abstract class ComponentReader
    {
        internal abstract int Copy(EntityData destination, ComponentId componentId,
            Array? row, int slotIndex, bool add, bool set);
    }

    private static class ComponentType<T>
    {
        internal static readonly bool IsTag = ComponentRegistry.IsTagType(typeof(T));
    }

    private sealed class TypedComponentReader<T> : ComponentReader
    {
        internal static readonly TypedComponentReader<T> Instance = new();

        internal override int Copy(EntityData destination, ComponentId componentId,
            Array? row, int slotIndex, bool add, bool set)
        {
            bool exists = destination._components.TryGetValue(componentId, out IEntry? entry);
            if (exists ? !set : !add)
            {
                return 0;
            }
            if (entry is not null)
            {
                EnsureType<T>(componentId, entry);
            }
            if (ComponentType<T>.IsTag)
            {
                if (!exists)
                {
                    destination.AddEntry(componentId, new ComponentPresenceEntry(typeof(T)));
                }
                else if (entry is { ComponentType: null })
                {
                    destination._components[componentId] = new ComponentPresenceEntry(typeof(T));
                }
                return 1;
            }

            // Data routes supply the current typed row; tag routes return above.
            T value = ((T[])row!).RefAt(slotIndex);
            if (entry is ComponentEntry<T> component)
            {
                component.Value = value;
            }
            else if (exists)
            {
                destination._components[componentId] = new ComponentEntry<T>(value);
            }
            else
            {
                destination.AddEntry(componentId, new ComponentEntry<T>(value));
            }
            return 1;
        }
    }

    private interface IEntry
    {
        Type? ComponentType { get; }
        bool HasValue { get; }

        void InitializeAdded(int componentRow, ref GeneratedComponentValueWriter writer);

        void InitializeCreated(int componentRow, ref GeneratedComponentValueWriter writer);
    }

    private sealed class ComponentPresenceEntry(Type? componentType) : IEntry
    {
        Type? IEntry.ComponentType => componentType;
        bool IEntry.HasValue => false;

        void IEntry.InitializeAdded(int componentRow, ref GeneratedComponentValueWriter writer) { }

        void IEntry.InitializeCreated(int componentRow, ref GeneratedComponentValueWriter writer) { }
    }

    private sealed class ComponentEntry<T>(T value) : IEntry
    {
        internal T Value = value;

        Type? IEntry.ComponentType => typeof(T);
        bool IEntry.HasValue => true;

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

    internal ComponentSet ComponentSet { get; }

    internal Archetype? TargetArchetype { get; set; }

    internal int ShapeVersion { get; }

    internal EntityDataPlan(int shapeVersion, ComponentSet componentSet)
    {
        ShapeVersion = shapeVersion;
        ComponentSet = componentSet;
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
