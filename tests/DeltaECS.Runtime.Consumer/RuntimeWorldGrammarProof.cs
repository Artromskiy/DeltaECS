namespace Delta.ECS.Runtime.Consumer;

public static partial class RuntimeApiGrammarProof
{
    private static void VerifyWorldAndHandleApi()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(931_011));
        var world = new World(layouts, initialEntityCapacity: 0);
        Require(ReferenceEquals(world.Layouts, layouts));
        Require(world.AliveEntityCount == 0);

        Entity invalid = default;
        Require(!invalid.IsValid);

        Entity entity = world.Create(positionId);
        Require(entity.IsValid);
        Require(world.IsAlive(entity));
        Require(world.AliveEntityCount == 1);

        QuerySpec emptySpec = QuerySpec.Empty;
        Query query = world.CreateQuery(in emptySpec);
        Require(query.IsValid);
        Require(world.Destroy(entity));
        Require(world.AliveEntityCount == 0);

        world.Dispose();
        Require(!query.IsValid);
    }
}
