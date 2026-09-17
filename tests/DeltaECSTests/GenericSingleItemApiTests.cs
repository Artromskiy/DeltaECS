namespace Delta.ECS.Tests;

using System;
using Delta.ECS;
using NUnit.Framework;

[TestFixture]
internal sealed class GenericSingleItemApiTests
{
    [Test]
    public void PrimaryGenericOverloadsResolveTheRegisteredPrimaryComponent()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.Register<Position>(new SchemaId(60_061));
        layouts.Register<Velocity>(new SchemaId(60_062));
        using var world = new World(layouts);

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
    public void HasChecksPrimaryComponentWithoutReadingOrChangingStamp()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_003));
        layouts.Register<Velocity>(new SchemaId(60_004));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, new Position { X = 1, Y = 2 });
        Entity stale = entity;
        world.Destroy(stale);

        Entity current = world.Create(positionId, new Position { X = 3, Y = 4 });
        Assert.That(world.TryGetComponentStamp(current, positionId, out Stamp before), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(world.Has<Position>(current), Is.True);
            Assert.That(world.Has<Velocity>(current), Is.False);
            Assert.That(world.Has<NamedRef>(current), Is.False);
            Assert.That(world.Has(current, positionId), Is.True);
            Assert.That(world.Has(current, layouts.GetPrimary<Velocity>()), Is.False);
            Assert.That(world.Has<Position>(stale), Is.False);
            Assert.That(world.Has(stale, positionId), Is.False);
            Assert.That(world.Has<Position>(default), Is.False);
            Assert.That(world.Has(default, positionId), Is.False);
        });

        Assert.That(world.TryGetComponentStamp(current, positionId, out Stamp after), Is.True);
        Assert.That(after, Is.EqualTo(before));
    }

    [Test]
    public void GenericAndNonGenericSingleEntityOperationsHaveEquivalentResults()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_007));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_008));
        using var world = new World(layouts);
        Entity generic = world.Create<Position>();
        Entity nonGeneric = world.Create(stackalloc[] { positionId });
        Entity[] typedBatch = new Entity[2];
        world.Create<Position>(positionId, typedBatch.Length, typedBatch);

        Assert.Multiple(() =>
        {
            Assert.That(typedBatch, Has.Length.EqualTo(2));
            Assert.That(typedBatch, Has.All.Matches<Entity>(entity => world.IsAlive(entity)));
            Assert.That(world.Has<Position>(generic), Is.True);
            Assert.That(world.Has(generic, positionId), Is.True);
            Assert.That(world.Has<Position>(generic, positionId), Is.True);
            Assert.That(world.TryGetComponentStamp<Position>(generic, out Stamp genericStamp), Is.True);
            Assert.That(world.TryGetComponentStamp(generic, positionId, out Stamp nonGenericStamp), Is.True);
            Assert.That(genericStamp, Is.EqualTo(nonGenericStamp));
        });

        var value = new Velocity { X = 3, Y = 4 };
        Assert.That(world.Add<Velocity>(generic, in value), Is.True);
        Assert.That(world.Add(nonGeneric, stackalloc[] { velocityId }), Is.True);
        Assert.That(world.Has<Velocity>(generic), Is.True);
        Assert.That(world.Has(nonGeneric, velocityId), Is.True);
        Assert.That(world.Remove<Velocity>(generic), Is.True);
        Assert.That(world.Remove(nonGeneric, stackalloc[] { velocityId }), Is.True);
        Assert.That(world.Has<Velocity>(generic), Is.False);
        Assert.That(world.Has(nonGeneric, velocityId), Is.False);
    }

    [Test]
    public void UntypedSingleEntityStructuralOperationsReportWhetherTheyChanged()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_071));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_072));
        using var world = new World(layouts);
        Entity entity = world.Create(new[] { positionId });

        Assert.That(world.Add(entity, new[] { velocityId }), Is.True);
        Assert.That(world.Add(entity, new[] { velocityId }), Is.False);
        Assert.That(world.Remove(entity, new[] { velocityId }), Is.True);
        Assert.That(world.Remove(entity, new[] { velocityId }), Is.False);
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
    public void GetReadRefReturnsPrimaryAndExplicitRowsWithoutChangingStamp()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_006));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, new Position { X = 5, Y = 6 });
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp before), Is.True);

        ref readonly Position primary = ref world.GetReadRef<Position>(entity);
        ref readonly Position explicitId = ref world.GetReadRef<Position>(entity, positionId);

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
    public void TypedPairAddInitializesBothComponentsInOneTransition()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId healthId = layouts.Register<Health>(new SchemaId(60_013));
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_014));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_015));
        using var world = new World(layouts);
        Entity entity = world.Create(healthId, new Health { Value = 7 });
        var position = new Position { X = 1, Y = 2 };
        var velocity = new Velocity { X = 3, Y = 4 };

        Assert.That(world.Add(entity, in position, in velocity), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(world.TryGet(entity, positionId, out Position actualPosition), Is.True);
            Assert.That(world.TryGet(entity, velocityId, out Velocity actualVelocity), Is.True);
            Assert.That(actualPosition, Is.EqualTo(position));
            Assert.That(actualVelocity, Is.EqualTo(velocity));
            Assert.That(world.Get<Health>(entity), Is.EqualTo(new Health { Value = 7 }));
        });

        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionStamp), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, velocityId, out Stamp velocityStamp), Is.True);
        Assert.That(positionStamp, Is.EqualTo(new Stamp(1)));
        Assert.That(velocityStamp, Is.EqualTo(new Stamp(1)));
        Assert.That(world.Add(entity, new Position { X = 9 }, new Velocity { X = 9 }), Is.False);
        Assert.That(world.TryGet(entity, positionId, out Position unchangedPosition), Is.True);
        Assert.That(unchangedPosition, Is.EqualTo(position));
    }

    [Test]
    public void TypedMultiValueSetValidatesTheArchetypeBeforeWriting()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_022));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_023));
        using var world = new World(layouts);
        Entity complete = world.Create(stackalloc[] { positionId, velocityId });
        Entity positionOnly = world.Create(positionId, new Position { X = 1, Y = 2 });

        Assert.That(world.Set(complete, new Position { X = 3, Y = 4 }, new Velocity { X = 5, Y = 6 }), Is.True);
        Assert.That(world.Set<Position, Velocity>(complete, new Position { X = 7, Y = 8 }, new Velocity { X = 9, Y = 10 }), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(complete), Is.EqualTo(new Position { X = 7, Y = 8 }));
            Assert.That(world.Get<Velocity>(complete), Is.EqualTo(new Velocity { X = 9, Y = 10 }));
        });

        Assert.Throws<InvalidOperationException>(() =>
            world.Set(positionOnly, new Position { X = 11, Y = 12 }, new Velocity { X = 13, Y = 14 }));
        Assert.That(world.Get<Position>(positionOnly), Is.EqualTo(new Position { X = 1, Y = 2 }));
    }

    [Test]
    public void TypedValueAddInitializesFourComponentsInOneTransition()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_016));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_017));
        ComponentId healthId = layouts.Register<Health>(new SchemaId(60_018));
        ComponentId namedRefId = layouts.Register<NamedRef>(new SchemaId(60_019));
        ComponentId refMarkerId = layouts.Register<RefMarker>(new SchemaId(60_020));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, new Position { X = 1, Y = 2 });
        var velocity = new Velocity { X = 3, Y = 4 };
        var health = new Health { Value = 5 };
        var named = new NamedRef { Name = "equipped", Id = 6 };
        var marker = new RefMarker { Payload = new RefPayload(7), Value = 8 };

        Assert.That(world.Add(entity, velocity, health, named, marker), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(entity, positionId), Is.EqualTo(new Position { X = 1, Y = 2 }));
            Assert.That(world.Get<Velocity>(entity, velocityId), Is.EqualTo(velocity));
            Assert.That(world.Get<Health>(entity, healthId), Is.EqualTo(health));
            Assert.That(world.Get<NamedRef>(entity, namedRefId), Is.EqualTo(named));
            Assert.That(world.Get<RefMarker>(entity, refMarkerId), Is.EqualTo(marker));
        });

        Assert.That(world.Add(
            entity,
            new Velocity { X = 10, Y = 10 },
            new Health { Value = 10 },
            new NamedRef { Name = "changed", Id = 10 },
            new RefMarker { Payload = new RefPayload(10), Value = 10 }), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Velocity>(entity, velocityId), Is.EqualTo(velocity));
            Assert.That(world.Get<Health>(entity, healthId), Is.EqualTo(health));
            Assert.That(world.Get<NamedRef>(entity, namedRefId), Is.EqualTo(named));
            Assert.That(world.Get<RefMarker>(entity, refMarkerId), Is.EqualTo(marker));
        });
    }

    [Test]
    public void BatchCreateSupportsOwnedAndCallerOwnedEntityStorage()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_031));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_032));
        using var world = new World(layouts);

        Entity[] created = new Entity[5];
        world.Create(new[] { positionId, velocityId }, created.Length, created);
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
        Entity[] entities = new Entity[4];
        world.Create(stackalloc[] { positionId }, entities.Length, entities);
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
        Entity[] entities = new Entity[2];
        world.Create(stackalloc[] { positionId }, entities.Length, entities);
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
