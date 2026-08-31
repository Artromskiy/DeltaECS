namespace Delta.ECS.Tests;

using Delta.ECS;
using Delta.ECS.Integration;
using NUnit.Framework;

[TestFixture]
public sealed class StampInvariantTests
{
    [Test]
    public void ComponentStampsAreWorldLocalAndDoNotRequireAWorldRevision()
    {
        var layoutsA = new ComponentLayoutRegistry();
        var layoutsB = new ComponentLayoutRegistry();
        ComponentId positionA = layoutsA.Register<Position>(new SchemaId(41_001));
        ComponentId positionB = layoutsB.Register<Position>(new SchemaId(41_001));
        using var worldA = new World(layoutsA);
        using var worldB = new World(layoutsB);

        Entity entityA = worldA.Create(positionA);
        Entity entityB = worldB.Create(positionB);

        Assert.That(worldA.TryGetComponentStamp(entityA, positionA, out Stamp stampA), Is.True);
        Assert.That(worldB.TryGetComponentStamp(entityB, positionB, out Stamp stampB), Is.True);
        Assert.That(stampA, Is.EqualTo(new Stamp(1)));
        Assert.That(stampB, Is.EqualTo(new Stamp(1)));
    }

    [Test]
    public void StructuralMovePreservesExistingTermsAndInitializesAddedTerms()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(41_011));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(41_012));
        using var world = new World(layouts, chunkCapacity: 2);
        Entity entity = world.Create(positionId);
        Assert.That(world.Set(entity, positionId, new Position { X = 7 }), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionBefore), Is.True);

        Assert.That(world.Add(entity, velocityId, new Velocity()), Is.True);

        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionAfter), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, velocityId, out Stamp velocityAfter), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(positionAfter, Is.EqualTo(positionBefore));
            Assert.That(velocityAfter, Is.EqualTo(new Stamp(1)));
        });
    }

    [Test]
    public void PointWriteChangesOnlyTheSelectedEntityComponent()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(41_021));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(41_022));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, velocityId);
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionBefore), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, velocityId, out Stamp velocityBefore), Is.True);

        Assert.That(world.Set(entity, positionId, new Position { X = 1 }), Is.True);

        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionAfter), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, velocityId, out Stamp velocityAfter), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(positionAfter, Is.EqualTo(new Stamp(positionBefore.Value + 1)));
            Assert.That(velocityAfter, Is.EqualTo(velocityBefore));
        });
    }

    [Test]
    public void GeneratedDenseWriteChangesArchetypeTermOncePerExecution()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(41_031));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        var query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp before), Is.True);

        world.ForEach<Position>(
            in query,
            static (ref Position position) => position.X++);

        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp after), Is.True);
        Assert.That(after, Is.EqualTo(new Stamp(before.Value + 1)));
    }

    [Test]
    public void FailedIntegrationWritesDoNotChangeTheComponentStamp()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(41_041));
        using var storage = new World(layouts);
        IEcsWorld world = storage;
        world.Initialize();
        Entity entity = world.Create(stackalloc[] { positionId });
        Assert.That(world.TryRead(entity, positionId, out ComponentSnapshot initial, out _), Is.True);

        Assert.That(world.TryWrite(entity, positionId, new Position { X = 1 }, initial.Stamp, out Stamp written, out _), Is.True);
        Assert.That(world.TryWrite(entity, positionId, new Position { X = 2 }, initial.Stamp, out Stamp rejected, out EcsWriteError error), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(rejected, Is.EqualTo(default(Stamp)));
            Assert.That(error.Code, Is.EqualTo(EcsWriteErrorCode.StaleStamp));
        });
        Assert.That(world.TryRead(entity, positionId, out ComponentSnapshot current, out _), Is.True);
        Assert.That(current.Stamp, Is.EqualTo(written));
        world.Shutdown();
    }
}
