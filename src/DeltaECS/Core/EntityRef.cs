using System.Runtime.CompilerServices;

namespace Delta.ECS;

/// <summary>A scoped entity handle for an entity-aware iteration callback.</summary>
/// <remarks>Component access uses the regular world APIs and resolves the entity handle on each call.</remarks>
public ref struct EntityRef
{
    private readonly World _world;
    internal Entity _entity;

    internal EntityRef(World world, Entity entity = default)
    {
        _world = world;
        _entity = entity;
    }

    /// <summary>Gets the stable handle for the current entity.</summary>
    public Entity Handle => _entity;

    /// <summary>Converts this scoped view to its stable entity handle.</summary>
    public static implicit operator Entity(EntityRef entity) => entity.Handle;

    /// <summary>Gets the current entity's index.</summary>
    public int Index => Handle.Index;

    /// <summary>Gets the current entity's lifetime generation.</summary>
    public int Generation => Handle.Generation;

    /// <summary>Reports whether the current entity has the specified component registration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has(ComponentId componentId) => _world.Has(_entity, componentId);

    /// <summary>Reports whether the current entity has the primary registration for <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>() => _world.Has<T>(_entity);

    /// <summary>Reports whether the current entity has the specified registration of <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>(ComponentId componentId) => _world.Has<T>(_entity, componentId);

    /// <summary>Attempts to read a component by registration, returning its default value when absent or mismatched.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>(ComponentId componentId, out T value) => _world.TryGet(_entity, componentId, out value);

    /// <summary>Attempts to read the primary component for <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>(out T value) => _world.TryGet<T>(_entity, out value);

    /// <summary>Reads the primary component for <typeparamref name="T"/> or throws when it is missing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>() => _world.Get<T>(_entity);

    /// <summary>Reads the specified component registration as <typeparamref name="T"/> or throws when it is missing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>(ComponentId componentId) => _world.Get<T>(_entity, componentId);

    /// <summary>Gets a writable component reference and marks its change stamp.</summary>
    /// <remarks>For a tag, this returns the shared default placeholder; writes through it are not stored.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef<T>(ComponentId componentId) => ref _world.GetRef<T>(_entity, componentId);

    /// <summary>Gets a writable reference to the primary component for <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef<T>() => ref _world.GetRef<T>(_entity);

    /// <summary>Gets a read-only reference to the primary component for <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T GetReadRef<T>() => ref _world.GetReadRef<T>(_entity);

    /// <summary>Gets a read-only reference to the specified component registration as <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T GetReadRef<T>(ComponentId componentId) => ref _world.GetReadRef<T>(_entity, componentId);

    /// <summary>Attempts to read the stamp for the primary component of <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetComponentStamp<T>(out Stamp stamp) => _world.TryGetComponentStamp<T>(_entity, out stamp);

    /// <summary>Attempts to read the stamp for the specified component registration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetComponentStamp(ComponentId componentId, out Stamp stamp)
        => _world.TryGetComponentStamp(_entity, componentId, out stamp);

    /// <summary>Attempts to read the stamp for the specified registration of <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetComponentStamp<T>(ComponentId componentId, out Stamp stamp)
        => _world.TryGetComponentStamp<T>(_entity, componentId, out stamp);
}
