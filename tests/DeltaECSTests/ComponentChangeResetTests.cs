namespace Delta.ECS.Tests;

using System;
using System.Collections.Generic;

using NUnit.Framework;

[TestFixture(typeof(ChangeResetValue))]
[TestFixture(typeof(ChangeResetTag))]
internal sealed class ComponentChangeResetTests<T> where T : struct
{
    [Test]
    public void ResetBeforeFirstInvocationAcceptsExistingMembershipWithoutNotifications()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<T>(new SchemaId(75_001));
        Entity existing = world.Create(id);
        Entity removed = world.Create(id);
        var changes = new List<(Entity, ChangeKind)>();
        var operation = world.ForEachChange<T>(id, (entity, kind, ref _) => changes.Add((entity, kind)));
        _ = world.GetRef<T>(existing, id);
        world.Remove(removed, id);
        Entity added = world.Create(id);

        operation.Reset();
        operation.Invoke();
        operation.Reset();
        operation.Invoke();
        Assert.That(changes, Is.Empty);

        _ = world.GetRef<T>(existing, id);
        world.Remove(added, id);
        world.Add(removed, id);
        operation.Invoke();
        Assert.That(changes, Is.EquivalentTo(new[]
        {
            (existing, ChangeKind.Modified), (added, ChangeKind.Removed), (removed, ChangeKind.Added)
        }));
    }

    [Test]
    public void ResetOfEmptySelectionPreservesFutureAdditions()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<T>(new SchemaId(75_001));
        var changes = new List<(Entity, ChangeKind)>();
        var operation = world.ForEachChange<T>(id, (entity, kind, ref _) => changes.Add((entity, kind)));
        operation.Reset();
        Entity entity = world.Create(id);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entity, ChangeKind.Added) }));
    }

    [Test]
    public void ResetAffectsOnlyItsOwnObserverAndCommitsPendingMembership()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<T>(new SchemaId(75_001));
        Entity existing = world.Create(id);
        Entity removed = world.Create(id);
        var firstChanges = new List<(Entity, ChangeKind)>();
        var secondChanges = new List<(Entity, ChangeKind)>();
        var first = world.ForEachChange<T>(id, (entity, kind, ref _) => firstChanges.Add((entity, kind)));
        var second = world.ForEachChange<T>(id, (entity, kind, ref _) => secondChanges.Add((entity, kind)));
        first.Invoke();
        second.Invoke();
        firstChanges.Clear();
        secondChanges.Clear();
        _ = world.GetRef<T>(existing, id);
        world.Remove(removed, id);
        Entity added = world.Create(id);

        first.Reset();
        first.Invoke();
        Assert.That(firstChanges, Is.Empty);
        second.Invoke();
        Assert.That(secondChanges, Is.EquivalentTo(new[]
        {
            (existing, ChangeKind.Modified), (removed, ChangeKind.Removed), (added, ChangeKind.Added)
        }));

        _ = world.GetRef<T>(added, id);
        world.Remove(existing, id);
        world.Add(removed, id);
        first.Invoke();
        Assert.That(firstChanges, Is.EquivalentTo(new[]
        {
            (added, ChangeKind.Modified), (existing, ChangeKind.Removed), (removed, ChangeKind.Added)
        }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ResetDiscardsDenseAndSparseWritesAndKeepsFollowingWrites(bool parallel)
    {
        using var world = new World();
        ComponentId id = world.Components.Register<T>(new SchemaId(75_001));
        ComponentId extra = world.Components.Register<long>(new SchemaId(75_002));
        var entities = new Entity[2_103];
        world.Create([id, extra], entities);
        var changes = new List<(Entity, ChangeKind)>();
        var operation = world.ForEachChange<T>(id, (entity, kind, ref _) => changes.Add((entity, kind)));
        operation.Reset();
        Query query = world.WhereAll(id);
        IOperation writer = CreateWriter(world, in query, parallel);

        _ = world.GetRef<T>(entities[65], id);
        writer.Invoke();
        operation.Reset();
        operation.Invoke();
        Assert.That(changes, Is.Empty);

        writer.Invoke();
        operation.Invoke();
        Assert.That(changes.Count, Is.EqualTo(entities.Length));
        Assert.That(changes, Has.All.Matches<(Entity Entity, ChangeKind Kind)>(change => change.Kind == ChangeKind.Modified));
        changes.Clear();
        _ = world.GetRef<T>(entities[1_027], id);
        operation.Reset();
        _ = world.GetRef<T>(entities[192], id);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entities[192], ChangeKind.Modified) }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ResetUsesCurrentFilteredMembership(bool initialized)
    {
        using var world = new World();
        ComponentId id = world.Components.Register<T>(new SchemaId(75_001));
        ComponentId filter = world.Components.Register<ChangeResetFilter>(new SchemaId(75_002));
        var entities = new Entity[193];
        world.Create([id], entities);
        world.Add(entities.AsSpan(0, 65), filter);
        world.Add(entities[128], filter);
        world.Add(entities[192], filter);
        Query query = world.WhereAll(filter);
        var changes = new List<(Entity, ChangeKind)>();
        var operation = world.ForEachChange<T>(in query, id, (entity, kind, ref _) => changes.Add((entity, kind)));
        if (initialized)
        {
            operation.Invoke();
            changes.Clear();
        }
        world.Remove(entities[0], filter);
        world.Add(entities[127], filter);
        world.Remove(entities[128], id);
        _ = world.GetRef<T>(entities[192], id);

        operation.Reset();
        operation.Invoke();
        Assert.That(changes, Is.Empty);
        _ = world.GetRef<T>(entities[0], id);
        _ = world.GetRef<T>(entities[127], id);
        world.Remove(entities[192], filter);
        world.Add(entities[128], id);
        operation.Invoke();
        Assert.That(changes, Is.EquivalentTo(new[]
        {
            (entities[127], ChangeKind.Modified), (entities[192], ChangeKind.Removed), (entities[128], ChangeKind.Added)
        }));
    }

    [Test]
    public void ResetKeepsReusedEntityGenerationsSeparate()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<T>(new SchemaId(75_001));
        Entity old = world.Create(id);
        var changes = new List<(Entity, ChangeKind)>();
        var operation = world.ForEachChange<T>(id, (entity, kind, ref _) => changes.Add((entity, kind)));
        operation.Invoke();
        changes.Clear();
        world.Destroy(old);
        Entity replacement = world.Create(id);
        Assert.That(replacement.Index, Is.EqualTo(old.Index));
        operation.Reset();
        operation.Invoke();
        Assert.That(changes, Is.Empty);
        _ = world.GetRef<T>(replacement, id);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (replacement, ChangeKind.Modified) }));
        changes.Clear();

        world.Destroy(replacement);
        Entity transient = world.Create(id);
        world.Destroy(transient);
        operation.Reset();
        Entity newest = world.Create(id);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (newest, ChangeKind.Added) }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ResetResolvesPendingGroupChanges(bool initialized)
    {
        using var world = new World();
        ComponentId id = world.Components.Register<T>(new SchemaId(75_001));
        ComponentId group = world.Components.Register<ChangeResetGroup>(new SchemaId(75_002), group: true);
        Entity leaving = world.Create(group, new ChangeResetGroup(1));
        Entity entering = world.Create(group, new ChangeResetGroup(2));
        world.Add([leaving, entering], id);
        Query query = world.WhereAll(new ChangeResetGroup(1));
        var changes = new List<(Entity, ChangeKind)>();
        var operation = world.ForEachChange<T>(in query, id, (entity, kind, ref _) => changes.Add((entity, kind)));
        if (initialized)
        {
            operation.Invoke();
            changes.Clear();
        }
        world.GetRef<ChangeResetGroup>(leaving, group) = new ChangeResetGroup(2);
        world.GetRef<ChangeResetGroup>(entering, group) = new ChangeResetGroup(1);
        operation.Reset();
        operation.Invoke();
        Assert.That(changes, Is.Empty);

        _ = world.GetRef<T>(entering, id);
        world.GetRef<ChangeResetGroup>(leaving, group) = new ChangeResetGroup(1);
        operation.Invoke();
        Assert.That(changes, Is.EquivalentTo(new[] { (entering, ChangeKind.Modified), (leaving, ChangeKind.Added) }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ResetDiscardsNotificationsRetainedAfterCallbackFailure(bool initialized)
    {
        using var world = new World();
        ComponentId id = world.Components.Register<T>(new SchemaId(75_001));
        Entity entity = world.Create(id);
        bool fail = !initialized;
        var changes = new List<(Entity, ChangeKind)>();
        var operation = world.ForEachChange<T>(id, (current, kind, ref _) =>
        {
            if (fail) { throw new InvalidOperationException("Callback failed"); }
            changes.Add((current, kind));
        });
        if (initialized)
        {
            operation.Invoke();
            changes.Clear();
            _ = world.GetRef<T>(entity, id);
            fail = true;
        }
        Assert.Throws<InvalidOperationException>(operation.Invoke);
        operation.Reset();
        operation.Invoke();
        Assert.That(changes, Is.Empty);
        fail = false;
        _ = world.GetRef<T>(entity, id);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entity, ChangeKind.Modified) }));
    }

    [Test]
    public void ResetFromOwnCallbackFailsWithoutCorruptingInvocation()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<T>(new SchemaId(75_001));
        Entity entity = world.Create(id);
        Action reset = static () => { };
        int callbacks = 0;
        var operation = world.ForEachChange<T>(id, (current, kind, ref _) =>
        {
            callbacks++;
            Assert.Throws<InvalidOperationException>(() => reset());
        });
        reset = operation.Reset;
        operation.Invoke();
        operation.Invoke();
        _ = world.GetRef<T>(entity, id);
        operation.Invoke();
        Assert.That(callbacks, Is.EqualTo(2));
        operation.Reset();
    }

    private static IOperation CreateWriter(World world, in Query query, bool parallel)
    {
        if (typeof(T) == typeof(ChangeResetTag))
        {
            return parallel
                ? world.ForEachParallel(in query, static (ref ChangeResetTag _) => { }, workerCount: 4)
                : world.ForEach(in query, static (ref ChangeResetTag _) => { });
        }

        return parallel
            ? world.ForEachParallel(in query, static (ref ChangeResetValue _) => { }, workerCount: 4)
            : world.ForEach(in query, static (ref ChangeResetValue _) => { });
    }
}

internal struct ChangeResetValue { public int Value; }
internal struct ChangeResetTag { }
internal struct ChangeResetFilter { }
internal readonly record struct ChangeResetGroup(int Value);
