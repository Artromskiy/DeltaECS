namespace Delta.ECS;

public sealed partial class World
{
    /// <summary>
    /// Creates an entity containing one component and initializes its value.
    /// </summary>
    public Entity Create<T>(ComponentId componentId, in T value)
    {
        EnsureRegisteredType<T>(componentId);
        Entity entity = Create(componentId);
        InitializeComponentValue(entity, componentId, in value);

        return entity;
    }

    /// <summary>Adds one typed component to an alive entity and initializes its value.</summary>
    public bool Add<T>(Entity entity, ComponentId componentId, in T value)
    {
        if (!IsRegisteredType<T>(componentId)
            || !IsAlive(entity)
            || TryGetCore<T>(entity, componentId, out _))
        {
            return false;
        }

        Add(new[] { componentId }, entity);
        InitializeComponentValue(entity, componentId, in value);
        return true;
    }

    /// <summary>Removes one typed component from an alive entity.</summary>
    public bool Remove<T>(Entity entity, ComponentId componentId)
    {
        if (!IsRegisteredType<T>(componentId)
            || !IsAlive(entity)
            || !TryGetCore<T>(entity, componentId, out _))
        {
            return false;
        }

        Remove(new[] { componentId }, entity);
        return !TryGetCore<T>(entity, componentId, out _);
    }

    /// <summary>Reads one component when the entity owns a matching component row.</summary>
    public bool TryGet<T>(Entity entity, ComponentId componentId, out T value)
        => TryGetCore(entity, componentId, out value);

    /// <summary>
    /// Reads one component, throwing when the entity is stale, missing the row,
    /// or the requested type does not match the registered component type.
    /// </summary>
    public T Get<T>(Entity entity, ComponentId componentId)
    {
        EnsureRegisteredType<T>(componentId);
        if (!TryGet(entity, componentId, out T value))
        {
            return ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
        }

        return value;
    }

    /// <summary>Writes one component value and reports whether the row was updated.</summary>
    public bool Set<T>(Entity entity, ComponentId componentId, in T value)
        => SetCore(entity, componentId, in value);

    private bool IsRegisteredType<T>(ComponentId componentId)
    {
        return _layouts.TryGet(componentId, out var layout)
            && layout.RuntimeType == typeof(T);
    }

    private void EnsureRegisteredType<T>(ComponentId componentId)
    {
        if (!_layouts.TryGet(componentId, out var layout))
        {
            ThrowHelper.ThrowComponentNotRegistered(componentId);
        }

        if (layout.RuntimeType != typeof(T))
        {
            ThrowHelper.ThrowGenericComponentTypeMismatch<T>(componentId, layout.RuntimeType!);
        }
    }

    private void InitializeComponentValue<T>(Entity entity, ComponentId componentId, in T value)
    {
        if (!TryResolve(entity, out int recordIndex))
        {
            ThrowHelper.ThrowStructuralCreateFailed();
        }

        ref readonly var record = ref RecordAt(recordIndex);
        var archetype = _archetypes[record.Archetype];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            ThrowHelper.ThrowStructuralComponentMissing();
        }

        archetype.GetChunk(record.Chunk).GetComponentRow<T>(componentIndex)[record.SlotIndex] = value;
    }
}
