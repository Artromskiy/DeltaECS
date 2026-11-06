namespace Delta.ECS.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using NUnit.Framework;

[TestFixture]
internal sealed class GroupedComponentTests
{
    [Test]
    public void AnyAndNoneMatchValuesAndIncludeAbsentComponentsCorrectly()
    {
        using var world = CreateWorld(out ComponentId member);
        ComponentId payload = world.Components.Register<int>(new SchemaId(81_002));
        Entity first = world.Create(member, new MemberOf(1));
        Entity second = world.Create(member, new MemberOf(2));
        Entity absent = world.Create(payload);
        Assert.That(Collect(world, world.WhereAny(new MemberOf(1))), Is.EqualTo(new[] { first }));
        Assert.That(Collect(world, world.WhereAny(member, new MemberOf(2))), Is.EqualTo(new[] { second }));
        Assert.That(Collect(world, world.WhereNone(new MemberOf(1))), Is.EquivalentTo(new[] { second, absent }));
        Assert.That(Collect(world, world.WhereNone(member, new MemberOf(2))), Is.EquivalentTo(new[] { first, absent }));
        Query either = world.WhereAny(new MemberOf(1)).WhereAny(new MemberOf(2));
        Assert.That(Collect(world, either), Is.EquivalentTo(new[] { first, second }));
        Query neither = world.WhereNone(new MemberOf(1)).WhereNone(new MemberOf(2));
        Assert.That(Collect(world, neither), Is.EqualTo(new[] { absent }));
    }

    [Test]
    public void AnyCombinesGroupValuesDataPresenceAndSparseTagsWithOr()
    {
        using var world = CreateWorld(out ComponentId member);
        ComponentId payload = world.Components.Register<int>(new SchemaId(81_002));
        ComponentId tag = world.Components.Register<Paused>(new SchemaId(81_003));
        ComponentId alternative = world.Components.Register<long>(new SchemaId(81_004));
        Entity byGroup = world.Create(member, new MemberOf(1));
        Entity byTag = world.Create(member, new MemberOf(2));
        Entity excluded = world.Create(member, new MemberOf(2));
        Entity byData = world.Create(alternative);
        Entity absent = world.Create(payload);
        world.Add([byGroup, byTag, excluded, byData], payload);
        world.Add(byTag, tag);
        Query selected = world.WhereAll(payload).WhereAny(new MemberOf(1)).WhereAny(tag).WhereAny(alternative);
        Assert.That(Collect(world, selected), Is.EquivalentTo(new[] { byGroup, byTag, byData }));
        Assert.That(Locate(world, byTag), Is.SameAs(Locate(world, excluded)));
        Assert.That(world.IsAlive(absent), Is.True);
    }

    [Test]
    public void AnyAndNoneChangeObserversTrackEnteringAndLeavingGroups()
    {
        using var world = CreateWorld(out ComponentId member);
        ComponentId health = world.Components.Register<int>(new SchemaId(81_002));
        Entity entity = world.Create(member, new MemberOf(1));
        world.Add(entity, health, 100);
        Query any = world.WhereAll(health).WhereAny(new MemberOf(1));
        Query none = world.WhereAll(health).WhereNone(new MemberOf(1));
        var anyChanges = new List<ChangeKind>();
        var noneChanges = new List<ChangeKind>();
        var anyObserver = world.ForEachChange(in any, health, (Entity _, ChangeKind kind, ref int _) => anyChanges.Add(kind));
        var noneObserver = world.ForEachChange(in none, health, (Entity _, ChangeKind kind, ref int _) => noneChanges.Add(kind));
        anyObserver.Invoke();
        noneObserver.Invoke();
        Assert.That(anyChanges, Is.EqualTo(new[] { ChangeKind.Added }));
        Assert.That(noneChanges, Is.Empty);
        anyChanges.Clear();
        world.Set(entity, member, new MemberOf(2));
        anyObserver.Invoke();
        noneObserver.Invoke();
        Assert.That(anyChanges, Is.EqualTo(new[] { ChangeKind.Removed }));
        Assert.That(noneChanges, Is.EqualTo(new[] { ChangeKind.Added }));
    }

    [Test]
    public void GroupFlagDefaultsToOrdinaryRegistrationAndRejectsUnsupportedTypes()
    {
        var layouts = new ComponentRegistry();
        ComponentId ordinary = layouts.Register<MemberOf>(new SchemaId(81_001));
        Assert.That(layouts.GetGrouping(ordinary), Is.Null);
        Assert.That(() => layouts.Register<PositionWithoutEquality>(new SchemaId(81_002), group: true), Throws.ArgumentException);
        Assert.That(() => layouts.Register<string>(new SchemaId(81_003), group: true), Throws.ArgumentException);
        Assert.That(layouts.Count, Is.EqualTo(1));
        ComponentId grouped = layouts.Register<MemberOf>(new SchemaId(81_004), group: true);
        Assert.That(() => layouts.Register<MemberOf>(new SchemaId(81_004)), Throws.InvalidOperationException);
        Assert.That(layouts.GetGrouping(grouped), Is.Not.Null);
    }

    [Test]
    public void RegistrationRetainsOneSchemaAndSupportsPrimaryAndExplicitIds()
    {
        using var world = new World();
        ComponentId member = world.Components.Register<MemberOf>(new SchemaId(81_001), group: true);
        Assert.That(world.Components.Register<MemberOf>(new SchemaId(81_001), group: true), Is.EqualTo(member));
        Assert.That(world.Components.GetPrimary<MemberOf>(), Is.EqualTo(member));
        Assert.That(world.Components.TryGetComponentId(new SchemaId(81_001), out ComponentId resolved), Is.True);
        Assert.That(resolved, Is.EqualTo(member));
        Entity first = world.Create(member, new MemberOf(1));
        Entity second = world.Create(member, new MemberOf(2));
        Assert.That(Collect(world, world.WhereAll(new MemberOf(1))), Is.EqualTo(new[] { first }));
        Assert.That(Collect(world, world.WhereAll(member, new MemberOf(2))), Is.EqualTo(new[] { second }));
        Assert.That(world.Components.GetComponentIds<MemberOf>().Length, Is.EqualTo(1));
        AssertHomogeneous(world);
    }

    [Test]
    public void GetRefDefersUntilNextOperationAndDoesNotModifyOtherMembers()
    {
        using var world = CreateWorld(out ComponentId member);
        Entity first = world.Create(member, new MemberOf(1));
        Entity second = world.Create(member, new MemberOf(1));
        Query oldGroup = world.WhereAll(new MemberOf(1));
        Archetype original = Locate(world, first).Archetype;
        ref MemberOf value = ref world.GetRef<MemberOf>(first);
        value = new MemberOf(2);
        Assert.That(Locate(world, first).Archetype, Is.SameAs(original));
        Assert.That(Locate(world, second).Archetype, Is.SameAs(original));
        Assert.That(Locate(world, first).GroupingDirty, Is.True);
        Assert.That(Collect(world, oldGroup), Is.EqualTo(new[] { second }));
        Assert.That(world.Get<MemberOf>(first).Value, Is.EqualTo(2));
        Assert.That(Locate(world, first).Archetype, Is.Not.SameAs(original));
        AssertHomogeneous(world);
    }

    [Test]
    public void CachedReadAndWriteFunctorsRefreshBeforeAccessingRows()
    {
        using var world = CreateWorld(out ComponentId member);
        Entity first = world.Create(member, new MemberOf(1));
        Query group = world.WhereAll(new MemberOf(1));
        var reader = new CountMembers();
        var read = world.ForEach(in group, ref reader);
        read.Invoke(ref reader);
        Assert.That(reader.Count, Is.EqualTo(1));
        world.Set(first, new MemberOf(2));
        reader.Count = 0;
        read.Invoke(ref reader);
        Assert.That(reader.Count, Is.Zero);
        Entity second = world.Create(member, new MemberOf(1));
        var writer = world.ForEach(in group, new ChangeGroup(3));
        writer.Invoke();
        Assert.That(Locate(world, second).GroupingDirty, Is.True);
        reader.Count = 0;
        read.Invoke(ref reader);
        Assert.That(reader.Count, Is.Zero);
        Assert.That(world.Get<MemberOf>(second).Value, Is.EqualTo(3));
        AssertHomogeneous(world);
    }

    [Test]
    public void ReadOnlyAccessLeavesCleanChunksUntouched()
    {
        using var world = CreateWorld(out ComponentId member);
        Entity entity = world.Create(member, new MemberOf(1));
        Query group = world.WhereAll(new MemberOf(1));
        Chunk chunk = Locate(world, entity);
        int archetypes = world.Archetypes.Count;
        Assert.That(world.GetReadRef<MemberOf>(entity).Value, Is.EqualTo(1));
        world.ForEach(in group, new CountMembers()).Invoke();
        world.ForEachEntity(in group, static (EntityRef current) => GC.KeepAlive(current.GetReadRef<MemberOf>().Value)).Invoke();
        Assert.That(chunk.GroupingDirty, Is.False);
        Assert.That(world.Archetypes.Count, Is.EqualTo(archetypes));
    }

    [Test]
    public void EntityRefAccessesDeferAcrossNestedOperationsAndRefreshCachedAccesses()
    {
        using var world = CreateWorld(out ComponentId member);
        Entity entity = world.Create(member, new MemberOf(1));
        Query group = world.WhereAll(new MemberOf(1));
        var operation = world.ForEachEntity(in group, (EntityRef current) =>
        {
            current.GetRef<MemberOf>() = new MemberOf(2);
            Assert.That(world.Get<MemberOf>(entity).Value, Is.EqualTo(2));
            Assert.That(Locate(world, entity).Archetype.GroupingRows[0].ValueId, Is.Not.EqualTo(
                ((ComponentGrouping<MemberOf>)world.Components.GetGrouping(member)!).GetValueId(new MemberOf(2))));
        });
        operation.Invoke();
        Assert.That(Locate(world, entity).GroupingDirty, Is.True);
        Assert.That(Collect(world, group), Is.Empty);
        world.Set(entity, new MemberOf(1));
        operation.Invoke();
        Assert.That(Collect(world, group), Is.Empty);
        AssertHomogeneous(world);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GeneratedVariadicWritesRegroupAllSelectedEntities(bool parallel)
    {
        using var world = CreateWorld(out ComponentId member);
        ComponentId payload = world.Components.Register<int>(new SchemaId(81_002));
        var entities = new Entity[Chunk.MaxCapacity * 3 + 7];
        world.Create([member, payload], entities);
        Query all = world.WhereAll([member, payload]);
        if (parallel)
        {
            world.ForEachEntityParallel(in all, static (EntityRef entity, ref MemberOf group, ref int data) =>
            {
                group = new MemberOf(entity.Index % 4);
                data = entity.Index + 100;
            }, workerCount: 4).Invoke();
        }
        else
        {
            world.ForEachEntity(in all, static (EntityRef entity, ref MemberOf group, ref int data) =>
            {
                group = new MemberOf(entity.Index % 4);
                data = entity.Index + 100;
            }).Invoke();
        }
        Assert.That(Collect(world, all).Count, Is.EqualTo(entities.Length));
        foreach (Entity entity in entities)
        {
            Assert.That(world.Get<MemberOf>(entity).Value, Is.EqualTo(entity.Index % 4));
            Assert.That(world.Get<int>(entity, payload), Is.EqualTo(entity.Index + 100));
        }
        for (int group = 0; group < 4; group++)
        {
            Assert.That(Collect(world, world.WhereAll(new MemberOf(group))).Count,
                Is.EqualTo(entities.Count(entity => entity.Index % 4 == group)));
        }
        AssertHomogeneous(world);
    }

    [Test]
    public void ExplicitRegistrationsAndSeveralGroupingComponentsStayIndependent()
    {
        using var world = CreateWorld(out ComponentId first);
        ComponentId second = world.Components.Register<MemberOf>(new SchemaId(81_002), group: true);
        var data = new EntityData();
        data.Add(first, new MemberOf(1));
        data.Add(second, new MemberOf(2));
        Entity entity = world.Create(data);
        Query selected = world.WhereAll(first, new MemberOf(1)).WhereAll(second, new MemberOf(2));
        Assert.That(Collect(world, selected), Is.EqualTo(new[] { entity }));
        world.ForEach(in selected, second, static (ref MemberOf group) => group = new MemberOf(3)).Invoke();
        Assert.That(Collect(world, selected), Is.Empty);
        Assert.That(Collect(world, world.WhereAll(first, new MemberOf(1)).WhereAll(second, new MemberOf(3))),
            Is.EqualTo(new[] { entity }));
        Assert.That(world.Get<MemberOf>(entity, first).Value, Is.EqualTo(1));
        AssertHomogeneous(world);
    }

    [Test]
    public void QueryFiltersComposeWithTagsAndStructuralTransitions()
    {
        using var world = CreateWorld(out ComponentId member);
        ComponentId paused = world.Components.Register<Paused>(new SchemaId(81_002));
        ComponentId payload = world.Components.Register<int>(new SchemaId(81_003));
        Entity first = world.Create(member, new MemberOf(1));
        Entity second = world.Create(member, new MemberOf(1));
        world.Add(second, paused);
        Query query = world.WhereAll(new MemberOf(1)).WhereNone(paused);
        Assert.That(Collect(world, query), Is.EqualTo(new[] { first }));
        world.Add(first, payload, 42);
        Assert.That(Collect(world, query), Is.EqualTo(new[] { first }));
        Assert.That(world.Get<int>(first, payload), Is.EqualTo(42));
        world.Remove(first, payload);
        world.Remove(second, paused);
        Assert.That(Collect(world, query), Is.EquivalentTo(new[] { first, second }));
        AssertHomogeneous(world);
    }

    [Test]
    public void EntityDataAndBatchWritesUseTheSameGrouping()
    {
        using var world = CreateWorld(out ComponentId member);
        var data = new EntityData();
        data.Add(member, new MemberOf(1));
        Entity first = world.Create(data);
        Entity second = world.Create(member);
        world.Set(second, data);
        Assert.That(Collect(world, world.WhereAll(new MemberOf(1))), Is.EquivalentTo(new[] { first, second }));
        Entity[] entities = [first, second];
        world.Set(entities, member, new MemberOf(2));
        Assert.That(Collect(world, world.WhereAll(new MemberOf(2))), Is.EquivalentTo(entities));
        world.Remove(first, member);
        world.Add(first, data);
        data.AddOrSet(member, new MemberOf(3));
        world.AddOrSet(second, data);
        Assert.That(Collect(world, world.WhereAll(new MemberOf(1))), Is.EqualTo(new[] { first }));
        Assert.That(Collect(world, world.WhereAll(new MemberOf(3))), Is.EqualTo(new[] { second }));
        AssertHomogeneous(world);
    }

    [Test]
    public void EntityListAndPredicateWritesRegroupOnlyTheirTargets()
    {
        using var world = CreateWorld(out ComponentId member);
        Entity first = world.Create(member, new MemberOf(1));
        Entity second = world.Create(member, new MemberOf(1));
        Query all = world.WhereAll(member);
        Entity[] selection = [second];
        world.ForEach(selection, in all, static (ref MemberOf group) => group = new MemberOf(2)).Invoke();
        Assert.That(Collect(world, world.WhereAll(new MemberOf(1))), Is.EqualTo(new[] { first }));
        world.Where(in all, static (in MemberOf group) => group.Value == 2)
            .ForEach(static (ref MemberOf group) => group = new MemberOf(3)).Invoke();
        Assert.That(Collect(world, world.WhereAll(new MemberOf(3))), Is.EqualTo(new[] { second }));
        AssertHomogeneous(world);
    }

    [Test]
    public void GroupChangesNotifyFilteredChangeOperationsWithoutFalseRemovals()
    {
        using var world = CreateWorld(out ComponentId member);
        ComponentId health = world.Components.Register<int>(new SchemaId(81_002));
        Entity entity = world.Create(member, new MemberOf(1));
        world.Add(entity, health, 100);
        Query query = world.WhereAll(new MemberOf(1));
        var changes = new List<(Entity, ChangeKind)>();
        var observer = world.ForEachChange<int>(in query, health,
            (Entity current, ChangeKind change, ref int value) => changes.Add((current, change)));
        observer.Invoke();
        changes.Clear();
        world.GetRef<MemberOf>(entity) = new MemberOf(2);
        observer.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entity, ChangeKind.Removed) }));
        changes.Clear();
        world.Set(entity, new MemberOf(1));
        observer.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entity, ChangeKind.Added) }));
        changes.Clear();
        observer.Invoke();
        Assert.That(changes, Is.Empty);
    }

    [Test]
    public void ChangedCallbackWritesAlsoRegroupOnTheNextOperation()
    {
        using var world = CreateWorld(out ComponentId member);
        Entity entity = world.Create(member, new MemberOf(1));
        var observer = world.ForEachChange<MemberOf>(member, static (Entity current, ChangeKind change, ref MemberOf value) =>
        {
            if (change != ChangeKind.Removed)
            {
                value = new MemberOf(2);
            }
        });
        observer.Invoke();
        Assert.That(Locate(world, entity).GroupingDirty, Is.True);
        Assert.That(Collect(world, world.WhereAll(new MemberOf(2))), Is.EqualTo(new[] { entity }));
    }

    [Test]
    public void ThrowingCallbacksLeaveDirtyWorkForTheNextOperationAndReleaseTheLease()
    {
        using var world = CreateWorld(out ComponentId member);
        Entity entity = world.Create(member, new MemberOf(1));
        Query query = world.WhereAll(member);
        Assert.That(() => world.ForEach(in query, static (ref MemberOf value) => ChangeThenFail(ref value)).Invoke(), Throws.InvalidOperationException);
        Assert.That(Collect(world, world.WhereAll(new MemberOf(2))), Is.EqualTo(new[] { entity }));
        Assert.That(world.Destroy(entity), Is.True);
    }

    [Test]
    public void DestroyAndIndexReuseDoNotLoseDataOrChangeIdentity()
    {
        using var world = CreateWorld(out ComponentId member);
        Entity old = world.Create(member, new MemberOf(1));
        world.GetRef<MemberOf>(old) = new MemberOf(2);
        world.Destroy(old);
        Entity replacement = world.Create(member, new MemberOf(3));
        Assert.That(world.IsAlive(old), Is.False);
        Assert.That(Collect(world, world.WhereAll(new MemberOf(2))), Is.Empty);
        Assert.That(Collect(world, world.WhereAll(new MemberOf(3))), Is.EqualTo(new[] { replacement }));
        Assert.That(world.IsAlive(replacement), Is.True);
        AssertHomogeneous(world);
    }

    [Test]
    public void GroupingSupportsDefaultValuesAndEmptyTags()
    {
        using var world = CreateWorld(out ComponentId member);
        ComponentId tag = world.Components.Register<Paused>(new SchemaId(81_002), group: true);
        Entity first = world.Create(member);
        Entity second = world.Create([member, tag]);
        Assert.That(Collect(world, world.WhereAll(default(MemberOf))), Is.EquivalentTo(new[] { first, second }));
        Assert.That(Collect(world, world.WhereAll(default(Paused))), Is.EqualTo(new[] { second }));
        world.Set(first, new MemberOf(4));
        world.GetRef<MemberOf>(first) = default;
        Assert.That(Collect(world, world.WhereAll(default(MemberOf))), Is.EquivalentTo(new[] { first, second }));
        AssertHomogeneous(world);
    }

    [Test]
    public void RegistryIsSharedButGroupingStorageBelongsToEachWorld()
    {
        var registry = new ComponentRegistry();
        ComponentId member = registry.Register<MemberOf>(new SchemaId(81_001), group: true);
        using var first = new World(registry);
        using var second = new World(registry);
        Entity left = first.Create(member, new MemberOf(1));
        Entity right = second.Create(member, new MemberOf(2));
        first.GetRef<MemberOf>(left) = new MemberOf(3);
        Assert.That(Collect(second, second.WhereAll(new MemberOf(2))), Is.EqualTo(new[] { right }));
        Assert.That(Locate(first, left).GroupingDirty, Is.True);
        Assert.That(Collect(first, first.WhereAll(new MemberOf(3))), Is.EqualTo(new[] { left }));
        AssertHomogeneous(first);
        AssertHomogeneous(second);
    }

    [Test]
    [SuppressMessage("Security", "CA5394", Justification = "A fixed seed makes the state-machine test reproducible; randomness has no security role.")]
    public void RandomTransitionsMatchAnIndependentValueModel()
    {
        using var world = CreateWorld(out ComponentId member);
        ComponentId payload = world.Components.Register<int>(new SchemaId(81_002));
        var model = new Dictionary<Entity, MemberOf>();
        // Fixed input makes relocation and swap-back failures reproducible.
        var random = new Random(5309);
        for (int step = 0; step < 1200; step++)
        {
            int operation = model.Count == 0 ? 0 : random.Next(5);
            if (operation == 0)
            {
                var group = new MemberOf(random.Next(8));
                Entity created = world.Create(member, group);
                model.Add(created, group);
            }
            else
            {
                Entity entity = model.Keys.ElementAt(random.Next(model.Count));
                var group = new MemberOf(random.Next(8));
                switch (operation)
                {
                    case 1:
                        world.GetRef<MemberOf>(entity) = group;
                        model[entity] = group;
                        break;
                    case 2:
                        world.Set(entity, member, group);
                        model[entity] = group;
                        break;
                    case 3:
                        world.AddOrSet(entity, payload, step);
                        break;
                    case 4:
                        world.Destroy(entity);
                        model.Remove(entity);
                        break;
                }
            }
            if (step % 19 != 0)
            {
                continue;
            }
            for (int group = 0; group < 8; group++)
            {
                Assert.That(Collect(world, world.WhereAll(new MemberOf(group))), Is.EquivalentTo(
                    model.Where(pair => pair.Value.Value == group).Select(pair => pair.Key)));
            }
            AssertHomogeneous(world);
        }
    }

    [Test]
    public void GroupedRegistrationPreservesConstraintSelectedVisitorRoutes()
    {
        using var world = CreateWorld(out ComponentId member);
        var visitor = new UnmanagedVisitor();
        Assert.That(world.Components.TryVisit(member, visitor), Is.True);
        Assert.That(visitor.Visited, Is.True);
        ComponentId named = world.Components.Register<NamedGroup>(new SchemaId(81_002), group: true);
        Entity first = world.Create(named, new NamedGroup("Squad"));
        Entity second = world.Create(named, new NamedGroup("squad"));
        Assert.That(Collect(world, world.WhereAll(new NamedGroup("Squad"))), Is.EqualTo(new[] { first }));
        Assert.That(Collect(world, world.WhereAll(new NamedGroup("squad"))), Is.EqualTo(new[] { second }));
        AssertHomogeneous(world);
    }

    [Test]
    public void QueryWideStructuralMovesPreserveGroupsTagsAndValues()
    {
        using var world = CreateWorld(out ComponentId member);
        ComponentId payload = world.Components.Register<int>(new SchemaId(81_002));
        ComponentId tag = world.Components.Register<Paused>(new SchemaId(81_003));
        var entities = new Entity[Chunk.MaxCapacity * 2 + 17];
        world.Create([member, tag], entities);
        Query all = world.WhereAll(member);
        world.ForEachEntity(in all, static (EntityRef entity, ref MemberOf group) => group = new MemberOf(entity.Index % 3)).Invoke();
        Assert.That(world.Add(in all, payload), Is.EqualTo(entities.Length));
        Assert.That(world.Remove(in all, payload), Is.EqualTo(entities.Length));
        for (int group = 0; group < 3; group++)
        {
            Query selected = world.WhereAll(new MemberOf(group)).WhereAll(tag);
            Assert.That(Collect(world, selected), Is.EquivalentTo(entities.Where(entity => entity.Index % 3 == group)));
        }
        AssertHomogeneous(world);
    }

    [Test]
    public void UnrelatedStructuralTransitionsDoNotReportGroupingValueChanges()
    {
        using var world = CreateWorld(out ComponentId member);
        ComponentId payload = world.Components.Register<int>(new SchemaId(81_002));
        ComponentId second = world.Components.Register<MemberOf>(new SchemaId(81_003), group: true);
        Entity entity = world.Create(member, new MemberOf(1));
        var changes = new List<ChangeKind>();
        var observer = world.ForEachChange(member,
            (Entity _, ChangeKind kind, ref MemberOf _) => changes.Add(kind));
        observer.Invoke();
        changes.Clear();

        world.Add(entity, payload, 10);
        observer.Invoke();
        world.Add(entity, second, new MemberOf(2));
        observer.Invoke();
        world.Remove(entity, second);
        observer.Invoke();
        world.Remove(entity, payload);
        observer.Invoke();
        Assert.That(changes, Is.Empty);
        Assert.That(Collect(world, world.WhereAll(new MemberOf(1))), Is.EqualTo(new[] { entity }));
        AssertHomogeneous(world);
    }

    [Test]
    public void ParallelEntityRefWritesPublishDirtyFlagsAfterJoiningWorkers()
    {
        using var world = CreateWorld(out ComponentId member);
        var entities = new Entity[Chunk.MaxCapacity * 3];
        world.Create(member, entities.Length, entities);
        Query all = world.WhereAll(member);
        var update = world.ForEachEntityParallel(in all,
            static (EntityRef entity) => entity.GetRef<MemberOf>() = new MemberOf(entity.Index % 5), workerCount: 4);
        update.Invoke();
        for (int group = 0; group < 5; group++)
        {
            Assert.That(Collect(world, world.WhereAll(new MemberOf(group))),
                Is.EquivalentTo(entities.Where(entity => entity.Index % 5 == group)));
        }
        AssertHomogeneous(world);
    }

    [Test]
    public void FailedEqualityRetainsPendingWorkAndDoesNotPreventDisposal()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<FragileGroup>(new SchemaId(81_001), group: true);
        Entity entity = world.Create(id, new FragileGroup(1));
        Query selected = world.WhereAll(new FragileGroup(1));
        world.GetRef<FragileGroup>(entity) = new FragileGroup(2);
        FragileGroup.Fail = true;
        try
        {
            Assert.That(() => world.IsAlive(entity), Throws.InvalidOperationException);
        }
        finally
        {
            FragileGroup.Fail = false;
        }
        Assert.That(Collect(world, selected), Is.Empty);
        Assert.That(world.Get<FragileGroup>(entity).Value, Is.EqualTo(2));
        world.GetRef<FragileGroup>(entity) = new FragileGroup(3);
        FragileGroup.Fail = true;
        try
        {
            Assert.That(() => world.Dispose(), Throws.Nothing);
        }
        finally
        {
            FragileGroup.Fail = false;
        }
    }

    [Test]
    public void CopyIntoEntityDataAndDirectReadsResolvePendingGrouping()
    {
        using var world = CreateWorld(out ComponentId member);
        Entity entity = world.Create(member, new MemberOf(1));
        world.GetRef<MemberOf>(entity, member) = new MemberOf(2);
        var snapshot = new EntityData();
        snapshot.AddOrSet(world, entity);
        Assert.That(snapshot.Get<MemberOf>(member).Value, Is.EqualTo(2));
        Assert.That(Locate(world, entity).GroupingDirty, Is.False);
        world.GetRef<MemberOf>(entity) = new MemberOf(3);
        Assert.That(world.Get<MemberOf>(entity, member).Value, Is.EqualTo(3));
        Assert.That(Locate(world, entity).GroupingDirty, Is.False);
    }

    [Test]
    public void RejectsGroupingQueriesForOrdinaryRegistrationsAndInvalidQueries()
    {
        using var world = new World();
        ComponentId id = world.Components.Register<MemberOf>(new SchemaId(81_001));
        Assert.That(() => world.Components.Register<MemberOf>(new SchemaId(81_001), group: true), Throws.InvalidOperationException);
        Assert.That(() => world.WhereAll(id, new MemberOf(1)), Throws.ArgumentException);
        Assert.That(() => default(Query).WhereAll(new MemberOf(1)), Throws.InvalidOperationException);
    }

    [Test]
    public void GroupedChangeFilterReportsExitAndReentryWhilePlainObserversKeepMembership()
    {
        using var world = CreateWorld(out ComponentId member);
        Entity entity = world.Create(member, new MemberOf(1));
        Query selected = world.WhereAll(new MemberOf(1));
        var filtered = new List<ChangeKind>();
        var plain = new List<ChangeKind>();
        var selection = world.ForEachChange<MemberOf>(in selected, member,
            (Entity current, ChangeKind change, ref MemberOf value) => filtered.Add(change));
        var all = world.ForEachChange<MemberOf>(member,
            (Entity current, ChangeKind change, ref MemberOf value) => plain.Add(change));
        selection.Invoke();
        all.Invoke();
        filtered.Clear();
        plain.Clear();
        world.GetRef<MemberOf>(entity) = new MemberOf(2);
        selection.Invoke();
        all.Invoke();
        Assert.That(filtered, Is.EqualTo(new[] { ChangeKind.Removed }));
        Assert.That(plain, Is.EqualTo(new[] { ChangeKind.Modified }));
        filtered.Clear();
        plain.Clear();
        world.Set(entity, new MemberOf(1));
        selection.Invoke();
        all.Invoke();
        Assert.That(filtered, Is.EqualTo(new[] { ChangeKind.Added }));
        Assert.That(plain, Is.EqualTo(new[] { ChangeKind.Modified }));
    }

    [Test]
    public void EqualWritesReuseTheArchetypeAndUnrelatedWritesDoNotMarkGrouping()
    {
        using var world = CreateWorld(out ComponentId member);
        ComponentId payload = world.Components.Register<int>(new SchemaId(81_002));
        Entity entity = world.Create(member, new MemberOf(1));
        world.Add(entity, payload, 10);
        Query selected = world.WhereAll(new MemberOf(1)).WhereAll(payload);
        Chunk original = Locate(world, entity);
        world.ForEach(in selected, static (ref int data) => data++).Invoke();
        Assert.That(original.GroupingDirty, Is.False);
        int count = world.Archetypes.Count;
        world.GetRef<MemberOf>(entity) = new MemberOf(1);
        Assert.That(Collect(world, selected), Is.EqualTo(new[] { entity }));
        Assert.That(Locate(world, entity), Is.SameAs(original));
        Assert.That(world.Archetypes.Count, Is.EqualTo(count));
        Assert.That(original.GroupingDirty, Is.False);
    }

    [Test]
    public void PredicateSetAndQueryDestroyUseCurrentGroupMembership()
    {
        using var world = CreateWorld(out ComponentId member);
        Entity first = world.Create(member, new MemberOf(1));
        Entity second = world.Create(member, new MemberOf(2));
        Query all = world.WhereAll(member);
        world.Where(in all, static (in MemberOf group) => group.Value == 1).Set(new MemberOf(3)).Invoke();
        Query third = world.WhereAll(new MemberOf(3));
        Assert.That(Collect(world, third), Is.EqualTo(new[] { first }));
        Assert.That(world.Destroy(in third), Is.EqualTo(1));
        Assert.That(world.IsAlive(first), Is.False);
        Assert.That(world.IsAlive(second), Is.True);
        AssertHomogeneous(world);
    }

    private static void ChangeThenFail(ref MemberOf value)
    {
        value = new MemberOf(2);
        throw new InvalidOperationException("Expected test exception.");
    }

    private static World CreateWorld(out ComponentId member)
    {
        var world = new World();
        member = world.Components.Register<MemberOf>(new SchemaId(81_001), group: true);
        return world;
    }

    private static Chunk Locate(World world, Entity entity)
    {
        Assert.That(world.TryResolveEntityLocation(entity, out Chunk chunk, out _), Is.True);
        return chunk;
    }

    private static List<Entity> Collect(World world, Query query)
    {
        var result = new List<Entity>();
        world.ForEachEntity(in query, (EntityRef entity) => result.Add(entity)).Invoke();
        return result;
    }

    private static void AssertHomogeneous(World world)
    {
        foreach (Archetype archetype in world.Archetypes)
        {
            foreach (GroupedComponentRow group in archetype.GroupingRows)
            {
                for (int index = 0; index < archetype.ActiveChunkCount; index++)
                {
                    Chunk chunk = archetype.GetActiveChunk(index);
                    Assert.That(chunk.GroupingDirty, Is.False);
                    for (int slot = 0; slot < chunk.Count; slot++)
                    {
                        Assert.That(group.Grouping.Matches(chunk.RawComponentRows[group.Row], slot, group.ValueId), Is.True);
                    }
                }
            }
        }
    }

    internal sealed class UnmanagedVisitor : IUnmanagedComponentVisitor
    {
        internal bool Visited;
        public void Visit<T>(ComponentId id) where T : unmanaged => Visited = true;
    }

    internal readonly record struct NamedGroup(string Name);

    internal readonly record struct FragileGroup(int Value) : IEquatable<FragileGroup>
    {
        internal static bool Fail;
        [SuppressMessage("Design", "CA1065", Justification = "Deliberately fails user equality to verify that pending grouping work can recover.")]
        public bool Equals(FragileGroup other)
        {
            if (Fail)
            {
                throw new InvalidOperationException("Expected equality failure.");
            }
            return Value == other.Value;
        }
        public override int GetHashCode() => Value;
    }

    internal record struct MemberOf(int Value);
    internal readonly record struct Paused;

    internal struct CountMembers : IForEach
    {
        internal int Count;
        public void Invoke(in MemberOf group) => Count++;
    }

    internal readonly record struct ChangeGroup(int Value) : IForEach
    {
        public void Invoke(ref MemberOf group) => group = new MemberOf(Value);
    }

    private struct PositionWithoutEquality { internal int Value { get; set; } }
}
