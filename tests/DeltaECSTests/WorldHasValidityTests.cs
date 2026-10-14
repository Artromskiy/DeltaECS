using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed class WorldHasValidityTests
{
    [Test]
    public void HasPreservesComponentTagAndEntityGenerationChecks()
    {
        var layouts = new ComponentRegistry();
        ComponentId value = layouts.Register<HasValidityValue>(new SchemaId(51_001));
        ComponentId extra = layouts.Register<HasValidityExtra>(new SchemaId(51_002));
        ComponentId marker = layouts.Register<HasValidityTag>(new SchemaId(51_003));
        using var world = new World(layouts);
        Entity[] entities = new Entity[2];
        world.Create(value, entities.Length, entities);

        Entity alive = entities[0];
        Entity stale = entities[1];
        world.GetRef<HasValidityValue>(alive).Value = 1;
        Assert.That(world.Has(alive, value), Is.True);
        Assert.That(world.Has<HasValidityValue>(alive), Is.True);
        Assert.That(world.Has(alive, extra), Is.False);
        Assert.That(world.Has<HasValidityExtra>(alive), Is.False);
        Assert.That(world.Has(alive, marker), Is.False);
        Assert.That(world.Has(default, value), Is.False);
        Assert.That(world.Has(new Entity(-1, 1), value), Is.False);
        Assert.That(world.Has(new Entity(int.MaxValue, 1), value), Is.False);
        Assert.That(world.Has(new Entity(alive.Index, alive.Generation + 1), value), Is.False);
        Assert.That(world.Has(alive, new ComponentId(-1)), Is.False);

        Assert.That(world.Add(alive, [extra, marker]), Is.True);
        world.GetRef<HasValidityExtra>(alive).Value = 2;
        Assert.That(world.Has(alive, extra), Is.True);
        Assert.That(world.Has<HasValidityExtra>(alive), Is.True);
        Assert.That(world.Has(alive, marker), Is.True);
        Assert.That(world.Remove(alive, [extra, marker]), Is.True);
        Assert.That(world.Has(alive, extra), Is.False);
        Assert.That(world.Has(alive, marker), Is.False);

        Assert.That(world.Destroy(stale), Is.True);
        Assert.That(world.Has(stale, value), Is.False);
        Entity replacement = world.Create(value);
        Assert.That(replacement.Index, Is.EqualTo(stale.Index));
        Assert.That(world.Has(stale, value), Is.False);
        Assert.That(world.Has(replacement, value), Is.True);
    }

    private struct HasValidityValue
    {
        public int Value;
    }

    private struct HasValidityExtra
    {
        public int Value;
    }

    private struct HasValidityTag
    {
    }
}
