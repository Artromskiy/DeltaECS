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

    /// <summary>Attempts to read a component by registration, returning its default value when absent or mismatched.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>(ComponentId componentId, out T value) => _world.TryGet(_entity, componentId, out value);

    /// <summary>Gets a writable component reference and marks its change stamp.</summary>
    /// <remarks>For a tag, this returns the shared default placeholder; writes through it are not stored.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef<T>(ComponentId componentId) => ref _world.GetRef<T>(_entity, componentId);
}
