#nullable enable

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

        internal EntityContext(World world, Entity entity)
        {
            World = world;
            Entity = entity;
        }
    }
}
