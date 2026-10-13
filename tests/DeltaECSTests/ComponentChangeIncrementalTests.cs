namespace Delta.ECS.Tests;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using NUnit.Framework;

[TestFixture]
internal sealed class ComponentChangeIncrementalTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void PromotedMasksAreClearedIndependentlyAfterRetry(bool tag)
    {
        using var world = new World();
        ComponentId id = tag
            ? world.Components.Register<ComponentChangeChunkTag>(new SchemaId(73_052))
            : world.Components.Register<ComponentChangeChunkValue>(new SchemaId(73_052));
        ComponentId extra = world.Components.Register<ComponentChangeChunkExtra>(new SchemaId(73_053));
        var entities = new Entity[257];
        world.Create([id, extra], entities);
        var firstChanges = new List<Entity>();
        var secondChanges = new List<Entity>();
        bool fail = false;
        void RecordFirst(Entity entity, ChangeKind kind)
        {
            if (fail)
            {
                throw new InvalidOperationException("Promoted mask callback failure");
            }

            Assert.That(kind, Is.EqualTo(ChangeKind.Modified));
            firstChanges.Add(entity);
        }

        IOperation first = tag
            ? world.ForEachChange<ComponentChangeChunkTag>(id, (Entity entity, ChangeKind kind, ref ComponentChangeChunkTag _) =>
            {
                if (kind != ChangeKind.Added) { RecordFirst(entity, kind); }
            })
            : world.ForEachChange<ComponentChangeChunkValue>(id, (Entity entity, ChangeKind kind, ref ComponentChangeChunkValue _) =>
            {
                if (kind != ChangeKind.Added) { RecordFirst(entity, kind); }
            });
        IOperation second = tag
            ? world.ForEachChange<ComponentChangeChunkTag>(id, (Entity entity, ChangeKind kind, ref ComponentChangeChunkTag _) => secondChanges.Add(entity))
            : world.ForEachChange<ComponentChangeChunkValue>(id, (Entity entity, ChangeKind kind, ref ComponentChangeChunkValue _) => secondChanges.Add(entity));
        first.Invoke();
        second.Invoke();
        secondChanges.Clear();
        Query query = world.WhereAll(id);
        IOperation write = tag
            ? world.ForEach(in query, static (ref ComponentChangeChunkTag _) => { })
            : world.ForEach(in query, static (ref ComponentChangeChunkValue _) => { });

        // Promote a populated mask to All, then retry delivery after an exception.
        if (tag) { _ = world.GetRef<ComponentChangeChunkTag>(entities[129], id); }
        else { _ = world.GetRef<ComponentChangeChunkValue>(entities[129], id); }
        write.Invoke();
        fail = true;
        Assert.Throws<InvalidOperationException>(first.Invoke);
        second.Invoke();
        Assert.That(secondChanges, Is.EqualTo(entities));
        secondChanges.Clear();
        fail = false;
        first.Invoke();
        Assert.That(firstChanges, Is.EqualTo(entities));
        firstChanges.Clear();

        // Both populated-mask and untouched-mask buffers must start clean.
        for (int pass = 0; pass < 2; pass++)
        {
            int slot = pass == 0 ? 255 : 64;
            if (tag) { _ = world.GetRef<ComponentChangeChunkTag>(entities[slot], id); }
            else { _ = world.GetRef<ComponentChangeChunkValue>(entities[slot], id); }
            first.Invoke();
            second.Invoke();
            Assert.That(firstChanges, Is.EqualTo(new[] { entities[slot] }));
            Assert.That(secondChanges, Is.EqualTo(firstChanges));
            firstChanges.Clear();
            secondChanges.Clear();
            first.Invoke();
            second.Invoke();
            Assert.That(firstChanges, Is.Empty);
            Assert.That(secondChanges, Is.Empty);
            if (pass == 0)
            {
                write.Invoke();
                first.Invoke();
                second.Invoke();
                Assert.That(firstChanges, Is.EqualTo(entities));
                Assert.That(secondChanges, Is.EqualTo(entities));
                firstChanges.Clear();
                secondChanges.Clear();
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FullAndSparseWordsPreserveMembershipAndTail(bool tag)
    {
        using var world = new World();
        ComponentId id = tag
            ? world.Components.Register<ComponentChangeChunkTag>(new SchemaId(73_050))
            : world.Components.Register<ComponentChangeChunkValue>(new SchemaId(73_050));
        ComponentId extra = world.Components.Register<ComponentChangeChunkExtra>(new SchemaId(73_051));
        var entities = new Entity[193];
        world.Create([id, extra], entities.Length, entities);
        if (tag)
        {
            world.Remove(entities[65], id);
        }

        var changes = new List<(Entity, ChangeKind)>();
        IOperation operation = tag
            ? world.ForEachChange<ComponentChangeChunkTag>(id, (Entity entity, ChangeKind kind, ref ComponentChangeChunkTag _) => changes.Add((entity, kind)))
            : world.ForEachChange<ComponentChangeChunkValue>(id, (Entity entity, ChangeKind kind, ref ComponentChangeChunkValue _) => changes.Add((entity, kind)));
        operation.Invoke();
        Assert.That(changes.Count, Is.EqualTo(tag ? 192 : 193));
        changes.Clear();

        for (int index = 0; index < entities.Length; index++)
        {
            if (index == 65 || index >= 128 && index < 192)
            {
                continue;
            }
            if (tag)
            {
                _ = world.GetRef<ComponentChangeChunkTag>(entities[index], id);
            }
            else
            {
                _ = world.GetRef<ComponentChangeChunkValue>(entities[index], id);
            }
        }
        operation.Invoke();
        Assert.That(changes.Count, Is.EqualTo(128));
        int changeIndex = 0;
        for (int index = 0; index < entities.Length; index++)
        {
            if (index != 65 && (index < 128 || index == 192))
            {
                Assert.That(changes[changeIndex++], Is.EqualTo((entities[index], ChangeKind.Modified)));
            }
        }
        changes.Clear();
        operation.Invoke();
        Assert.That(changes, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MembershipTransitionsFoldAgainstSuccessfulSnapshot(bool tag)
    {
        using var world = new World();
        ComponentId id = tag
            ? world.Components.Register<ComponentChangeChunkTag>(new SchemaId(73_001))
            : world.Components.Register<ComponentChangeChunkValue>(new SchemaId(73_001));
        ComponentId extra = world.Components.Register<ComponentChangeChunkExtra>(new SchemaId(73_002));
        Entity existing = world.Create([id, extra]);
        Entity transient = world.Create(extra);
        var changes = new List<(Entity, ChangeKind)>();
        IOperation operation = tag
            ? world.ForEachChange<ComponentChangeChunkTag>(id, (Entity entity, ChangeKind kind, ref ComponentChangeChunkTag _) => changes.Add((entity, kind)))
            : world.ForEachChange<ComponentChangeChunkValue>(id, (Entity entity, ChangeKind kind, ref ComponentChangeChunkValue _) => changes.Add((entity, kind)));
        operation.Invoke();
        changes.Clear();

        world.Remove(existing, id);
        world.Add(existing, id);
        world.Add(transient, id);
        world.Remove(transient, id);
        operation.Invoke();

        Assert.That(changes, Is.EqualTo(new[] { (existing, ChangeKind.Modified) }));
        changes.Clear();
        operation.Invoke();
        Assert.That(changes, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReusedEntityIndexPreservesOldRemovalAndNewAddition(bool tag)
    {
        using var world = new World();
        ComponentId id = tag
            ? world.Components.Register<ComponentChangeChunkTag>(new SchemaId(73_003))
            : world.Components.Register<ComponentChangeChunkValue>(new SchemaId(73_003));
        var changes = new List<(Entity, ChangeKind)>();
        IOperation operation = tag
            ? world.ForEachChange<ComponentChangeChunkTag>(id, (Entity entity, ChangeKind kind, ref ComponentChangeChunkTag _) => changes.Add((entity, kind)))
            : world.ForEachChange<ComponentChangeChunkValue>(id, (Entity entity, ChangeKind kind, ref ComponentChangeChunkValue _) => changes.Add((entity, kind)));
        Entity old = world.Create(id);
        operation.Invoke();
        changes.Clear();

        world.Destroy(old);
        Entity replacement = world.Create(id);
        Assert.That(replacement.Index, Is.EqualTo(old.Index));
        operation.Invoke();
        Assert.That(changes, Is.EquivalentTo(new[] { (old, ChangeKind.Removed), (replacement, ChangeKind.Added) }));
        changes.Clear();
        world.Destroy(replacement);
        Entity transient = world.Create(id);
        world.Destroy(transient);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (replacement, ChangeKind.Removed) }));
    }

    [Test]
    public void StructuralDeltasAndDirtyMasksSurviveCallbackFailure()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<ComponentChangeChunkValue>(new SchemaId(73_004));
        Entity existing = world.Create(id);
        bool fail = false;
        var changes = new List<(Entity, ChangeKind)>();
        ComponentChangeOperation<ComponentChangeChunkValue> operation = world.ForEachChange<ComponentChangeChunkValue>(id,
            (Entity entity, ChangeKind kind, ref ComponentChangeChunkValue component) =>
            {
                if (kind == ChangeKind.Removed)
                {
                    Assert.That(Unsafe.IsNullRef(ref component), Is.True);
                }

                changes.Add((entity, kind));
                if (fail)
                {
                    throw new InvalidOperationException("Callback failure");
                }
            });
        operation.Invoke();
        changes.Clear();
        Entity added = world.Create(id);
        world.GetRef<ComponentChangeChunkValue>(existing, id).Value++;
        fail = true;
        Assert.Throws<InvalidOperationException>(operation.Invoke);
        fail = false;
        changes.Clear();
        operation.Invoke();
        Assert.That(changes, Is.EquivalentTo(new[] { (added, ChangeKind.Added), (existing, ChangeKind.Modified) }));
        changes.Clear();
        world.Destroy(existing);
        fail = true;
        Assert.Throws<InvalidOperationException>(operation.Invoke);
        changes.Clear();
        fail = false;
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (existing, ChangeKind.Removed) }));
    }

    [Test]
    public void BulkMembershipChangesAndDestroyReportOnlyAffectedRegistration()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<ComponentChangeChunkValue>(new SchemaId(73_005));
        ComponentId extra = world.Components.Register<ComponentChangeChunkExtra>(new SchemaId(73_006));
        var entities = new Entity[2_103];
        world.Create([extra], entities);
        var changes = new List<(Entity, ChangeKind)>();
        var operation = world.ForEachChange<ComponentChangeChunkValue>(id,
            (Entity entity, ChangeKind kind, ref ComponentChangeChunkValue _) => changes.Add((entity, kind)));
        operation.Invoke();
        Query query = world.WhereAll(extra);
        world.Add(in query, id);
        operation.Invoke();
        Assert.That(changes.Count, Is.EqualTo(entities.Length));
        Assert.That(changes, Is.All.Matches<(Entity Entity, ChangeKind Kind)>(change => change.Kind == ChangeKind.Added));
        changes.Clear();
        world.Remove(in query, extra);
        operation.Invoke();
        Assert.That(changes, Is.Empty);
        Query values = world.WhereAll(id);
        world.Destroy(in values);
        operation.Invoke();
        Assert.That(changes.Count, Is.EqualTo(entities.Length));
        Assert.That(changes, Is.All.Matches<(Entity Entity, ChangeKind Kind)>(change => change.Kind == ChangeKind.Removed));
    }

    [Test]
    public void DirtyRangeTransferCrossesWordsAndChunksWithoutLosingIdentity()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<ComponentChangeChunkValue>(new SchemaId(73_007));
        ComponentId extra = world.Components.Register<ComponentChangeChunkExtra>(new SchemaId(73_008));
        var entities = new Entity[2_103];
        world.Create([id], entities);
        var changes = new List<(Entity, ChangeKind)>();
        var operation = world.ForEachChange<ComponentChangeChunkValue>(id,
            (Entity entity, ChangeKind kind, ref ComponentChangeChunkValue _) => changes.Add((entity, kind)));
        operation.Invoke();
        changes.Clear();
        var expected = new List<(Entity, ChangeKind)>();
        for (int index = 1; index < entities.Length; index += 3)
        {
            world.GetRef<ComponentChangeChunkValue>(entities[index], id).Value++;
            expected.Add((entities[index], ChangeKind.Modified));
        }

        // Fill a partial destination before bulk transfer; it requires a split range.
        world.Add(entities[0], extra);
        Query query = world.WhereAll(id).WhereNone(extra);
        world.Add(in query, extra);
        operation.Invoke();
        Assert.That(changes, Is.EquivalentTo(expected));
    }

    [Test]
    public void GeneratedDestroyCompactionPreservesDirtyMasksAndRemovedEntities()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<ComponentChangeChunkValue>(new SchemaId(73_010));
        var entities = new Entity[2_103];
        world.Create([id], entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.GetRef<ComponentChangeChunkValue>(entities[index], id).Value = index;
        }

        var changes = new List<(Entity, ChangeKind)>();
        var operation = world.ForEachChange<ComponentChangeChunkValue>(id,
            (Entity entity, ChangeKind kind, ref ComponentChangeChunkValue _) => changes.Add((entity, kind)));
        operation.Invoke();
        changes.Clear();
        var expected = new List<(Entity, ChangeKind)>();
        for (int index = 0; index < entities.Length; index++)
        {
            if (index % 3 == 0)
            {
                _ = world.GetRef<ComponentChangeChunkValue>(entities[index], id);
            }

            if (index % 5 == 0)
            {
                expected.Add((entities[index], ChangeKind.Removed));
            }
            else if (index % 3 == 0)
            {
                expected.Add((entities[index], ChangeKind.Modified));
            }
        }

        Query query = world.WhereAll(id);
        world.Where(in query, static (in ComponentChangeChunkValue value) => value.Value % 5 == 0).Destroy().Invoke();
        operation.Invoke();
        Assert.That(changes, Is.EquivalentTo(expected));
    }

    [Test]
    public void TagDirtyMasksIntersectMembershipAfterGeneratedRemoval()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<ComponentChangeChunkValue>(new SchemaId(73_011));
        ComponentId tagId = world.Components.Register<ComponentChangeChunkTag>(new SchemaId(73_012));
        var entities = new Entity[1_103];
        world.Create([id], entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.GetRef<ComponentChangeChunkValue>(entities[index], id).Value = index;
            if (index % 2 == 0)
            {
                world.Add(entities[index], tagId);
            }
        }

        var changes = new List<(Entity, ChangeKind)>();
        var operation = world.ForEachChange<ComponentChangeChunkTag>(tagId,
            (Entity entity, ChangeKind kind, ref ComponentChangeChunkTag _) => changes.Add((entity, kind)));
        operation.Invoke();
        changes.Clear();
        Query tagged = world.WhereAll(tagId);
        world.ForEach(in tagged, static (ref ComponentChangeChunkTag _) => { }).Invoke();
        Query query = world.WhereAll(id);
        world.Where(in query, static (in ComponentChangeChunkValue value) => value.Value % 3 == 0).Remove(tagId).Invoke();
        operation.Invoke();
        var expected = new List<(Entity, ChangeKind)>();
        for (int index = 0; index < entities.Length; index += 2)
        {
            expected.Add((entities[index], index % 3 == 0 ? ChangeKind.Removed : ChangeKind.Modified));
        }

        Assert.That(changes, Is.EquivalentTo(expected));
    }

    [Test]
    public void InitialSnapshotIsCommittedOnlyAfterSuccessfulInvocation()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<ComponentChangeChunkValue>(new SchemaId(73_009));
        Entity first = world.Create(id);
        bool fail = true;
        var changes = new List<(Entity, ChangeKind)>();
        var operation = world.ForEachChange<ComponentChangeChunkValue>(id,
            (Entity entity, ChangeKind kind, ref ComponentChangeChunkValue _) =>
            {
                if (fail)
                {
                    throw new InvalidOperationException("Initial snapshot failure");
                }

                changes.Add((entity, kind));
            });
        Assert.Throws<InvalidOperationException>(operation.Invoke);
        world.Destroy(first);
        Entity replacement = world.Create(id);
        fail = false;
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (replacement, ChangeKind.Added) }));
    }
}
