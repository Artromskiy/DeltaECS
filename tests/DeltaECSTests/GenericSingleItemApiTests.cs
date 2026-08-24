namespace Delta.ECS.Tests;

using System;
using Delta.ECS;
using NUnit.Framework;

[TestFixture]
public sealed class GenericSingleItemApiTests
{
    [Test]
    public void TypedCreate_Get_Set_And_TryGet_UseTheExistingComponentRows()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_001));
        using var world = new World(layouts);

        Entity entity = world.Create(positionId, new Position { X = 1, Y = 2 });

        Assert.That(world.TryGet(entity, positionId, out Position initial), Is.True);
        Assert.That(initial, Is.EqualTo(new Position { X = 1, Y = 2 }));
        Assert.That(world.Get<Position>(entity, positionId), Is.EqualTo(initial));
        Assert.That(world.Set(entity, positionId, new Position { X = 3, Y = 4 }), Is.True);
        Assert.That(world.Get<Position>(entity, positionId), Is.EqualTo(new Position { X = 3, Y = 4 }));
    }

    [Test]
    public void TypedAddAndRemove_AreSingleComponentStructuralTransitions()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_011));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_012));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, new Position());

        Assert.That(world.Add(entity, velocityId, new Velocity { X = 5, Y = 6 }), Is.True);
        Assert.That(world.Add(entity, velocityId, new Velocity()), Is.False);
        Assert.That(world.TryGet(entity, velocityId, out Velocity _), Is.True);
        Assert.That(world.Remove<Velocity>(entity, velocityId), Is.True);
        Assert.That(world.Remove<Velocity>(entity, velocityId), Is.False);
        Assert.That(world.TryGet(entity, velocityId, out Velocity _), Is.False);
        Assert.That(world.TryGet(entity, positionId, out Position _), Is.True);
    }

    [Test]
    public void StaleAndMismatchedHandles_DoNotMutateThroughTheGenericBoundary()
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
        Assert.Throws<ArgumentException>(() => world.Create<Velocity>(positionId, new Velocity()));
        Assert.Throws<ArgumentException>(() => world.Create(componentId: new ComponentId(200), value: new Position()));
        Assert.That(world.AliveEntityCount, Is.Zero);
        Assert.That(world.Stamp, Is.EqualTo(afterDestroy));
    }
}
