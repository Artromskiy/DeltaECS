namespace Delta.ECS.Tests;

using System;

using NUnit.Framework;

[TestFixture]
internal sealed class EntityDataCopyTests
{
    [Test]
    public void SnapshotMergesRowsAndOnlyTheSourcesTagsInRegistrationOrder()
    {
        using var world = new World();
        ComponentId firstTag = world.Components.Register<Marker>(new SchemaId(93_001));
        ComponentId first = world.Components.Register<int>(new SchemaId(93_002));
        ComponentId middleTag = world.Components.Register<Marker>(new SchemaId(93_003));
        ComponentId second = world.Components.Register<int>(new SchemaId(93_004));
        ComponentId lastTag = world.Components.Register<Marker>(new SchemaId(93_005));
        ComponentId neighborTag = world.Components.Register<Marker>(new SchemaId(93_006));
        Entity source = CreateInt(world, first, 11);
        world.AddOrSet(source, second, 22);
        Entity neighbor = world.Create([first, second]);
        world.Add(neighbor, neighborTag);
        world.Add(source, [lastTag, middleTag, firstTag]);
        var data = new EntityData();
        data.AddOrSet(world, source);
        Assert.That(data.ComponentIds.ToArray(), Is.EqualTo(new[] { firstTag, first, middleTag, second, lastTag }));
        Assert.That(data.Get<int>(first), Is.EqualTo(11));
        Assert.That(data.Get<int>(second), Is.EqualTo(22));
        Assert.That(data.Has(neighborTag), Is.False);

        world.Remove(source, [firstTag, middleTag]);
        data.Clear();
        data.AddOrSet(world, source);
        Assert.That(data.ComponentIds.ToArray(), Is.EqualTo(new[] { first, second, lastTag }));
    }

    [Test]
    public void SetRebuildsAfterLateTagRegistrationEvenWhenArchetypeDoesNotChange()
    {
        using var world = new World();
        ComponentId value = world.Components.Register<int>(new SchemaId(93_010));
        Entity source = CreateInt(world, value, 7);
        var data = new EntityData();
        ComponentId futureTag = new(value.Value + 1);
        data.Add(futureTag);
        Assert.That(data.Set(world, source), Is.Zero);
        Assert.That(world.Components.Register<Marker>(new SchemaId(93_011)), Is.EqualTo(futureTag));
        world.Add(source, futureTag);
        Assert.That(data.Set(world, source), Is.EqualTo(1));
        world.Remove(source, futureTag);
        Assert.That(data.Set(world, source), Is.Zero);
        Assert.That(data.Has(futureTag), Is.True, "Set preserves destination entries absent from the source.");
    }

    [Test]
    public void SetRefreshesRoutesAfterDestinationAndSourceShapeChanges()
    {
        using var world = new World();
        ComponentId extra = world.Components.Register<int>(new SchemaId(93_020));
        ComponentId value = world.Components.Register<int>(new SchemaId(93_021));
        ComponentId other = world.Components.Register<int>(new SchemaId(93_022));
        Entity source = CreateInt(world, value, 17);
        var data = new EntityData();
        data.Add(value, 0);
        Assert.That(data.Set(world, source), Is.EqualTo(1));
        world.Add(source, extra);
        world.GetRef<int>(source, value) = 23;
        Assert.That(data.Set(world, source), Is.EqualTo(1));
        Assert.That(data.Get<int>(value), Is.EqualTo(23));
        data.Remove(value);
        data.Add(other, 3);
        Assert.That(data.Set(world, source), Is.Zero);
        world.AddOrSet(source, other, 29);
        Assert.That(data.Set(world, source), Is.EqualTo(1));
        Assert.That(data.Get<int>(other), Is.EqualTo(29));
        data.Clear();
        data.Add(value);
        Query query = world.WhereAll([value, extra]);
        world.Remove(in query, extra);
        Assert.That(data.Set(world, source), Is.EqualTo(1));
        Assert.That(data.Get<int>(value), Is.EqualTo(23));
    }

    [Test]
    public void ReaderCachesAreWorldScopedAndCopyingDoesNotReportWrites()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<int>(new SchemaId(93_030));
        Entity source = CreateInt(world, id, 41);
        int notifications = 0;
        var changes = world.ForEachChange<int>(id, (Entity _, ChangeKind kind, ref int value) => notifications++);
        changes.Invoke();
        notifications = 0;
        var data = new EntityData();
        data.AddOrSet(world, source);
        Assert.That(data.Set(world, source), Is.EqualTo(1));
        changes.Invoke();
        Assert.That(notifications, Is.Zero);

        using var otherWorld = new World();
        ComponentId otherId = otherWorld.Components.Register<string>(new SchemaId(93_031));
        Entity otherSource = otherWorld.Create(otherId, "other");
        Assert.That(otherId, Is.EqualTo(id));
        Assert.Throws<InvalidOperationException>(() => data.Set(otherWorld, otherSource));
        data.Clear();
        data.AddOrSet(otherWorld, otherSource);
        Assert.That(data.Get<string>(otherId), Is.EqualTo("other"));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void WarmCopiesReuseEntriesWithoutAllocating(int mode)
    {
        using var world = new World();
        ComponentId value = world.Components.Register<int>(new SchemaId(93_040));
        ComponentId tag = world.Components.Register<Marker>(new SchemaId(93_041));
        Entity source = CreateInt(world, value, 5);
        world.Add(source, tag);
        var data = new EntityData();
        data.AddOrSet(world, source);
        for (int index = 0; index < 64; index++)
        {
            Copy(mode, data, world, source);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            Copy(mode, data, world, source);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(allocated, Is.Zero);
        Assert.That(data.Get<int>(value), Is.EqualTo(5));
        Assert.That(data.Has(tag), Is.True);
    }

    private static Entity CreateInt(World world, ComponentId id, int value)
    {
        Entity entity = world.Create(id);
        world.Set(entity, id, value);
        return entity;
    }

    private static void Copy(int mode, EntityData data, World world, Entity source)
    {
        switch (mode)
        {
            case 0: data.Add(world, source); break;
            case 1: data.Set(world, source); break;
            default: data.AddOrSet(world, source); break;
        }
    }

    private struct Marker { }
}
