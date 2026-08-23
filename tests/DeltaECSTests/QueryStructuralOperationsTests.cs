using System;
using System.Collections.Generic;
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
        world.CreateBatch(new[] { PositionId }, first);
        world.CreateBatch(new[] { PositionId, HealthId }, second);

        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        var added = world.AddComponents(in query, new[] { VelocityId, extraA, extraB, extraC });

        Assert.That(added, Is.EqualTo(first.Length + second.Length));
        Assert.That(world.IsAlive(existingTarget), Is.True);
        foreach (var entity in first)
        {
            AssertAddedComponents(world, entity, VelocityId, extraA, extraB, extraC);
        }

        foreach (var entity in second)
        {
            AssertAddedComponents(world, entity, VelocityId, extraA, extraB, extraC);
            Assert.That(world.TryGetComponent<Health>(entity, HealthId, out _), Is.True);
        }

        var removed = world.RemoveComponents(in query, new[] { VelocityId, extraA, extraB, extraC });
        Assert.That(removed, Is.EqualTo(first.Length + second.Length + 1));
        Assert.That(world.TryGetComponent<Velocity>(existingTarget, VelocityId, out _), Is.False);
        foreach (var entity in first)
        {
            AssertRemovedComponents(world, entity, VelocityId, extraA, extraB, extraC);
        }

        foreach (var entity in second)
        {
            AssertRemovedComponents(world, entity, VelocityId, extraA, extraB, extraC);
            Assert.That(world.TryGetComponent<Health>(entity, HealthId, out _), Is.True);
        }
    }

    [Test]
    public void QueryDestroy_UpdatesGenerationsFreeRecordsAndAliveCount()
    {
        var layouts = CreateLayouts();
        var world = new World(layouts, chunkCapacity: 2);
        var destroyed = new Entity[5];
        var survivor = world.Create(new[] { HealthId });
        world.CreateBatch(new[] { PositionId }, destroyed);

        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
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
        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        var foreignQuery = foreign.CreateQuery(QuerySpec.ForComponents(PositionId));
        var invalid = default(Query);

        Assert.Throws<ArgumentException>(() => world.AddComponents(in invalid, new[] { VelocityId }));
        Assert.Throws<ArgumentException>(() => world.RemoveComponents(in foreignQuery, new[] { VelocityId }));
        Assert.Throws<ArgumentException>(() => world.Destroy(in foreignQuery));
        using var scope = world.OpenQuery(in query);
        Assert.Throws<InvalidOperationException>(() => world.AddComponents(in query, new[] { VelocityId }));
        Assert.That(world.IsAlive(entity), Is.True);
    }

    [Test]
    public void EmptyMatchingQuery_ReturnsZero_AndLeavesWorldUnchanged()
    {
        var layouts = CreateLayouts();
        var world = new World(layouts);
        var entity = world.Create(new[] { PositionId });
        var query = world.CreateQuery(QuerySpec.ForComponents(VelocityId));
        var aliveBefore = world.AliveEntityCount;
        var archetypeVersionBefore = world.ArchetypeVersion;
        var worldTickBefore = world.WorldTick;

        Assert.That(world.AddComponents(in query, new[] { HealthId }), Is.EqualTo(0));
        Assert.That(world.RemoveComponents(in query, new[] { PositionId }), Is.EqualTo(0));
        Assert.That(world.Destroy(in query), Is.EqualTo(0));

        Assert.That(world.AliveEntityCount, Is.EqualTo(aliveBefore));
        Assert.That(world.ArchetypeVersion, Is.EqualTo(archetypeVersionBefore));
        Assert.That(world.WorldTick, Is.EqualTo(worldTickBefore));
        Assert.That(world.IsAlive(entity), Is.True);
        Assert.That(world.TryGetComponent<Position>(entity, PositionId, out _), Is.True);
    }

    [Test]
    public void QueryStructuralOperations_ExplicitNoOps_PreserveEntitiesAndRecords()
    {
        var layouts = CreateLayouts();
        var world = new World(layouts);
        var entity = world.Create(new[] { PositionId });
        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        var versionBefore = world.ArchetypeVersion;

        Assert.That(world.AddComponents(in query, new[] { PositionId }), Is.EqualTo(0));
        Assert.That(world.RemoveComponents(in query, new[] { VelocityId }), Is.EqualTo(0));

        Assert.That(world.ArchetypeVersion, Is.EqualTo(versionBefore));
        Assert.That(world.IsAlive(entity), Is.True);
        Assert.That(world.TryGetComponent<Position>(entity, PositionId, out _), Is.True);
        Assert.That(world.TryGetComponent<Velocity>(entity, VelocityId, out _), Is.False);
    }

    [Test]
    public void QueryBlockTransition_PreservesReadWriteChangeTracking()
    {
        var layouts = CreateLayouts();
        var world = new World(layouts, chunkCapacity: 2);
        var entities = new Entity[3];
        world.CreateBatch(new[] { PositionId }, entities);
        var spec = QuerySpec.ForComponents(PositionId);
        var query = world.CreateQuery(in spec);
        var readPosition = query.AccessRead(PositionId);
        var writePosition = query.AccessWrite(PositionId);

        Assert.That(world.AddComponents(in query, new[] { VelocityId }), Is.EqualTo(entities.Length));

        var readBefore = world.WorldTick;
        var readChunkId = -1;
        using (var scope = world.OpenQuery(in query))
        {
            var readAccess = scope.Bind(readPosition);
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var chunk = chunks.Current;
                    if (chunk.SlotCount == 0)
                    {
                        continue;
                    }

                    readChunkId = chunk.GlobalChunkId;
                    var slots = chunk.Slots;
                    _ = slots.Get(readAccess);
                }
            }
        }

        Assert.That(readChunkId, Is.GreaterThanOrEqualTo(0));
        Assert.That(world.HasChangedSince(readChunkId, PositionId, readBefore), Is.False);

        var writeChunkId = -1;
        using (var scope = world.OpenQuery(in query))
        {
            var writeAccess = scope.Bind(writePosition);
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var chunk = chunks.Current;
                    if (chunk.SlotCount == 0)
                    {
                        continue;
                    }

                    writeChunkId = chunk.GlobalChunkId;
                    var slots = chunk.Slots;
                    _ = slots.Get(writeAccess);
                }
            }
        }

        Assert.That(writeChunkId, Is.EqualTo(readChunkId));
        Assert.That(world.HasChangedSince(writeChunkId, PositionId, readBefore), Is.True);
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

    private static List<WeakReference<ReferenceComponent>> CreateAndDestroyReferenceRows()
    {
        var layouts = CreateLayouts();
        var referenceId = layouts.Register(typeof(ReferenceComponent), new SchemaId(30));
        var markerId = layouts.Register(typeof(RefMarker), new SchemaId(31));
        var world = new World(layouts, chunkCapacity: 2);
        var entities = new Entity[4];
        world.CreateBatch(new[] { referenceId }, entities);
        var weakReferences = new List<WeakReference<ReferenceComponent>>();
        var values = new ReferenceComponent[entities.Length];
        for (var i = 0; i < entities.Length; i++)
        {
            var value = new ReferenceComponent { Value = i + 10 };
            values[i] = value;
            weakReferences.Add(new WeakReference<ReferenceComponent>(value));
            Assert.That(world.SetComponent(entities[i], referenceId, value), Is.True);
        }

        var query = world.CreateQuery(QuerySpec.ForComponents(referenceId));
        Assert.That(world.AddComponents(in query, new[] { markerId }), Is.EqualTo(entities.Length));
        for (var i = 0; i < entities.Length; i++)
        {
            Assert.That(world.TryGetComponent<ReferenceComponent>(entities[i], referenceId, out var actual), Is.True);
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

    private static void AssertAddedComponents(World world, Entity entity, ComponentId velocityId, params ComponentId[] ids)
    {
        Assert.That(world.TryGetComponent<Velocity>(entity, velocityId, out _), Is.True);
        foreach (var id in ids)
        {
            Assert.That(world.TryGetComponent<int>(entity, id, out _), Is.True);
        }
    }

    private static void AssertRemovedComponents(World world, Entity entity, ComponentId velocityId, params ComponentId[] ids)
    {
        Assert.That(world.TryGetComponent<Velocity>(entity, velocityId, out _), Is.False);
        foreach (var id in ids)
        {
            Assert.That(world.TryGetComponent<int>(entity, id, out _), Is.False);
        }
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
