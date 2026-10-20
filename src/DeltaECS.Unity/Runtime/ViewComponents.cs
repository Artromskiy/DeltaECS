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
    [ComponentSchema(ViewComponentSchemaIds.ViewToken)]
    public readonly struct ViewToken
    {
        /// <summary>Gets the identifier of the view prefab in a <see cref="ViewPrefabCatalog"/>.</summary>
        public string Id { get; }

        /// <summary>Creates a token that selects a view prefab by identifier.</summary>
        public ViewToken(string id) => Id = id;
    }

    /// <summary>Marks an entity whose Unity view has already been bound.</summary>
    [ComponentSchema(ViewComponentSchemaIds.ViewBound)]
    public struct ViewBound
    {
    }

    /// <summary>Hides an unbound ECS entity from the Unity Hierarchy window while this tag is present.</summary>
    /// <remarks>Entities bound to a GameObject keep their normal Unity hierarchy visibility.</remarks>
    [ComponentSchema(ViewComponentSchemaIds.HideFromEntityHierarchy)]
    public readonly struct HideFromEntityHierarchy
    {
    }
    internal static class ViewComponentSchemaIds
    {
        internal const ulong ViewToken = 0x405B11F5A17E14A1UL;
        internal const ulong ViewBound = 0xB31CB42A81D3B164UL;
        internal const ulong HideFromEntityHierarchy = 0x8C13A2E9764DB501UL;
    }
}
