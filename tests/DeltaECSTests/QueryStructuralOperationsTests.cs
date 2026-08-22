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
    private static readonly TagId ActiveTag = new(77);

    [Test]
    public void QueryAddRemove_UsesSnapshot_MultipleArchetypes_AndPreservesTags()
    {
        var layouts = CreateLayouts();
        var extraA = layouts.Register<int>(new SchemaId(20));
        var extraB = layouts.Register<int>(new SchemaId(21));
        var extraC = layouts.Register<int>(new SchemaId(22));
        var world = new World(layouts, chunkCapacity: 2);

        var first = new Entity[3];
        var second = new Entity[2];
        var existingTarget = world.Create(new[] { PositionId, VelocityId, extraA, extraB, extraC });
        world.CreateBatch(new[] { PositionId }, first);
        world.CreateBatch(new[] { PositionId, HealthId }, second);
        world.AddTag(first[0], ActiveTag);

        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        var added = world.AddComponents(in query, new[] { VelocityId, extraA, extraB, extraC });

        Assert.That(added, Is.EqualTo(first.Length + second.Length));
        Assert.That(world.IsAlive(existingTarget), Is.True);
        Assert.That(world.HasTag(first[0], ActiveTag), Is.True);
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
        Assert.That(world.HasTag(first[0], ActiveTag), Is.True);
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
    public void TaggedQueryStructuralOperations_FallbackToExactPartialHoles()
    {
        var layouts = CreateLayouts();
        var world = new World(layouts, chunkCapacity: 4);
        var entities = new Entity[4];
        world.CreateBatch(new[] { PositionId }, entities);
        world.AddTag(entities[0], ActiveTag);
        world.AddTag(entities[2], ActiveTag);
        var spec = new QuerySpec(
            new[] { PositionId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(),
            new[] { ActiveTag }, Array.Empty<TagId>(), Array.Empty<TagId>());
        var query = world.CreateQuery(in spec);

        Assert.That(world.AddComponents(in query, new[] { VelocityId }), Is.EqualTo(2));
        Assert.That(world.TryGetComponent<Velocity>(entities[0], VelocityId, out _), Is.True);
        Assert.That(world.TryGetComponent<Velocity>(entities[2], VelocityId, out _), Is.True);
        Assert.That(world.TryGetComponent<Velocity>(entities[1], VelocityId, out _), Is.False);
        Assert.That(world.TryGetComponent<Velocity>(entities[3], VelocityId, out _), Is.False);
        Assert.That(world.HasTag(entities[0], ActiveTag), Is.True);
        Assert.That(world.HasTag(entities[2], ActiveTag), Is.True);

        Assert.That(world.Destroy(in query), Is.EqualTo(2));
        Assert.That(world.AliveEntityCount, Is.EqualTo(2));
        Assert.That(world.IsAlive(entities[0]), Is.False);
        Assert.That(world.IsAlive(entities[2]), Is.False);
        Assert.That(world.IsAlive(entities[1]), Is.True);
        Assert.That(world.IsAlive(entities[3]), Is.True);
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
        var activeLeaseState = 0;
        Assert.Throws<InvalidOperationException>(() => world.Query(in query, ref activeLeaseState,
            (ref int _, ref QueryChunkCursor _) => world.AddComponents(in query, new[] { VelocityId })));
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
    public void QueryStructuralOperations_ExplicitNoOps_PreserveEntitiesRecordsAndTags()
    {
        var layouts = CreateLayouts();
        var world = new World(layouts);
        var entity = world.Create(new[] { PositionId });
        world.AddTag(entity, ActiveTag);
        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        var versionBefore = world.ArchetypeVersion;

        Assert.That(world.AddComponents(in query, new[] { PositionId }), Is.EqualTo(0));
        Assert.That(world.RemoveComponents(in query, new[] { VelocityId }), Is.EqualTo(0));

        Assert.That(world.ArchetypeVersion, Is.EqualTo(versionBefore));
        Assert.That(world.IsAlive(entity), Is.True);
        Assert.That(world.HasTag(entity, ActiveTag), Is.True);
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
        var readPosition = query.Access<Position>(PositionId, AccessMode.Read);
        var writePosition = query.Access<Position>(PositionId, AccessMode.Write);

        Assert.That(world.AddComponents(in query, new[] { VelocityId }), Is.EqualTo(entities.Length));

        var readChunkId = -1;
        var readBefore = world.WorldTick;
        var readState = new ChangeTrackingCursorState(readPosition);
        world.Query(in query, ref readState, static (ref ChangeTrackingCursorState state, ref QueryChunkCursor cursor) =>
        {
            if (cursor.SlotCount == 0)
            {
                return;
            }

            state.ChunkId = cursor.GlobalChunkId;
            _ = cursor.Get(state.ReadBinding);
        });
        readChunkId = readState.ChunkId;

        Assert.That(readChunkId, Is.GreaterThanOrEqualTo(0));
        Assert.That(world.HasChangedSince(readChunkId, PositionId, readBefore), Is.False);

        var writeChunkId = -1;
        var writeState = new ChangeTrackingCursorState(writePosition);
        world.Query(in query, ref writeState, static (ref ChangeTrackingCursorState state, ref QueryChunkCursor cursor) =>
        {
            if (cursor.SlotCount == 0)
            {
                return;
            }

            state.ChunkId = cursor.GlobalChunkId;
            _ = cursor.Get(state.WriteBinding);
        });
        writeChunkId = writeState.ChunkId;

        Assert.That(writeChunkId, Is.EqualTo(readChunkId));
        Assert.That(world.HasChangedSince(writeChunkId, PositionId, readBefore), Is.True);
    }

    private sealed class ChangeTrackingCursorState
    {
        public ChangeTrackingCursorState(ReadRequest binding)
        {
            ReadBinding = binding;
        }

        public ChangeTrackingCursorState(WriteRequest binding)
        {
            WriteBinding = binding;
        }

        public ReadRequest ReadBinding { get; }
        public WriteRequest WriteBinding { get; }
        public int ChunkId { get; set; } = -1;
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
        var referenceId = layouts.Register<ReferenceComponent>(new SchemaId(30));
        var markerId = layouts.Register<RefMarker>(new SchemaId(31));
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
        layouts.Register<Position>(new SchemaId(1));
        layouts.Register<Velocity>(new SchemaId(2));
        layouts.Register<Health>(new SchemaId(3));
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
