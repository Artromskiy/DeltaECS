namespace Delta.ECS.Tests;

using System.Collections.Generic;

using NUnit.Framework;

[TestFixture]
internal sealed class ComponentChangeChunkIterationTests
{
    [Test]
    public void DenseWritesReportEveryEntityAsModified()
    {
        var layouts = new ComponentRegistry();
        ComponentId valueId = layouts.Register<ComponentChangeChunkValue>(new SchemaId(72_021));
        using var world = new World(layouts, initialEntityCapacity: 2_048);
        var entities = new Entity[2_048];
        world.Create([valueId], entities);
        var changes = new List<ChangeKind>();
        ComponentChangeOperation<ComponentChangeChunkValue> operation = world.ForEachChange<ComponentChangeChunkValue>(
            valueId,
            (_, kind, ref _) => changes.Add(kind));
        operation.Invoke();
        changes.Clear();

        Query query = world.WhereAll(valueId);
        world.ForEach(in query, static (ref ComponentChangeChunkValue value) => value.Value++).Invoke();
        operation.Invoke();

        Assert.That(changes, Has.Count.EqualTo(entities.Length));
        Assert.That(changes, Is.All.EqualTo(ChangeKind.Modified));
    }

    [Test]
    public void SparseEntityWriteReportsOnlyItsChunkSlot()
    {
        var layouts = new ComponentRegistry();
        ComponentId valueId = layouts.Register<ComponentChangeChunkValue>(new SchemaId(72_022));
        using var world = new World(layouts);
        Entity first = world.Create(valueId);
        _ = world.Create(valueId);
        var changes = new List<(Entity Entity, ChangeKind Kind)>();
        ComponentChangeOperation<ComponentChangeChunkValue> operation = world.ForEachChange<ComponentChangeChunkValue>(
            valueId,
            (entity, kind, ref _) => changes.Add((entity, kind)));
        operation.Invoke();
        changes.Clear();

        world.GetRef<ComponentChangeChunkValue>(first, valueId).Value++;
        operation.Invoke();

        Assert.That(changes, Is.EqualTo(new[] { (first, ChangeKind.Modified) }));
    }

    [Test]
    public void RecycledChunkIdDoesNotCarryPreviousDirtyStateToNewChunk()
    {
        var layouts = new ComponentRegistry();
        ComponentId valueId = layouts.Register<ComponentChangeChunkValue>(new SchemaId(72_023));
        ComponentId extraId = layouts.Register<ComponentChangeChunkExtra>(new SchemaId(72_024));
        using var world = new World(layouts);
        Entity existing = world.Create(valueId);
        Assert.That(world.TryResolveEntityLocation(existing, out Chunk firstChunk, out _), Is.True);
        int recycledChunkId = firstChunk.GlobalId;
        var changes = new List<(Entity Entity, ChangeKind Kind)>();
        ComponentChangeOperation<ComponentChangeChunkValue> operation = world.ForEachChange<ComponentChangeChunkValue>(
            valueId,
            (entity, kind, ref _) => changes.Add((entity, kind)));
        operation.Invoke();
        changes.Clear();

        world.GetRef<ComponentChangeChunkValue>(existing, valueId).Value++;
        Assert.That(world.Add(existing, extraId), Is.True);
        Entity added = world.Create(valueId);
        Assert.That(world.TryResolveEntityLocation(added, out Chunk replacementChunk, out _), Is.True);
        Assert.That(replacementChunk.GlobalId, Is.EqualTo(recycledChunkId));

        operation.Invoke();

        Assert.That(changes, Is.EquivalentTo(new[]
        {
            (existing, ChangeKind.Modified),
            (added, ChangeKind.Added)
        }));
    }

    [Test]
    public void ParallelEntityRefWritesAreRecordedByPreparedChunkMasks()
    {
        var layouts = new ComponentRegistry();
        ComponentId valueId = layouts.Register<ComponentChangeChunkValue>(new SchemaId(72_025));
        using var world = new World(layouts, initialEntityCapacity: 2_048);
        var entities = new Entity[2_048];
        world.Create([valueId], entities);
        int modifiedCount = 0;
        ComponentChangeOperation<ComponentChangeChunkValue> operation = world.ForEachChange<ComponentChangeChunkValue>(
            valueId,
            (_, kind, ref _) =>
            {
                if (kind == ChangeKind.Modified)
                {
                    modifiedCount++;
                }
            });
        operation.Invoke();
        Query query = world.WhereAll(valueId);
        ForEachEntityAction write = entity => entity.GetRef<ComponentChangeChunkValue>(valueId).Value++;

        world.ForEachEntityParallel(in query, write, workerCount: 4).Invoke();
        operation.Invoke();

        Assert.That(modifiedCount, Is.EqualTo(entities.Length));
    }

    [Test]
    public void TagWritesAndMembershipChangesReportExpectedEvents()
    {
        var layouts = new ComponentRegistry();
        ComponentId tagId = layouts.Register<ComponentChangeChunkTag>(new SchemaId(72_026));
        ComponentId unrelatedId = layouts.Register<ComponentChangeChunkExtra>(new SchemaId(72_027));
        using var world = new World(layouts);
        Entity existing = world.Create(tagId);
        Entity added = world.Create(unrelatedId);
        var changes = new List<(Entity Entity, ChangeKind Kind)>();
        ComponentChangeOperation<ComponentChangeChunkTag> operation = world.ForEachChange<ComponentChangeChunkTag>(
            tagId,
            (entity, kind, ref _) => changes.Add((entity, kind)));

        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (existing, ChangeKind.Added) }));
        changes.Clear();

        _ = world.GetRef<ComponentChangeChunkTag>(existing, tagId);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (existing, ChangeKind.Modified) }));
        changes.Clear();

        Assert.That(world.Add(added, tagId), Is.True);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (added, ChangeKind.Added) }));
        changes.Clear();

        Assert.That(world.Remove(existing, tagId), Is.True);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (existing, ChangeKind.Removed) }));
    }
}

internal struct ComponentChangeChunkValue
{
    internal int Value;
}

internal struct ComponentChangeChunkExtra
{
    internal int Value { get; set; }
}

internal readonly struct ComponentChangeChunkTag;
