namespace Delta.ECS;

using System;
using System.Collections.Generic;

/// <summary>Stores typed component values and tags under explicit component IDs.</summary>
/// <remarks>This data is independent of a world. Component IDs are validated when a world consumes it.</remarks>
public sealed class EntityData
{
    private static readonly TagEntry Tag = new();
    private readonly Dictionary<ComponentId, IEntry> _components = new();
    private readonly List<ComponentId> _componentIds = new();

    /// <summary>Gets the number of components and tags stored in this data.</summary>
    public int Count => _components.Count;

    internal ReadOnlySpan<ComponentId> ComponentIds => ArrayAccess.AsSpan(_componentIds);

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
                throw TypeMismatch<T>(componentId);
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
            if (existing is not ComponentEntry<T> component)
            {
                throw TypeMismatch<T>(componentId);
            }

            component.Value = value;
            return;
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
                throw new InvalidOperationException($"Component ID {componentId} already stores a component value.");
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
            throw new KeyNotFoundException($"No component data is stored for component ID {componentId}.");
        }

        if (entry is not ComponentEntry<T> component)
        {
            throw TypeMismatch<T>(componentId);
        }

        return component.Value;
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

        if (entry is not ComponentEntry<T> component)
        {
            throw TypeMismatch<T>(componentId);
        }

        component.Value = value;
        return true;
    }

    /// <summary>Gets a writable reference to a stored component value.</summary>
    /// <exception cref="KeyNotFoundException">No component value is stored under the ID.</exception>
    /// <exception cref="InvalidOperationException">The stored value has a different CLR type or is a tag.</exception>
    public ref T GetRef<T>(ComponentId componentId)
    {
        if (!_components.TryGetValue(componentId, out IEntry? entry))
        {
            throw new KeyNotFoundException($"No component data is stored for component ID {componentId}.");
        }

        if (entry is not ComponentEntry<T> component)
        {
            throw TypeMismatch<T>(componentId);
        }

        return ref component.Value;
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

        return true;
    }

    /// <summary>Removes all stored component values and tags.</summary>
    public void Clear()
    {
        _components.Clear();
        _componentIds.Clear();
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
                    throw new InvalidOperationException($"Component ID {componentId} is not registered as a tag.");
                }

                continue;
            }

            if (entry.ComponentType != registeredType)
            {
                ThrowHelper.ThrowComponentTypeMismatch(componentId, entry.ComponentType!);
            }
        }
    }

    internal void InitializeAdded(ref GeneratedComponentValueWriter writer)
    {
        for (int index = 0; index < _componentIds.Count; index++)
        {
            ComponentId componentId = _componentIds[index];
            _components[componentId].InitializeAdded(componentId, ref writer);
        }
    }

    internal void InitializeCreated(ref GeneratedComponentValueWriter writer)
    {
        for (int index = 0; index < _componentIds.Count; index++)
        {
            ComponentId componentId = _componentIds[index];
            _components[componentId].InitializeCreated(componentId, ref writer);
        }
    }

    private static void EnsureValid(ComponentId componentId)
    {
        if (!componentId.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(componentId));
        }
    }

    private static InvalidOperationException TypeMismatch<T>(ComponentId componentId)
        => new($"Component ID {componentId} does not store a value of type {typeof(T)}.");

    private void AddEntry(ComponentId componentId, IEntry entry)
    {
        _components.Add(componentId, entry);
        _componentIds.Add(componentId);
    }

    private interface IEntry
    {
        Type? ComponentType { get; }
        bool IsTag { get; }

        void InitializeAdded(ComponentId componentId, ref GeneratedComponentValueWriter writer);

        void InitializeCreated(ComponentId componentId, ref GeneratedComponentValueWriter writer);
    }

    private sealed class TagEntry : IEntry
    {
        Type? IEntry.ComponentType => null;
        bool IEntry.IsTag => true;

        void IEntry.InitializeAdded(ComponentId componentId, ref GeneratedComponentValueWriter writer) { }

        void IEntry.InitializeCreated(ComponentId componentId, ref GeneratedComponentValueWriter writer) { }
    }

    private sealed class ComponentEntry<T>(T value) : IEntry
    {
        internal T Value = value;

        Type? IEntry.ComponentType => typeof(T);
        bool IEntry.IsTag => false;

        void IEntry.InitializeAdded(ComponentId componentId, ref GeneratedComponentValueWriter writer)
            => writer.InitializeAdded(componentId, in Value);

        void IEntry.InitializeCreated(ComponentId componentId, ref GeneratedComponentValueWriter writer)
            => writer.InitializeCreated(componentId, in Value);
    }
}
