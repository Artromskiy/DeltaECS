namespace Delta.ECS.Runtime.Consumer;

public static partial class RuntimeApiGrammarProof
{
    private static void VerifyObjectAccess()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(935_001));
        ComponentId markerId = layouts.Register<Marker>(new SchemaId(935_002));
        using var world = new World(layouts);
        ReadOnlySpan<ComponentId> ids = world.Components.GetComponentIds();
        Require(ids.Length == 2);
        Require(ids[0] == positionId);
        Require(ids[1] == markerId);
        Require(layouts.IsTag(markerId));
        Require(layouts.GetSchemaId(positionId) == new SchemaId(935_001));
        Require(layouts.GetComponentType(positionId) == typeof(Position));
        layouts.AddAlias("runtime.position", positionId);
        ReadOnlySpan<string> aliases = layouts.GetAliases(positionId);
        Require(aliases.Length == 1);
        Require(aliases[0] == "runtime.position");
        Require(layouts.GetAliases(markerId).IsEmpty);

        Entity entity = world.Create(positionId);
        Require(world.TryRead(entity, positionId, out object? value));
        Require(value is Position);
        Require(world.TryWrite(entity, positionId, new Position { Value = 77 }));
        Require(world.Get<Position>(entity, positionId).Value == 77);
        Require(world.TryWrite(entity, markerId, new Marker()));
        Require(world.Has(entity, markerId));
        Require(world.Destroy(entity));
        Require(!world.TryRead(entity, positionId, out _));
    }
}
