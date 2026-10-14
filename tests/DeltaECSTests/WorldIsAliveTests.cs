using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed class WorldIsAliveTests
{
    [Test]
    public void IsAliveRejectsEmptyWorldForgedAndOutOfRangeHandles()
    {
        using var world = new World();

        Assert.That(world.IsAlive(default), Is.False);
        Assert.That(world.IsAlive(new Entity(int.MaxValue, 1)), Is.False);
        Assert.That(world.IsAlive(new Entity(-1, 1)), Is.False);
    }

    [Test]
    public void IsAliveTracksDestroyReuseAndDisposedWorld()
    {
        var layouts = new ComponentRegistry();
        ComponentId valueId = layouts.Register<AliveValue>(new SchemaId(30_101));
        var world = new World(layouts);
        Entity[] entities = new Entity[4];
        world.Create([valueId], entities);

        Entity stale = entities[1];
        Assert.That(world.IsAlive(stale), Is.True);
        Assert.That(world.IsAlive(new Entity(stale.Index, stale.Generation + 1)), Is.False);
        Assert.That(world.Destroy(stale), Is.True);
        Assert.That(world.IsAlive(stale), Is.False);

        Entity replacement = world.Create(valueId);
        Assert.That(replacement.Index, Is.EqualTo(stale.Index));
        Assert.That(replacement.Generation, Is.Not.EqualTo(stale.Generation));
        Assert.That(world.IsAlive(stale), Is.False);
        Assert.That(world.IsAlive(replacement), Is.True);

        world.Dispose();
        Assert.That(world.IsAlive(replacement), Is.False);
    }

    private struct AliveValue
    {
    }
}
