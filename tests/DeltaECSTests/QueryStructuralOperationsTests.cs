using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Delta.ECS;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed class QueryStructuralOperationsTests
{
    private static readonly ComponentId PositionId = new(0);
    private static readonly ComponentId VelocityId = new(1);
    private static readonly ComponentId HealthId = new(2);
    [Test]
    public void QueryAddRemoveUsesSnapshotMultipleArchetypes()
    {
        var layouts = CreateLayouts();
        var extraA = layouts.Register<int>(new SchemaId(20));
        var extraB = layouts.Register<int>(new SchemaId(21));
        var extraC = layouts.Register<int>(new SchemaId(22));
        using var world = new World(layouts);

        var first = new Entity[3];
        var second = new Entity[2];
        var existingTarget = world.Create(new[] { PositionId, VelocityId, extraA, extraB, extraC });
        world.Create(new[] { PositionId }, first);
        world.Create(new[] { PositionId, HealthId }, second);

        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId));
        var added = world.Add(in query, new[] { VelocityId, extraA, extraB, extraC });

        Assert.That(added, Is.EqualTo(first.Length + second.Length));
        Assert.That(world.IsAlive(existingTarget), Is.True);
        foreach (var entity in first)
        {
            AssertAddedComponents(world, entity, VelocityId, extraA, extraB, extraC);
        }

        foreach (var entity in second)
        {
            AssertAddedComponents(world, entity, VelocityId, extraA, extraB, extraC);
            Assert.That(world.TryGet<Health>(entity, HealthId, out _), Is.True);
        }

        var removed = world.Remove(in query, new[] { VelocityId, extraA, extraB, extraC });
        Assert.That(removed, Is.EqualTo(first.Length + second.Length + 1));
        Assert.That(world.TryGet<Velocity>(existingTarget, VelocityId, out _), Is.False);
        foreach (var entity in first)
        {
            AssertRemovedComponents(world, entity, VelocityId, extraA, extraB, extraC);
        }

        foreach (var entity in second)
        {
            AssertRemovedComponents(world, entity, VelocityId, extraA, extraB, extraC);
            Assert.That(world.TryGet<Health>(entity, HealthId, out _), Is.True);
        }
    }

    [Test]
    public void QueryDestroyUpdatesGenerationsFreeRecordsAndAliveCount()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts);
        var destroyed = new Entity[5];
        var survivor = world.Create(new[] { HealthId });
        world.Create(new[] { PositionId }, destroyed);

        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId));
        Assert.That(world.Destroy(in query), Is.EqualTo(destroyed.Length));
        Assert.That(world.AliveEntityCount, Is.EqualTo(1));
        Assert.That(world.IsAlive(survivor), Is.True);
        foreach (var entity in destroyed)
        {
            Assert.That(world.IsAlive(entity), Is.False);
            Assert.That(world.Destroy(entity), Is.False);
        }

        var replacement = world.Create(new[] { PositionId });
        Assert.That(Array.Exists(destroyed, entity => entity.Index == replacement.Index && entity.Generation != replacement.Generation), Is.True);
        Assert.That(replacement.Generation, Is.GreaterThan(0));
        Assert.That(world.AliveEntityCount, Is.EqualTo(2));
    }

    [Test]
    public void ScalarTransitionReusesDestroyedChunkAndKeepsStaleHandlesInvalid()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts);
        var live = world.Create(new[] { PositionId });
        var destroyed = new Entity[2];
        world.Create(new[] { VelocityId }, destroyed);

        var query = world.CreateQuery(QuerySpec.WhereAll(VelocityId));
        Assert.That(world.Destroy(in query), Is.EqualTo(destroyed.Length));

        Assert.That(world.Add(live, new[] { VelocityId }), Is.True);
        Assert.That(world.IsAlive(live), Is.True);
        foreach (var entity in destroyed)
        {
            Assert.That(world.IsAlive(entity), Is.False);
        }
    }

    [Test]
    public void QueryStructuralOperationsRejectDefaultForeignAndActiveLeaseHandles()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts);
        using var foreign = new World(layouts);
        var entity = world.Create(new[] { PositionId });
        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId));
        var foreignQuery = foreign.CreateQuery(QuerySpec.WhereAll(PositionId));
        var invalid = default(Query);

        Assert.Throws<ArgumentException>(() => world.Add(in invalid, new[] { VelocityId }));
        Assert.Throws<ArgumentException>(() => world.Remove(in foreignQuery, new[] { VelocityId }));
        Assert.Throws<ArgumentException>(() => world.Destroy(in foreignQuery));
        World callbackWorld = world;
        Assert.Throws<InvalidOperationException>(() => world.ForEachEntity(
            in query,
            ref callbackWorld,
            static (ref World owner, Entity current, in Position _) => owner.Destroy(current)));
        Assert.That(world.IsAlive(entity), Is.True);
    }

    [Test]
    public void QueryHandleBecomesInvalidWhenItsWorldIsDisposed()
    {
        var layouts = CreateLayouts();
        var world = new World(layouts);
        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId));

        Assert.That(query.IsValid, Is.True);
        world.Dispose();

        Assert.That(query.IsValid, Is.False);
    }

    [Test]
    public void EmptyMatchingQueryReturnsZeroAndLeavesWorldUnchanged()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts);
        var entity = world.Create(new[] { PositionId });
        var query = world.CreateQuery(QuerySpec.WhereAll(VelocityId));
        var aliveBefore = world.AliveEntityCount;

        Assert.That(world.Add(in query, new[] { HealthId }), Is.EqualTo(0));
        Assert.That(world.Remove(in query, new[] { PositionId }), Is.EqualTo(0));
        Assert.That(world.Destroy(in query), Is.EqualTo(0));

        Assert.That(world.AliveEntityCount, Is.EqualTo(aliveBefore));
        Assert.That(world.IsAlive(entity), Is.True);
        Assert.That(world.TryGet<Position>(entity, PositionId, out _), Is.True);
    }

    [Test]
    public void QueryStructuralOperationsExplicitNoOpsPreserveEntitiesAndRecords()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts);
        var entity = world.Create(new[] { PositionId });
        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId));

        Assert.That(world.Add(in query, new[] { PositionId }), Is.EqualTo(0));
        Assert.That(world.Remove(in query, new[] { VelocityId }), Is.EqualTo(0));

        Assert.That(world.IsAlive(entity), Is.True);
        Assert.That(world.TryGet<Position>(entity, PositionId, out _), Is.True);
        Assert.That(world.TryGet<Velocity>(entity, VelocityId, out _), Is.False);
    }

    [Test]
    public void QueryRangeCopyPreservesReferenceRowsAndDestroyReleasesThem()
    {
        var weakReferences = CreateAndDestroyReferenceRows();
        ForceCollection();
        foreach (var weakReference in weakReferences)
        {
            Assert.That(weakReference.TryGetTarget(out _), Is.False);
        }
    }

    [Test]
    public void QueryAddAdoptsFullChunksAndFillsExistingTargetTail()
    {
        var layouts = CreateLayouts();
        var markerId = layouts.Register<byte>(new SchemaId(40));
        using var world = new World(layouts);

        var existingTarget = new Entity[2];
        world.Create(new[] { PositionId, markerId }, existingTarget.Length, existingTarget);
        var source = new Entity[1_026];
        world.Create(new[] { PositionId }, source.Length, source);
        for (int index = 0; index < source.Length; index++)
        {
            Assert.That(world.Set(source[index], PositionId, new Position { X = index + 10, Y = -index }), Is.True);
        }
        Assert.That(world.TryGetComponentStamp(source[0], PositionId, out var sourceStamp), Is.True);

        var sourceQuery = world.CreateQuery(new QuerySpec(
            new[] { PositionId },
            Array.Empty<ComponentId>(),
            new[] { markerId }));
        var targetQuery = world.CreateQuery(QuerySpec.WhereAll(PositionId, markerId));
        int existingTargetChunkId = CollectChunkIds(targetQuery, world).Single();
        var sourceChunkIds = CollectChunkIds(sourceQuery, world);
        Assert.That(sourceChunkIds.Count, Is.EqualTo(3));

        Assert.That(world.Add(in sourceQuery, new[] { markerId }), Is.EqualTo(source.Length));

        var targetChunkIds = CollectChunkIds(targetQuery, world);
        Assert.That(targetChunkIds, Does.Contain(existingTargetChunkId));
        Assert.That(targetChunkIds, Does.Contain(sourceChunkIds.Min()));
        Assert.That(targetChunkIds, Does.Not.Contain(sourceChunkIds.Max()));
        Assert.That(world.AliveEntityCount, Is.EqualTo(existingTarget.Length + source.Length));

        foreach (var entity in source)
        {
            Assert.That(world.TryGet<Position>(entity, PositionId, out var position), Is.True);
            Assert.That(position.X, Is.EqualTo(entity.Index - existingTarget.Length + 10));
            Assert.That(world.TryGet<byte>(entity, markerId, out var marker), Is.True);
            Assert.That(marker, Is.EqualTo(0));
        }
        Assert.That(world.TryGetComponentStamp(source[0], PositionId, out var migratedStamp), Is.True);
        Assert.That(migratedStamp, Is.EqualTo(sourceStamp));

        var observed = targetQuery.Cached.MatchingChunkPlans().Length == 0
            ? 0
            : CountMatchingEntities(targetQuery);
        Assert.That(observed, Is.EqualTo(existingTarget.Length + source.Length));
        Assert.That(world.Destroy(in targetQuery), Is.EqualTo(existingTarget.Length + source.Length));
        Assert.That(world.AliveEntityCount, Is.Zero);
    }

    [Test]
    public void QueryRemoveAdoptsFullChunksAndFillsExistingTargetTail()
    {
        var layouts = CreateLayouts();
        var markerId = layouts.Register<byte>(new SchemaId(41));
        using var world = new World(layouts);

        var existingTarget = new Entity[2];
        world.Create(new[] { PositionId }, existingTarget.Length, existingTarget);
        var source = new Entity[1_026];
        world.Create(new[] { PositionId, markerId }, source.Length, source);
        for (int index = 0; index < source.Length; index++)
        {
            Assert.That(world.Set(source[index], PositionId, new Position { X = index + 20, Y = index }), Is.True);
        }

        var sourceQuery = world.CreateQuery(QuerySpec.WhereAll(PositionId, markerId));
        var targetQuery = world.CreateQuery(new QuerySpec(
            new[] { PositionId },
            Array.Empty<ComponentId>(),
            new[] { markerId }));
        int existingTargetChunkId = CollectChunkIds(targetQuery, world).Single();
        var sourceChunkIds = CollectChunkIds(sourceQuery, world);
        Assert.That(sourceChunkIds.Count, Is.EqualTo(3));

        Assert.That(world.Remove(in sourceQuery, new[] { markerId }), Is.EqualTo(source.Length));

        var targetChunkIds = CollectChunkIds(targetQuery, world);
        Assert.That(targetChunkIds, Does.Contain(existingTargetChunkId));
        Assert.That(targetChunkIds, Does.Contain(sourceChunkIds.Min()));
        Assert.That(targetChunkIds, Does.Not.Contain(sourceChunkIds.Max()));
        foreach (var entity in source)
        {
            Assert.That(world.TryGet<Position>(entity, PositionId, out var position), Is.True);
            Assert.That(position.X, Is.EqualTo(entity.Index - existingTarget.Length + 20));
            Assert.That(world.TryGet<byte>(entity, markerId, out _), Is.False);
        }
    }

    private static List<WeakReference<ReferenceComponent>> CreateAndDestroyReferenceRows()
    {
        var layouts = CreateLayouts();
        var referenceId = layouts.Register<ReferenceComponent>(new SchemaId(30));
        var markerId = layouts.Register<RefMarker>(new SchemaId(31));
        using var world = new World(layouts);
        var entities = new Entity[4];
        world.Create(new[] { referenceId }, entities);
        var weakReferences = new List<WeakReference<ReferenceComponent>>();
        var values = new ReferenceComponent[entities.Length];
        for (var i = 0; i < entities.Length; i++)
        {
            var value = new ReferenceComponent { Value = i + 10 };
            values[i] = value;
            weakReferences.Add(new WeakReference<ReferenceComponent>(value));
            Assert.That(world.Set(entities[i], referenceId, value), Is.True);
        }

        var query = world.CreateQuery(QuerySpec.WhereAll(referenceId));
        Assert.That(world.Add(in query, new[] { markerId }), Is.EqualTo(entities.Length));
        for (var i = 0; i < entities.Length; i++)
        {
            Assert.That(world.TryGet<ReferenceComponent>(entities[i], referenceId, out var actual), Is.True);
            Assert.That(actual, Is.SameAs(values[i]));
        }

        Assert.That(world.Destroy(in query), Is.EqualTo(entities.Length));
        return weakReferences;
    }

    private static ComponentLayoutRegistry CreateLayouts()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.Register<Position>(new SchemaId(1));
        layouts.Register<Velocity>(new SchemaId(2));
        layouts.Register<Health>(new SchemaId(3));
        return layouts;
    }

    private static HashSet<int> CollectChunkIds(in Query query, World world)
    {
        var ids = new HashSet<int>();
        foreach (ref readonly ChunkPlan chunk in query.Cached.MatchingChunkPlans())
        {
            ids.Add(chunk.Chunk.GlobalId);
        }

        return ids;
    }

    private static int CountMatchingEntities(in Query query)
    {
        var count = 0;
        foreach (ref readonly ChunkPlan chunk in query.Cached.MatchingChunkPlans())
        {
            count += chunk.Chunk.Count;
        }

        return count;
    }

    private static void AssertAddedComponents(World world, Entity entity, ComponentId velocityId, params ComponentId[] ids)
    {
        Assert.That(world.TryGet<Velocity>(entity, velocityId, out _), Is.True);
        foreach (var id in ids)
        {
            Assert.That(world.TryGet<int>(entity, id, out _), Is.True);
        }
    }

    private static void AssertRemovedComponents(World world, Entity entity, ComponentId velocityId, params ComponentId[] ids)
    {
        Assert.That(world.TryGet<Velocity>(entity, velocityId, out _), Is.False);
        foreach (var id in ids)
        {
            Assert.That(world.TryGet<int>(entity, id, out _), Is.False);
        }
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
