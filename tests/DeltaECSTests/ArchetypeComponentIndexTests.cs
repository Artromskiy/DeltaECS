using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed class ArchetypeComponentIndexTests
{
    [Test]
    public void EmptyArchetypeRejectsMissingAndInvalidComponentIds()
    {
        Archetype archetype = CreateArchetype(default);
        try
        {
            foreach (ComponentId componentId in new[] { ComponentId.Invalid, new ComponentId(0), new ComponentId(int.MaxValue) })
            {
                Assert.That(archetype.Contains(componentId), Is.False);
                Assert.That(archetype.TryGetComponentIndex(componentId, out int index), Is.False);
                Assert.That(index, Is.EqualTo(-1));
            }
        }
        finally
        {
            archetype.Dispose();
        }
    }

    [Test]
    public void SparseHighComponentIdsResolveTheirRowsAcrossWordBoundaries()
    {
        ComponentMask mask = ComponentMask.From(
        [
            new ComponentId(0), new ComponentId(31), new ComponentId(32), new ComponentId(63),
            new ComponentId(64), new ComponentId(255), new ComponentId(256), new ComponentId(511),
            new ComponentId(1024), new ComponentId(65_536)
        ]);
        Archetype archetype = CreateArchetype(mask);
        try
        {
            for (int value = -1; value <= 1025; value++)
            {
                AssertLookup(archetype, mask, new ComponentId(value));
            }

            AssertLookup(archetype, mask, new ComponentId(65_535));
            AssertLookup(archetype, mask, new ComponentId(65_536));
            AssertLookup(archetype, mask, new ComponentId(65_537));
            AssertLookup(archetype, mask, new ComponentId(int.MaxValue));
        }
        finally
        {
            archetype.Dispose();
        }
    }

    [Test]
    public void DisposingArchetypePreservesSharedImmutableMaskForRecreation()
    {
        ComponentMask mask = ComponentMask.From([new ComponentId(2), new ComponentId(300)]);
        Archetype original = CreateArchetype(mask);
        original.Dispose();

        Assert.That(mask.Count, Is.EqualTo(2));
        Archetype recreated = CreateArchetype(mask);
        try
        {
            AssertLookup(recreated, mask, new ComponentId(2));
            AssertLookup(recreated, mask, new ComponentId(300));
            AssertLookup(recreated, mask, new ComponentId(299));
        }
        finally
        {
            recreated.Dispose();
        }
    }

    [Test]
    public void LateRegistrationAndTagChangesPreserveRowsThroughArchetypeTransitions()
    {
        var layouts = new ComponentRegistry();
        ComponentId firstId = layouts.Register<int>(new SchemaId(91_001));
        using var world = new World(layouts);
        Entity entity = world.Create(firstId);
        world.GetRef<int>(entity, firstId) = 42;
        Archetype original = world.Archetypes[0];

        ComponentId tagId = layouts.Register<IndexTag>(new SchemaId(91_002));
        ComponentId lateId = ComponentId.Invalid;
        for (int index = 0; index < 300; index++)
        {
            lateId = layouts.Register<int>(new SchemaId((ulong)(92_000 + index)));
        }

        Assert.That(original.TryGetComponentIndex(lateId, out int absentIndex), Is.False);
        Assert.That(absentIndex, Is.EqualTo(-1));
        Assert.That(world.TryGet(entity, lateId, out int _), Is.False);

        Assert.That(world.Add(entity, lateId), Is.True);
        world.GetRef<int>(entity, lateId) = 123;
        Assert.That(world.Add(entity, tagId), Is.True);
        Assert.That(world.Has<IndexTag>(entity), Is.True);
        Assert.That(world.Get<int>(entity, firstId), Is.EqualTo(42));
        Assert.That(world.Get<int>(entity, lateId), Is.EqualTo(123));
        Assert.That(original.TryGetComponentIndex(firstId, out int originalIndex), Is.True);
        Assert.That(originalIndex, Is.Zero);
        Assert.That(original.Contains(lateId), Is.False);
        Assert.That(original.Contains(tagId), Is.False);

        Assert.That(world.Remove(entity, lateId), Is.True);
        Assert.That(world.Has<IndexTag>(entity), Is.True);
        Assert.That(world.Get<int>(entity, firstId), Is.EqualTo(42));
        Assert.That(world.TryGet(entity, lateId, out int _), Is.False);
        Assert.That(world.Remove(entity, tagId), Is.True);
        Assert.That(world.Has<IndexTag>(entity), Is.False);
        Assert.That(world.Get<int>(entity, firstId), Is.EqualTo(42));
    }

    private static Archetype CreateArchetype(ComponentMask mask)
    {
        var componentIds = new ComponentId[mask.Count];
        mask.CopyComponentIds(componentIds);
        return new Archetype(
            0,
            mask,
            new ComponentLayout[mask.Count],
            new ComponentRowOperations[mask.Count],
            componentIds,
            new ComponentRowArrayPool());
    }

    private static void AssertLookup(Archetype archetype, ComponentMask mask, ComponentId componentId)
    {
        int expectedIndex = mask.Rank(componentId);
        Assert.That(archetype.Contains(componentId), Is.EqualTo(expectedIndex >= 0), $"component={componentId}");
        Assert.That(archetype.TryGetComponentIndex(componentId, out int index), Is.EqualTo(expectedIndex >= 0), $"component={componentId}");
        Assert.That(index, Is.EqualTo(expectedIndex), $"component={componentId}");
    }

    internal readonly struct IndexTag;
}
