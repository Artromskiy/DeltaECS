namespace Delta.ECS.Runtime.Consumer;

public static partial class RuntimeApiGrammarProof
{
    private static void VerifyStructuralOperations()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(932_001));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(932_002));
        ComponentId markerId = layouts.Register<Marker>(new SchemaId(932_003));
        using var world = new World(layouts);

        Entity one = world.Create(positionId);
        Require(world.IsAlive(one));
        Require(world.Has(one, positionId));
        Entity spanCreated = world.Create([positionId, velocityId]);
        Require(world.IsAlive(spanCreated));
        Require(world.Has(spanCreated, positionId));
        Require(world.Has(spanCreated, velocityId));

        Span<Entity> output = stackalloc Entity[2];
        Require(world.Create([positionId], output) == 2);
        Require(world.Create(positionId, 2, output) == 2);
        Require(world.Create([positionId, velocityId], output) == 2);
        Require(world.Create([positionId, velocityId], 2, output) == 2);
        Require(world.Create([markerId], 2) == 2);

        Entity first = world.Create(positionId);
        Entity second = world.Create(positionId);
        Entity third = world.Create(positionId);
        Require(world.Add(first, velocityId));
        Require(!world.Add(first, velocityId));
        Require(world.Add(first, [markerId]));
        Require(world.Add(second, [velocityId, markerId]));

        Entity[] batch = [first, second, third];
        ReadOnlySpan<ComponentId> velocityComponents = [velocityId];
        Require(world.Add(batch, velocityComponents) == 1);
        Require(world.Add(batch, velocityId) == 0);
        Require(world.Add(batch, [markerId]) == 1);

        Query positioned = world.WhereAll(positionId);
        Query unmarked = positioned.WhereNone([markerId]);
        int unmarkedCount = CountEntities(world, in unmarked);
        Require(unmarkedCount > 0);
        Require(world.Add(in unmarked, markerId) == unmarkedCount);
        Require(world.Add(in positioned, [markerId]) == 0);

        Require(world.Remove(first, velocityId));
        Require(!world.Remove(first, [velocityId]));
        Require(world.Remove(second, [velocityId, markerId]));
        Require(world.Remove(batch, velocityId) == 1);
        Require(world.Remove(batch, [markerId]) == 2);

        Query marked = positioned.WhereAll([markerId]);
        int markedCount = CountEntities(world, in marked);
        Require(world.Remove(in positioned, markerId) == markedCount);
        Require(world.Remove(in positioned, [markerId]) == 0);

        Span<Entity> destroyBatch = [one];
        Require(world.Destroy(destroyBatch) == 1);
        Require(!world.Destroy(one));

        int expectedDestroyed = CountEntities(world, in positioned);
        Require(world.Destroy(in positioned) == expectedDestroyed);
    }
}
