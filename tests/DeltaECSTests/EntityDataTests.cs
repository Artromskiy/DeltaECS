namespace Delta.ECS.Tests;

using NUnit.Framework;

[TestFixture]
internal sealed class EntityDataTests
{
    [Test]
    public void StoresValuesByExplicitComponentIdAndReturnsMutableReferences()
    {
        var data = new EntityData();
        ComponentId firstId = new(1);
        ComponentId secondId = new(2);

        Assert.That(data.Add(firstId, 10), Is.True);
        Assert.That(data.Add(secondId, 20), Is.True);

        ref int value = ref data.GetRef<int>(firstId);
        value++;

        Assert.Multiple(() =>
        {
            Assert.That(data.Count, Is.EqualTo(2));
            Assert.That(data.Get<int>(firstId), Is.EqualTo(11));
            Assert.That(data.Get<int>(secondId), Is.EqualTo(20));
        });
    }

    [Test]
    public void ExposesStoredComponentIdsInInsertionOrder()
    {
        var data = new EntityData();
        ComponentId firstId = new(1);
        ComponentId tagId = new(2);
        ComponentId lastId = new(3);

        data.Add(firstId, 10);
        data.Add(tagId);
        data.Add(lastId, 30);

        ReadOnlySpan<ComponentId> componentIds = data.ComponentIds;

        Assert.That(componentIds.Length, Is.EqualTo(3));
        Assert.That(componentIds[0], Is.EqualTo(firstId));
        Assert.That(componentIds[1], Is.EqualTo(tagId));
        Assert.That(componentIds[2], Is.EqualTo(lastId));
    }

    [Test]
    public void DuplicateAddPreservesTheExistingValue()
    {
        var data = new EntityData();
        ComponentId componentId = new(1);

        Assert.That(data.Add(componentId, 10), Is.True);
        Assert.That(data.Add(componentId, 20), Is.False);

        Assert.That(data.Get<int>(componentId), Is.EqualTo(10));
    }

    [Test]
    public void AddOrSetAddsOrReplacesTheValue()
    {
        var data = new EntityData();
        ComponentId componentId = new(1);

        data.AddOrSet(componentId, 10);
        data.AddOrSet(componentId, 20);

        Assert.Multiple(() =>
        {
            Assert.That(data.Count, Is.EqualTo(1));
            Assert.That(data.Get<int>(componentId), Is.EqualTo(20));
        });
    }

    [Test]
    public void SetOnlyReplacesAnExistingValue()
    {
        var data = new EntityData();
        ComponentId componentId = new(1);

        Assert.That(data.Set(componentId, 20), Is.False);
        Assert.That(data.Has(componentId), Is.False);

        data.Add(componentId, 10);

        Assert.That(data.Set(componentId, 20), Is.True);
        Assert.That(data.Get<int>(componentId), Is.EqualTo(20));
    }

    [Test]
    public void DefersTypeResolutionForPresenceOnlyEntries()
    {
        var data = new EntityData();
        ComponentId componentId = new(1);
        data.Add(componentId, 10);

        Assert.Throws<System.InvalidOperationException>(() => data.Add(componentId, "wrong type"));
        Assert.That(data.Add(componentId), Is.False);

        var untypedData = new EntityData();
        Assert.That(untypedData.Add(componentId), Is.True);
        Assert.That(untypedData.Add(componentId, 10), Is.False);
        untypedData.AddOrSet(componentId, 10);
        Assert.That(untypedData.Get<int>(componentId), Is.EqualTo(10));
    }

    [Test]
    public void TagsHavePresenceButNoValue()
    {
        var data = new EntityData();
        ComponentId tagId = new(1);

        Assert.That(data.Add(tagId, default(Alive)), Is.True);
        Assert.That(data.Add(tagId, default(Alive)), Is.False);
        Assert.That(data.Has(tagId), Is.True);
        Assert.That(data.TryGet<Alive>(tagId, out _), Is.False);
        Assert.That(data.TryGet<int>(tagId, out _), Is.False);
        Assert.Throws<System.InvalidOperationException>(() => data.GetRef<int>(tagId));
    }

    [Test]
    public void RemoveAndClearReleaseStoredEntries()
    {
        var data = new EntityData();
        ComponentId componentId = new(1);
        ComponentId tagId = new(2);
        data.Add(componentId, 10);
        data.Add(tagId);

        Assert.That(data.Remove(componentId), Is.True);
        Assert.That(data.Remove(componentId), Is.False);
        Assert.That(data.Has(tagId), Is.True);

        data.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(data.Count, Is.Zero);
            Assert.That(data.Has(tagId), Is.False);
        });
    }

    [Test]
    public void WorldResolvesUntypedIdsAndInitializesDefaultValues()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(91_001));
        ComponentId aliveId = layouts.Register<Alive>(new SchemaId(91_002));
        using var world = new World(layouts);
        var data = new EntityData();
        data.Add(positionId, new Position { Value = 17 });
        data.Add(aliveId, default(Alive));
        ComponentId defaultValueId = layouts.Register<int>(new SchemaId(91_003));
        data.Add(defaultValueId);

        Entity entity = world.Create(data);

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(entity, positionId).Value, Is.EqualTo(17));
            Assert.That(world.Has(entity, aliveId), Is.True);
            Assert.That(world.Get<int>(entity, defaultValueId), Is.Zero);
        });
    }

    [Test]
    public void WorldAddInitializesOnlyNewRows()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(91_011));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(91_012));
        ComponentId aliveId = layouts.Register<Alive>(new SchemaId(91_017));
        ComponentId defaultValueId = layouts.Register<int>(new SchemaId(91_019));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        world.GetRef<Position>(entity, positionId).Value = 5;
        var data = new EntityData();
        data.Add(positionId, new Position { Value = 99 });
        data.Add(velocityId, new Velocity { Value = 7 });
        data.Add(aliveId);
        data.Add(defaultValueId);

        Assert.That(world.Add(entity, data), Is.EqualTo(3));

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(entity, positionId).Value, Is.EqualTo(5));
            Assert.That(world.Get<Velocity>(entity, velocityId).Value, Is.EqualTo(7));
            Assert.That(world.Has(entity, aliveId), Is.True);
            Assert.That(world.Get<int>(entity, defaultValueId), Is.Zero);
        });

        world.GetRef<Velocity>(entity, velocityId).Value = 11;
        Assert.That(world.Add(entity, data), Is.Zero);
        Assert.That(world.Get<Velocity>(entity, velocityId).Value, Is.EqualTo(11));
    }

    [Test]
    public void WorldAddOrSetInitializesNewRowsAndReplacesExistingValues()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(91_013));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(91_014));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        world.GetRef<Position>(entity, positionId).Value = 5;
        var data = new EntityData();
        data.Add(positionId, new Position { Value = 9 });
        data.Add(velocityId, new Velocity { Value = 12 });

        world.AddOrSet(entity, data);

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(entity, positionId).Value, Is.EqualTo(9));
            Assert.That(world.Get<Velocity>(entity, velocityId).Value, Is.EqualTo(12));
        });
    }

    [Test]
    public void WorldSetOnlyReplacesWhenEveryStoredComponentIsPresent()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(91_015));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(91_016));
        ComponentId aliveId = layouts.Register<Alive>(new SchemaId(91_018));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        world.Add(entity, aliveId);
        world.GetRef<Position>(entity, positionId).Value = 5;
        var data = new EntityData();
        data.Add(positionId, new Position { Value = 9 });
        data.Add(velocityId, new Velocity { Value = 12 });
        data.Add(aliveId);

        Assert.That(world.Set(entity, data), Is.Zero);
        Assert.That(world.Get<Position>(entity, positionId).Value, Is.EqualTo(5));

        world.Add(entity, velocityId);

        Assert.That(world.Set(entity, data), Is.EqualTo(data.Count));
        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(entity, positionId).Value, Is.EqualTo(9));
            Assert.That(world.Get<Velocity>(entity, velocityId).Value, Is.EqualTo(12));
        });

        ref Velocity velocity = ref world.GetRef<Velocity>(entity, velocityId);
        velocity.Value = 19;
        var presenceOnly = new EntityData();
        presenceOnly.Add(velocityId);
        Assert.That(world.Set(entity, presenceOnly), Is.EqualTo(1));
        world.AddOrSet(entity, presenceOnly);
        Assert.That(world.Get<Velocity>(entity, velocityId).Value, Is.EqualTo(19));
    }

    [Test]
    public void WorldCachesEntityDataRoutesPerSourceArchetypeAndRefreshesAfterShapeChanges()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(91_031));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(91_032));
        ComponentId aliveId = layouts.Register<Alive>(new SchemaId(91_033));
        using var world = new World(layouts);
        var data = new EntityData();
        data.Add(positionId, new Position { Value = 7 });
        data.Add(velocityId, new Velocity { Value = 11 });

        Entity positionEntity = world.Create(positionId);
        Entity velocityEntity = world.Create(velocityId);
        Assert.That(world.Add(positionEntity, data), Is.EqualTo(1));
        Assert.That(world.Add(velocityEntity, data), Is.EqualTo(1));

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(positionEntity, positionId).Value, Is.Zero);
            Assert.That(world.Get<Velocity>(positionEntity, velocityId).Value, Is.EqualTo(11));
            Assert.That(world.Get<Position>(velocityEntity, positionId).Value, Is.EqualTo(7));
            Assert.That(world.Get<Velocity>(velocityEntity, velocityId).Value, Is.Zero);
        });

        data.Set(positionId, new Position { Value = 17 });
        Entity createdEntity = world.Create(data);
        Assert.That(world.Get<Position>(createdEntity, positionId).Value, Is.EqualTo(17));

        data.Add(aliveId);
        Entity taggedEntity = world.Create(data);
        Assert.That(world.Has(taggedEntity, aliveId), Is.True);

        data.Remove(positionId);
        Entity reducedEntity = world.Create(data);
        Assert.Multiple(() =>
        {
            Assert.That(world.Has(reducedEntity, positionId), Is.False);
            Assert.That(world.Get<Velocity>(reducedEntity, velocityId).Value, Is.EqualTo(11));
            Assert.That(world.Has(reducedEntity, aliveId), Is.True);
        });

        data.Clear();
        data.Add(velocityId, new Velocity { Value = 23 });
        Entity clearedEntity = world.Create(data);
        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Velocity>(clearedEntity, velocityId).Value, Is.EqualTo(23));
            Assert.That(world.Has(clearedEntity, positionId), Is.False);
            Assert.That(world.Has(clearedEntity, aliveId), Is.False);
        });

        var otherLayouts = new ComponentRegistry();
        ComponentId conflictingId = otherLayouts.Register<Velocity>(new SchemaId(91_034));
        Assert.That(conflictingId, Is.EqualTo(positionId));
        using var otherWorld = new World(otherLayouts);
        var positionData = new EntityData();
        positionData.Add(positionId, new Position { Value = 1 });
        Assert.Throws<System.ArgumentException>(() => otherWorld.Create(positionData));
    }

    [Test]
    public void WorldRejectsDataWithAValueTypeThatDoesNotMatchTheRegistration()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(91_021));
        using var world = new World(layouts);
        var data = new EntityData();
        data.Add(positionId, new Velocity { Value = 7 });

        Assert.Throws<System.ArgumentException>(() => world.Create(data));
        Assert.That(world.AliveEntityCount, Is.Zero);
    }

    [Test]
    public void WorldRejectsAnUntypedIdThatIsNotRegistered()
    {
        using var world = new World();
        var data = new EntityData();
        data.Add(new ComponentId(91_099));

        Assert.Throws<System.ArgumentOutOfRangeException>(() => world.Create(data));
        Assert.That(world.AliveEntityCount, Is.Zero);
    }

    [TestCase("Add", 2, 5, 4)]
    [TestCase("Set", 1, 10, 2)]
    [TestCase("AddOrSet", 0, 10, 4)]
    public void CopiesEntityValuesAndTagsWithTheSelectedMergeMode(string mode, int expectedCount, int expectedValue, int expectedSize)
    {
        var layouts = new ComponentRegistry();
        ComponentId first = layouts.Register<int>(new SchemaId(92_001));
        ComponentId second = layouts.Register<int>(new SchemaId(92_002));
        ComponentId tag = layouts.Register<Alive>(new SchemaId(92_003));
        ComponentId extra = layouts.Register<int>(new SchemaId(92_004));
        using var world = new World(layouts);
        Entity entity = world.Create<int>(first, 10);
        world.Add(entity, second, 20);
        world.Add(entity, tag);
        var data = new EntityData();
        data.Add(first, 5);
        data.Add(extra, 30);

        int count = 0;
        switch (mode)
        {
            case "Add":
                count = data.Add(world, entity);
                break;
            case "Set":
                count = data.Set(world, entity);
                break;
            default:
                data.AddOrSet(world, entity);
                break;
        }

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(expectedCount));
            Assert.That(data.Count, Is.EqualTo(expectedSize));
            Assert.That(data.Get<int>(first), Is.EqualTo(expectedValue));
            Assert.That(data.Get<int>(extra), Is.EqualTo(30));
            Assert.That(data.Has(tag), Is.EqualTo(mode != "Set"));
            Assert.That(data.Has(second), Is.EqualTo(mode != "Set"));
        });
    }

    [Test]
    public void CopiesPresentTagsAndReferenceValuesAndCanReplaceAfterClear()
    {
        var layouts = new ComponentRegistry();
        ComponentId valueId = layouts.Register<object>(new SchemaId(92_011));
        ComponentId tagId = layouts.Register<Alive>(new SchemaId(92_012));
        using var world = new World(layouts);
        object value = new();
        Entity entity = world.Create(valueId, value);
        world.Add(entity, tagId);
        var data = new EntityData();
        data.Add(tagId);
        Assert.That(data.Set(world, entity), Is.EqualTo(1));
        Assert.That(data.Add(world, entity), Is.EqualTo(1));
        Assert.That(data.Add(world, entity), Is.Zero);
        Assert.That(data.Set(world, entity), Is.EqualTo(2));
        Assert.That(data.Get<object>(valueId), Is.SameAs(value));

        world.Remove(entity, tagId);
        data.Clear();
        data.AddOrSet(world, entity);
        Assert.That(data.Count, Is.EqualTo(1));
        Assert.That(data.Has(tagId), Is.False);

        world.Destroy(entity);
        Assert.That(data.Add(world, entity), Is.Zero);
        Assert.That(data.Set(world, entity), Is.Zero);
        data.AddOrSet(world, entity);
        Assert.That(data.Count, Is.EqualTo(1));
    }

    private struct Alive { }

    private struct Position
    {
        public int Value;
    }

    private struct Velocity
    {
        public int Value;
    }
}
