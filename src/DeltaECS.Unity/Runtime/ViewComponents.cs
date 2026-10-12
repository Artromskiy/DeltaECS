using Delta.ECS;

namespace Delta.ECS.Unity
{
    /// <summary>Receives the entity context after a Unity view is bound to an entity.</summary>
    public interface IEntityView
    {
        /// <summary>Initializes this view for its bound entity.</summary>
        void OnBind(EntityContext context);
    }

    /// <summary>Selects the Unity prefab used to represent an entity.</summary>
    [ComponentSchema(0x405B11F5A17E14A1UL)]
    public readonly struct ViewToken
    {
        /// <summary>Gets the identifier of the view prefab in a <see cref="ViewPrefabCatalog"/>.</summary>
        public string Id { get; }

        /// <summary>Creates a token that selects a view prefab by identifier.</summary>
        public ViewToken(string id) => Id = id;
    }

    /// <summary>Marks an entity whose Unity view has already been bound.</summary>
    [ComponentSchema(0xB31CB42A81D3B164UL)]
    public struct ViewBound
    {
    }
}
