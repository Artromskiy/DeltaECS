namespace Delta.ECS.Runtime.Consumer;

public static partial class RuntimeApiGrammarProof
{
    private static void VerifyQueries()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(931_001));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(931_002));
        ComponentId markerId = layouts.Register<Marker>(new SchemaId(931_003));
        using var world = new World(layouts);

        Span<ComponentId> positionedAndMoving = [positionId, velocityId];
        Entity moving = world.Create(positionedAndMoving);
        Entity stationary = world.Create(positionId);
        Entity marked = world.Create([velocityId, markerId]);
        world.GetRef<Velocity>(moving, velocityId).Value = 1;
        world.GetRef<Velocity>(marked, velocityId).Value = 2;

        QuerySpec allSpec = QuerySpec.WhereAll(positionId)
            .WithAny([velocityId, markerId])
            .WithNone([markerId]);
        Query allQuery = world.CreateQuery(in allSpec);
        Require(CountEntities(world, in allQuery) == 1);

        QuerySpec emptySpec = QuerySpec.Empty;
        Query emptyQuery = world.CreateQuery(in emptySpec);
        Require(CountEntities(world, in emptyQuery) == 3);

        QuerySpec anySpec = QuerySpec.WhereAny([positionId, markerId]);
        Query anyQuery = world.CreateQuery(in anySpec);
        Require(CountEntities(world, in anyQuery) == 3);

        QuerySpec singleAnySpec = QuerySpec.WhereAny(markerId);
        Query singleAnySpecQuery = world.CreateQuery(in singleAnySpec);
        Require(CountEntities(world, in singleAnySpecQuery) == 1);

        QuerySpec noneSpec = QuerySpec.Empty
            .WithNone([markerId])
            .WithAll([velocityId]);
        Query noneQuery = world.CreateQuery(in noneSpec);
        Require(CountEntities(world, in noneQuery) == 1);

        QuerySpec singleNoneSpec = QuerySpec.WhereNone(markerId);
        Query singleNoneSpecQuery = world.CreateQuery(in singleNoneSpec);
        Require(CountEntities(world, in singleNoneSpecQuery) == 2);

        Query singleAll = world.WhereAll(positionId);
        Query spanAll = world.WhereAll([positionId, velocityId]);
        Query singleAny = world.WhereAny(markerId);
        Query spanAny = world.WhereAny([positionId, markerId]);
        Query singleNone = world.WhereNone(markerId);
        Query spanNone = world.WhereNone([markerId, velocityId]);
        Require(CountEntities(world, in singleAll) == 2);
        Require(CountEntities(world, in spanAll) == 1);
        Require(CountEntities(world, in singleAny) == 1);
        Require(CountEntities(world, in spanAny) == 3);
        Require(CountEntities(world, in singleNone) == 2);
        Require(CountEntities(world, in spanNone) == 1);

        Query composed = singleAll
            .WhereAll([positionId])
            .WhereAny([velocityId, markerId])
            .WhereNone([markerId]);
        Require(CountEntities(world, in composed) == 1);
        Require(world.IsAlive(moving));
        Require(world.IsAlive(stationary));
        Require(world.IsAlive(marked));
    }

    private static int CountEntities(World world, in Query query)
    {
        int count = 0;
        world.ForEachEntity(in query, _ => count++).Invoke();
        return count;
    }
}
