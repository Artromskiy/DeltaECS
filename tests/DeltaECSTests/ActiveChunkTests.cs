using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed class ActiveChunkTests
{
    private static readonly ComponentId PositionId = new(0);

    [Test]
    public void EmptyChunksAreExcludedAndReusedChunksRejoinTheActiveList()
    {
        var layouts = new ComponentRegistry();
        layouts.Register<Position>(new SchemaId(1));
        using var world = new World(layouts);
        const int chunkSize = Chunk.Capacity;
        var entities = new Entity[chunkSize * 3];
        world.Create([PositionId], entities);

        var archetype = world.Archetypes[0];
        Assert.That(archetype.ChunkCount, Is.EqualTo(3));
        Assert.That(archetype.ActiveChunkCount, Is.EqualTo(3));
        AssertActiveChunks(archetype);

        Assert.That(world.Destroy(entities.AsSpan(0, chunkSize)), Is.EqualTo(chunkSize));
        Assert.That(archetype.ChunkCount, Is.EqualTo(3));
        Assert.That(archetype.ActiveChunkCount, Is.EqualTo(2));
        AssertActiveChunks(archetype);

        var query = QuerySpec.WhereAll(PositionId);
        var queryHandle = world.CreateQuery(in query);
        int queriedSlots = CountQueriedSlots(world, queryHandle);
        Assert.That(queriedSlots, Is.EqualTo(chunkSize * 2));

        var replacement = new Entity[chunkSize];
        Assert.That(world.Create([PositionId], replacement), Is.EqualTo(chunkSize));
        world.GetRef<Position>(replacement[0], PositionId) = new Position { X = 11 };
        world.GetRef<Position>(replacement[1], PositionId) = new Position { X = 13 };
        Assert.That(archetype.ActiveChunkCount, Is.EqualTo(3));
        AssertActiveChunks(archetype);

        queriedSlots = CountQueriedSlots(world, queryHandle);
        Assert.That(queriedSlots, Is.EqualTo(chunkSize * 3));
        Assert.That(SumPositions(world, queryHandle), Is.EqualTo(24));
    }

    [Test]
    public void BulkDestroyedChunkReuseDoesNotResolveStaleEntityRecords()
    {
        var layouts = new ComponentRegistry();
        layouts.Register<Position>(new SchemaId(2));
        using var world = new World(layouts);
        var stale = new Entity[Chunk.Capacity];
        world.Create([PositionId], stale);

        Assert.That(world.Destroy(stale), Is.EqualTo(stale.Length));
        var replacements = new Entity[stale.Length];
        world.Create([PositionId], replacements);
        world.GetRef<Position>(replacements[0], PositionId) = new Position { X = 29 };

        Assert.That(world.IsAlive(stale[0]), Is.False);
        Assert.That(world.TryGet<Position>(stale[0], PositionId, out _), Is.False);
        Assert.That(world.IsAlive(replacements[0]), Is.True);
        Assert.That(world.GetRef<Position>(replacements[0], PositionId).X, Is.EqualTo(29));
    }

    [Test]
    public void EntityRecordsFollowChunksAcrossAddAndRemoveTransitions()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<int>(new SchemaId(3));
        ComponentId velocityId = layouts.Register<float>(new SchemaId(4));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        world.GetRef<int>(entity, positionId) = 17;

        Assert.That(world.Add(entity, [velocityId]), Is.True);
        world.GetRef<float>(entity, velocityId) = 2.5f;
        Assert.That(world.GetRef<int>(entity, positionId), Is.EqualTo(17));
        Assert.That(world.GetRef<float>(entity, velocityId), Is.EqualTo(2.5f));

        Assert.That(world.Remove(entity, [velocityId]), Is.True);
        Assert.That(world.IsAlive(entity), Is.True);
        Assert.That(world.GetRef<int>(entity, positionId), Is.EqualTo(17));
        Assert.That(world.Has(entity, velocityId), Is.False);
    }

    [Test]
    public void DisposedWorldDoesNotResolveRecordsPointingToReleasedChunks()
    {
        var layouts = new ComponentRegistry();
        ComponentId id = layouts.Register<int>(new SchemaId(5));
        var world = new World(layouts);
        Entity entity = world.Create(id);
        world.Dispose();

        Assert.That(world.IsAlive(entity), Is.False);
        Assert.That(world.Has(entity, id), Is.False);
        Assert.That(world.TryGet(entity, id, out int _), Is.False);
    }

    private static int CountQueriedSlots(World world, in Query query)
    {
        int count = 0;
        foreach (ref readonly ChunkPlan chunk in query.Cached.MatchingChunkPlans())
        {
            count += chunk.Chunk.Count;
        }

        return count;
    }

    private static float SumPositions(World world, in Query query)
    {
        float sum = 0;
        world.ForEach(in query, ref sum, static (ref float total, in Position position) => total += position.X).Invoke(ref sum);

        return sum;
    }

    private static void AssertActiveChunks(Archetype archetype)
    {
        for (int activeIndex = 0; activeIndex < archetype.ActiveChunkCount; activeIndex++)
        {
            var chunk = archetype.GetActiveChunk(activeIndex);
            Assert.That(chunk.IsEmpty, Is.False);
        }
    }
}
