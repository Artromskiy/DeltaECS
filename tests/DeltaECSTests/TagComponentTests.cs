namespace Delta.ECS.Tests;

using System;
using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
internal sealed class TagComponentTests
{
    [Test]
    public void TagsAreStoredWithoutRowsAndFilterGeneratedQueries()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(98_001));
        ComponentId markedId = layouts.Register<MarkedTag>(new SchemaId(98_002));
        ComponentId otherId = layouts.Register<OtherTag>(new SchemaId(98_003));
        ComponentId blockedId = layouts.Register<BlockedTag>(new SchemaId(98_004));
        ComponentId extraId = layouts.Register<ExtraData>(new SchemaId(98_005));
        using var world = new World(layouts);

        var entities = new Entity[Chunk.Capacity + 3];
        world.Create(new[] { positionId }, entities);
        Query query = world
            .WhereAll<Position>()
            .WhereAny<MarkedTag, OtherTag, ExtraData>()
            .WhereNone<BlockedTag>();

        for (int index = 0; index < entities.Length; index++)
        {
            if (index % 3 == 0)
            {
                world.Add(entities[index], new[] { markedId });
            }

            if (index % 5 == 0)
            {
                world.Add(entities[index], new[] { otherId });
            }

            if (index % 7 == 0)
            {
                world.Add(entities[index], new[] { blockedId });
            }
        }

        world.Add(entities[1], new[] { extraId });

        Assert.That(world.Has<MarkedTag>(entities[3]), Is.True);
        Assert.That(world.Has<MarkedTag>(entities[4]), Is.False);
        Assert.That(world.Has<BlockedTag>(entities[7]), Is.True);

        int expected = CountMatches(entities.Length, static index =>
            (index % 3 == 0 || index % 5 == 0 || index == 1) && index % 7 != 0);
        Assert.That(Count(world, in query), Is.EqualTo(expected));

        int explicitIdCount = 0;
        world.ForEach(
            in query,
            positionId,
            ref explicitIdCount,
            static (ref int count, in Position _) => count++);
        Assert.That(explicitIdCount, Is.EqualTo(expected));

        Query markedOnly = world.WhereAll<MarkedTag>();
        int markedOnlyCount = 0;
        ForEachContextEntityAction<int> countTaggedEntity = static (ref int count, Entity _) => count++;
        world.ForEachEntity(in markedOnly, ref markedOnlyCount, countTaggedEntity);
        Assert.That(markedOnlyCount, Is.EqualTo(CountMatches(entities.Length, static index => index % 3 == 0)));

        world.Add(entities[3], new[] { extraId });
        Assert.That(world.Has<MarkedTag>(entities[3]), Is.True);
        Assert.That(Count(world, in query), Is.EqualTo(expected));
        world.Remove(entities[3], new[] { extraId });
        Assert.That(world.Has<MarkedTag>(entities[3]), Is.True);

        var functor = new CountPositionFunctor();
        world.ForEach(in query, ref functor);
        Assert.That(functor.Count, Is.EqualTo(expected));

        world.Remove(entities[3], new[] { markedId });
        world.Add(entities[4], new[] { markedId });
        expected = CountMatches(entities.Length, static index =>
            ((index % 3 == 0 && index != 3) || index % 5 == 0 || index == 4 || index == 1)
            && index % 7 != 0);
        Assert.That(Count(world, in query), Is.EqualTo(expected));

        Query marked = world.WhereAll<Position>().WhereAll<MarkedTag>();
        int markedCount = CountMatches(entities.Length, static index => index % 3 == 0 && index != 3 || index == 4);
        Assert.That(world.Destroy(in marked), Is.EqualTo(markedCount));
        Assert.That(world.Has<MarkedTag>(entities[3]), Is.False);
        Assert.That(world.IsAlive(entities[3]), Is.True);
        Assert.That(world.IsAlive(entities[4]), Is.False);
        Assert.That(world.IsAlive(entities[5]), Is.True);
    }

    [Test]
    public void AddingAndRemovingTagsThroughQueriesOnlyChangesMatchingEntities()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(98_011));
        ComponentId markedId = layouts.Register<MarkedTag>(new SchemaId(98_012));
        ComponentId blockedId = layouts.Register<BlockedTag>(new SchemaId(98_013));
        _ = layouts.Register<OtherTag>(new SchemaId(98_014));
        using var world = new World(layouts);
        var entities = new Entity[8];
        world.Create(new[] { positionId }, entities);
        for (int index = 0; index < entities.Length; index += 2)
        {
            world.Add(entities[index], new[] { markedId });
        }

        Query marked = world.WhereAll<Position>().WhereAll<MarkedTag>();
        Assert.That(world.Add(in marked, new[] { blockedId }), Is.EqualTo(4));
        Assert.That(world.Remove(in marked, new[] { markedId }), Is.EqualTo(4));
        foreach (Entity entity in entities)
        {
            Assert.That(world.Has<BlockedTag>(entity), Is.EqualTo((entity.Index & 1) == 0));
            Assert.That(world.Has<MarkedTag>(entity), Is.False);
        }

        world.Add<MarkedTag>(entities[1]);
        Assert.That(world.Has<MarkedTag>(entities[1]), Is.True);
        Assert.That(world.Remove<MarkedTag>(entities[1]), Is.True);

        Entity tagOnlyEntity = world.Create<OtherTag>();
        Assert.That(world.Has<OtherTag>(tagOnlyEntity), Is.True);
        Assert.That(world.Has<Position>(tagOnlyEntity), Is.False);
        Assert.That(world.Remove<OtherTag>(tagOnlyEntity), Is.True);
        Assert.That(world.Has<OtherTag>(tagOnlyEntity), Is.False);
    }

    [Test]
    public void ParallelAndEntityListIterationApplyTagFilters()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId valueId = layouts.Register<TagValue>(new SchemaId(98_031));
        ComponentId markedId = layouts.Register<MarkedTag>(new SchemaId(98_032));
        using var world = new World(layouts);
        var entities = new Entity[Chunk.Capacity + 6];
        world.Create(new[] { valueId }, entities);
        for (int index = 0; index < entities.Length; index += 2)
        {
            world.Add(entities[index], new[] { markedId });
        }

        Query query = world.WhereAll<TagValue>().WhereAll<MarkedTag>();
        world.ForEachParallel(in query, static (ref TagValue value) => value.Value++, workerCount: 2);
        world.ForEach(entities.AsSpan(), in query, valueId, static (ref TagValue value) => value.Value += 10);
        world.ForEachParallel(
            entities.AsSpan(),
            in query,
            valueId,
            static (ref TagValue value) => value.Value += 100,
            workerCount: 2);

        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.GetRef<TagValue>(entities[index], valueId).Value, Is.EqualTo(index % 2 == 0 ? 111 : 0));
        }
    }

    [Test]
    public void TaggedIterationUsesDenseChunksAndFiltersPartialChunks()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId valueId = layouts.Register<TagValue>(new SchemaId(98_033));
        ComponentId markedId = layouts.Register<MarkedTag>(new SchemaId(98_034));
        using var world = new World(layouts);
        var entities = new Entity[Chunk.Capacity + 4];
        world.Create(new[] { valueId }, entities);
        for (int index = 0; index < Chunk.Capacity + 2; index++)
        {
            world.Add(entities[index], new[] { markedId });
        }

        Query query = world.WhereAll<TagValue>().WhereAll<MarkedTag>();
        int visited = 0;
        world.ForEach(
            in query,
            ref visited,
            static (ref int count, in TagValue _) => count++);

        Assert.That(visited, Is.EqualTo(Chunk.Capacity + 2));
    }

    [Test]
    public void WhereCallbacksAndStructuralTerminalsHonorTagFilters()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId valueId = layouts.Register<TagValue>(new SchemaId(98_041));
        ComponentId markedId = layouts.Register<MarkedTag>(new SchemaId(98_042));
        ComponentId blockedId = layouts.Register<BlockedTag>(new SchemaId(98_043));
        ComponentId extraId = layouts.Register<ExtraData>(new SchemaId(98_044));
        using var world = new World(layouts);
        var entities = new Entity[12];
        world.Create(new[] { valueId }, entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.GetRef<TagValue>(entities[index], valueId).Value = index;
            if ((index & 1) == 0)
            {
                world.Add(entities[index], new[] { markedId });
            }
        }

        Query query = world.WhereAll<TagValue>().WhereAll<MarkedTag>();
        world.Where(in query, static (in TagValue value) => (value.Value & 3) == 0)
            .ForEach(static (ref TagValue value) => value.Value++);
        world.Where(in query, static (in TagValue value) => (value.Value & 3) == 1)
            .Add<BlockedTag>();
        world.Where(in query, static (in TagValue value) => value.Value == 1)
            .Add<ExtraData>();

        for (int index = 0; index < entities.Length; index++)
        {
            bool marked = (index & 1) == 0;
            bool changed = marked && (index & 3) == 0;
            bool blocked = marked && (index & 3) == 0;
            int expectedValue = index + (changed ? 1 : 0);
            Assert.That(world.GetRef<TagValue>(entities[index], valueId).Value, Is.EqualTo(expectedValue));
            Assert.That(world.Has<BlockedTag>(entities[index]), Is.EqualTo(blocked), $"index={index}");
            Assert.That(world.Has<ExtraData>(entities[index]), Is.EqualTo(index == 0));
            Assert.That(world.Has<MarkedTag>(entities[index]), Is.EqualTo(marked));
        }
    }

    [Test]
    public void EntityStampIterationMapsTagSelectedEntitiesAndStamps()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId valueId = layouts.Register<TagValue>(new SchemaId(98_061));
        ComponentId markedId = layouts.Register<MarkedTag>(new SchemaId(98_062));
        using var world = new World(layouts);
        var entities = new Entity[8];
        world.Create(new[] { valueId }, entities);
        var expected = new Dictionary<int, Stamp>();
        for (int index = 0; index < entities.Length; index++)
        {
            world.GetRef<TagValue>(entities[index], valueId).Value = index;
            if ((index & 1) == 1)
            {
                world.Add(entities[index], new[] { markedId });
                Assert.That(world.TryGetComponentStamp(entities[index], valueId, out Stamp stamp), Is.True);
                expected.Add(entities[index].Index, stamp);
            }
        }

        Query query = world.WhereAll<TagValue>().WhereAll<MarkedTag>();
        world.ForEachEntityStamp<Dictionary<int, Stamp>, TagValue>(
            in query,
            ref expected,
            static (ref Dictionary<int, Stamp> stamps, Entity entity, in Stamp stamp) =>
            {
                Assert.That(stamps.Remove(entity.Index, out Stamp expectedStamp), Is.True);
                Assert.That(stamp, Is.EqualTo(expectedStamp));
            });

        Assert.That(expected, Is.Empty);
    }

    [Test]
    public void RegistrationInfersTagsFromFieldlessValueTypes()
    {
        var layouts = new ComponentLayoutRegistry();

        ComponentId tagId = layouts.Register<MarkedTag>(new SchemaId(98_021));
        ComponentId dataId = layouts.Register<TagWithData>(new SchemaId(98_022));
        ComponentId emptyClassId = layouts.Register<EmptyClass>(new SchemaId(98_023));
        ComponentId enumId = layouts.Register<TagEnum>(new SchemaId(98_024));
        ComponentId primitiveId = layouts.Register<int>(new SchemaId(98_025));

        Assert.That(layouts.IsTag(tagId), Is.True);
        Assert.That(layouts.IsTag(dataId), Is.False);
        Assert.That(layouts.IsTag(emptyClassId), Is.False);
        Assert.That(layouts.IsTag(enumId), Is.False);
        Assert.That(layouts.IsTag(primitiveId), Is.False);

        using var world = new World(layouts);
        Entity entity = world.Create(primitiveId);
        world.Set(entity, primitiveId, 42);
        Assert.That(world.Get<int>(entity, primitiveId), Is.EqualTo(42));
    }

    [Test]
    public void RepeatedFieldlessTypeRegistrationsRemainTags()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId first = layouts.Register<MarkedTag>(new SchemaId(98_051));
        ComponentId second = layouts.Register<MarkedTag>(new SchemaId(98_052));

        Assert.That(layouts.IsTag(first), Is.True);
        Assert.That(layouts.IsTag(second), Is.True);
    }

    private static int Count(World world, in Query query)
    {
        int count = 0;
        world.ForEach(in query, ref count, static (ref int total, in Position _) => total++);
        return count;
    }

    private static int CountMatches(int count, Func<int, bool> predicate)
    {
        int matches = 0;
        for (int index = 0; index < count; index++)
        {
            if (predicate(index))
            {
                matches++;
            }
        }
        return matches;
    }

    internal readonly struct MarkedTag;
    internal readonly struct OtherTag;
    internal readonly struct BlockedTag;

    private sealed class EmptyClass;

    private enum TagEnum
    {
        None
    }

    private readonly struct TagWithData
    {
        public readonly int Value;

        public TagWithData(int value) => Value = value;
    }

    internal struct CountPositionFunctor : IForEach
    {
        public int Count;

        public void Invoke(ref Position _) => Count++;
    }

    internal struct TagValue
    {
        public int Value;
    }

    internal struct ExtraData
    {
        public int Value;

        public ExtraData() => Value = 0;
    }
}
