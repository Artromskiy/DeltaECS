using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Delta.ECS;

namespace Delta.ECS.Tests;

[TestFixture]
public sealed class QueryStructuralOperationsTests
{
    private static readonly ComponentId PositionId = new(0);
    private static readonly ComponentId VelocityId = new(1);
    private static readonly ComponentId HealthId = new(2);
    [Test]
    public void QueryAddRemove_UsesSnapshot_MultipleArchetypes()
    {
        var layouts = CreateLayouts();
        var extraA = layouts.Register(typeof(int), new SchemaId(20));
        var extraB = layouts.Register(typeof(int), new SchemaId(21));
        var extraC = layouts.Register(typeof(int), new SchemaId(22));
        var world = new World(layouts, chunkCapacity: 2);

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
    public void QueryDestroy_UpdatesGenerationsFreeRecordsAndAliveCount()
    {
        var layouts = CreateLayouts();
        var world = new World(layouts, chunkCapacity: 2);
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
    public void QueryStructuralOperations_Reject_DefaultForeignAndActiveLeaseHandles()
    {
        var layouts = CreateLayouts();
        var world = new World(layouts);
        var foreign = new World(layouts);
        var entity = world.Create(new[] { PositionId });
        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId));
        var foreignQuery = foreign.CreateQuery(QuerySpec.WhereAll(PositionId));
        var invalid = default(Query);

        Assert.Throws<ArgumentException>(() => world.Add(in invalid, new[] { VelocityId }));
        Assert.Throws<ArgumentException>(() => world.Remove(in foreignQuery, new[] { VelocityId }));
        Assert.Throws<ArgumentException>(() => world.Destroy(in foreignQuery));
        using var scope = world.BeginScope(in query);
        Assert.Throws<InvalidOperationException>(() => world.Add(in query, new[] { VelocityId }));
        Assert.That(world.IsAlive(entity), Is.True);
    }

    [Test]
    public void QueryHandle_BecomesInvalidWhenItsWorldIsDisposed()
    {
        var layouts = CreateLayouts();
        var world = new World(layouts);
        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId));

        Assert.That(query.IsValid, Is.True);
        world.Dispose();

        Assert.That(query.IsValid, Is.False);
    }

    [Test]
    public void EmptyMatchingQuery_ReturnsZero_AndLeavesWorldUnchanged()
    {
        var layouts = CreateLayouts();
        var world = new World(layouts);
        var entity = world.Create(new[] { PositionId });
        var query = world.CreateQuery(QuerySpec.WhereAll(VelocityId));
        var aliveBefore = world.AliveEntityCount;
        var archetypeVersionBefore = world.ArchetypeVersion;

        Assert.That(world.Add(in query, new[] { HealthId }), Is.EqualTo(0));
        Assert.That(world.Remove(in query, new[] { PositionId }), Is.EqualTo(0));
        Assert.That(world.Destroy(in query), Is.EqualTo(0));

        Assert.That(world.AliveEntityCount, Is.EqualTo(aliveBefore));
        Assert.That(world.ArchetypeVersion, Is.EqualTo(archetypeVersionBefore));
        Assert.That(world.IsAlive(entity), Is.True);
        Assert.That(world.TryGet<Position>(entity, PositionId, out _), Is.True);
    }

    [Test]
    public void QueryStructuralOperations_ExplicitNoOps_PreserveEntitiesAndRecords()
    {
        var layouts = CreateLayouts();
        var world = new World(layouts);
        var entity = world.Create(new[] { PositionId });
        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId));
        var versionBefore = world.ArchetypeVersion;

        Assert.That(world.Add(in query, new[] { PositionId }), Is.EqualTo(0));
        Assert.That(world.Remove(in query, new[] { VelocityId }), Is.EqualTo(0));

        Assert.That(world.ArchetypeVersion, Is.EqualTo(versionBefore));
        Assert.That(world.IsAlive(entity), Is.True);
        Assert.That(world.TryGet<Position>(entity, PositionId, out _), Is.True);
        Assert.That(world.TryGet<Velocity>(entity, VelocityId, out _), Is.False);
    }

    [Test]
    public void QueryRangeCopy_PreservesReferenceRows_AndDestroyReleasesThem()
    {
        var weakReferences = CreateAndDestroyReferenceRows();
        ForceCollection();
        foreach (var weakReference in weakReferences)
        {
            Assert.That(weakReference.TryGetTarget(out _), Is.False);
        }
    }

    [Test]
    public void QueryAdd_AdoptsFullChunks_AndFillsExistingTargetTail()
    {
        var layouts = CreateLayouts();
        var markerId = layouts.Register(typeof(byte), new SchemaId(40));
        var world = new World(layouts, chunkCapacity: 4);

        var existingTarget = world.Create(new[] { PositionId, markerId }, 2);
        var source = world.Create(new[] { PositionId }, 6);
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
        Assert.That(sourceChunkIds.Count, Is.EqualTo(2));

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

        {
            using var scope = world.BeginScope(in targetQuery);
            int observed = 0;
            var chunks = scope.Chunks;
            while (chunks.MoveNext())
            {
                observed += chunks.Current.SlotCount;
            }

            Assert.That(observed, Is.EqualTo(existingTarget.Length + source.Length));
        }
        Assert.That(world.Destroy(in targetQuery), Is.EqualTo(existingTarget.Length + source.Length));
        Assert.That(world.AliveEntityCount, Is.Zero);
    }

    [Test]
    public void QueryRemove_AdoptsFullChunks_AndFillsExistingTargetTail()
    {
        var layouts = CreateLayouts();
        var markerId = layouts.Register(typeof(byte), new SchemaId(41));
        var world = new World(layouts, chunkCapacity: 4);

        var existingTarget = world.Create(new[] { PositionId }, 2);
        var source = world.Create(new[] { PositionId, markerId }, 6);
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
        Assert.That(sourceChunkIds.Count, Is.EqualTo(2));

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
        var referenceId = layouts.Register(typeof(ReferenceComponent), new SchemaId(30));
        var markerId = layouts.Register(typeof(RefMarker), new SchemaId(31));
        var world = new World(layouts, chunkCapacity: 2);
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
        layouts.Register(typeof(Position), new SchemaId(1));
        layouts.Register(typeof(Velocity), new SchemaId(2));
        layouts.Register(typeof(Health), new SchemaId(3));
        return layouts;
    }

    private static HashSet<int> CollectChunkIds(in Query query, World world)
    {
        var ids = new HashSet<int>();
        using var scope = world.BeginScope(in query);
        var chunks = scope.Chunks;
        while (chunks.MoveNext())
        {
            ids.Add(chunks.Current.GlobalChunkId);
        }

        return ids;
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
