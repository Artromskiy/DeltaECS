namespace Delta.ECS.Tests;

using System;
using Delta.ECS;
using NUnit.Framework;

[TestFixture]
public sealed class GenericSingleItemApiTests
{
    [Test]
    public void PrimaryGenericOverloadsResolveTheRegisteredPrimaryComponent()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.Register<Position>(new SchemaId(60_061));
        layouts.Register<Velocity>(new SchemaId(60_062));
        using var world = new World(layouts, chunkCapacity: 2);

        var initial = new Position { X = 1, Y = 2 };
        ComponentId positionId = layouts.GetPrimary<Position>();
        Entity entity = world.Create(positionId, in initial);
        var replacement = new Position { X = 3, Y = 4 };

        Assert.That(world.TryGet(entity, out Position actual), Is.True);
        Assert.That(actual, Is.EqualTo(initial));
        Assert.That(world.Get<Position>(entity), Is.EqualTo(initial));
        Assert.That(world.Set(entity, in replacement), Is.True);
        Assert.That(world.Get<Position>(entity), Is.EqualTo(replacement));

        var entities = new Entity[2];
        Assert.That(world.Create<Position>(entities.Length, entities), Is.EqualTo(entities.Length));
        var velocity = new Velocity { X = 5, Y = 6 };
        Assert.That(world.Add<Velocity>(entities, in velocity), Is.EqualTo(entities.Length));
        Assert.That(world.TryGet(entities[0], out Velocity added), Is.True);
        Assert.That(added, Is.EqualTo(velocity));
        Assert.That(world.Remove<Velocity>(entities), Is.EqualTo(entities.Length));
        Assert.That(world.TryGet(entities[0], out Velocity _), Is.False);
    }

    [Test]
    public void UntypedSingleEntityStructuralOperationsReportWhetherTheyChanged()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_071));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_072));
        using var world = new World(layouts);
        Entity entity = world.Create(new[] { positionId });

        Assert.That(world.Add(new[] { velocityId }, entity), Is.True);
        Assert.That(world.Add(new[] { velocityId }, entity), Is.False);
        Assert.That(world.Remove(new[] { velocityId }, entity), Is.True);
        Assert.That(world.Remove(new[] { velocityId }, entity), Is.False);
    }

    [Test]
    public void TypedCreateGetSetAndTryGetUseTheExistingComponentRows()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_001));
        using var world = new World(layouts);

        Entity entity = world.Create(positionId, new Position { X = 1, Y = 2 });
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp createdStamp), Is.True);

        Assert.That(world.TryGet(entity, positionId, out Position initial), Is.True);
        Assert.That(initial, Is.EqualTo(new Position { X = 1, Y = 2 }));
        Assert.That(world.Get<Position>(entity, positionId), Is.EqualTo(initial));
        Assert.That(world.Set(entity, positionId, new Position { X = 3, Y = 4 }), Is.True);
        Assert.That(world.Get<Position>(entity, positionId), Is.EqualTo(new Position { X = 3, Y = 4 }));
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp setStamp), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(createdStamp, Is.EqualTo(new Stamp(1)));
            Assert.That(createdStamp, Is.Not.EqualTo(setStamp));
        });
    }

    [Test]
    public void GetRefReturnsMutablePrimaryAndExplicitComponentRows()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_005));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, new Position { X = 1, Y = 2 });
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp before), Is.True);

        ref Position primary = ref world.GetRef<Position>(entity);
        primary.X = 3;
        ref Position explicitId = ref world.GetRef<Position>(entity, positionId);
        explicitId.Y = 4;

        Assert.That(world.Get<Position>(entity), Is.EqualTo(new Position { X = 3, Y = 4 }));
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp after), Is.True);
        Assert.That(after, Is.Not.EqualTo(before));
    }

    [Test]
    public void RefReadReturnsPrimaryAndExplicitRowsWithoutChangingStamp()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_006));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, new Position { X = 5, Y = 6 });
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp before), Is.True);

        ref readonly Position primary = ref world.RefRead<Position>(entity);
        ref readonly Position explicitId = ref world.RefRead<Position>(entity, positionId);

        Assert.That(primary, Is.EqualTo(new Position { X = 5, Y = 6 }));
        Assert.That(explicitId, Is.EqualTo(primary));
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp after), Is.True);
        Assert.That(after, Is.EqualTo(before));
    }

    [Test]
    public void SetFailsFastWhenTheEntityDoesNotContainTheComponent()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_081));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_082));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, new Position());
        Entity missing = world.Create(velocityId, new Velocity());

        Assert.Throws<InvalidOperationException>(
            () => world.Set(missing, positionId, new Position()));
        Assert.Throws<InvalidOperationException>(
            () => world.Set(entity, velocityId, new Velocity()));
        Assert.Throws<ArgumentException>(
            () => world.Set<Velocity>(entity, positionId, new Velocity()));
        Assert.That(world.Get<Position>(entity), Is.EqualTo(new Position()));
    }

    [Test]
    public void TypedAddAndRemoveAreSingleComponentStructuralTransitions()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_011));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_012));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, new Position());
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionBefore), Is.True);

        Assert.That(world.Add(entity, velocityId, new Velocity { X = 5, Y = 6 }), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionAfterAdd), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, velocityId, out Stamp velocityAfterAdd), Is.True);
        Assert.That(world.Add(entity, velocityId, new Velocity()), Is.False);
        Assert.That(world.TryGet(entity, velocityId, out Velocity _), Is.True);
        Assert.That(world.Remove<Velocity>(entity, velocityId), Is.True);
        Assert.That(world.Remove<Velocity>(entity, velocityId), Is.False);
        Assert.That(world.TryGet(entity, velocityId, out Velocity _), Is.False);
        Assert.That(world.TryGet(entity, positionId, out Position _), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionAfterRemove), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(positionAfterAdd, Is.EqualTo(positionBefore));
            Assert.That(positionAfterRemove, Is.EqualTo(positionBefore));
            Assert.That(velocityAfterAdd, Is.EqualTo(new Stamp(1)));
        });
    }

    [Test]
    public void BatchCreateSupportsOwnedAndCallerOwnedEntityStorage()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_031));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_032));
        using var world = new World(layouts, chunkCapacity: 2);

        Entity[] created = world.Create(new[] { positionId, velocityId }, 5);
        var destination = new Entity[3];
        int written = world.Create(stackalloc[] { positionId }, 3, destination);
        var typedDestination = new Entity[2];
        int typedWritten = world.Create<Position>(positionId, 2, typedDestination);

        Assert.Multiple(() =>
        {
            Assert.That(created, Has.Length.EqualTo(5));
            Assert.That(written, Is.EqualTo(3));
            Assert.That(typedWritten, Is.EqualTo(2));
            Assert.That(world.AliveEntityCount, Is.EqualTo(10));
            Assert.That(created, Has.All.Matches<Entity>(entity => world.IsAlive(entity)));
            Assert.That(destination, Has.All.Matches<Entity>(entity => world.IsAlive(entity)));
            Assert.That(typedDestination, Has.All.Matches<Entity>(entity => world.IsAlive(entity)));
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => world.Create(stackalloc[] { positionId }, -1));
        Assert.Throws<ArgumentException>(() => world.Create(stackalloc[] { positionId }, 2, new Entity[1]));
    }

    [Test]
    public void TypedBatchAddInitializesOnlyNewComponentsAndRemoveSkipsIneligibleEntities()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_041));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_042));
        using var world = new World(layouts);
        Entity[] entities = world.Create(stackalloc[] { positionId }, 4);
        Entity stale = entities[1];
        Assert.That(world.Destroy(stale), Is.True);

        var velocity = new Velocity { X = 5, Y = 6 };
        Assert.That(world.Add(entities, velocityId, in velocity), Is.EqualTo(3));
        Assert.That(world.Add(entities, velocityId, in velocity), Is.EqualTo(0));
        Assert.That(world.TryGet(entities[0], velocityId, out Velocity initialized), Is.True);
        Assert.That(initialized, Is.EqualTo(velocity));
        Assert.That(world.TryGet(stale, velocityId, out Velocity _), Is.False);

        Assert.That(world.Remove<Velocity>(entities, velocityId), Is.EqualTo(3));
        Assert.That(world.Remove<Velocity>(entities, velocityId), Is.EqualTo(0));
        Assert.That(world.TryGet(entities[0], velocityId, out Velocity _), Is.False);
        Assert.That(world.TryGet(entities[2], positionId, out Position _), Is.True);
    }

    [Test]
    public void TypedBatchOperationsRejectMismatchedComponentTypes()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_051));
        using var world = new World(layouts);
        Entity[] entities = world.Create(stackalloc[] { positionId }, 2);
        var velocity = new Velocity { X = 1, Y = 2 };

        Assert.Multiple(() =>
        {
            Assert.That(world.Add(entities, positionId, in velocity), Is.Zero);
            Assert.That(world.Remove<Velocity>(entities, positionId), Is.Zero);
            Assert.That(world.TryGet(entities[0], positionId, out Position _), Is.True);
        });
    }

    [Test]
    public void StaleAndMismatchedHandlesDoNotMutateThroughTheGenericBoundary()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_021));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, new Position());
        Assert.That(world.Destroy(entity), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(world.TryGet(entity, positionId, out Position _), Is.False);
            Assert.Throws<InvalidOperationException>(
                () => world.Set(entity, positionId, new Position()));
            Assert.That(world.Add(entity, positionId, new Position()), Is.False);
            Assert.That(world.Remove<Position>(entity, positionId), Is.False);
        });

        Assert.Throws<InvalidOperationException>(() => world.Get<Position>(entity, positionId));
        Assert.Throws<ArgumentException>(() => world.Get<Velocity>(entity, positionId));
        Assert.Throws<ArgumentException>(() => world.Get<Position>(entity, new ComponentId(200)));
        Assert.Throws<ArgumentException>(() => world.Create<Velocity>(positionId, new Velocity()));
        Assert.Throws<ArgumentException>(() => world.Create(componentId: new ComponentId(200), value: new Position()));
        Assert.That(world.AliveEntityCount, Is.Zero);
    }
}
