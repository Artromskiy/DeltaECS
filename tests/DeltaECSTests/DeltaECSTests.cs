using System;
using System.Collections.Generic;
using NUnit.Framework;
using Delta.ECS;

namespace Delta.ECS.Tests;

[TestFixture]
public sealed class DeltaECSDeliveryTests
{
    private static readonly ComponentId PositionId = new ComponentId(0);
    private static readonly ComponentId VelocityId = new ComponentId(1);
    private static readonly ComponentId HealthId = new ComponentId(2);
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Entity_Create_Destroy_RecyclesGeneration()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);

        var e1 = world.Create(new[] { PositionId, VelocityId });
        Assert.True(world.IsAlive(e1));

        var destroyed = world.Destroy(e1);
        Assert.True(destroyed);
        Assert.False(world.IsAlive(e1));

        var e2 = world.Create(new[] { PositionId, VelocityId });
        Assert.AreEqual(e1.Index, e2.Index);
        Assert.AreNotEqual(e1.Generation, e2.Generation);
    }

    [Test]
    public void ArchetypeHandle_Canonicalizes_And_Creates_Entities()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);

        var first = world.GetOrCreateArchetype(VelocityId, PositionId, PositionId);
        var version = world.ArchetypeVersion;
        var same = world.GetOrCreateArchetype(PositionId, VelocityId);

        Assert.That(first.IsValid, Is.True);
        Assert.That(first, Is.EqualTo(same));
        Assert.That(world.ArchetypeVersion, Is.EqualTo(version));

        var entities = new Entity[2];
        Assert.That(world.Create(first, entities), Is.EqualTo(2));
        Assert.That(world.Create(first).IsAlive, Is.True);
        Assert.That(world.AliveEntityCount, Is.EqualTo(3));
    }

    [Test]
    public void ArchetypeHandle_Rejects_Invalid_And_Foreign_World_Handles()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);
        var foreignWorld = new World(layouts);
        var handle = world.GetOrCreateArchetype(PositionId);

        Assert.Throws<ArgumentException>(() => foreignWorld.Create(handle));
        Assert.Throws<ArgumentException>(() => world.Create(ArchetypeHandle.Invalid));
        Assert.Throws<ArgumentException>(() => foreignWorld.Create(handle, new Entity[1]));
        Assert.Throws<ArgumentException>(() => world.Create(ArchetypeHandle.Invalid, new Entity[1]));
        Assert.That(ArchetypeHandle.Invalid.IsValid, Is.False);
    }

    [Test]
    public void DenseBatch_Create_Destroy_Succeeds_And_Query()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);

        var requested = 2_000;
        var created = new Entity[requested];
        world.Create(new[] { PositionId, VelocityId }, created);
        Assert.AreEqual(requested, world.AliveEntityCount);

        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId, VelocityId));
        var position = query.AccessWrite(PositionId);
        var velocity = query.AccessWrite(VelocityId);
        var sum = 0L;
        using (var scope = world.OpenQuery(in query))
        {
            var preparedPosition = position;
            var preparedVelocity = velocity;
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var slots = chunks.Current.Slots;
                    var pos = slots.GetRow(preparedPosition);
                    var vel = slots.GetRow(preparedVelocity);
                    while (slots.MoveNext())
                    {
                        ref var p = ref pos.Ref<Position>(slots);
                        ref var v = ref vel.Ref<Velocity>(slots);
                        p = new Position { X = slots.CurrentIndex, Y = slots.CurrentIndex * 2f };
                        v = new Velocity { X = 1, Y = 1 };
                        sum += (long)p.X + (long)v.Y;
                    }
                }
            }
        }

        Assert.Greater(sum, 0);

        world.Destroy(created);
        Assert.AreEqual(0, world.AliveEntityCount);
    }

    [Test]
    public void QueryScope_Uses_Independent_Archetype_Chunk_And_Slot_Iterators()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 2);
        var positionOnly = new Entity[3];
        var positionVelocity = new Entity[4];
        world.Create(new[] { PositionId }, positionOnly);
        world.Create(new[] { PositionId, VelocityId }, positionVelocity);

        var expected = new Dictionary<Entity, int>();
        var nextValue = 0;
        foreach (var entity in positionOnly)
        {
            expected[entity] = nextValue;
            world.Set(entity, PositionId, new Position { X = nextValue++ });
        }

        foreach (var entity in positionVelocity)
        {
            expected[entity] = nextValue;
            world.Set(entity, PositionId, new Position { X = nextValue++ });
        }

        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId));
        var position = query.AccessWrite(PositionId);
        var before = world.WorldTick;
        var archetypeCount = 0;
        var chunkCount = 0;
        var slotCount = 0;
        var writtenChunks = new List<int>();

        using (var scope = world.OpenQuery(in query))
        {
            var preparedPosition = position;
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                archetypeCount++;
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    chunkCount++;
                    var chunk = chunks.Current;
                    writtenChunks.Add(chunk.GlobalChunkId);
                    var entities = chunk.Entities;
                    var slots = chunk.Slots;
                    var positions = slots.GetRow(preparedPosition);
                    var expectedSlot = 0;
                    while (slots.MoveNext())
                    {
                        Assert.That(slots.CurrentIndex, Is.EqualTo(expectedSlot++));
                        var entity = entities[slots.CurrentIndex];
                        ref var value = ref positions.Ref<Position>(slots);
                        Assert.That(value.X, Is.EqualTo(expected[entity]));
                        value.X++;
                        slotCount++;
                    }
                }
            }

            Assert.Throws<InvalidOperationException>(() => world.Destroy(positionOnly[0]));
        }

        Assert.That(archetypeCount, Is.EqualTo(2));
        Assert.That(chunkCount, Is.EqualTo(4));
        Assert.That(slotCount, Is.EqualTo(7));
        foreach (var chunkId in writtenChunks)
        {
            Assert.That(world.HasChangedSince(chunkId, PositionId, before), Is.True);
        }
    }

    [Test]
    public void QueryScopeChunksFlattenMatchingArchetypesWithoutChangingSlotSemantics()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        using var world = new World(layouts, chunkCapacity: 2);
        var positionOnly = new Entity[3];
        var positionVelocity = new Entity[4];
        world.Create([PositionId], positionOnly);
        world.Create([PositionId, VelocityId], positionVelocity);

        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId));
        var position = query.AccessWrite(PositionId);
        var before = world.WorldTick;
        var chunkCount = 0;
        var slotCount = 0;
        var archetypeIds = new HashSet<int>();

        using (var scope = world.OpenQuery(in query))
        {
            var preparedPosition = position;
            var chunks = scope.Chunks;
            while (chunks.MoveNext())
            {
                chunkCount++;
                var chunk = chunks.Current;
                archetypeIds.Add(chunk.ArchetypeId);
                var slots = chunk.Slots;
                var positions = slots.GetRow(preparedPosition);
                while (slots.MoveNext())
                {
                    positions.Ref<Position>(slots).X++;
                    slotCount++;
                }
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(archetypeIds, Has.Count.EqualTo(2));
            Assert.That(chunkCount, Is.EqualTo(4));
            Assert.That(slotCount, Is.EqualTo(7));
        });

        using var verifyScope = world.OpenQuery(in query);
        var verifyChunks = verifyScope.Chunks;
        while (verifyChunks.MoveNext())
        {
            Assert.That(world.HasChangedSince(verifyChunks.Current.GlobalChunkId, PositionId, before), Is.True);
        }
    }

    [Test]
    public void NonGenericAccessRequest_BindsRows_AndTracksOnlyWrites()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 4);
        var entity = world.Create(new[] { PositionId, VelocityId });
        world.Set(entity, PositionId, new Position { X = 1, Y = 2 });
        world.Set(entity, VelocityId, new Velocity { X = 3, Y = 4 });

        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId, VelocityId));
        var position = query.AccessWrite(PositionId);
        var velocity = query.AccessRead(VelocityId);
        var before = world.WorldTick;

        using (var scope = world.OpenQuery(in query))
        {
            var write = position;
            var read = velocity;
            var archetypes = scope.Archetypes;
            Assert.That(archetypes.MoveNext(), Is.True);
            var chunks = archetypes.Current.Chunks;
            Assert.That(chunks.MoveNext(), Is.True);
            var slots = chunks.Current.Slots;
            var positions = slots.GetRow(write);
            var velocities = slots.GetRow(read);
            Assert.That(slots.MoveNext(), Is.True);
            ref var p = ref positions.Ref<Position>(slots);
            ref readonly var v = ref velocities.Ref<Velocity>(slots);
            p.X += v.X;
        }

        Assert.That(world.TryGet<Position>(entity, PositionId, out var result), Is.True);
        Assert.That(result.X, Is.EqualTo(4));
        Assert.That(world.HasChangedSince(0, PositionId, before), Is.True);
        Assert.That(world.HasChangedSince(0, VelocityId, before), Is.False);
    }

    [Test]
    public void LeaseEntities_AndComponentRows_StayAligned_OnBothLeaseSurfaces()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 4);
        var created = new Entity[5];
        world.Create(new[] { PositionId, VelocityId }, created);
        var expected = new Dictionary<Entity, int>();
        for (var i = 0; i < created.Length; i++)
        {
            expected.Add(created[i], i);
            world.Set(created[i], PositionId, new Position { X = i, Y = -i });
            world.Set(created[i], VelocityId, new Velocity { X = i + 10, Y = i + 20 });
        }

        var spec = QuerySpec.WhereAll(PositionId, VelocityId);
        var query = world.CreateQuery(in spec);
        var position = query.AccessRead(PositionId);
        var velocity = query.AccessRead(VelocityId);
        var denseLeaseCount = 0;
        using (var scope = world.OpenQuery(in query))
        {
            var preparedPosition = position;
            var preparedVelocity = velocity;
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var chunk = chunks.Current;
                    var entities = chunk.Entities;
                    var slots = chunk.Slots;
                    var positions = slots.GetRow(preparedPosition);
                    var velocities = slots.GetRow(preparedVelocity);
                    while (slots.MoveNext())
                    {
                        var entity = entities[slots.CurrentIndex];
                        Assert.That(expected.ContainsKey(entity), Is.True);
                        Assert.That(positions.Ref<Position>(slots).X, Is.EqualTo(expected[entity]));
                        Assert.That(velocities.Ref<Velocity>(slots).X, Is.EqualTo(expected[entity] + 10));
                        denseLeaseCount++;
                    }
                }
            }
        }

        var alignedCount = 0;
        using (var scope = world.OpenQuery(in query))
        {
            var preparedPosition = position;
            var preparedVelocity = velocity;
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var chunk = chunks.Current;
                    ReadOnlySpan<Entity> entities = chunk.Entities;
                    var slots = chunk.Slots;
                    var positions = slots.GetRow(preparedPosition);
                    var velocities = slots.GetRow(preparedVelocity);
                    while (slots.MoveNext())
                    {
                        var entity = entities[slots.CurrentIndex];
                        Assert.That(expected.ContainsKey(entity), Is.True);
                        Assert.That(positions.Ref<Position>(slots).X, Is.EqualTo(expected[entity]));
                        Assert.That(velocities.Ref<Velocity>(slots).X, Is.EqualTo(expected[entity] + 10));
                        alignedCount++;
                    }
                }
            }
        }

        Assert.That(denseLeaseCount, Is.EqualTo(created.Length));
        Assert.That(alignedCount, Is.EqualTo(created.Length));
    }

    [Test]
    public void ForwardIteration_HandlesEmptySingleAndFullChunks()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 4);
        var spec = QuerySpec.WhereAll(PositionId);
        var emptyQuery = world.CreateQuery(in spec);
        var position = emptyQuery.AccessRead(PositionId);
        var emptyChunkCount = 0;
        using (var scope = world.OpenQuery(in emptyQuery))
        {
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    emptyChunkCount++;
                }
            }
        }
        Assert.That(emptyChunkCount, Is.EqualTo(0));

        var singleWorld = new World(layouts, chunkCapacity: 4);
        var single = singleWorld.Create(new[] { PositionId });
        var singleQuery = singleWorld.CreateQuery(QuerySpec.WhereAll(PositionId));
        var singleChunkCount = 0;
        using (var scope = singleWorld.OpenQuery(in singleQuery))
        {
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var chunk = chunks.Current;
                    singleChunkCount++;
                    Assert.That(chunk.SlotCount, Is.EqualTo(1));
                    Assert.That(chunk.Entities[0], Is.EqualTo(single));
                }
            }
        }
        Assert.That(singleChunkCount, Is.EqualTo(1));

        var created = new Entity[4];
        world.Create(new[] { PositionId }, created);
        for (var i = 0; i < created.Length; i++)
        {
            world.Set(created[i], PositionId, new Position { X = i, Y = 0 });
        }

        var fullChunkCount = 0;
        using (var scope = world.OpenQuery(in emptyQuery))
        {
            var preparedPosition = position;
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var chunk = chunks.Current;
                    Assert.That(chunk.SlotCount, Is.EqualTo(4));
                    var entities = chunk.Entities;
                    var slots = chunk.Slots;
                    var positions = slots.GetRow(preparedPosition);
                    var expectedSlot = 0;
                    while (slots.MoveNext())
                    {
                        Assert.That(slots.CurrentIndex, Is.EqualTo(expectedSlot++));
                        Assert.That(entities[slots.CurrentIndex].IsAlive, Is.True);
                        Assert.That(positions.Ref<Position>(slots).X, Is.EqualTo(slots.CurrentIndex));
                        fullChunkCount++;
                    }
                }
            }
        }

        Assert.That(fullChunkCount, Is.EqualTo(created.Length));
    }

    [Test]
    public void ImmediateBatchTransition_CompletesBeforeReturn_AndIsIdempotent()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 2);
        var entities = new Entity[5];
        world.Create(new[] { PositionId }, entities);

        Assert.That(world.Add(new[] { VelocityId }, entities), Is.EqualTo(entities.Length));
        foreach (var entity in entities)
        {
            Assert.That(world.TryGet<Velocity>(entity, VelocityId, out _), Is.True);
        }

        Assert.That(world.Add(new[] { VelocityId }, entities), Is.EqualTo(0));
        Assert.That(world.Remove(new[] { VelocityId }, entities), Is.EqualTo(entities.Length));
        foreach (var entity in entities)
        {
            Assert.That(world.TryGet<Velocity>(entity, VelocityId, out _), Is.False);
        }
    }

    [Test]
    public void Query_Cache_Remains_Valid_After_New_Archetype_Appears()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);

        var query = new QuerySpec(new[] { PositionId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>());

        var initial = new Entity[1];
        world.Create(new[] { PositionId }, initial);
        var before = 0;
        before = CountDenseQuery(world, query);

        var withVelocity = new Entity[2];
        world.Create(new[] { PositionId, VelocityId }, withVelocity);

        var after = 0;
        after = CountDenseQuery(world, query);

        Assert.AreEqual(before + 2, after, $"before={before}, after={after}, alive={world.AliveEntityCount}");
    }

    [Test]
    public void QuerySpec_Is_Immutable_After_Creation()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);

        var all = new[] { PositionId };
        var query = new QuerySpec(all, Array.Empty<ComponentId>(), Array.Empty<ComponentId>());
        all[0] = VelocityId;

        world.Create(new[] { PositionId });

        var count = CountDenseQuery(world, query);
        Assert.AreEqual(1, count);
    }

    [Test]
    public void QuerySpec_ComponentMasks_Deduplicate_Filter_And_Enumerate_In_Order()
    {
        var query = new QuerySpec(
            new[] { new ComponentId(129), new ComponentId(7), PositionId, new ComponentId(193), new ComponentId(65), new ComponentId(7), ComponentId.Invalid },
            new[] { VelocityId, VelocityId, ComponentId.Invalid },
            new[] { HealthId, HealthId });
        var equivalent = new QuerySpec(
            new[] { new ComponentId(65), new ComponentId(193), new ComponentId(129), PositionId, new ComponentId(7) },
            new[] { VelocityId },
            new[] { HealthId });

        Assert.That(query, Is.EqualTo(equivalent));
        Assert.That(query.GetHashCode(), Is.EqualTo(equivalent.GetHashCode()));
        Assert.That(query.AllMask.Count, Is.EqualTo(5));
        Assert.That(query.AnyMask.Contains(VelocityId), Is.True);
        Assert.That(query.NoneMask.Contains(HealthId), Is.True);

        var expected = new[] { 0, 7, 65, 129, 193 };
        var index = 0;
        foreach (var componentId in query.AllMask)
        {
            Assert.That(index, Is.LessThan(expected.Length));
            Assert.That(componentId.Value, Is.EqualTo(expected[index++]));
        }

        Assert.That(index, Is.EqualTo(expected.Length));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuerySpec(
            new[] { new ComponentId(ComponentMask.Capacity) },
            Array.Empty<ComponentId>(), Array.Empty<ComponentId>()));
    }

    [Test]
    public void ComponentQueryMasks_Match_All_Any_None_And_Combined_Conditions()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);
        world.Create(new[] { PositionId, VelocityId });
        world.Create(new[] { PositionId });
        world.Create(new[] { VelocityId });
        world.Create(new[] { HealthId });

        var all = QuerySpec.WhereAll(PositionId, VelocityId);
        var any = new QuerySpec(
            Array.Empty<ComponentId>(), new[] { HealthId, VelocityId }, Array.Empty<ComponentId>());
        var none = new QuerySpec(
            Array.Empty<ComponentId>(), Array.Empty<ComponentId>(), new[] { HealthId });
        var combined = new QuerySpec(
            new[] { PositionId }, new[] { VelocityId }, new[] { HealthId });

        Assert.That(CountDenseQuery(world, all), Is.EqualTo(1));
        Assert.That(CountDenseQuery(world, any), Is.EqualTo(3));
        Assert.That(CountDenseQuery(world, none), Is.EqualTo(3));
        Assert.That(CountDenseQuery(world, combined), Is.EqualTo(1));
    }

    [Test]
    public void QueryPlan_ComponentRowPlan_Uses_Deterministic_Mask_Order()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);
        world.Create(new[] { PositionId, VelocityId });
        var query = new QuerySpec(
            new[] { VelocityId, PositionId, VelocityId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>());
        var handle = world.CreateQuery(in query);
        var cached = handle.Cached;

        Assert.That(cached.MatchingArchetypes(world).Length, Is.EqualTo(1));
        var rowPlan = cached.ComponentRowIndices(0);
        Assert.That(rowPlan.Length, Is.EqualTo(2));
        Assert.That(rowPlan[0], Is.EqualTo(0));
        Assert.That(rowPlan[1], Is.EqualTo(1));
    }

    [Test]
    public void WriteAccess_Marks_Only_Yielded_Rows_And_ReadBinding_Does_Not_Mark()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 4);
        var entity = world.Create(new[] { PositionId, VelocityId });
        var chunkId = -1;
        var spec = QuerySpec.WhereAll(PositionId);
        var query = world.CreateQuery(in spec);
        var readPosition = query.AccessRead(PositionId);
        var writePosition = query.AccessWrite(PositionId);
        var before = world.WorldTick;

        using (var scope = world.OpenQuery(in query))
        {
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    if (chunkId < 0)
                    {
                        chunkId = chunks.Current.GlobalChunkId;
                    }
                }
            }
        }
        Assert.That(chunkId, Is.GreaterThanOrEqualTo(0));
        Assert.That(world.HasChangedSince(chunkId, PositionId, before), Is.False);
        Assert.That(world.HasChangedSince(chunkId, VelocityId, before), Is.False);

        using (var scope = world.OpenQuery(in query))
        {
            var prepared = writePosition;
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    _ = chunks.Current.Slots.GetRow(prepared);
                }
            }
        }
        var afterWrite = world.WorldTick;
        Assert.That(afterWrite, Is.GreaterThan(before));
        Assert.That(world.HasChangedSince(chunkId, PositionId, before), Is.True);
        Assert.That(world.HasChangedSince(chunkId, VelocityId, before), Is.False);

        using (var scope = world.OpenQuery(in query))
        {
            var prepared = readPosition;
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    _ = chunks.Current.Slots.GetRow(prepared);
                }
            }
        }
        Assert.That(world.HasChangedSince(chunkId, VelocityId, afterWrite), Is.False);

        using (var scope = world.OpenQuery(in query))
        {
            var prepared = writePosition;
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    _ = chunks.Current.Slots.GetRow(prepared);
                }
            }
        }

        Assert.That(world.WorldTick, Is.GreaterThan(afterWrite));
        Assert.That(world.HasChangedSince(chunkId, PositionId, afterWrite), Is.True);
        Assert.That(world.IsAlive(entity), Is.True);
    }

    [Test]
    public void QuerySurface_Uses_The_Renamed_API()
    {
        var assembly = typeof(World).Assembly;
        Assert.That(assembly.GetType("Delta.ECS.QueryAccess"), Is.Null);
        Assert.That(assembly.GetType("Delta.ECS.DenseChunkAccessor"), Is.Null);
        Assert.That(assembly.GetType("Delta.ECS.DenseChunkScope"), Is.Null);

        var publicMethods = typeof(World).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        Assert.That(publicMethods.Any(static method => method.Name == "Execute"), Is.False);
        Assert.That(publicMethods.Any(static method => method.Name == "QueryChunks"), Is.False);

        var publicInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
        Assert.That(typeof(Query).GetMethods(publicInstance).Any(static method => method.Name == "Access" && method.IsGenericMethod), Is.False);
        Assert.That(typeof(QueryScope).GetMethods(publicInstance).Any(static method => method.Name.StartsWith("Bind", StringComparison.Ordinal) && method.IsGenericMethod), Is.False);
        Assert.That(typeof(QuerySlots).GetMethods(publicInstance).Any(static method => method.Name == "Get" && method.IsGenericMethod), Is.False);
        Assert.That(assembly.GetType("Delta.ECS.ReadRow`1"), Is.Null);
        Assert.That(assembly.GetType("Delta.ECS.WriteRow`1"), Is.Null);
    }

    [Test]
    public void AccessRequests_Are_NonGeneric_QueryBound_And_Precisely_Track_Writes()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 2);
        world.Create(new[] { PositionId, VelocityId });
        world.Create(new[] { PositionId, VelocityId, HealthId });

        var spec = QuerySpec.WhereAll(PositionId, VelocityId);
        var query = world.CreateQuery(in spec);
        var position = query.AccessWrite(PositionId);
        var velocity = query.AccessRead(VelocityId);

        Assert.That(query.Cached, Is.Not.Null);

        var cursorRows = 0;
        var writtenChunks = new HashSet<int>();
        using (var scope = world.OpenQuery(in query))
        {
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var chunk = chunks.Current;
                    cursorRows += chunk.SlotCount;
                }
            }
        }
        Assert.That(cursorRows, Is.EqualTo(2));

        var before = world.WorldTick;
        var rows = 0;
        using (var scope = world.OpenQuery(in query))
        {
            var preparedPosition = position;
            var preparedVelocity = velocity;
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var chunk = chunks.Current;
                    var slots = chunk.Slots;
                    var positions = slots.GetRow(preparedPosition);
                    var velocities = slots.GetRow(preparedVelocity);
                    writtenChunks.Add(chunk.GlobalChunkId);
                    while (slots.MoveNext())
                    {
                        ref var row = ref positions.Ref<Position>(slots);
                        row.X += velocities.Ref<Velocity>(slots).X;
                        rows++;
                    }
                }
            }
        }

        Assert.That(rows, Is.EqualTo(2));
        Assert.That(writtenChunks.Count, Is.EqualTo(2));
        foreach (var chunkId in writtenChunks)
        {
            Assert.That(world.HasChangedSince(chunkId, PositionId, before), Is.True);
            Assert.That(world.HasChangedSince(chunkId, VelocityId, before), Is.False);
        }

        var simpleRows = 0;
        using (var scope = world.OpenQuery(in query))
        {
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    simpleRows += chunks.Current.SlotCount;
                }
            }
        }
        Assert.That(simpleRows, Is.EqualTo(2));

        Assert.DoesNotThrow(() => query.AccessRead(PositionId));

        var anyDescription = new QuerySpec(
            new[] { PositionId }, new[] { VelocityId }, Array.Empty<ComponentId>());
        var anyQuery = world.CreateQuery(in anyDescription);
        Assert.Throws<ArgumentException>(() => anyQuery.AccessRead(VelocityId));

        Assert.DoesNotThrow(() => ExecuteDenseWriteAccess(world, query, position));
    }

    [Test]
    public void NonGeneric_AccessBindGet_Uses_RefBoundary_And_Preserves_WriteTracking()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 4);
        world.Create(new[] { PositionId, VelocityId });

        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId, VelocityId));
        ReadAccess readRequest = query.AccessRead(VelocityId);
        WriteAccess writeRequest = query.AccessWrite(PositionId);
        var before = world.WorldTick;
        var sum = 0f;

        using (var scope = world.OpenQuery(in query))
        {
            var read = readRequest;
            var write = writeRequest;
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var slots = chunks.Current.Slots;
                    var positions = slots.GetRow(write);
                    var velocities = slots.GetRow(read);
                    while (slots.MoveNext())
                    {
                        ref var position = ref positions.Ref<Position>(slots);
                        ref readonly var velocity = ref velocities.Ref<Velocity>(slots);
                        position.X += velocity.X + 1;
                        sum += position.X;
                    }
                }
            }
        }

        Assert.That(sum, Is.GreaterThan(0));
        Assert.That(world.HasChangedSince(0, PositionId, before), Is.True);
        Assert.That(world.HasChangedSince(0, VelocityId, before), Is.False);
    }

    [Test]
    public void AccessRequest_Refreshes_Values_For_New_Archetypes()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 2);
        world.Create(new[] { VelocityId });

        var spec = QuerySpec.WhereAll(VelocityId);
        var query = world.CreateQuery(in spec);
        var velocity = query.AccessRead(VelocityId);

        var firstRows = 0;
        using (var scope = world.OpenQuery(in query))
        {
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    firstRows += chunks.Current.SlotCount;
                }
            }
        }

        // The new archetype stores Velocity at physical row 1 instead of row 0.
        world.Create(new[] { PositionId, VelocityId });

        var secondRows = 0;
        using (var scope = world.OpenQuery(in query))
        {
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    secondRows += chunks.Current.SlotCount;
                }
            }
        }

        Assert.That(firstRows, Is.EqualTo(1));
        Assert.That(secondRows, Is.EqualTo(2));
    }

    [Test]
    public void AccessRequests_Reject_Mismatched_Query_And_Foreign_World()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);
        world.Create(new[] { PositionId, VelocityId });
        var spec = QuerySpec.WhereAll(PositionId, VelocityId);
        var query = world.CreateQuery(in spec);
        var otherDescription = QuerySpec.WhereAll(PositionId);
        var otherQuery = world.CreateQuery(in otherDescription);
        var mismatchedBinding = otherQuery.AccessRead(PositionId);

        Assert.Throws<InvalidOperationException>(() => ExecuteDenseReadAccess(world, query, mismatchedBinding));

        var foreignWorld = new World(layouts);
        foreignWorld.Create(new[] { PositionId, VelocityId });
        var foreignQuery = foreignWorld.CreateQuery(QuerySpec.WhereAll(PositionId, VelocityId));
        var foreignBinding = foreignQuery.AccessRead(PositionId);

        Assert.Throws<InvalidOperationException>(() => ExecuteDenseReadAccess(world, query, foreignBinding));

        var defaultBinding = default(ReadAccess);
        Assert.That(defaultBinding.Query, Is.Null);
        Assert.Throws<InvalidOperationException>(() => ExecuteDenseReadAccess(world, query, defaultBinding));
    }

    [Test]
    public void Escaping_ComponentRef_Api_Is_Removed()
    {
        var method = typeof(World).GetMethod("GetComponentRef", new[] { typeof(Entity), typeof(ComponentId) });
        Assert.That(method, Is.Null);
    }

    [Test]
    public void O1_Chunk_Acquisition_Reuses_NonFull_Chunks_Without_Doubling()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 4);

        var initial = new Entity[16];
        world.Create(new[] { PositionId }, initial);

        var baseline = CollectChunkIds(world);

        for (var i = 0; i < 8; i++)
        {
            world.Destroy(initial[i]);
        }

        var afterDestroy = CollectChunkIds(world);
        Assert.LessOrEqual(afterDestroy.Count, baseline.Count);

        var replacement = new Entity[8];
        world.Create(new[] { PositionId }, replacement);
        var afterRecycle = CollectChunkIds(world);

        foreach (var id in afterRecycle)
        {
            Assert.True(baseline.Contains(id), $"Unexpected new chunk id after recycle: {id}");
        }

        Assert.AreEqual(16, world.AliveEntityCount);
        Assert.That(afterRecycle.Count, Is.EqualTo(baseline.Count));
    }

    [Test]
    public void Cached_Ref_Query_Mutates_Rows_Without_Allocating_After_Warmup()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 4);
        var entities = new Entity[5];
        world.Create(new[] { PositionId, VelocityId }, entities);
        for (var i = 0; i < entities.Length; i++)
        {
            world.Set(entities[i], PositionId, new Position { X = i, Y = 0 });
            world.Set(entities[i], VelocityId, new Velocity { X = 1, Y = 2 });
        }

        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId, VelocityId));
        var position = query.AccessWrite(PositionId);
        var velocity = query.AccessRead(VelocityId);
        for (var warmup = 0; warmup < 3; warmup++)
        {
            var warmupSum = 0f;
            RunDenseMovement(world, in query, position, velocity, ref warmupSum);
        }

        for (var i = 0; i < entities.Length; i++)
        {
            world.Set(entities[i], PositionId, new Position { X = i, Y = 0 });
            world.Set(entities[i], VelocityId, new Velocity { X = 1, Y = 2 });
        }

        var sum = 0f;
        RunDenseMovement(world, in query, position, velocity, ref sum);
        RunDenseMovement(world, in query, position, velocity, ref sum);
        var firstMeasuredAfter = GC.GetAllocatedBytesForCurrentThread();
        RunDenseMovement(world, in query, position, velocity, ref sum);
        var after = GC.GetAllocatedBytesForCurrentThread();

        // VSTest's current-thread allocation counter reports a one-time 24-byte
        // host artifact for this isolated sample; the BDN MemoryDiagnoser is the
        // authoritative per-operation allocation gate for the same cached loop.
        Assert.That(after - firstMeasuredAfter, Is.LessThanOrEqualTo(24));
        Assert.That(sum, Is.EqualTo(60f));
        Assert.That(world.TryGet<Position>(entities[0], PositionId, out var actualPosition));
        Assert.That(actualPosition.X, Is.EqualTo(3f));
        Assert.That(actualPosition.Y, Is.EqualTo(6f));
    }

    [Test]
    public void Registry_Deduplicates_EqualSchema_AndRejects_ConflictingLayout()
    {
        var layouts = new ComponentLayoutRegistry();
        var first = layouts.Register(typeof(Position), new SchemaId(10_001));
        var duplicate = layouts.Register(typeof(Position), new SchemaId(10_001));

        Assert.AreEqual(first, duplicate);
        Assert.AreEqual(1, layouts.Count);
        Assert.Throws<InvalidOperationException>(() => layouts.Register(typeof(Velocity), new SchemaId(10_001)));
        Assert.AreEqual(1, layouts.Count);
    }

    [Test]
    public void ArrayRows_ManagedStruct_UsesIndependentVirtualRows_AndCachedIndices()
    {
        var layouts = new ComponentLayoutRegistry();
        var localId = layouts.Register(typeof(NamedRef), new SchemaId(10_101));
        var worldId = layouts.Register(typeof(NamedRef), new SchemaId(10_102));
        var world = new World(layouts, chunkCapacity: 4);
        var query = world.CreateQuery(QuerySpec.WhereAll(localId, worldId));
        var local = query.AccessRead(localId);
        var worldRow = query.AccessRead(worldId);
        var entity = world.Create(new[] { localId, worldId });

        world.Set(entity, localId, new NamedRef { Name = "local", Id = 1 });
        world.Set(entity, worldId, new NamedRef { Name = "world", Id = 2 });

        NamedRef first = default;
        NamedRef second = default;
        var count = 0;
        using (var scope = world.OpenQuery(in query))
        {
            var preparedLocal = local;
            var preparedWorld = worldRow;
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var slots = chunks.Current.Slots;
                    var firstRows = slots.GetRow(preparedLocal);
                    var secondRows = slots.GetRow(preparedWorld);
                    while (slots.MoveNext())
                    {
                        first = firstRows.Ref<NamedRef>(slots);
                        second = secondRows.Ref<NamedRef>(slots);
                        count++;
                    }
                }
            }
        }

        Assert.AreEqual(1, count);
        Assert.AreEqual("local", first.Name);
        Assert.AreEqual("world", second.Name);
        Assert.AreEqual(1, first.Id);
        Assert.AreEqual(2, second.Id);
        Assert.AreNotEqual(first.Name, second.Name);
    }

    [Test]
    public void ArrayRows_ReferenceComponent_UsesTheSameTypedRowAndSurvivesTransition()
    {
        var layouts = new ComponentLayoutRegistry();
        var referenceId = layouts.Register(typeof(ReferenceComponent), new SchemaId(10_151));
        var markerId = layouts.Register(typeof(RefMarker), new SchemaId(10_152));
        var world = new World(layouts, chunkCapacity: 4);
        var entity = world.Create(new[] { referenceId });
        var component = new ReferenceComponent { Value = 42 };

        Assert.That(world.Set(entity, referenceId, component), Is.True);
        Assert.That(world.TryGet(entity, referenceId, out ReferenceComponent actual), Is.True);
        Assert.That(actual, Is.SameAs(component));

        var query = world.CreateQuery(QuerySpec.WhereAll(referenceId));
        var reference = query.AccessWrite(referenceId);
        using (var scope = world.OpenQuery(in query))
        {
            var preparedReference = reference;
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var slots = chunks.Current.Slots;
                    var rows = slots.GetRow(preparedReference);
                    while (slots.MoveNext())
                    {
                        Assert.That(rows.Ref<ReferenceComponent>(slots), Is.SameAs(component));
                        rows.Ref<ReferenceComponent>(slots).Value++;
                    }
                }
            }
        }

        world.Add(new[] { markerId }, entity);

        Assert.That(world.TryGet(entity, referenceId, out actual), Is.True);
        Assert.That(actual, Is.SameAs(component));
        Assert.That(actual.Value, Is.EqualTo(43));
        Assert.That(world.Destroy(entity), Is.True);
        Assert.That(world.TryGet(entity, referenceId, out ReferenceComponent _), Is.False);
    }

    [Test]
    public void ArrayRows_Transitions_PreserveMappedRows_And_ClearDestroyedReferences()
    {
        var layouts = new ComponentLayoutRegistry();
        var localId = layouts.Register(typeof(NamedRef), new SchemaId(10_201));
        var worldId = layouts.Register(typeof(NamedRef), new SchemaId(10_202));
        var markerId = layouts.Register(typeof(RefMarker), new SchemaId(10_203));
        var world = new World(layouts, chunkCapacity: 4);
        var entity = world.Create(new[] { localId, worldId });

        world.Set(entity, localId, new NamedRef { Name = "local", Id = 11 });
        world.Set(entity, worldId, new NamedRef { Name = "world", Id = 22 });
        world.Add(new[] { markerId }, entity);

        Assert.True(world.TryGet(entity, localId, out NamedRef localAfterAdd));
        Assert.True(world.TryGet(entity, worldId, out NamedRef worldAfterAdd));
        Assert.AreEqual(11, localAfterAdd.Id);
        Assert.AreEqual(22, worldAfterAdd.Id);

        world.Remove(new[] { markerId }, entity);
        Assert.True(world.TryGet(entity, localId, out NamedRef localAfterRemove));
        Assert.True(world.TryGet(entity, worldId, out NamedRef worldAfterRemove));
        Assert.AreEqual("local", localAfterRemove.Name);
        Assert.AreEqual("world", worldAfterRemove.Name);

        var weak = CreateDestroyedReference();
        ForceCollection();
        Assert.False(weak.TryGetTarget(out _), "destroy must clear the removed ArrayRows reference slot");
    }

    [Test]
    public void ArrayRows_StaleHandle_AndLeaseMutation_AreRejected()
    {
        var layouts = new ComponentLayoutRegistry();
        var id = layouts.Register(typeof(NamedRef), new SchemaId(10_301));
        var world = new World(layouts);
        var entity = world.Create(new[] { id });
        var spec = QuerySpec.WhereAll(id);
        var query = world.CreateQuery(in spec);
        using (var scope = world.OpenQuery(in query))
        {
            Assert.Throws<InvalidOperationException>(() => world.Destroy(entity));
        }
        Assert.True(world.IsAlive(entity));
        Assert.True(world.Destroy(entity));
        Assert.False(world.IsAlive(entity));
        Assert.False(world.Destroy(entity));
    }

    [Test]
    public void Transition_Add_Remove_Preserves_Data()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);

        var first = world.Create(new[] { PositionId, VelocityId });
        world.Set(first, PositionId, new Position { X = 10, Y = 11 });
        world.Set(first, VelocityId, new Velocity { X = 20, Y = 21 });

        world.Add(new[] { HealthId }, first);

        Assert.True(world.TryGet<Position>(first, PositionId, out var posAfterAdd));
        Assert.True(world.TryGet<Velocity>(first, VelocityId, out var velAfterAdd));
        Assert.True(world.TryGet<Health>(first, HealthId, out var healthAfterAdd));
        Assert.AreEqual(10, posAfterAdd.X);
        Assert.AreEqual(21, velAfterAdd.Y);
        Assert.AreEqual(0, healthAfterAdd.Value);

        world.Remove(new[] { VelocityId }, first);

        Assert.True(world.TryGet<Position>(first, PositionId, out var posAfterRemove));
        Assert.True(world.TryGet<Health>(first, HealthId, out _));
        Assert.False(world.TryGet<Velocity>(first, VelocityId, out _));
        Assert.AreEqual(10, posAfterRemove.X);
    }

    [Test]
    public void RandomizedInvariants_WithTransitions()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);
        var random = new Random(123456);

        var model = new Dictionary<int, EntityState>();

        var allEntities = new List<Entity>();
        for (var step = 0; step < 2_000; step++)
        {
            var action = random.Next(4);
            var snapshot = new Entity[allEntities.Count + 16];
            var snapshotCount = world.CollectAliveEntities(snapshot);

            if (action == 0 && allEntities.Count < 200)
            {
                var count = random.Next(1, 8);
                for (var i = 0; i < count; i++)
                {
                    var useVelocity = random.NextDouble() > 0.5;
                    var useHealth = random.NextDouble() > 0.5;
                    var list = new List<ComponentId> { PositionId };
                    if (useVelocity)
                    {
                        list.Add(VelocityId);
                    }

                    if (useHealth)
                    {
                        list.Add(HealthId);
                    }

                    var entity = world.Create(list.ToArray());
                    var state = new EntityState
                    {
                        ArchetypeKey = useVelocity && useHealth ? 3 : useVelocity || useHealth ? 1 : 0,
                        Position = new Position { X = random.NextSingle() * 100f, Y = random.NextSingle() * 100f },
                        Velocity = useVelocity ? new Velocity { X = random.NextSingle() * 2f, Y = random.NextSingle() * 2f } : null,
                        Health = useHealth ? new Health { Value = random.Next(0, 100) } : null,
                    };

                    world.Set(entity, PositionId, state.Position);
                    if (state.Velocity.HasValue)
                    {
                        world.Set(entity, VelocityId, state.Velocity.Value);
                    }

                    if (state.Health.HasValue)
                    {
                        world.Set(entity, HealthId, state.Health.Value);
                    }

                    model[entity.Index] = state;
                    allEntities.Add(entity);
                }
            }
            else if (action == 1 && allEntities.Count > 0)
            {
                var index = random.Next(allEntities.Count);
                var entity = allEntities[index];
                world.Destroy(entity);
                model.Remove(entity.Index);
                allEntities.RemoveAt(index);
            }
            else if (action == 2 && allEntities.Count > 0)
            {
                var index = random.Next(allEntities.Count);
                var entity = allEntities[index];
                var addVelocity = random.NextDouble() > 0.5;
                var current = model[entity.Index];

                if (addVelocity)
                {
                    world.Add(new[] { VelocityId }, entity);

                    if (!current.Velocity.HasValue)
                    {
                        current.Velocity = new Velocity();
                    }
                }
                else
                {
                    world.Remove(new[] { VelocityId }, entity);
                    current.Velocity = null;
                }

                model[entity.Index] = current;
            }
            else if (action == 3 && allEntities.Count > 0)
            {
                var index = random.Next(allEntities.Count);
                var entity = allEntities[index];
                if (world.IsAlive(entity))
                {
                    var newPosition = new Position { X = random.NextSingle(), Y = random.NextSingle() };
                    world.Set(entity, PositionId, newPosition);

                    var updated = model[entity.Index];
                    updated.Position = newPosition;
                    model[entity.Index] = updated;
                }
            }

            ValidateInvariant(world, model);

            if (world.AliveEntityCount != model.Count)
            {
                Assert.Fail($"Mismatch after step {step}: alive={world.AliveEntityCount}, model={model.Count}");
            }
        }
    }

    private static void RegisterComponentLayouts(ComponentLayoutRegistry layouts)
    {
        layouts.Register(typeof(Position), new SchemaId(1));
        layouts.Register(typeof(Velocity), new SchemaId(2));
        layouts.Register(typeof(Health), new SchemaId(3));
    }

    private static int CountDenseQuery(World world, in QuerySpec query)
    {
        var handle = world.CreateQuery(in query);
        var count = 0;
        using (var scope = world.OpenQuery(in handle))
        {
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    count += chunks.Current.SlotCount;
                }
            }
        }

        return count;
    }

    private static void ExecuteDenseReadAccess(World world, Query query, ReadAccess access)
    {
        using var scope = world.OpenQuery(in query);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                if (chunks.Current.SlotCount != 0)
                {
                    _ = chunks.Current.Slots.GetRow(access);
                    return;
                }
            }
        }

        Assert.Fail("The query had no chunk in which to validate the access.");
    }

    private static void ExecuteDenseWriteAccess(World world, Query query, WriteAccess access)
    {
        using var scope = world.OpenQuery(in query);
        var prepared = access;
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                _ = chunks.Current.Slots.GetRow(prepared);
            }
        }
    }

    private static void RunDenseMovement(
        World world,
        in Query query,
        WriteAccess position,
        ReadAccess velocity,
        ref float sum)
    {
        using var scope = world.OpenQuery(in query);
        var preparedPosition = position;
        var preparedVelocity = velocity;
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                var positions = slots.GetRow(preparedPosition);
                var velocities = slots.GetRow(preparedVelocity);
                while (slots.MoveNext())
                {
                    ref var currentPosition = ref positions.Ref<Position>(slots);
                    ref readonly var currentVelocity = ref velocities.Ref<Velocity>(slots);
                    currentPosition.X += currentVelocity.X;
                    currentPosition.Y += currentVelocity.Y;
                    sum += currentPosition.X;
                }
            }
        }
    }

    private static void ValidateInvariant(World world, Dictionary<int, EntityState> model)
    {
        var active = new Entity[Math.Max(model.Count, 1) * 2];
        var alive = world.CollectAliveEntities(active);
        if (alive != model.Count)
        {
            Assert.Fail($"CollectAliveEntities mismatch: model={model.Count}, alive={alive}, worldAlive={world.AliveEntityCount}");
        }

        var exact = new Entity[model.Count];
        world.CollectAliveEntities(exact);

        for (var i = 0; i < exact.Length; i++)
        {
            var entity = exact[i];
            Assert.True(model.ContainsKey(entity.Index));

            var expected = model[entity.Index];
            Assert.True(world.TryGet<Position>(entity, PositionId, out var position));
            Assert.That(Math.Abs(expected.Position.X - position.X) < 1e-5f, $"Mismatch Position.X for entity {entity}");
            Assert.That(Math.Abs(expected.Position.Y - position.Y) < 1e-5f, $"Mismatch Position.Y for entity {entity}");

            if (expected.Velocity.HasValue)
            {
                Assert.True(world.TryGet<Velocity>(entity, VelocityId, out var velocity));
                Assert.AreEqual(expected.Velocity.Value.X, velocity.X, 1e-5f);
            }
            else
            {
                Assert.False(world.TryGet<Velocity>(entity, VelocityId, out _));
            }

            if (expected.Health.HasValue)
            {
                Assert.True(world.TryGet<Health>(entity, HealthId, out var health));
                Assert.AreEqual(expected.Health.Value.Value, health.Value);
            }
            else
            {
                Assert.False(world.TryGet<Health>(entity, HealthId, out _));
            }
        }
    }

    private static HashSet<int> CollectChunkIds(World world)
    {
        var query = world.CreateQuery(QuerySpec.WhereAll(PositionId));
        var chunkIds = new HashSet<int>();
        using (var scope = world.OpenQuery(in query))
        {
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    chunkIds.Add(chunks.Current.GlobalChunkId);
                }
            }
        }

        return chunkIds;
    }

    private static WeakReference<RefPayload> CreateDestroyedReference()
    {
        var layouts = new ComponentLayoutRegistry();
        var id = layouts.Register(typeof(RefMarker), new SchemaId(10_401));
        var world = new World(layouts);
        var entity = world.Create(new[] { id });
        var payload = new RefPayload(42);
        world.Set(entity, id, new RefMarker { Payload = payload, Value = 42 });
        var weak = new WeakReference<RefPayload>(payload);
        world.Destroy(entity);
        return weak;
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private struct EntityState
    {
        public Position Position;
        public Velocity? Velocity;
        public Health? Health;
        public int ArchetypeKey;
    }

}
