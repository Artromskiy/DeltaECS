namespace Delta.ECS.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using NUnit.Framework;

[TestFixture]
internal sealed class GrowingChunkTests
{
    private static readonly int[] CopiedTagIndices = [0, 2];

    [Test]
    public void ScalarCreationGrowsAllRowsTogetherAndPreservesEntitiesValuesAndTags()
    {
        var layouts = new ComponentRegistry();
        ComponentId value = layouts.Register<int>(new SchemaId(1));
        ComponentId label = layouts.Register<string>(new SchemaId(2));
        ComponentId tag = layouts.Register<GrowingChunkTag>(new SchemaId(3));
        using var world = new World(layouts);
        var entities = new Entity[Chunk.MaxCapacity + 17];
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = world.Create([value, label]);
            entities[index] = entity;
            world.Set(entity, value, index);
            world.Set(entity, label, index.ToString(CultureInfo.InvariantCulture));
            if (index % 3 == 0)
            {
                world.Add(entity, tag);
            }
        }

        Archetype archetype = world.Archetypes[0];
        Assert.That(archetype.ActiveChunkCount, Is.EqualTo(2));
        AssertStorage(archetype.GetActiveChunk(0), Chunk.MaxCapacity, Chunk.MaxCapacity);
        AssertStorage(archetype.GetActiveChunk(1), 17, 32);
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.IsAlive(entities[index]), Is.True);
            Assert.That(world.Get<int>(entities[index], value), Is.EqualTo(index));
            Assert.That(world.Get<string>(entities[index], label), Is.EqualTo(index.ToString(CultureInfo.InvariantCulture)));
            Assert.That(world.Has(entities[index], tag), Is.EqualTo(index % 3 == 0));
        }
    }

    [TestCase(17)]
    [TestCase(65)]
    [TestCase(1024)]
    [TestCase(1031)]
    [TestCase(2049)]
    public void BulkCreationAllocatesRoundedCapacityForEachChunk(int count)
    {
        using var world = new World();
        ComponentId value = world.Components.Register<int>(new SchemaId(1));
        var entities = new Entity[count];
        world.Create([value], entities);
        Archetype archetype = world.Archetypes[0];
        Assert.That(archetype.ActiveChunkCount, Is.EqualTo((count + Chunk.MaxCapacity - 1) / Chunk.MaxCapacity));
        for (int index = 0; index < archetype.ActiveChunkCount; index++)
        {
            int expectedCount = Math.Min(count - index * Chunk.MaxCapacity, Chunk.MaxCapacity);
            int capacity = Chunk.InitialCapacity;
            while (capacity < expectedCount)
            {
                capacity *= 2;
            }
            AssertStorage(archetype.GetActiveChunk(index), expectedCount, capacity);
        }
        Assert.That(entities.Distinct().Count(), Is.EqualTo(count));
        Assert.That(entities.All(entity => world.IsAlive(entity) && world.Get<int>(entity, value) == 0), Is.True);
    }

    [Test]
    public void CachedTypedAndEntityAwareOperationsRefreshAfterScalarAndBulkGrowth()
    {
        using var world = new World();
        ComponentId value = world.Components.Register<int>(new SchemaId(1));
        Query query = world.WhereAll(value);
        long sum = 0;
        var typed = world.ForEach(in query, ref sum, static (ref long total, in int item) => total += item);
        var increment = new GrowingChunkIncrementFunctor();
        var writer = world.ForEach(in query, ref increment);
        var visited = new List<Entity>();
        var entityAware = world.ForEachEntity(in query, entity => visited.Add(entity));
        long entitySum = 0;
        var entityAccess = world.ForEachEntity(in query, entity => entitySum += entity.Get<int>(value));
        var entities = new List<Entity>();
        foreach (int target in new[] { 1, 17, 65, 257, 1024, 1025 })
        {
            var appended = new Entity[target - entities.Count];
            if (appended.Length == 1)
            {
                appended[0] = world.Create(value);
            }
            else
            {
                world.Create([value], appended);
            }
            foreach (Entity entity in appended)
            {
                entities.Add(entity);
                world.Set(entity, value, entities.Count);
            }

            sum = 0;
            entitySum = 0;
            visited.Clear();
            typed.Invoke(ref sum);
            entityAware.Invoke();
            entityAccess.Invoke();
            long expected = (long)target * (target + 1) / 2;
            Assert.That(sum, Is.EqualTo(expected));
            Assert.That(entitySum, Is.EqualTo(expected));
            Assert.That(visited, Is.EquivalentTo(entities));
            writer.Invoke(ref increment);
            sum = 0;
            typed.Invoke(ref sum);
            Assert.That(sum, Is.EqualTo(expected + target));
            foreach (Entity entity in entities)
            {
                world.GetRef<int>(entity, value)--;
            }
        }
    }

    [Test]
    public void GrowthPreservesPendingFreeEntityRecords()
    {
        using var world = new World();
        ComponentId value = world.Components.Register<int>(new SchemaId(1));
        var previous = new Entity[10];
        world.Create([value], previous);
        Query query = world.WhereAll(value);
        world.Destroy(in query);
        var current = new Entity[300];
        world.Create([value], current);
        Assert.That(current.Distinct().Count(), Is.EqualTo(current.Length));
        Assert.That(previous.All(entity => !world.IsAlive(entity)), Is.True);
        Assert.That(current.All(world.IsAlive), Is.True);
        Assert.That(current.Select(entity => entity.Index), Does.Contain(previous[0].Index));
        AssertStorage(world.Archetypes[0].GetActiveChunk(0), 300, 512);
    }

    [TestCase(17, 1)]
    [TestCase(10, 257)]
    public void WholeChunkAdoptionHandlesDifferentDonorCapacity(int sourceCount, int donorCount)
    {
        using var world = new World();
        ComponentId value = world.Components.Register<int>(new SchemaId(1));
        ComponentId extra = world.Components.Register<long>(new SchemaId(2));
        var sources = new Entity[sourceCount];
        var donors = new Entity[donorCount];
        world.Create([value], sources);
        world.Create([value, extra], donors);
        foreach (Entity entity in sources)
        {
            world.Set(entity, value, 73);
        }
        foreach (Entity entity in donors)
        {
            world.Destroy(entity);
        }
        Query query = world.WhereAll(value).WhereNone(extra);
        world.Add(in query, extra);
        var appended = new Entity[300];
        world.Create([value, extra], appended);
        Assert.That(sources.All(entity => world.Get<int>(entity, value) == 73 && world.Has(entity, extra)), Is.True);
        Assert.That(appended.All(entity => world.IsAlive(entity) && world.Get<long>(entity, extra) == 0), Is.True);
        Query target = world.WhereAll(value, extra);
        foreach (ref readonly ChunkPlan plan in target.Cached.MatchingChunkPlans())
        {
            AssertStorage(plan.Chunk, plan.Chunk.Count, plan.Chunk.Capacity);
        }
    }

    [Test]
    public void TagMasksPreserveBitsWhenGrowingAndCopyBetweenDifferentStrides()
    {
        var source = new ChunkTagMasks(16);
        source.EnsureTagCapacity(3);
        source.Set(0, 0);
        source.Set(0, 15);
        source.Set(2, 15);
        source.EnsureSlotCapacity(128);
        source.Set(2, 127);
        Assert.That(source.Contains(0, 0) && source.Contains(0, 15) && source.Contains(2, 15), Is.True);
        Assert.That(source.Word(2, 2), Is.Zero);
        var target = new ChunkTagMasks(1024);
        target.EnsureTagCapacity(5);
        target.Set(4, 900);
        target.Set(4, 901);
        source.CopySlotTo(target, 15, 900);
        Assert.That(target.Contains(0, 900) && target.Contains(2, 900), Is.True);
        Assert.That(target.Contains(4, 900), Is.False);
        Assert.That(target.Contains(4, 901), Is.True);
        target.Set(4, 100);
        source.CopyRangeTo(target, 0, 63, 128);
        Assert.That(target.Contains(0, 63) && target.Contains(0, 78) && target.Contains(2, 190), Is.True);
        Assert.That(target.Contains(4, 100), Is.False);
        var narrow = new ChunkTagMasks(16);
        narrow.EnsureTagCapacity(5);
        target.CopyRangeTo(narrow, 63, 0, 16);
        Assert.That(narrow.Contains(0, 0) && narrow.Contains(0, 15) && narrow.Contains(2, 15), Is.True);
        Assert.That(narrow.GetActiveTagIndices().ToArray(), Is.EqualTo(CopiedTagIndices));
    }

    [Test]
    public void ComponentRowPoolSeparatesCapacitiesAndClearsReferences()
    {
        var pool = new ComponentRowArrayPool();
        ComponentRowOperations operations = ComponentRowOperations.ForType<string>();
        var small = (string[])pool.Rent(typeof(string), operations, 16);
        small[0] = "stored";
        pool.Return(small);
        Array large = pool.Rent(typeof(string), operations, 32);
        Assert.That(large.Length, Is.EqualTo(32));
        Assert.That(large, Is.Not.SameAs(small));
        var reused = (string[])pool.Rent(typeof(string), operations, 16);
        Assert.That(reused, Is.SameAs(small));
        Assert.That(reused[0], Is.Null);
    }

    [Test]
    public void OrderedKeyCacheRefreshesAcrossRepeatedGrowth()
    {
        using var world = new World();
        ComponentId value = world.Components.Register<int>(new SchemaId(1));
        Query query = world.WhereAll(value);
        OrderedQuery ordered = query.OrderBy(static (in int left, in int right) => left.CompareTo(right));
        int count = 0;
        foreach (int target in new[] { 1, 17, 65, 257, 1025 })
        {
            Entity minimum = default;
            while (count < target)
            {
                minimum = world.Create<int>(value, -++count);
            }
            Assert.That(ordered.First().Invoke(), Is.EqualTo(minimum));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ChangeObserversRetainPendingWritesAndAdditionsAcrossGrowth(bool isTag)
    {
        using var world = new World();
        ComponentId value = world.Components.Register<int>(new SchemaId(1));
        ComponentId tag = world.Components.Register<GrowingChunkTag>(new SchemaId(2));
        ComponentId observed = isTag ? tag : value;
        var previous = new Entity[10];
        world.Create([value, tag], previous);
        var changes = new List<(Entity Entity, ChangeKind Kind)>();
        IOperation operation = isTag
            ? world.ForEachChange<GrowingChunkTag>(observed, (Entity entity, ChangeKind kind, ref GrowingChunkTag _) => changes.Add((entity, kind)))
            : world.ForEachChange<int>(observed, (Entity entity, ChangeKind kind, ref int _) => changes.Add((entity, kind)));
        operation.Invoke();
        changes.Clear();
        if (isTag)
        {
            _ = world.GetRef<GrowingChunkTag>(previous[0], observed);
        }
        else
        {
            world.GetRef<int>(previous[0], observed) = 77;
        }
        var added = new Entity[200];
        world.Create([value, tag], added);
        world.Destroy(previous[1]);
        operation.Invoke();
        Assert.That(changes.Count(item => item.Kind == ChangeKind.Added), Is.EqualTo(200));
        Assert.That(changes.Where(item => item.Kind == ChangeKind.Modified).Select(item => item.Entity), Is.EqualTo(new[] { previous[0] }));
        Assert.That(changes.Where(item => item.Kind == ChangeKind.Removed).Select(item => item.Entity), Is.EqualTo(new[] { previous[1] }));
        changes.Clear();
        operation.Invoke();
        Assert.That(changes, Is.Empty);
    }

    [Test]
    public void SmallGroupsAllocateSmallChunksAndDoNotShrinkAfterRemoval()
    {
        using var world = new World();
        ComponentId group = world.Components.Register<GrowingChunkGroup>(new SchemaId(1), group: true);
        var entities = new List<Entity>();
        for (int key = 0; key < 100; key++)
        {
            for (int index = 0; index < 10; index++)
            {
                entities.Add(world.Create(group, new GrowingChunkGroup(key)));
            }
        }
        Query query = world.WhereAll(group);
        world.ForEachEntity(in query, static (EntityRef _) => { }).Invoke();
        int slots = 0;
        foreach (ref readonly ChunkPlan plan in query.Cached.MatchingChunkPlans())
        {
            AssertStorage(plan.Chunk, 10, 16);
            slots += plan.Chunk.Capacity;
        }
        Assert.That(slots, Is.EqualTo(1600));
        Entity first = entities[0];
        for (int index = 1; index < 10; index++)
        {
            world.Destroy(entities[index]);
        }
        Query firstGroup = world.WhereAll(new GrowingChunkGroup(0));
        Assert.That(world.IsAlive(first), Is.True);
        AssertStorage(firstGroup.Cached.MatchingChunkPlans()[0].Chunk, 1, 16);
    }

    private static void AssertStorage(Chunk chunk, int count, int capacity)
    {
        Assert.That(chunk.Count, Is.EqualTo(count));
        Assert.That(chunk.Capacity, Is.EqualTo(capacity));
        Assert.That(chunk.RawEntities.Length, Is.EqualTo(capacity));
        Assert.That(chunk.RawComponentRows.All(row => row.Length == capacity), Is.True);
        Assert.That(capacity, Is.LessThanOrEqualTo(Chunk.MaxCapacity));
    }
}

internal readonly record struct GrowingChunkGroup(int Value);
internal struct GrowingChunkTag;

internal struct GrowingChunkIncrementFunctor : IForEach
{
    public void Invoke(ref int value) => value++;
}
