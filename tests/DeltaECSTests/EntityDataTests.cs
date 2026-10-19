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
    public void RejectsReusingAnIdWithAnotherEntryKindOrType()
    {
        var data = new EntityData();
        ComponentId componentId = new(1);
        data.Add(componentId, 10);

        Assert.Throws<System.InvalidOperationException>(() => data.Add(componentId, "wrong type"));
        Assert.Throws<System.InvalidOperationException>(() => data.AddTag(componentId));

        var tagData = new EntityData();
        tagData.AddTag(componentId);
        Assert.Throws<System.InvalidOperationException>(() => tagData.Add(componentId, 10));
    }

    [Test]
    public void TagsHavePresenceButNoValue()
    {
        var data = new EntityData();
        ComponentId tagId = new(1);

        Assert.That(data.AddTag(tagId), Is.True);
        Assert.That(data.AddTag(tagId), Is.False);
        Assert.That(data.Has(tagId), Is.True);
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
        data.AddTag(tagId);

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
    public void WorldCreateInitializesStoredValuesAndTags()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(91_001));
        ComponentId aliveId = layouts.Register<Alive>(new SchemaId(91_002));
        using var world = new World(layouts);
        var data = new EntityData();
        data.Add(positionId, new Position { Value = 17 });
        data.AddTag(aliveId);

        Entity entity = world.Create(data);

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(entity, positionId).Value, Is.EqualTo(17));
            Assert.That(world.Has(entity, aliveId), Is.True);
        });
    }

    [Test]
    public void WorldAddInitializesOnlyNewRows()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(91_011));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(91_012));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        world.GetRef<Position>(entity, positionId).Value = 5;
        var data = new EntityData();
        data.Add(positionId, new Position { Value = 99 });
        data.Add(velocityId, new Velocity { Value = 7 });

        Assert.That(world.Add(entity, data), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(entity, positionId).Value, Is.EqualTo(5));
            Assert.That(world.Get<Velocity>(entity, velocityId).Value, Is.EqualTo(7));
        });

        world.GetRef<Velocity>(entity, velocityId).Value = 11;
        Assert.That(world.Add(entity, data), Is.False);
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
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        world.GetRef<Position>(entity, positionId).Value = 5;
        var data = new EntityData();
        data.Add(positionId, new Position { Value = 9 });
        data.Add(velocityId, new Velocity { Value = 12 });

        Assert.That(world.Set(entity, data), Is.False);
        Assert.That(world.Get<Position>(entity, positionId).Value, Is.EqualTo(5));

        data.Remove(velocityId);

        Assert.That(world.Set(entity, data), Is.True);
        Assert.That(world.Get<Position>(entity, positionId).Value, Is.EqualTo(9));
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
