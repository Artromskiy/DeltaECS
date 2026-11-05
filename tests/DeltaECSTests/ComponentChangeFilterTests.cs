namespace Delta.ECS.Tests;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using NUnit.Framework;

[TestFixture]
internal sealed class ComponentChangeFilterTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void TracksEntryWritesAndExit(bool trackedTag, bool filterTag)
    {
        using var world = new World();
        ComponentId tracked = RegisterTracked(world, trackedTag);
        ComponentId filter = RegisterFilter(world, filterTag);
        Query query = world.WhereAll(filter);
        Entity included = world.Create([tracked, filter]);
        Entity excluded = world.Create(tracked);
        Entity missingTracked = world.Create(filter);
        var changes = new List<(Entity, ChangeKind)>();
        IOperation operation = Observe(world, in query, tracked, trackedTag, changes);

        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (included, ChangeKind.Added) }));
        changes.Clear();
        Write(world, excluded, tracked, trackedTag);
        operation.Invoke();
        Assert.That(changes, Is.Empty);

        world.Add(excluded, filter);
        world.Add(missingTracked, tracked);
        operation.Invoke();
        Assert.That(changes, Is.EquivalentTo(new[] { (excluded, ChangeKind.Added), (missingTracked, ChangeKind.Added) }));
        changes.Clear();
        Write(world, included, tracked, trackedTag);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (included, ChangeKind.Modified) }));
        changes.Clear();

        world.Remove(included, filter);
        world.Remove(missingTracked, tracked);
        operation.Invoke();
        Assert.That(changes, Is.EquivalentTo(new[] { (included, ChangeKind.Removed), (missingTracked, ChangeKind.Removed) }));
        changes.Clear();
        Write(world, included, tracked, trackedTag);
        operation.Invoke();
        Assert.That(changes, Is.Empty);
        world.Add(included, filter);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (included, ChangeKind.Added) }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CombinesAnyAndNoneAndCoalescesFilterChanges(bool trackedTag)
    {
        using var world = new World();
        ComponentId tracked = RegisterTracked(world, trackedTag);
        ComponentId first = RegisterFilter(world, false);
        ComponentId second = RegisterFilter(world, true);
        ComponentId excluded = world.Components.Register<ChangeFilterExcluded>(new SchemaId(74_004));
        Query query = world.WhereAny([first, second]).WhereNone(excluded);
        Entity entity = world.Create([tracked, first]);
        var changes = new List<(Entity, ChangeKind)>();
        IOperation operation = Observe(world, in query, tracked, trackedTag, changes);
        operation.Invoke();
        changes.Clear();

        world.Add(entity, second);
        world.Remove(entity, first);
        operation.Invoke();
        Assert.That(changes, Is.Empty);
        world.Remove(entity, second);
        world.Add(entity, second);
        operation.Invoke();
        Assert.That(changes, Is.Empty);

        // A filter-only structural event must not hide a write in the same chunk.
        world.Add(entity, first);
        Write(world, entity, tracked, trackedTag);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entity, ChangeKind.Modified) }));
        changes.Clear();
        world.Add(entity, excluded);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entity, ChangeKind.Removed) }));
        changes.Clear();
        world.Remove(entity, excluded);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entity, ChangeKind.Added) }));
        changes.Clear();
        world.Remove(entity, first);
        world.Remove(entity, second);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entity, ChangeKind.Removed) }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void IntersectsDenseAndSparseWritesWithTagFilters(bool trackedTag)
    {
        using var world = new World();
        ComponentId tracked = RegisterTracked(world, trackedTag);
        ComponentId filter = RegisterFilter(world, true);
        var entities = new Entity[193];
        world.Create([tracked], entities);
        world.Add(entities.AsSpan(0, 65), filter);
        world.Add(entities[128], filter);
        world.Add(entities[192], filter);
        Query query = world.WhereAll(filter);
        var changes = new List<(Entity, ChangeKind)>();
        IOperation operation = Observe(world, in query, tracked, trackedTag, changes);
        operation.Invoke();
        Assert.That(changes.Count, Is.EqualTo(67));
        changes.Clear();
        Query all = world.WhereAll(tracked);
        IOperation write = trackedTag
            ? world.ForEach(in all, static (ref ChangeFilterTrackedTag _) => { })
            : world.ForEach(in all, static (ref ChangeFilterValue _) => { });
        write.Invoke();
        operation.Invoke();
        Assert.That(changes.Count, Is.EqualTo(67));
        Assert.That(changes, Has.All.Matches<(Entity Entity, ChangeKind Kind)>(item => item.Kind == ChangeKind.Modified));
        changes.Clear();
        Write(world, entities[66], tracked, trackedTag);
        Write(world, entities[128], tracked, trackedTag);
        Write(world, entities[192], tracked, trackedTag);
        operation.Invoke();
        Assert.That(changes, Is.EquivalentTo(new[] { (entities[128], ChangeKind.Modified), (entities[192], ChangeKind.Modified) }));
        changes.Clear();

        world.Remove(in query, filter);
        operation.Invoke();
        Assert.That(changes.Count, Is.EqualTo(67));
        Assert.That(changes, Has.All.Matches<(Entity Entity, ChangeKind Kind)>(item => item.Kind == ChangeKind.Removed));
        changes.Clear();
        world.Add(entities, filter);
        operation.Invoke();
        Assert.That(changes.Count, Is.EqualTo(entities.Length));
        changes.Clear();
        operation.Invoke();
        Assert.That(changes, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void KeepsGenerationsSeparateAfterLeavingFilterAndDestruction(bool trackedTag)
    {
        using var world = new World();
        ComponentId tracked = RegisterTracked(world, trackedTag);
        ComponentId filter = RegisterFilter(world, false);
        Query query = world.WhereAll(filter);
        Entity old = world.Create([tracked, filter]);
        var changes = new List<(Entity, ChangeKind)>();
        IOperation operation = Observe(world, in query, tracked, trackedTag, changes);
        operation.Invoke();
        changes.Clear();
        world.Remove(old, filter);
        world.Destroy(old);
        Entity replacement = world.Create([tracked, filter]);
        Assert.That(replacement.Index, Is.EqualTo(old.Index));
        operation.Invoke();
        Assert.That(changes, Is.EquivalentTo(new[] { (old, ChangeKind.Removed), (replacement, ChangeKind.Added) }));
        changes.Clear();
        world.Remove(replacement, tracked);
        world.Add(replacement, tracked);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (replacement, ChangeKind.Modified) }));
        changes.Clear();
        Entity transient = world.Create([tracked, filter]);
        world.Destroy(transient);
        operation.Invoke();
        Assert.That(changes, Is.Empty);
    }

    [Test]
    public void FailedCallbackRetainsMembershipAndWriteNotifications()
    {
        using var world = new World();
        ComponentId tracked = RegisterTracked(world, false);
        ComponentId filter = RegisterFilter(world, true);
        Query query = world.WhereNone(filter);
        Entity first = world.Create(tracked);
        Entity second = world.Create([tracked, filter]);
        bool fail = true;
        var changes = new List<(Entity, ChangeKind)>();
        var operation = world.ForEachChange<ChangeFilterValue>(in query, tracked,
            (Entity entity, ChangeKind kind, ref ChangeFilterValue _) =>
            {
                if (fail) { throw new InvalidOperationException("User callback failed"); }
                changes.Add((entity, kind));
            });
        Assert.Throws<InvalidOperationException>(operation.Invoke);
        fail = false;
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (first, ChangeKind.Added) }));
        changes.Clear();
        world.Add(first, filter);
        world.Remove(second, filter);
        fail = true;
        Assert.Throws<InvalidOperationException>(operation.Invoke);
        // These structural changes also prove that the query lease was released.
        world.Remove(first, filter);
        world.Add(first, filter);
        fail = false;
        operation.Invoke();
        Assert.That(changes, Is.EquivalentTo(new[] { (first, ChangeKind.Removed), (second, ChangeKind.Added) }));
        changes.Clear();
        Write(world, second, tracked, false);
        fail = true;
        Assert.Throws<InvalidOperationException>(operation.Invoke);
        fail = false;
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (second, ChangeKind.Modified) }));
        changes.Clear();
        operation.Invoke();
        Assert.That(changes, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MatchesSnapshotModelAcrossStructuralChanges(bool trackedTag)
    {
        using var world = new World();
        ComponentId tracked = RegisterTracked(world, trackedTag);
        ComponentId required = RegisterFilter(world, false);
        ComponentId alternate = world.Components.Register<ChangeFilterValue>(new SchemaId(74_005));
        ComponentId tag = RegisterFilter(world, true);
        ComponentId excluded = world.Components.Register<ChangeFilterExcluded>(new SchemaId(74_004));
        ComponentId[] ids = [tracked, required, alternate, tag, excluded];
        Query query = world.WhereAll(required).WhereAny([alternate, tag]).WhereNone(excluded);
        var changes = new List<(Entity, ChangeKind)>();
        IOperation operation = Observe(world, in query, tracked, trackedTag, changes);
        var live = new List<Entity>();
        var previous = new HashSet<Entity>();
        var changed = new HashSet<Entity>();
        var random = new Random(3091);
        operation.Invoke();
        for (int pass = 0; pass < 100; pass++)
        {
            for (int step = 0; step < 32; step++)
            {
                if (live.Count == 0 || random.Next(8) == 0)
                {
                    live.Add(world.Create([tracked]));
                    continue;
                }

                int index = random.Next(live.Count);
                Entity entity = live[index];
                int action = random.Next(4);
                if (action == 0)
                {
                    world.Destroy(entity);
                    live.RemoveAt(index);
                    continue;
                }
                if (action == 1)
                {
                    if (world.Has(entity, tracked))
                    {
                        Write(world, entity, tracked, trackedTag);
                        changed.Add(entity);
                    }
                    continue;
                }

                ComponentId id = ids[random.Next(ids.Length)];
                bool applied = action == 2 ? world.Add(entity, id) : world.Remove(entity, id);
                if (applied && id == tracked)
                {
                    changed.Add(entity);
                }
            }

            var current = new HashSet<Entity>();
            var expected = new List<(Entity, ChangeKind)>();
            foreach (Entity entity in live)
            {
                if (!world.Has(entity, tracked) || !world.Has(entity, required)
                    || (!world.Has(entity, alternate) && !world.Has(entity, tag)) || world.Has(entity, excluded))
                {
                    continue;
                }

                current.Add(entity);
                if (!previous.Contains(entity))
                {
                    expected.Add((entity, ChangeKind.Added));
                }
                else if (changed.Contains(entity))
                {
                    expected.Add((entity, ChangeKind.Modified));
                }
            }
            foreach (Entity entity in previous)
            {
                if (!current.Contains(entity))
                {
                    expected.Add((entity, ChangeKind.Removed));
                }
            }

            operation.Invoke();
            Assert.That(changes, Is.EquivalentTo(expected), $"Pass {pass}");
            changes.Clear();
            changed.Clear();
            previous = current;
            operation.Invoke();
            Assert.That(changes, Is.Empty);
        }
    }

    [Test]
    public void RejectsInvalidInputsAndSupportsAnEmptyQuery()
    {
        using var world = new World();
        using var other = new World();
        ComponentId tracked = RegisterTracked(world, false);
        Query invalid = default;
        Query foreign = other.CreateQuery(QuerySpec.Empty);
        Assert.Throws<ArgumentException>(() => world.ForEachChange<ChangeFilterValue>(in invalid, tracked, static (Entity _, ChangeKind _, ref ChangeFilterValue _) => { }));
        Assert.Throws<ArgumentException>(() => world.ForEachChange<ChangeFilterValue>(in foreign, tracked, static (Entity _, ChangeKind _, ref ChangeFilterValue _) => { }));
        Query empty = world.CreateQuery(QuerySpec.Empty);
        Assert.Throws<ArgumentNullException>(() => world.ForEachChange<ChangeFilterValue>(in empty, tracked, null!));
        Assert.Throws<ArgumentException>(() => world.ForEachChange<ChangeFilterTrackedTag>(in empty, tracked, static (Entity _, ChangeKind _, ref ChangeFilterTrackedTag _) => { }));
        var changes = new List<(Entity, ChangeKind)>();
        IOperation operation = Observe(world, in empty, tracked, false, changes);
        Entity entity = world.Create(tracked);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entity, ChangeKind.Added) }));
    }

    private static ComponentId RegisterTracked(World world, bool tag) => tag
        ? world.Components.Register<ChangeFilterTrackedTag>(new SchemaId(74_001))
        : world.Components.Register<ChangeFilterValue>(new SchemaId(74_001));

    private static ComponentId RegisterFilter(World world, bool tag) => tag
        ? world.Components.Register<ChangeFilterTag>(new SchemaId(74_003))
        : world.Components.Register<ChangeFilterValue>(new SchemaId(74_002));

    private static IOperation Observe(World world, in Query query, ComponentId id, bool tag, List<(Entity, ChangeKind)> changes) => tag
        ? world.ForEachChange<ChangeFilterTrackedTag>(in query, id, (Entity entity, ChangeKind kind, ref ChangeFilterTrackedTag value) =>
        {
            Assert.That(Unsafe.IsNullRef(ref value), Is.EqualTo(kind == ChangeKind.Removed));
            changes.Add((entity, kind));
        })
        : world.ForEachChange<ChangeFilterValue>(in query, id, (Entity entity, ChangeKind kind, ref ChangeFilterValue value) =>
        {
            Assert.That(Unsafe.IsNullRef(ref value), Is.EqualTo(kind == ChangeKind.Removed));
            changes.Add((entity, kind));
        });

    private static void Write(World world, Entity entity, ComponentId id, bool tag)
    {
        if (tag) { _ = world.GetRef<ChangeFilterTrackedTag>(entity, id); }
        else { world.GetRef<ChangeFilterValue>(entity, id).Value++; }
    }
}

internal struct ChangeFilterValue { public int Value; }
internal struct ChangeFilterTrackedTag;
internal struct ChangeFilterTag;
internal struct ChangeFilterExcluded;
