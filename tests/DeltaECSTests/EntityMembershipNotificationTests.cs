namespace Delta.ECS.Tests;

using System;
using System.Collections.Generic;

using NUnit.Framework;

[TestFixture]
internal sealed class EntityMembershipNotificationTests
{
    [TestCase(false, 0)]
    [TestCase(false, 1)]
    [TestCase(false, 2)]
    [TestCase(false, 3)]
    [TestCase(true, 0)]
    [TestCase(true, 1)]
    [TestCase(true, 2)]
    [TestCase(true, 3)]
    public void AllCreationAndDestructionRoutesReportFullEntityIdentities(bool omitOutput, int destruction)
    {
        using var world = new World();
        ComponentId valueId = world.Components.Register<MembershipValue>(new SchemaId(75_001));
        ComponentId extraId = world.Components.Register<MembershipValue>(new SchemaId(75_002));
        var changed = new List<Entity>();
        world.EntityMembershipChanged += entities =>
        {
            foreach (Entity entity in entities)
            {
                changed.Add(entity);
            }
        };

        const int count = 2 * Chunk.Capacity + 7;
        if (omitOutput)
        {
            world.Create(valueId, count);
        }
        else
        {
            world.Create([valueId], new Entity[count]);
        }
        Assert.That(changed.Count, Is.EqualTo(count));
        Entity[] created = changed.ToArray();
        Assert.That(new HashSet<Entity>(created).Count, Is.EqualTo(count));
        changed.Clear();
        world.Add(created, extraId);
        world.Remove(created, extraId);
        Assert.That(changed, Is.Empty, "Archetype moves do not change entity membership.");

        Query query = world.WhereAll(valueId);
        switch (destruction)
        {
            case 0:
                foreach (Entity entity in created)
                {
                    world.Destroy(entity);
                }
                break;
            case 1:
                Array.Reverse(created);
                world.Destroy(created);
                break;
            case 2:
                world.Destroy(in query);
                break;
            case 3:
                world.Where(in query, static (in MembershipValue _) => true).Destroy().Invoke();
                break;
        }
        Assert.That(changed, Is.EquivalentTo(created));
        Assert.That(world.AliveEntityCount, Is.Zero);
    }

    [Test]
    public void FilteredDestroyReportsOnlyRemovedEntities()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<MembershipValue>(new SchemaId(75_004));
        var entities = new Entity[64];
        world.Create([id], entities);
        var expected = new List<Entity>();
        for (int index = 0; index < entities.Length; index++)
        {
            world.GetRef<MembershipValue>(entities[index], id).Value = index;
            if ((index & 1) == 0)
            {
                expected.Add(entities[index]);
            }
        }
        var changed = new List<Entity>();
        world.EntityMembershipChanged += removed =>
        {
            foreach (Entity entity in removed)
            {
                changed.Add(entity);
            }
        };
        Query query = world.WhereAll(id);
        world.Where(in query, static (in MembershipValue value) => (value.Value & 1) == 0).Destroy().Invoke();
        Assert.That(changed, Is.EquivalentTo(expected));
        foreach (Entity entity in changed)
        {
            Assert.That(world.IsAlive(entity), Is.False);
        }
        Assert.That(world.AliveEntityCount, Is.EqualTo(entities.Length - expected.Count));
    }

    [Test]
    public void ReusedIndexKeepsBothLifetimesInNotifications()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<MembershipValue>(new SchemaId(75_003));
        Entity oldEntity = world.Create(id);
        var changed = new List<Entity>();
        world.EntityMembershipChanged += entities =>
        {
            foreach (Entity entity in entities)
            {
                changed.Add(entity);
            }
        };
        world.Destroy(oldEntity);
        Entity replacement = world.Create(id);
        Assert.That(replacement.Index, Is.EqualTo(oldEntity.Index));
        Assert.That(replacement.Generation, Is.Not.EqualTo(oldEntity.Generation));
        Assert.That(changed, Is.EqualTo(new[] { oldEntity, replacement }));
    }

    public struct MembershipValue
    {
        public int Value;
    }
}
