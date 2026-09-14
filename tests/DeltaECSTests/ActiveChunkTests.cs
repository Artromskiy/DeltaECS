using Delta.ECS;
using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
public sealed class ActiveChunkTests
{
    private static readonly ComponentId PositionId = new(0);

    [Test]
    public void EmptyChunks_Are_Excluded_And_Reused_Chunks_Rejoin_The_Active_List()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.Register(typeof(Position), new SchemaId(1));
        var world = new World(layouts);
        const int chunkSize = 512;
        var entities = new Entity[chunkSize * 3];
        world.Create(stackalloc[] { PositionId }, entities);

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
        var queriedSlots = CountQueriedSlots(world, queryHandle);
        Assert.That(queriedSlots, Is.EqualTo(chunkSize * 2));

        var replacement = new Entity[chunkSize];
        Assert.That(world.Create(stackalloc[] { PositionId }, replacement), Is.EqualTo(chunkSize));
        Assert.That(world.Set(replacement[0], PositionId, new Position { X = 11 }), Is.True);
        Assert.That(world.Set(replacement[1], PositionId, new Position { X = 13 }), Is.True);
        Assert.That(archetype.ActiveChunkCount, Is.EqualTo(3));
        AssertActiveChunks(archetype);

        queriedSlots = CountQueriedSlots(world, queryHandle);
        Assert.That(queriedSlots, Is.EqualTo(chunkSize * 3));
        Assert.That(SumPositions(world, queryHandle), Is.EqualTo(24));
    }

    private static int CountQueriedSlots(World world, in Query query)
    {
        var count = 0;
        foreach (ref readonly ChunkPlan chunk in query.Cached.MatchingChunkPlans())
        {
            count += chunk.Chunk.Count;
        }

        return count;
    }

    private static float SumPositions(World world, in Query query)
    {
        float sum = 0;
        world.ForEach(in query, ref sum, static (ref float total, in Position position) => total += position.X);

        return sum;
    }

    private static void AssertActiveChunks(Archetype archetype)
    {
        for (var activeIndex = 0; activeIndex < archetype.ActiveChunkCount; activeIndex++)
        {
            var chunk = archetype.GetActiveChunk(activeIndex);
            Assert.That(chunk.IsEmpty, Is.False);
        }
    }
}
