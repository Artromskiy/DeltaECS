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
        layouts.Register<Position>(new SchemaId(1));
        var world = new World(layouts, chunkCapacity: 2);
        var handle = world.GetArchetype(PositionId);
        var entities = new Entity[6];
        world.CreateBatch(handle, entities);

        var archetype = world.Archetypes[handle.ArchetypeId];
        Assert.That(archetype.ChunkCount, Is.EqualTo(3));
        Assert.That(archetype.ActiveChunkCount, Is.EqualTo(3));
        AssertActiveChunks(archetype);

        Assert.That(world.Destroy(entities[0]), Is.True);
        Assert.That(world.Destroy(entities[1]), Is.True);
        Assert.That(archetype.ChunkCount, Is.EqualTo(3));
        Assert.That(archetype.ActiveChunkCount, Is.EqualTo(2));
        AssertActiveChunks(archetype);

        var query = QueryDescription.ForComponents(PositionId);
        var queriedSlots = 0;
        world.Query(in query, scope => queriedSlots += scope.SlotCount);
        Assert.That(queriedSlots, Is.EqualTo(4));

        var replacement = new Entity[2];
        Assert.That(world.CreateBatch(handle, replacement), Is.EqualTo(2));
        Assert.That(archetype.ActiveChunkCount, Is.EqualTo(3));
        AssertActiveChunks(archetype);

        queriedSlots = 0;
        world.Query(in query, scope => queriedSlots += scope.SlotCount);
        Assert.That(queriedSlots, Is.EqualTo(6));
    }

    private static void AssertActiveChunks(Archetype archetype)
    {
        for (var activeIndex = 0; activeIndex < archetype.ActiveChunkCount; activeIndex++)
        {
            var chunk = archetype.GetChunk(archetype.GetActiveChunkIndex(activeIndex));
            Assert.That(chunk.IsEmpty, Is.False);
        }
    }
}
