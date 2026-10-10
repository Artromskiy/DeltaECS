namespace Delta.ECS.Runtime.Consumer;

public static partial class RuntimeApiGrammarProof
{
    private static void VerifyStructuralOperations()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(932_001));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(932_002));
        ComponentId markerId = layouts.Register<Marker>(new SchemaId(932_003));
        using var world = new World(layouts);

        Entity one = world.Create(positionId);
        Require(world.IsAlive(one));
        Require(world.Has(one, positionId));
        Require(world.TryGetComponentStamp(one, positionId, out _));
        Entity spanCreated = world.Create(stackalloc ComponentId[] { positionId, velocityId });
        Require(world.IsAlive(spanCreated));
        Require(world.Has(spanCreated, positionId));
        Require(world.Has(spanCreated, velocityId));

        Span<Entity> output = stackalloc Entity[2];
        Require(world.Create(stackalloc ComponentId[] { positionId }, output) == 2);
        Require(world.Create(positionId, 2, output) == 2);
        Require(world.Create(stackalloc ComponentId[] { positionId, velocityId }, output) == 2);
        Require(world.Create(stackalloc ComponentId[] { positionId, velocityId }, 2, output) == 2);
        Require(world.Create(stackalloc ComponentId[] { markerId }, 2) == 2);

        Entity first = world.Create(positionId);
        Entity second = world.Create(positionId);
        Entity third = world.Create(positionId);
        Require(world.Add(first, velocityId));
        Require(!world.Add(first, velocityId));
        Require(world.Add(first, new[] { markerId }));
        Require(world.Add(second, stackalloc ComponentId[] { velocityId, markerId }));

        Entity[] batch = [first, second, third];
        ReadOnlySpan<ComponentId> velocityComponents = stackalloc ComponentId[] { velocityId };
        Require(world.Add(batch, velocityComponents) == 1);
        Require(world.Add(batch, velocityId) == 0);
        Require(world.Add(batch, new[] { markerId }) == 1);

        Query positioned = world.WhereAll(positionId);
        Query unmarked = positioned.WhereNone(stackalloc ComponentId[] { markerId });
        int unmarkedCount = CountEntities(world, in unmarked);
        Require(unmarkedCount > 0);
        Require(world.Add(in unmarked, markerId) == unmarkedCount);
        Require(world.Add(in positioned, stackalloc ComponentId[] { markerId }) == 0);

        Require(world.Remove(first, velocityId));
        Require(!world.Remove(first, new[] { velocityId }));
        Require(world.Remove(second, stackalloc ComponentId[] { velocityId, markerId }));
        Require(world.Remove(batch, velocityId) == 1);
        Require(world.Remove(batch, new[] { markerId }) == 2);

        Query marked = positioned.WhereAll(stackalloc ComponentId[] { markerId });
        int markedCount = CountEntities(world, in marked);
        Require(world.Remove(in positioned, markerId) == markedCount);
        Require(world.Remove(in positioned, stackalloc ComponentId[] { markerId }) == 0);

        Span<Entity> destroyBatch = stackalloc Entity[1] { one };
        Require(world.Destroy(destroyBatch) == 1);
        Require(!world.Destroy(one));

        int expectedDestroyed = CountEntities(world, in positioned);
        Require(world.Destroy(in positioned) == expectedDestroyed);
    }
}
