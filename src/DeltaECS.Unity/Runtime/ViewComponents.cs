using Delta.ECS;

namespace Delta.ECS.Unity
{
    /// <summary>Selects the Unity prefab used to represent an entity.</summary>
    [DeltaEcsComponent(SchemaId = 0x405B11F5A17E14A1UL)]
    public readonly struct ViewToken
    {
        /// <summary>Gets the identifier of the view prefab in a <see cref="ViewPrefabCatalog"/>.</summary>
        public string Id { get; }

        /// <summary>Creates a token that selects a view prefab by identifier.</summary>
        public ViewToken(string id) => Id = id;
    }

    /// <summary>Marks an entity whose Unity view has already been bound.</summary>
    [DeltaEcsComponent(SchemaId = 0xB31CB42A81D3B164UL)]
    public struct ViewBound
    {
    }
}
