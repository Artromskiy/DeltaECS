namespace Delta.ECS.Tests;

using System;
using System.Collections.Generic;

using NUnit.Framework;

[TestFixture]
internal sealed class StructuralChangeRoutingTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void SlotTransfersPreserveSourceAndTargetDirtyState(bool tag, bool sourceAll)
    {
        using var world = new World();
        ComponentId id = tag
            ? world.Components.Register<ComponentChangeChunkTag>(new SchemaId(74_001))
            : world.Components.Register<ComponentChangeChunkValue>(new SchemaId(74_001));
        ComponentId marker = world.Components.Register<ComponentChangeChunkExtra>(new SchemaId(74_002));
        var source = new Entity[3];
        var target = new Entity[2];
        world.Create([id], source);
        world.Create([id, marker], target);
        var changes = new List<(Entity, ChangeKind)>();
        IOperation observer = tag
            ? world.ForEachChange<ComponentChangeChunkTag>(id, (Entity entity, ChangeKind kind, ref ComponentChangeChunkTag _) => changes.Add((entity, kind)))
            : world.ForEachChange<ComponentChangeChunkValue>(id, (Entity entity, ChangeKind kind, ref ComponentChangeChunkValue _) => changes.Add((entity, kind)));
        observer.Invoke();
        changes.Clear();
        Query sourceQuery = world.WhereAll(id).WhereNone(marker);
        Query targetQuery = world.WhereAll([id, marker]);
        IOperation writeTarget = tag
            ? world.ForEach(in targetQuery, static (ref ComponentChangeChunkTag _) => { })
            : world.ForEach(in targetQuery, static (ref ComponentChangeChunkValue _) => { });
        writeTarget.Invoke();
        if (sourceAll)
        {
            IOperation writeSource = tag
                ? world.ForEach(in sourceQuery, static (ref ComponentChangeChunkTag _) => { })
                : world.ForEach(in sourceQuery, static (ref ComponentChangeChunkValue _) => { });
            writeSource.Invoke();
        }
        else if (tag)
        {
            _ = world.GetRef<ComponentChangeChunkTag>(source[0], id);
            _ = world.GetRef<ComponentChangeChunkTag>(source[2], id);
        }
        else
        {
            _ = world.GetRef<ComponentChangeChunkValue>(source[0], id);
            _ = world.GetRef<ComponentChangeChunkValue>(source[2], id);
        }

        world.Add(source[1], marker);
        world.Add(source[0], marker);
        world.Remove(source[0], marker);
        observer.Invoke();
        var expected = new List<(Entity, ChangeKind)>();
        foreach (Entity entity in source)
        {
            if (sourceAll || entity != source[1])
            {
                expected.Add((entity, ChangeKind.Modified));
            }
        }

        foreach (Entity entity in target)
        {
            expected.Add((entity, ChangeKind.Modified));
        }

        Assert.That(changes, Is.EquivalentTo(expected));
        changes.Clear();
        observer.Invoke();
        Assert.That(changes, Is.Empty);
    }

    [Test]
    public void CompleteEntityBatchPreservesValuesAndLateObserverRoutes()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<ComponentChangeChunkValue>(new SchemaId(74_003));
        ComponentId extra = world.Components.Register<ComponentChangeChunkValue>(new SchemaId(74_004));
        var entities = new Entity[20];
        world.Create([id, extra], entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.GetRef<ComponentChangeChunkValue>(entities[index], id).Value = index;
        }

        // Cache both transitions before an observer exists.
        world.Remove(entities, extra);
        world.Add(entities, extra);
        var changes = new List<(Entity, ChangeKind)>();
        IOperation observer = world.ForEachChange<ComponentChangeChunkValue>(extra,
            (Entity entity, ChangeKind kind, ref ComponentChangeChunkValue _) => changes.Add((entity, kind)));
        observer.Invoke();
        changes.Clear();
        Array.Reverse(entities);
        Assert.That(world.Remove(entities, extra), Is.EqualTo(entities.Length));
        observer.Invoke();
        Assert.That(changes.Count, Is.EqualTo(entities.Length));
        Assert.That(changes, Is.All.Matches<(Entity Entity, ChangeKind Kind)>(change => change.Kind == ChangeKind.Removed));
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.Get<ComponentChangeChunkValue>(entities[index], id).Value, Is.EqualTo(entities.Length - index - 1));
        }

        changes.Clear();
        Assert.That(world.Add(entities, extra), Is.EqualTo(entities.Length));
        observer.Invoke();
        Assert.That(changes.Count, Is.EqualTo(entities.Length));
        Assert.That(changes, Is.All.Matches<(Entity Entity, ChangeKind Kind)>(change => change.Kind == ChangeKind.Added));
    }

    [Test]
    public void StructuralBatchesRejectDuplicatesBeforeChangingEntities()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<ComponentChangeChunkValue>(new SchemaId(74_005));
        ComponentId extra = world.Components.Register<ComponentChangeChunkExtra>(new SchemaId(74_006));
        ComponentId added = world.Components.Register<ComponentChangeChunkExtra>(new SchemaId(74_011));
        var entities = new Entity[3];
        world.Create([id, extra], entities);

        Assert.Throws<ArgumentException>(() => world.Remove([entities[0], entities[0], entities[1]], extra));
        Assert.That(world.Has(entities[0], extra), Is.True);
        Assert.That(world.Has(entities[1], extra), Is.True);

        Assert.Throws<ArgumentException>(() => world.Add([entities[0], entities[0]], added));
        Assert.That(world.Has(entities[0], added), Is.False);

        var value = new ComponentChangeChunkValue { Value = 42 };
        Assert.Throws<ArgumentException>(() => world.Set([entities[0], entities[0]], id, in value));
        Assert.That(world.Get<ComponentChangeChunkValue>(entities[0], id).Value, Is.Zero);

        Assert.Throws<ArgumentException>(() => world.AddOrSet([entities[0], entities[0]], id, in value));
        Assert.That(world.Get<ComponentChangeChunkValue>(entities[0], id).Value, Is.Zero);
    }

    [Test]
    public void StructuralBatchAllowsDifferentGenerationsWithTheSameIndex()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<ComponentChangeChunkValue>(new SchemaId(74_010));
        Entity oldEntity = world.Create([id]);
        Assert.That(world.Destroy(oldEntity), Is.True);
        Entity currentEntity = world.Create([id]);
        Assert.That(currentEntity.Index, Is.EqualTo(oldEntity.Index));
        Assert.That(currentEntity.Generation, Is.Not.EqualTo(oldEntity.Generation));
        Assert.That(world.Destroy(oldEntity), Is.False);

        Assert.That(world.Set([oldEntity, currentEntity], id, new ComponentChangeChunkValue { Value = 7 }), Is.EqualTo(1));
        Assert.That(world.Get<ComponentChangeChunkValue>(currentEntity, id).Value, Is.EqualTo(7));
        Assert.Throws<ArgumentException>(() => world.Destroy([oldEntity, oldEntity]));
        Assert.That(world.IsAlive(currentEntity), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AdoptedChunksRefreshMixedDataAndTagFilters(bool queryBatch)
    {
        using var world = new World();
        ComponentId id = world.Components.Register<ComponentChangeChunkValue>(new SchemaId(74_007));
        ComponentId marker = world.Components.Register<ComponentChangeChunkExtra>(new SchemaId(74_008));
        ComponentId allTag = world.Components.Register<ComponentChangeChunkTag>(new SchemaId(74_009));
        ComponentId anyTag = world.Components.Register<ComponentChangeChunkTag>(new SchemaId(74_010));
        var entities = new Entity[4];
        world.Create([id, allTag], entities);
        world.Add(entities[0], anyTag);
        world.Add(entities[2], anyTag);
        Query query = world.WhereAll([id, allTag]).WhereAny([marker, anyTag]);
        Query mutation = world.WhereAll(id);
        Chunk chunk = query.Cached.MatchingChunkPlans()[0].Chunk;
        Assert.That(query.Cached.TryGetTagSlots(chunk, out ReadOnlySpan<int> selected), Is.True);
        Assert.That(selected.Length, Is.EqualTo(2));
        if (queryBatch)
        {
            world.Add(in mutation, marker);
        }
        else
        {
            world.Add(entities, marker);
        }

        Assert.That(query.Cached.TryGetTagSlots(chunk, out _), Is.False);
        if (queryBatch)
        {
            world.Remove(in mutation, marker);
        }
        else
        {
            world.Remove(entities, marker);
        }

        Assert.That(query.Cached.TryGetTagSlots(chunk, out selected), Is.True);
        Assert.That(selected.Length, Is.EqualTo(2));
    }
}
