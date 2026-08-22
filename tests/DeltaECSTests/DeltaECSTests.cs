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
    private static readonly TagId TagActive = new TagId(1);
    private static readonly TagId TagVisible = new TagId(2);
    private static readonly QueryAction<QueryState> s_cursorQuerySlots = QuerySlots;
    private static readonly QueryAction<ArrayQueryState> s_readArrayRows = ReadArrayRows;
    private static readonly QueryAction<LeaseMutationState> s_destroyDuringLease = DestroyDuringLease;
    private static readonly QueryAction<ChunkIdState> s_collectChunkIds = CollectChunkIds;

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

        var first = world.GetArchetype(VelocityId, PositionId, PositionId);
        var version = world.ArchetypeVersion;
        var same = world.ResolveArchetype(PositionId, VelocityId);

        Assert.That(first.IsValid, Is.True);
        Assert.That(first, Is.EqualTo(same));
        Assert.That(world.ArchetypeVersion, Is.EqualTo(version));

        var entities = new Entity[2];
        Assert.That(world.CreateBatch(first, entities), Is.EqualTo(2));
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
        var handle = world.GetArchetype(PositionId);

        Assert.Throws<ArgumentException>(() => foreignWorld.Create(handle));
        Assert.Throws<ArgumentException>(() => world.Create(ArchetypeHandle.Invalid));
        Assert.Throws<ArgumentException>(() => foreignWorld.CreateBatch(handle, new Entity[1]));
        Assert.Throws<ArgumentException>(() => world.CreateBatch(ArchetypeHandle.Invalid, new Entity[1]));
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
        world.CreateBatch(new[] { PositionId, VelocityId }, created);
        Assert.AreEqual(requested, world.AliveEntityCount);

        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId, VelocityId));
        var position = query.Access<Position>(PositionId, AccessMode.Write);
        var velocity = query.Access<Velocity>(VelocityId, AccessMode.Write);
        var sum = 0L;
        world.Query(in query, ref sum, (ref long total, ref QueryChunkCursor cursor) =>
        {
            var pos = cursor.GetWrite(position);
            var vel = cursor.GetWrite(velocity);
            while (cursor.MoveNext())
            {
                ref var p = ref pos.Ref<Position>(cursor);
                ref var v = ref vel.Ref<Velocity>(cursor);
                p = new Position { X = cursor.CurrentIndex, Y = cursor.CurrentIndex * 2f };
                v = new Velocity { X = 1, Y = 1 };
                total += (long)p.X + (long)v.Y;
            }
        });

        Assert.Greater(sum, 0);

        world.DestroyBatch(created);
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
        world.CreateBatch(new[] { PositionId }, positionOnly);
        world.CreateBatch(new[] { PositionId, VelocityId }, positionVelocity);

        var expected = new Dictionary<Entity, int>();
        var nextValue = 0;
        foreach (var entity in positionOnly)
        {
            expected[entity] = nextValue;
            world.SetComponent(entity, PositionId, new Position { X = nextValue++ });
        }

        foreach (var entity in positionVelocity)
        {
            expected[entity] = nextValue;
            world.SetComponent(entity, PositionId, new Position { X = nextValue++ });
        }

        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        var position = query.Access<Position>(PositionId, AccessMode.Write);
        var before = world.WorldTick;
        var archetypeCount = 0;
        var chunkCount = 0;
        var slotCount = 0;
        var writtenChunks = new List<int>();

        using (var scope = world.OpenQuery(in query))
        {
            var preparedPosition = scope.BindWrite(position);
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
                    var positions = slots.Get(preparedPosition);
                    while (slots.MoveNext())
                    {
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
    public void NonGenericAccessRequest_BindsRows_AndTracksOnlyWrites()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 4);
        var entity = world.Create(new[] { PositionId, VelocityId });
        world.SetComponent(entity, PositionId, new Position { X = 1, Y = 2 });
        world.SetComponent(entity, VelocityId, new Velocity { X = 3, Y = 4 });

        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId, VelocityId));
        var position = query.Access(PositionId, AccessMode.Write);
        var velocity = query.Access(VelocityId, AccessMode.Read);
        var before = world.WorldTick;

        using (var scope = world.OpenQuery(in query))
        {
            var write = scope.BindWrite(position);
            var read = scope.BindRead(velocity);
            var archetypes = scope.Archetypes;
            Assert.That(archetypes.MoveNext(), Is.True);
            var chunks = archetypes.Current.Chunks;
            Assert.That(chunks.MoveNext(), Is.True);
            var slots = chunks.Current.Slots;
            var positions = slots.Get(write);
            var velocities = slots.Get(read);
            Assert.That(slots.MoveNext(), Is.True);
            ref var p = ref positions.Ref<Position>(slots);
            ref readonly var v = ref velocities.Ref<Velocity>(slots);
            p.X += v.X;
        }

        Assert.That(world.TryGetComponent<Position>(entity, PositionId, out var result), Is.True);
        Assert.That(result.X, Is.EqualTo(4));
        Assert.That(world.HasChangedSince(0, PositionId, before), Is.True);
        Assert.That(world.HasChangedSince(0, VelocityId, before), Is.False);
    }

    [Test]
    public void QueryScope_Rejects_Tag_Predicates_At_Scope_Creation()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);
        var tagged = new QuerySpec(
            new[] { PositionId },
            Array.Empty<ComponentId>(),
            Array.Empty<ComponentId>(),
            new[] { TagActive },
            Array.Empty<TagId>(),
            Array.Empty<TagId>());
        var query = world.CreateQuery(in tagged);

        Assert.Throws<ArgumentException>(() =>
        {
            using var _ = world.OpenQuery(in query);
        });
    }

    [Test]
    public void LeaseEntities_AndComponentRows_StayAligned_OnBothLeaseSurfaces()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 4);
        var created = new Entity[5];
        world.CreateBatch(new[] { PositionId, VelocityId }, created);
        var expected = new Dictionary<Entity, int>();
        for (var i = 0; i < created.Length; i++)
        {
            expected.Add(created[i], i);
            world.SetComponent(created[i], PositionId, new Position { X = i, Y = -i });
            world.SetComponent(created[i], VelocityId, new Velocity { X = i + 10, Y = i + 20 });
        }

        var spec = QuerySpec.ForComponents(PositionId, VelocityId);
        var query = world.CreateQuery(in spec);
        var position = query.Access<Position>(PositionId, AccessMode.Read);
        var velocity = query.Access<Velocity>(VelocityId, AccessMode.Read);
        var denseLeaseCount = 0;
        world.Query(in query, ref denseLeaseCount, (ref int count, ref QueryChunkCursor cursor) =>
        {
            var entities = cursor.Entities;
            var positions = cursor.GetRead(position);
            var velocities = cursor.GetRead(velocity);
            while (cursor.MoveNext())
            {
                var entity = entities[cursor.CurrentIndex];
                Assert.That(expected.ContainsKey(entity), Is.True);
                Assert.That(positions.Ref<Position>(cursor).X, Is.EqualTo(expected[entity]));
                Assert.That(velocities.Ref<Velocity>(cursor).X, Is.EqualTo(expected[entity] + 10));
                count++;
            }
        });

        var state = new AlignmentState(expected, position, velocity);
        world.Query(in query, ref state, AssertAlignedRows);

        Assert.That(denseLeaseCount, Is.EqualTo(created.Length));
        Assert.That(state.Count, Is.EqualTo(created.Length));
    }

    [Test]
    public void ReverseIteration_HandlesEmptySingleFullChunks_AndOverlayHoles()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 4);
        var spec = QuerySpec.ForComponents(PositionId);
        var emptyQuery = world.CreateQuery(in spec);
        var position = emptyQuery.Access<Position>(PositionId, AccessMode.Read);
        var emptyChunkCount = 0;
        world.Query(
            in emptyQuery,
            ref emptyChunkCount,
            static (ref int count, ref QueryChunkCursor cursor) => count++);
        Assert.That(emptyChunkCount, Is.EqualTo(0));

        var singleWorld = new World(layouts, chunkCapacity: 4);
        var single = singleWorld.Create(new[] { PositionId });
        var singleQuery = singleWorld.CreateQuery(QuerySpec.ForComponents(PositionId));
        var singleChunkCount = 0;
        singleWorld.Query(in singleQuery, ref singleChunkCount, (ref int count, ref QueryChunkCursor cursor) =>
        {
            count++;
            Assert.That(cursor.SlotCount, Is.EqualTo(1));
            Assert.That(cursor.Entities[0], Is.EqualTo(single));
        });
        Assert.That(singleChunkCount, Is.EqualTo(1));

        var created = new Entity[4];
        world.CreateBatch(new[] { PositionId }, created);
        for (var i = 0; i < created.Length; i++)
        {
            world.SetComponent(created[i], PositionId, new Position { X = i, Y = 0 });
        }

        var fullChunkCount = 0;
        world.Query(in emptyQuery, ref fullChunkCount, (ref int count, ref QueryChunkCursor cursor) =>
        {
            Assert.That(cursor.SlotCount, Is.EqualTo(4));
            var entities = cursor.Entities;
            var positions = cursor.GetRead(position);
            while (cursor.MoveNext())
            {
                Assert.That(entities[cursor.CurrentIndex].IsAlive, Is.True);
                Assert.That(positions.Ref<Position>(cursor).X, Is.EqualTo(cursor.CurrentIndex));
                count++;
            }
        });

        var tagged = new QuerySpec(
            new[] { PositionId },
            Array.Empty<ComponentId>(),
            Array.Empty<ComponentId>(),
            new[] { TagActive },
            Array.Empty<TagId>(),
            Array.Empty<TagId>());
        world.AddTag(created[0], TagActive);
        world.AddTag(created[2], TagActive);
        var observed = new HashSet<Entity>();
        var taggedQuery = world.CreateQuery(in tagged);
        var taggedState = new OverlayQueryState(observed);
        world.Query(in taggedQuery, ref taggedState, static (ref OverlayQueryState state, ref QueryChunkCursor cursor) =>
        {
            var entities = cursor.Entities;
            while (cursor.MoveNext())
            {
                if (!cursor.IsActiveSlot(cursor.CurrentIndex))
                {
                    state.SawPartialChunk = true;
                    continue;
                }
                Assert.That(state.Entities.Add(entities[cursor.CurrentIndex]), Is.True);
            }
        });

        Assert.That(fullChunkCount, Is.EqualTo(created.Length));
        Assert.That(taggedState.SawPartialChunk, Is.True);
        Assert.That(observed, Is.EquivalentTo(new[] { created[0], created[2] }));

        foreach (var entity in created)
        {
            world.AddTag(entity, TagActive);
        }

        var fullTaggedState = new OverlayQueryState(new HashSet<Entity>());
        world.Query(in taggedQuery, ref fullTaggedState, static (ref OverlayQueryState state, ref QueryChunkCursor cursor) =>
        {
            var sawInactive = false;
            while (cursor.MoveNext())
            {
                if (!cursor.IsActiveSlot(cursor.CurrentIndex))
                {
                    sawInactive = true;
                    continue;
                }

                Assert.That(state.Entities.Add(cursor.Entities[cursor.CurrentIndex]), Is.True);
            }
            Assert.That(sawInactive, Is.False);
        });

        Assert.That(fullTaggedState.Entities, Is.EquivalentTo(created));
    }

    [Test]
    public void TaggedQueries_Reuse_Scratch_And_Preserve_None_Full_Partial_Across_Action_Path()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 2);
        var entities = new Entity[5];
        world.CreateBatch(new[] { PositionId }, entities);
        world.AddTag(entities[0], TagActive);
        world.AddTag(entities[4], TagActive);

        var tagged = new QuerySpec(
            new[] { PositionId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(),
            new[] { TagActive }, Array.Empty<TagId>(), Array.Empty<TagId>());
        var handle = world.CreateQuery(in tagged);

        var actionSummary = new OverlaySummary();
        world.Query(in handle, ref actionSummary, static (ref OverlaySummary state, ref QueryChunkCursor cursor) =>
        {
            state.Observe(ref cursor);
        });
        Assert.That(actionSummary.ActiveSlots, Is.EqualTo(2));
        Assert.That(actionSummary.Chunks, Is.EqualTo(2));
        Assert.That(actionSummary.SawPartial, Is.True);
        Assert.That(actionSummary.SawFull, Is.True);

        var callbackSummary = new OverlaySummary();
        world.Query(in handle, ref callbackSummary, static (ref OverlaySummary state, ref QueryChunkCursor cursor) =>
        {
            state.Observe(ref cursor);
        });
        Assert.That(callbackSummary.ActiveSlots, Is.EqualTo(2));
        Assert.That(callbackSummary.Chunks, Is.EqualTo(2));
        Assert.That(callbackSummary.SawPartial, Is.True);
        Assert.That(callbackSummary.SawFull, Is.True);

        var repeatedSummary = new OverlaySummary();
        world.Query(in handle, ref repeatedSummary, static (ref OverlaySummary state, ref QueryChunkCursor cursor) =>
        {
            state.Observe(ref cursor);
        });

        Assert.That(repeatedSummary.ActiveSlots, Is.EqualTo(2));
        Assert.That(repeatedSummary.Chunks, Is.EqualTo(2));
        Assert.That(repeatedSummary.SawPartial, Is.True);
        Assert.That(repeatedSummary.SawFull, Is.True);

        var missingAll = new QuerySpec(
            new[] { PositionId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(),
            new[] { new TagId(404) }, Array.Empty<TagId>(), Array.Empty<TagId>());
        var missingAny = new QuerySpec(
            new[] { PositionId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(),
            Array.Empty<TagId>(), new[] { new TagId(405) }, Array.Empty<TagId>());
        Assert.That(CountQuery(world, missingAll), Is.EqualTo(0));
        Assert.That(CountQuery(world, missingAny), Is.EqualTo(0));

        var missingNone = new QuerySpec(
            new[] { PositionId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(),
            Array.Empty<TagId>(), Array.Empty<TagId>(), new[] { new TagId(406) });
        var noneCount = CountQuery(world, missingNone);
        Assert.That(noneCount, Is.EqualTo(entities.Length));
    }

    [Test]
    public void Query_Supports_Tagged_Partial_And_Full_Chunks()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 2);
        var entities = new Entity[5];
        world.CreateBatch(new[] { PositionId }, entities);
        world.AddTag(entities[0], TagActive);
        world.AddTag(entities[4], TagActive);

        var spec = new QuerySpec(
            new[] { PositionId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(),
            new[] { TagActive }, Array.Empty<TagId>(), Array.Empty<TagId>());
        var query = world.CreateQuery(in spec);
        var position = query.Access<Position>(PositionId, AccessMode.Read);
        var state = new CursorTaggedState(position);
        world.Query(in query, ref state, static (ref CursorTaggedState current, ref QueryChunkCursor cursor) =>
        {
            var positions = cursor.GetRead(current.Position);
            while (cursor.MoveNext())
            {
                if (!cursor.IsActiveSlot(cursor.CurrentIndex))
                {
                    continue;
                }

                current.ActiveCount++;
                current.Sum += positions.Ref<Position>(cursor).X;
            }
        });

        Assert.That(state.ActiveCount, Is.EqualTo(2));

        var enumerated = 0;
        world.Query(in query, ref enumerated, (ref int count, ref QueryChunkCursor cursor) =>
        {
            var rows = cursor.GetRead(position);
            while (cursor.MoveNext())
            {
                if (cursor.IsActiveSlot(cursor.CurrentIndex))
                {
                    _ = rows.Ref<Position>(cursor);
                    count++;
                }
            }
        });

        Assert.That(enumerated, Is.EqualTo(2));
    }

    [Test]
    public void OverlayMask_Fills_Only_Configured_Words_And_Clears_Unused_Chunk_Bits()
    {
        var manager = new OverlayTagManager(chunkCapacity: 130);
        var query = new QuerySpec(
            Array.Empty<ComponentId>(), Array.Empty<ComponentId>(), Array.Empty<ComponentId>(),
            Array.Empty<TagId>(), Array.Empty<TagId>(), new[] { new TagId(404) });
        var scratch = new ulong[17];
        Array.Fill(scratch, ulong.MaxValue);

        var result = manager.BuildMask(query, chunkId: 0, chunkSize: 65, scratch);

        Assert.That(result, Is.EqualTo(OverlayMaskResult.Full));
        Assert.That(scratch[0], Is.EqualTo(ulong.MaxValue));
        Assert.That(scratch[1], Is.EqualTo(1UL));
        Assert.That(scratch[2], Is.EqualTo(0UL));
    }

    [Test]
    public void ImmediateBatchTransition_CompletesBeforeReturn_AndIsIdempotent()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 2);
        var entities = new Entity[5];
        world.CreateBatch(new[] { PositionId }, entities);

        Assert.That(world.AddComponents(new[] { VelocityId }, entities), Is.EqualTo(entities.Length));
        foreach (var entity in entities)
        {
            Assert.That(world.TryGetComponent<Velocity>(entity, VelocityId, out _), Is.True);
        }

        Assert.That(world.AddComponents(new[] { VelocityId }, entities), Is.EqualTo(0));
        Assert.That(world.RemoveComponents(new[] { VelocityId }, entities), Is.EqualTo(entities.Length));
        foreach (var entity in entities)
        {
            Assert.That(world.TryGetComponent<Velocity>(entity, VelocityId, out _), Is.False);
        }
    }

    [Test]
    public void Query_Cache_Remains_Valid_After_New_Archetype_Appears()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);

        var query = new QuerySpec(new[] { PositionId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(), Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>());

        var initial = new Entity[1];
        world.CreateBatch(new[] { PositionId }, initial);
        var before = 0;
        before = CountQuery(world, query);

        var withVelocity = new Entity[2];
        world.CreateBatch(new[] { PositionId, VelocityId }, withVelocity);

        var after = 0;
        after = CountQuery(world, query);

        Assert.AreEqual(before + 2, after, $"before={before}, after={after}, alive={world.AliveEntityCount}");
    }

    [Test]
    public void QuerySpec_Is_Immutable_After_Creation()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);

        var all = new[] { PositionId };
        var query = new QuerySpec(all, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(), Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>());
        all[0] = VelocityId;

        world.Create(new[] { PositionId });

        var count = CountQuery(world, query);
        Assert.AreEqual(1, count);
    }

    [Test]
    public void QuerySpec_ComponentMasks_Deduplicate_Filter_And_Enumerate_In_Order()
    {
        var query = new QuerySpec(
            new[] { new ComponentId(129), new ComponentId(7), PositionId, new ComponentId(193), new ComponentId(65), new ComponentId(7), ComponentId.Invalid },
            new[] { VelocityId, VelocityId, ComponentId.Invalid },
            new[] { HealthId, HealthId },
            Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>());
        var equivalent = new QuerySpec(
            new[] { new ComponentId(65), new ComponentId(193), new ComponentId(129), PositionId, new ComponentId(7) },
            new[] { VelocityId },
            new[] { HealthId },
            Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>());

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
            Array.Empty<ComponentId>(), Array.Empty<ComponentId>(),
            Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>()));
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

        var all = QuerySpec.ForComponents(PositionId, VelocityId);
        var any = new QuerySpec(
            Array.Empty<ComponentId>(), new[] { HealthId, VelocityId }, Array.Empty<ComponentId>(),
            Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>());
        var none = new QuerySpec(
            Array.Empty<ComponentId>(), Array.Empty<ComponentId>(), new[] { HealthId },
            Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>());
        var combined = new QuerySpec(
            new[] { PositionId }, new[] { VelocityId }, new[] { HealthId },
            Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>());

        Assert.That(CountQuery(world, all), Is.EqualTo(1));
        Assert.That(CountQuery(world, any), Is.EqualTo(3));
        Assert.That(CountQuery(world, none), Is.EqualTo(3));
        Assert.That(CountQuery(world, combined), Is.EqualTo(1));
    }

    [Test]
    public void QueryPlan_ComponentRowPlan_Uses_Deterministic_Mask_Order()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);
        world.Create(new[] { PositionId, VelocityId });
        var query = new QuerySpec(
            new[] { VelocityId, PositionId, VelocityId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(),
            Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>());
        var handle = world.CreateQuery(in query);
        var cached = handle.Cached;

        Assert.That(cached.MatchingArchetypes(world).Length, Is.EqualTo(1));
        var rowPlan = cached.ComponentRowIndices(0);
        Assert.That(rowPlan.Length, Is.EqualTo(2));
        Assert.That(rowPlan[0], Is.EqualTo(0));
        Assert.That(rowPlan[1], Is.EqualTo(1));
    }

    [Test]
    public void WriteRequest_Marks_Only_Yielded_Rows_And_ReadBinding_Does_Not_Mark()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 4);
        var entity = world.Create(new[] { PositionId, VelocityId });
        var chunkId = -1;
        var spec = QuerySpec.ForComponents(PositionId);
        var query = world.CreateQuery(in spec);
        var readPosition = query.Access<Position>(PositionId, AccessMode.Read);
        var writePosition = query.Access<Position>(PositionId, AccessMode.Write);
        var before = world.WorldTick;

        world.Query(in query, ref chunkId, static (ref int id, ref QueryChunkCursor cursor) =>
        {
            if (id < 0)
            {
                id = cursor.GlobalChunkId;
            }
        });
        Assert.That(chunkId, Is.GreaterThanOrEqualTo(0));
        Assert.That(world.HasChangedSince(chunkId, PositionId, before), Is.False);
        Assert.That(world.HasChangedSince(chunkId, VelocityId, before), Is.False);

        world.Query(in query, ref writePosition, static (ref AccessRequest binding, ref QueryChunkCursor cursor) =>
        {
            _ = cursor.GetWrite(binding);
        });
        var afterWrite = world.WorldTick;
        Assert.That(afterWrite, Is.GreaterThan(before));
        Assert.That(world.HasChangedSince(chunkId, PositionId, before), Is.True);
        Assert.That(world.HasChangedSince(chunkId, VelocityId, before), Is.False);

        world.Query(in query, ref readPosition, static (ref AccessRequest binding, ref QueryChunkCursor cursor) =>
        {
            _ = cursor.GetRead(binding);
        });
        Assert.That(world.HasChangedSince(chunkId, VelocityId, afterWrite), Is.False);

        world.Query(in query, ref writePosition, static (ref AccessRequest binding, ref QueryChunkCursor cursor) =>
        {
            _ = cursor.GetWrite(binding);
        });

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
        Assert.That(publicMethods.Any(static method => method.Name == "Query"), Is.True);
        Assert.That(publicMethods.Any(static method => method.Name == "QueryChunks"), Is.False);
    }

    [Test]
    public void AccessRequests_Are_Typed_QueryBound_And_Precisely_Track_Writes()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 2);
        world.Create(new[] { PositionId, VelocityId });
        world.Create(new[] { PositionId, VelocityId, HealthId });

        var spec = QuerySpec.ForComponents(PositionId, VelocityId);
        var query = world.CreateQuery(in spec);
        var position = query.Access<Position>(PositionId, AccessMode.Write);
        var velocity = query.Access<Velocity>(VelocityId, AccessMode.Read);

        Assert.That(query.Cached, Is.Not.Null);

        var cursorRows = 0;
        world.Query(in query, ref cursorRows, (ref int rows, ref QueryChunkCursor cursor) =>
        {
            rows += cursor.SlotCount;
        });
        Assert.That(cursorRows, Is.EqualTo(2));

        var state = new BoundRowState(position, velocity);
        var before = world.WorldTick;
        world.Query(in query, ref state, ApplyBoundRows);

        Assert.That(state.Rows, Is.EqualTo(2));
        Assert.That(state.Chunks.Count, Is.EqualTo(2));
        foreach (var chunkId in state.Chunks)
        {
            Assert.That(world.HasChangedSince(chunkId, PositionId, before), Is.True);
            Assert.That(world.HasChangedSince(chunkId, VelocityId, before), Is.False);
        }

        var simpleRows = 0;
        world.Query(in query, ref simpleRows, (ref int rows, ref QueryChunkCursor cursor) =>
        {
            rows += cursor.SlotCount;
        });
        Assert.That(simpleRows, Is.EqualTo(2));

        var wrongType = Assert.Throws<ArgumentException>(() => query.Access<Velocity>(PositionId, AccessMode.Read));
        Assert.That(wrongType!.Message, Does.Contain("registered"));

        var anyDescription = new QuerySpec(
            new[] { PositionId }, new[] { VelocityId }, Array.Empty<ComponentId>(),
            Array.Empty<TagId>(), Array.Empty<TagId>(), Array.Empty<TagId>());
        var anyQuery = world.CreateQuery(in anyDescription);
        Assert.Throws<ArgumentException>(() => anyQuery.Access<Velocity>(VelocityId, AccessMode.Read));

        Assert.DoesNotThrow(() => ExecuteReadWithWriteBinding(world, query, position));
    }

    [Test]
    public void AccessRequest_Refreshes_Values_For_New_Archetypes()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts, chunkCapacity: 2);
        world.Create(new[] { VelocityId });

        var spec = QuerySpec.ForComponents(VelocityId);
        var query = world.CreateQuery(in spec);
        var velocity = query.Access<Velocity>(VelocityId, AccessMode.Read);

        var firstRows = 0;
        world.Query(in query, ref firstRows, (ref int rows, ref QueryChunkCursor cursor) =>
        {
            rows += cursor.SlotCount;
        });

        // The new archetype stores Velocity at physical row 1 instead of row 0.
        world.Create(new[] { PositionId, VelocityId });

        var secondRows = 0;
        world.Query(in query, ref secondRows, (ref int rows, ref QueryChunkCursor cursor) =>
        {
            rows += cursor.SlotCount;
        });

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
        var spec = QuerySpec.ForComponents(PositionId, VelocityId);
        var query = world.CreateQuery(in spec);
        var otherDescription = QuerySpec.ForComponents(PositionId);
        var otherQuery = world.CreateQuery(in otherDescription);
        var mismatchedBinding = otherQuery.Access<Position>(PositionId, AccessMode.Read);

        Assert.Throws<InvalidOperationException>(() => world.Query(in query, ref mismatchedBinding, static (ref AccessRequest binding, ref QueryChunkCursor cursor) =>
        {
            _ = cursor.GetRead(binding);
        }));

        var foreignWorld = new World(layouts);
        foreignWorld.Create(new[] { PositionId, VelocityId });
        var foreignQuery = foreignWorld.CreateQuery(QuerySpec.ForComponents(PositionId, VelocityId));
        var foreignBinding = foreignQuery.Access<Position>(PositionId, AccessMode.Read);

        Assert.Throws<InvalidOperationException>(() => world.Query(in query, ref foreignBinding, static (ref AccessRequest binding, ref QueryChunkCursor cursor) =>
        {
            _ = cursor.GetRead(binding);
        }));

        var defaultBinding = default(AccessRequest);
        Assert.That(defaultBinding.Query, Is.Null);
        Assert.Throws<InvalidOperationException>(() => world.Query(in query, ref defaultBinding, static (ref AccessRequest binding, ref QueryChunkCursor cursor) =>
        {
            _ = cursor.GetRead(binding);
        }));
    }

    [Test]
    public void Invalid_TagId_Is_Rejected_By_World_And_Query_Contracts()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);
        var entity = world.Create(new[] { PositionId });
        var invalid = new TagId(-1);

        Assert.Throws<ArgumentOutOfRangeException>(() => world.AddTag(entity, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.RemoveTag(entity, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.HasTag(entity, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuerySpec(
            new[] { PositionId },
            Array.Empty<ComponentId>(),
            Array.Empty<ComponentId>(),
            new[] { invalid },
            Array.Empty<TagId>(),
            Array.Empty<TagId>()));
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
        world.CreateBatch(new[] { PositionId }, initial);

        var baseline = CollectChunkIds(world);

        for (var i = 0; i < 8; i++)
        {
            world.Destroy(initial[i]);
        }

        var afterDestroy = CollectChunkIds(world);
        Assert.LessOrEqual(afterDestroy.Count, baseline.Count);

        var replacement = new Entity[8];
        world.CreateBatch(new[] { PositionId }, replacement);
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
        world.CreateBatch(new[] { PositionId, VelocityId }, entities);
        for (var i = 0; i < entities.Length; i++)
        {
            world.SetComponent(entities[i], PositionId, new Position { X = i, Y = 0 });
            world.SetComponent(entities[i], VelocityId, new Velocity { X = 1, Y = 2 });
        }

        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId, VelocityId));
        var position = query.Access<Position>(PositionId, AccessMode.Write);
        var velocity = query.Access<Velocity>(VelocityId, AccessMode.Read);
        var state = new QueryState { Position = position, Velocity = velocity };
        for (var warmup = 0; warmup < 3; warmup++)
        {
            world.Query(in query, ref state, s_cursorQuerySlots);
        }

        for (var i = 0; i < entities.Length; i++)
        {
            world.SetComponent(entities[i], PositionId, new Position { X = i, Y = 0 });
            world.SetComponent(entities[i], VelocityId, new Velocity { X = 1, Y = 2 });
        }

        state = new QueryState { Position = position, Velocity = velocity };
        world.Query(in query, ref state, s_cursorQuerySlots);
        world.Query(in query, ref state, s_cursorQuerySlots);
        var firstMeasuredAfter = GC.GetAllocatedBytesForCurrentThread();
        world.Query(in query, ref state, s_cursorQuerySlots);
        var after = GC.GetAllocatedBytesForCurrentThread();

        // VSTest's current-thread allocation counter reports a one-time 24-byte
        // host artifact for this isolated sample; the BDN MemoryDiagnoser is the
        // authoritative per-operation allocation gate for the same cached loop.
        Assert.That(after - firstMeasuredAfter, Is.LessThanOrEqualTo(24));
        Assert.That(state.Sum, Is.EqualTo(60f));
        Assert.That(world.TryGetComponent<Position>(entities[0], PositionId, out var actualPosition));
        Assert.That(actualPosition.X, Is.EqualTo(3f));
        Assert.That(actualPosition.Y, Is.EqualTo(6f));
    }

    [Test]
    public void Registry_Deduplicates_EqualSchema_AndRejects_ConflictingLayout()
    {
        var layouts = new ComponentLayoutRegistry();
        var first = layouts.Register<Position>(new SchemaId(10_001));
        var duplicate = layouts.Register<Position>(new SchemaId(10_001));

        Assert.AreEqual(first, duplicate);
        Assert.AreEqual(1, layouts.Count);
        Assert.Throws<InvalidOperationException>(() => layouts.Register<Velocity>(new SchemaId(10_001)));
        Assert.AreEqual(1, layouts.Count);
    }

    [Test]
    public void ArrayRows_ManagedStruct_UsesIndependentVirtualRows_AndCachedIndices()
    {
        var layouts = new ComponentLayoutRegistry();
        var localId = layouts.Register<NamedRef>(new SchemaId(10_101));
        var worldId = layouts.Register<NamedRef>(new SchemaId(10_102));
        var world = new World(layouts, chunkCapacity: 4);
        var query = world.CreateQuery(QuerySpec.ForComponents(localId, worldId));
        var local = query.Access<NamedRef>(localId, AccessMode.Read);
        var worldRow = query.Access<NamedRef>(worldId, AccessMode.Read);
        var entity = world.Create(new[] { localId, worldId });

        world.SetComponent(entity, localId, new NamedRef { Name = "local", Id = 1 });
        world.SetComponent(entity, worldId, new NamedRef { Name = "world", Id = 2 });

        var state = new ArrayQueryState { FirstRow = local, SecondRow = worldRow };
        world.Query(in query, ref state, s_readArrayRows);

        Assert.AreEqual(1, state.Count);
        Assert.AreEqual("local", state.First.Name);
        Assert.AreEqual("world", state.Second.Name);
        Assert.AreEqual(1, state.First.Id);
        Assert.AreEqual(2, state.Second.Id);
        Assert.AreNotEqual(state.First.Name, state.Second.Name);
    }

    [Test]
    public void ArrayRows_ReferenceComponent_UsesTheSameTypedRowAndSurvivesTransition()
    {
        var layouts = new ComponentLayoutRegistry();
        var referenceId = layouts.Register<ReferenceComponent>(new SchemaId(10_151));
        var markerId = layouts.Register<RefMarker>(new SchemaId(10_152));
        var world = new World(layouts, chunkCapacity: 4);
        var entity = world.Create(new[] { referenceId });
        var component = new ReferenceComponent { Value = 42 };

        Assert.That(world.SetComponent(entity, referenceId, component), Is.True);
        Assert.That(world.TryGetComponent(entity, referenceId, out ReferenceComponent actual), Is.True);
        Assert.That(actual, Is.SameAs(component));

        var query = world.CreateQuery(QuerySpec.ForComponents(referenceId));
        var reference = query.Access<ReferenceComponent>(referenceId, AccessMode.Write);
        var referenceState = new ReferenceRowState(reference, component);
        world.Query(in query, ref referenceState, static (ref ReferenceRowState current, ref QueryChunkCursor cursor) =>
        {
            var row = cursor.GetWrite(current.Binding);
            while (cursor.MoveNext())
            {
                Assert.That(row.Ref<ReferenceComponent>(cursor), Is.SameAs(current.Expected));
                row.Ref<ReferenceComponent>(cursor).Value++;
            }
        });

        world.AddComponents(new[] { markerId }, entity);

        Assert.That(world.TryGetComponent(entity, referenceId, out actual), Is.True);
        Assert.That(actual, Is.SameAs(component));
        Assert.That(actual.Value, Is.EqualTo(43));
        Assert.That(world.Destroy(entity), Is.True);
        Assert.That(world.TryGetComponent(entity, referenceId, out ReferenceComponent _), Is.False);
    }

    [Test]
    public void ArrayRows_Transitions_PreserveMappedRows_And_ClearDestroyedReferences()
    {
        var layouts = new ComponentLayoutRegistry();
        var localId = layouts.Register<NamedRef>(new SchemaId(10_201));
        var worldId = layouts.Register<NamedRef>(new SchemaId(10_202));
        var markerId = layouts.Register<RefMarker>(new SchemaId(10_203));
        var world = new World(layouts, chunkCapacity: 4);
        var entity = world.Create(new[] { localId, worldId });

        world.SetComponent(entity, localId, new NamedRef { Name = "local", Id = 11 });
        world.SetComponent(entity, worldId, new NamedRef { Name = "world", Id = 22 });
        world.AddComponents(new[] { markerId }, entity);

        Assert.True(world.TryGetComponent(entity, localId, out NamedRef localAfterAdd));
        Assert.True(world.TryGetComponent(entity, worldId, out NamedRef worldAfterAdd));
        Assert.AreEqual(11, localAfterAdd.Id);
        Assert.AreEqual(22, worldAfterAdd.Id);

        world.RemoveComponents(new[] { markerId }, entity);
        Assert.True(world.TryGetComponent(entity, localId, out NamedRef localAfterRemove));
        Assert.True(world.TryGetComponent(entity, worldId, out NamedRef worldAfterRemove));
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
        var id = layouts.Register<NamedRef>(new SchemaId(10_301));
        var world = new World(layouts);
        var entity = world.Create(new[] { id });
        var spec = QuerySpec.ForComponents(id);
        var query = world.CreateQuery(in spec);
        var state = new LeaseMutationState { World = world, Entity = entity };

        Assert.Throws<InvalidOperationException>(() => world.Query(in query, ref state, s_destroyDuringLease));
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
        world.SetComponent(first, PositionId, new Position { X = 10, Y = 11 });
        world.SetComponent(first, VelocityId, new Velocity { X = 20, Y = 21 });

        world.AddComponents(new[] { HealthId }, first);

        Assert.True(world.TryGetComponent<Position>(first, PositionId, out var posAfterAdd));
        Assert.True(world.TryGetComponent<Velocity>(first, VelocityId, out var velAfterAdd));
        Assert.True(world.TryGetComponent<Health>(first, HealthId, out var healthAfterAdd));
        Assert.AreEqual(10, posAfterAdd.X);
        Assert.AreEqual(21, velAfterAdd.Y);
        Assert.AreEqual(0, healthAfterAdd.Value);

        world.RemoveComponents(new[] { VelocityId }, first);

        Assert.True(world.TryGetComponent<Position>(first, PositionId, out var posAfterRemove));
        Assert.True(world.TryGetComponent<Health>(first, HealthId, out _));
        Assert.False(world.TryGetComponent<Velocity>(first, VelocityId, out _));
        Assert.AreEqual(10, posAfterRemove.X);
    }

    [Test]
    public void OverlayTags_Filter_With_All_Any_None_Without_ArchetypeChange()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);

        var entities = new Entity[64];
        for (var i = 0; i < entities.Length; i++)
        {
            entities[i] = world.Create(new[] { PositionId });
            if ((i & 1) == 0)
            {
                world.AddTag(entities[i], TagActive);
            }

            if ((i & 3) == 0)
            {
                world.AddTag(entities[i], TagVisible);
            }
        }

        var even = new QuerySpec(
            new[] { PositionId },
            Array.Empty<ComponentId>(),
            Array.Empty<ComponentId>(),
            new[] { TagActive },
            Array.Empty<TagId>(),
            Array.Empty<TagId>());

        var visibleEven = new QuerySpec(
            new[] { PositionId },
            Array.Empty<ComponentId>(),
            Array.Empty<ComponentId>(),
            new[] { TagActive },
            Array.Empty<TagId>(),
            new[] { TagVisible });

        var visibleOnly = new QuerySpec(
            new[] { PositionId },
            Array.Empty<ComponentId>(),
            Array.Empty<ComponentId>(),
            Array.Empty<TagId>(),
            new[] { TagVisible },
            Array.Empty<TagId>());

        var c1 = 0;
        var c2 = 0;
        var c3 = 0;

        c1 = CountQuery(world, even);
        c2 = CountQuery(world, visibleEven);
        c3 = CountQuery(world, visibleOnly);

        Assert.AreEqual(32, c1);
        Assert.AreEqual(16, c2);
        Assert.AreEqual(16, c3);
    }

    [Test]
    public void RandomizedInvariants_WithTransitionsAndTags()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);
        var random = new Random(123456);

        var model = new Dictionary<int, EntityState>();

        var allEntities = new List<Entity>();
        for (var step = 0; step < 2_000; step++)
        {
            var action = random.Next(5);
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
                        TagA = random.NextDouble() > 0.7,
                        TagB = random.NextDouble() > 0.7
                    };

                    world.SetComponent(entity, PositionId, state.Position);
                    if (state.Velocity.HasValue)
                    {
                        world.SetComponent(entity, VelocityId, state.Velocity.Value);
                    }

                    if (state.Health.HasValue)
                    {
                        world.SetComponent(entity, HealthId, state.Health.Value);
                    }

                    if (state.TagA)
                    {
                        world.AddTag(entity, TagActive);
                    }

                    if (state.TagB)
                    {
                        world.AddTag(entity, TagVisible);
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
                    world.AddComponents(new[] { VelocityId }, entity);

                    if (!current.Velocity.HasValue)
                    {
                        current.Velocity = new Velocity();
                    }
                }
                else
                {
                    world.RemoveComponents(new[] { VelocityId }, entity);
                    current.Velocity = null;
                }

                model[entity.Index] = current;
            }
            else if (action == 3 && allEntities.Count > 0)
            {
                var index = random.Next(allEntities.Count);
                var entity = allEntities[index];
                if (random.NextDouble() > 0.5)
                {
                    world.AddTag(entity, TagActive);
                    var updated = model[entity.Index];
                    updated.TagA = true;
                    model[entity.Index] = updated;
                }
                else
                {
                    world.RemoveTag(entity, TagActive);
                    var updated = model[entity.Index];
                    updated.TagA = false;
                    model[entity.Index] = updated;
                }
            }
            else if (action == 4 && allEntities.Count > 0)
            {
                var index = random.Next(allEntities.Count);
                var entity = allEntities[index];
                if (world.IsAlive(entity))
                {
                    var newPosition = new Position { X = random.NextSingle(), Y = random.NextSingle() };
                    world.SetComponent(entity, PositionId, newPosition);

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
        layouts.Register<Position>(new SchemaId(1));
        layouts.Register<Velocity>(new SchemaId(2));
        layouts.Register<Health>(new SchemaId(3));
    }

    private static int CountActiveSlots(ref QueryChunkCursor cursor)
    {
        var count = 0;
        while (cursor.MoveNext())
        {
            if (cursor.IsActiveSlot(cursor.CurrentIndex))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountQuery(World world, in QuerySpec query)
    {
        var handle = world.CreateQuery(in query);
        var count = 0;
        world.Query(in handle, ref count, static (ref int state, ref QueryChunkCursor cursor) =>
        {
            state += CountActiveSlots(ref cursor);
        });
        return count;
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
            Assert.True(world.TryGetComponent<Position>(entity, PositionId, out var position));
            Assert.That(Math.Abs(expected.Position.X - position.X) < 1e-5f, $"Mismatch Position.X for entity {entity}");
            Assert.That(Math.Abs(expected.Position.Y - position.Y) < 1e-5f, $"Mismatch Position.Y for entity {entity}");

            Assert.AreEqual(expected.TagA, world.HasTag(entity, TagActive));
            Assert.AreEqual(expected.TagB, world.HasTag(entity, TagVisible));

            if (expected.Velocity.HasValue)
            {
                Assert.True(world.TryGetComponent<Velocity>(entity, VelocityId, out var velocity));
                Assert.AreEqual(expected.Velocity.Value.X, velocity.X, 1e-5f);
            }
            else
            {
                Assert.False(world.TryGetComponent<Velocity>(entity, VelocityId, out _));
            }

            if (expected.Health.HasValue)
            {
                Assert.True(world.TryGetComponent<Health>(entity, HealthId, out var health));
                Assert.AreEqual(expected.Health.Value.Value, health.Value);
            }
            else
            {
                Assert.False(world.TryGetComponent<Health>(entity, HealthId, out _));
            }
        }
    }

    private struct QueryState
    {
        public float Sum;
        public AccessRequest Position;
        public AccessRequest Velocity;
    }

    private struct BoundRowState
    {
        public BoundRowState(AccessRequest position, AccessRequest velocity)
        {
            Position = position;
            Velocity = velocity;
            Rows = 0;
            Chunks = new HashSet<int>();
        }

        public AccessRequest Position;
        public AccessRequest Velocity;
        public int Rows;
        public HashSet<int> Chunks;
    }

    private static void ApplyBoundRows(ref BoundRowState state, ref QueryChunkCursor cursor)
    {
        var positions = cursor.GetWrite(state.Position);
        var velocities = cursor.GetRead(state.Velocity);
        state.Chunks.Add(cursor.GlobalChunkId);
        while (cursor.MoveNext())
        {
            ref var position = ref positions.Ref<Position>(cursor);
            position.X += velocities.Ref<Velocity>(cursor).X;
            state.Rows++;
        }
    }

    private static void ExecuteReadWithWriteBinding(
        World world,
        Query query,
        AccessRequest position)
    {
        var state = new BoundRowState(position, default);
        world.Query(in query, ref state, static (ref BoundRowState current, ref QueryChunkCursor cursor) =>
        {
            _ = cursor.GetWrite(current.Position);
        });
    }

    private struct AlignmentState
    {
        public AlignmentState(
            Dictionary<Entity, int> expected,
            AccessRequest position,
            AccessRequest velocity)
        {
            Expected = expected;
            Position = position;
            Velocity = velocity;
            Count = 0;
        }

        public Dictionary<Entity, int> Expected;
        public AccessRequest Position;
        public AccessRequest Velocity;
        public int Count;
    }

    private static void AssertAlignedRows(ref AlignmentState state, ref QueryChunkCursor cursor)
    {
        ReadOnlySpan<Entity> entities = cursor.Entities;
        var positions = cursor.GetRead(state.Position);
        var velocities = cursor.GetRead(state.Velocity);
        while (cursor.MoveNext())
        {
            var entity = entities[cursor.CurrentIndex];
            Assert.That(state.Expected.ContainsKey(entity), Is.True);
            Assert.That(positions.Ref<Position>(cursor).X, Is.EqualTo(state.Expected[entity]));
            Assert.That(velocities.Ref<Velocity>(cursor).X, Is.EqualTo(state.Expected[entity] + 10));
            state.Count++;
        }
    }

    private static void QuerySlots(ref QueryState state, ref QueryChunkCursor cursor)
    {
        var positions = cursor.GetWrite(state.Position);
        var velocities = cursor.GetRead(state.Velocity);
        while (cursor.MoveNext())
        {
            ref var position = ref positions.Ref<Position>(cursor);
            position.X += velocities.Ref<Velocity>(cursor).X;
            position.Y += velocities.Ref<Velocity>(cursor).Y;
            state.Sum += positions.Ref<Position>(cursor).X;
        }
    }

    private static void ReadArrayRows(ref ArrayQueryState state, ref QueryChunkCursor cursor)
    {
        var first = cursor.GetRead(state.FirstRow);
        var second = cursor.GetRead(state.SecondRow);
        while (cursor.MoveNext())
        {
            if (!cursor.IsActiveSlot(cursor.CurrentIndex))
            {
                continue;
            }

            state.First = first.Ref<NamedRef>(cursor);
            state.Second = second.Ref<NamedRef>(cursor);
            state.Count++;
        }
    }

    private static void DestroyDuringLease(ref LeaseMutationState state, ref QueryChunkCursor cursor)
    {
        state.World.Destroy(state.Entity);
    }

    private static HashSet<int> CollectChunkIds(World world)
    {
        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        var state = new ChunkIdState(new HashSet<int>());
        world.Query(in query, ref state, s_collectChunkIds);
        return state.ChunkIds;
    }

    private static void CollectChunkIds(ref ChunkIdState state, ref QueryChunkCursor cursor)
    {
        state.ChunkIds.Add(cursor.GlobalChunkId);
    }

    private static WeakReference<RefPayload> CreateDestroyedReference()
    {
        var layouts = new ComponentLayoutRegistry();
        var id = layouts.Register<RefMarker>(new SchemaId(10_401));
        var world = new World(layouts);
        var entity = world.Create(new[] { id });
        var payload = new RefPayload(42);
        world.SetComponent(entity, id, new RefMarker { Payload = payload, Value = 42 });
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
        public bool TagA;
        public bool TagB;
        public int ArchetypeKey;
    }

    private struct ArrayQueryState
    {
        public int Count;
        public NamedRef First;
        public NamedRef Second;
        public AccessRequest FirstRow;
        public AccessRequest SecondRow;
    }

    private struct OverlayQueryState
    {
        public OverlayQueryState(HashSet<Entity> entities)
        {
            Entities = entities;
            SawPartialChunk = false;
        }

        public HashSet<Entity> Entities;
        public bool SawPartialChunk;
    }

    private struct CursorTaggedState
    {
        public CursorTaggedState(AccessRequest position)
        {
            Position = position;
        }

        public AccessRequest Position;
        public int ActiveCount;
        public float Sum;
    }

    private sealed class OverlaySummary
    {
        public int ActiveSlots;
        public int Chunks;
        public bool SawPartial;
        public bool SawFull;

        public void Observe(ref QueryChunkCursor cursor)
        {
            Chunks++;
            var chunkActive = 0;
            while (cursor.MoveNext())
            {
                if (cursor.IsActiveSlot(cursor.CurrentIndex))
                {
                    chunkActive++;
                }
            }

            ActiveSlots += chunkActive;
            if (chunkActive == cursor.SlotCount)
            {
                SawFull = true;
            }
            else
            {
                SawPartial = true;
            }
        }
    }

    private readonly struct ReferenceRowState
    {
        public ReferenceRowState(
            AccessRequest binding,
            ReferenceComponent expected)
        {
            Binding = binding;
            Expected = expected;
        }

        public AccessRequest Binding { get; }
        public ReferenceComponent Expected { get; }
    }

    private struct LeaseMutationState
    {
        public World World;
        public Entity Entity;
    }

    private struct ChunkIdState
    {
        public HashSet<int> ChunkIds;

        public ChunkIdState(HashSet<int> chunkIds)
        {
            ChunkIds = chunkIds;
        }
    }
}
