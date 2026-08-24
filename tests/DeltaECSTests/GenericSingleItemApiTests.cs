namespace Delta.ECS.Tests;

using System;
using Delta.ECS;
using NUnit.Framework;

[TestFixture]
public sealed class GenericSingleItemApiTests
{
    [Test]
    public void TypedCreateGetSetAndTryGetUseTheExistingComponentRows()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_001));
        using var world = new World(layouts);
        Stamp beforeCreate = world.Stamp;

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
            Assert.That(createdStamp, Is.EqualTo(new Stamp(beforeCreate.Value + 1)));
            Assert.That(createdStamp, Is.Not.EqualTo(setStamp));
            Assert.That(setStamp, Is.EqualTo(world.Stamp));
        });
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
        Stamp beforeAdd = world.Stamp;

        Assert.That(world.Add(entity, velocityId, new Velocity { X = 5, Y = 6 }), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionAfterAdd), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, velocityId, out Stamp velocityAfterAdd), Is.True);
        Stamp afterAdd = world.Stamp;
        Assert.That(world.Add(entity, velocityId, new Velocity()), Is.False);
        Assert.That(world.Stamp, Is.EqualTo(afterAdd));
        Assert.That(world.TryGet(entity, velocityId, out Velocity _), Is.True);
        Assert.That(world.Remove<Velocity>(entity, velocityId), Is.True);
        Stamp afterRemove = world.Stamp;
        Assert.That(world.Remove<Velocity>(entity, velocityId), Is.False);
        Assert.That(world.Stamp, Is.EqualTo(afterRemove));
        Assert.That(world.TryGet(entity, velocityId, out Velocity _), Is.False);
        Assert.That(world.TryGet(entity, positionId, out Position _), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionAfterRemove), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(positionAfterAdd, Is.EqualTo(positionBefore));
            Assert.That(positionAfterRemove, Is.EqualTo(positionBefore));
            Assert.That(afterAdd, Is.EqualTo(new Stamp(beforeAdd.Value + 1)));
            Assert.That(velocityAfterAdd, Is.EqualTo(afterAdd));
            Assert.That(afterRemove, Is.Not.EqualTo(afterAdd));
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
        Stamp afterDestroy = world.Stamp;

        Assert.Multiple(() =>
        {
            Assert.That(world.TryGet(entity, positionId, out Position _), Is.False);
            Assert.That(world.Set(entity, positionId, new Position()), Is.False);
            Assert.That(world.Add(entity, positionId, new Position()), Is.False);
            Assert.That(world.Remove<Position>(entity, positionId), Is.False);
            Assert.That(world.Stamp, Is.EqualTo(afterDestroy));
        });

        Assert.Throws<InvalidOperationException>(() => world.Get<Position>(entity, positionId));
        Assert.Throws<ArgumentException>(() => world.Get<Velocity>(entity, positionId));
        Assert.Throws<ArgumentException>(() => world.Get<Position>(entity, new ComponentId(200)));
        Assert.Throws<ArgumentException>(() => world.Create<Velocity>(positionId, new Velocity()));
        Assert.Throws<ArgumentException>(() => world.Create(componentId: new ComponentId(200), value: new Position()));
        Assert.That(world.AliveEntityCount, Is.Zero);
        Assert.That(world.Stamp, Is.EqualTo(afterDestroy));
    }
}
