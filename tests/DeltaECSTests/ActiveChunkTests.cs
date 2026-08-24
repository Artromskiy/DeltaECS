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

        var query = QuerySpec.ForComponents(PositionId);
        var queryHandle = world.CreateQuery(in query);
        var queriedSlots = CountQueriedSlots(world, queryHandle);
        Assert.That(queriedSlots, Is.EqualTo(4));

        var replacement = new Entity[2];
        Assert.That(world.CreateBatch(handle, replacement), Is.EqualTo(2));
        Assert.That(world.SetComponent(replacement[0], PositionId, new Position { X = 11 }), Is.True);
        Assert.That(world.SetComponent(replacement[1], PositionId, new Position { X = 13 }), Is.True);
        Assert.That(archetype.ActiveChunkCount, Is.EqualTo(3));
        AssertActiveChunks(archetype);

        queriedSlots = CountQueriedSlots(world, queryHandle);
        Assert.That(queriedSlots, Is.EqualTo(6));
        Assert.That(SumPositions(world, queryHandle), Is.EqualTo(24));
    }

    private static int CountQueriedSlots(World world, in Query query)
    {
        var count = 0;
        using var scope = world.OpenQuery(in query);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                while (slots.MoveNext())
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static float SumPositions(World world, in Query query)
    {
        float sum = 0;
        var access = query.AccessRead(PositionId);
        using var scope = world.OpenQuery(in query);
        var prepared = scope.Bind(access);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                var positions = slots.Get(prepared);
                while (slots.MoveNext())
                {
                    sum += positions.Ref<Position>(slots).X;
                }
            }
        }

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
