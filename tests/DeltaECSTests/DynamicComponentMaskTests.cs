using System;
using NUnit.Framework;
using Delta.ECS;

namespace Delta.ECS.Tests;

[TestFixture]
public sealed class DynamicComponentMaskTests
{
    [Test]
    public void MaskSupportsComponentIdsAboveTheLegacyCapacity()
    {
        var mask = ComponentMask.From(new[]
        {
            new ComponentId(0),
            new ComponentId(255),
            new ComponentId(256),
            new ComponentId(511),
            new ComponentId(1024)
        });

        Assert.That(mask.Count, Is.EqualTo(5));
        Assert.That(mask.Contains(new ComponentId(256)), Is.True);
        Assert.That(mask.Contains(new ComponentId(1023)), Is.False);
        Assert.That(mask.Rank(new ComponentId(256)), Is.EqualTo(2));
        Assert.That(mask.Rank(new ComponentId(1024)), Is.EqualTo(4));

        var ids = new int[5];
        int index = 0;
        foreach (var componentId in mask)
        {
            ids[index++] = componentId.Value;
        }

        Assert.That(ids, Is.EqualTo(new[] { 0, 255, 256, 511, 1024 }));
    }

    [Test]
    public void MaskSetOperationsRemainContentBasedForDynamicWords()
    {
        var left = ComponentMask.From(new[] { new ComponentId(3), new ComponentId(256) });
        var right = ComponentMask.From(new[] { new ComponentId(256), new ComponentId(1024) });

        var union = left.Or(right);
        var difference = union.Except(right);

        Assert.That(union.ContainsAll(left), Is.True);
        Assert.That(union.ContainsAll(right), Is.True);
        Assert.That(left.Intersects(right), Is.True);
        Assert.That(difference, Is.EqualTo(left.Except(right)));
        Assert.That(difference.Contains(new ComponentId(3)), Is.True);
        Assert.That(difference.Contains(new ComponentId(256)), Is.False);
    }

    [Test]
    public void SetPreservesExistingHigherWords()
    {
        var mask = ComponentMask.From(new[] { new ComponentId(256) })
            .Set(new ComponentId(3));

        Assert.That(mask.Count, Is.EqualTo(2));
        Assert.That(mask.Contains(new ComponentId(3)), Is.True);
        Assert.That(mask.Contains(new ComponentId(256)), Is.True);
    }

    [Test]
    public void WorldCreatesArchetypeWithMoreThanLegacyMaskCapacity()
    {
        var layouts = new ComponentLayoutRegistry();
        var ids = new ComponentId[ComponentMask.Capacity + 1];
        for (int index = 0; index < ids.Length; index++)
        {
            ids[index] = layouts.Register(typeof(int), new SchemaId((ulong)(80_000 + index)));
        }

        using var world = new World(layouts, initialEntityCapacity: 1, chunkCapacity: 1);
        Entity entity = world.Create(ids);

        Assert.That(world.IsAlive(entity), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, ids[^1], out _), Is.True);
    }
}
