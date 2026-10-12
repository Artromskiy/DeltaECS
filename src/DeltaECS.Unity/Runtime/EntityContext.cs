#nullable enable

using System;
using Delta.ECS;

namespace Delta.ECS.Unity
{
    /// <summary>A stable entity handle paired with the world that owns it.</summary>
    public readonly struct EntityContext
    {
        /// <summary>Gets the entity handle.</summary>
        public Entity Entity { get; }

        /// <summary>Gets the owning world, or <see langword="null"/> when this is the default context.</summary>
        public World? World { get; }

        /// <summary>Gets whether this context refers to a live entity.</summary>
        public bool IsAlive => World != null && World.IsAlive(Entity);

        /// <summary>Reports whether the entity owns the specified component registration.</summary>
        public bool Has(ComponentId componentId) => World?.Has(Entity, componentId) ?? false;

        /// <summary>Reports whether the entity owns the primary registration for <typeparamref name="T"/>.</summary>
        public bool Has<T>() => World?.Has<T>(Entity) ?? false;

        /// <summary>Attempts to read the primary component for <typeparamref name="T"/>.</summary>
        public bool TryGet<T>(out T value)
        {
            World? world = World;
            if (world is null)
            {
                value = default!;
                return false;
            }

            return world.TryGet(Entity, out value);
        }

        /// <summary>Attempts to read the specified registration of <typeparamref name="T"/>.</summary>
        public bool TryGet<T>(ComponentId componentId, out T value)
        {
            World? world = World;
            if (world is null)
            {
                value = default!;
                return false;
            }

            return world.TryGet(Entity, componentId, out value);
        }

        /// <summary>Reads the primary component for <typeparamref name="T"/> or throws when it is missing.</summary>
        public T Get<T>() => GetWorld().Get<T>(Entity);

        /// <summary>Reads the specified component registration as <typeparamref name="T"/> or throws when it is missing.</summary>
        public T Get<T>(ComponentId componentId) => GetWorld().Get<T>(Entity, componentId);

        /// <summary>Gets a writable reference to the primary component for <typeparamref name="T"/>.</summary>
        public ref T GetRef<T>() => ref GetWorld().GetRef<T>(Entity);

        /// <summary>Gets a writable reference to the specified registration of <typeparamref name="T"/>.</summary>
        public ref T GetRef<T>(ComponentId componentId) => ref GetWorld().GetRef<T>(Entity, componentId);

        /// <summary>Gets a read-only reference to the primary component for <typeparamref name="T"/>.</summary>
        public ref readonly T GetReadRef<T>() => ref GetWorld().GetReadRef<T>(Entity);

        /// <summary>Gets a read-only reference to the specified registration of <typeparamref name="T"/>.</summary>
        public ref readonly T GetReadRef<T>(ComponentId componentId) => ref GetWorld().GetReadRef<T>(Entity, componentId);

        internal EntityContext(World world, Entity entity)
        {
            World = world;
            Entity = entity;
        }

        private World GetWorld()
            => World ?? UnityThrowHelpers.ThrowMissingEntityContextWorld();
    }
}
