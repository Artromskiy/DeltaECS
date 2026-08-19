using System;
using System.Collections.Generic;
using NUnit.Framework;
using Delta.ECS;

namespace Delta.ECS.Tests;

/// <summary>
/// Deterministic regression coverage for the structural algorithms.  These tests
/// intentionally use a small chunk size in most cases so that swap-back and
/// overlay masks are exercised across several chunks.
/// </summary>
[TestFixture]
public sealed class StructuralAlgorithmTests
{
    private static readonly TagId TagA = new(701);
    private static readonly TagId TagB = new(702);
    private static readonly TagId TagC = new(703);

    [Test]
    public void DestroyBatch_RandomContiguousDuplicateAndStaleHandles_PreservesSurvivors()
    {
        var layouts = new ComponentLayoutRegistry();
        var valueId = layouts.Register<DestroyValue>(new SchemaId(30_001));
        var world = new World(layouts, chunkCapacity: 7);
        var entities = new Entity[96];
        world.CreateBatch(new[] { valueId }, entities);
        for (var i = 0; i < entities.Length; i++)
        {
            world.SetComponent(entities[i], valueId, new DestroyValue { Value = 100_000 + i });
        }

        var stale = entities[3];
        Assert.That(world.Destroy(stale), Is.True);

        var requested = new List<Entity>();
        var expectedDestroyed = new HashSet<Entity>();
        for (var i = 20; i < 42; i++)
        {
            requested.Add(entities[i]);
            expectedDestroyed.Add(entities[i]);
        }

        var random = new Random(0xD35_701);
        for (var i = 0; i < 23; i++)
        {
            var index = random.Next(42, entities.Length);
            requested.Add(entities[index]);
            expectedDestroyed.Add(entities[index]);
            if ((i & 3) == 0)
            {
                requested.Add(entities[index]);
            }
        }

        // A stale handle and a duplicate must not affect the exact count.
        requested.Add(stale);
        requested.Add(stale);
        var destroyed = world.DestroyBatch(CollectionsMarshalCompat.AsSpan(requested));

        Assert.That(destroyed, Is.EqualTo(expectedDestroyed.Count));
        Assert.That(world.AliveEntityCount, Is.EqualTo(entities.Length - 1 - expectedDestroyed.Count));
        Assert.That(world.IsAlive(stale), Is.False);
        foreach (var entity in entities)
        {
            if (entity == stale || expectedDestroyed.Contains(entity))
            {
                Assert.That(world.IsAlive(entity), Is.False, entity.ToString());
                continue;
            }

            Assert.That(world.TryGetComponent<DestroyValue>(entity, valueId, out var value), Is.True);
            Assert.That(value.Value, Is.EqualTo(100_000 + entity.Index));
        }
    }

    [Test]
    public void DestroyBatch_RecreateRecyclesAllDestroyedRecordsWithNewGenerations()
    {
        var layouts = new ComponentLayoutRegistry();
        var valueId = layouts.Register<DestroyValue>(new SchemaId(30_002));
        var world = new World(layouts, chunkCapacity: 5);
        var old = new Entity[64];
        world.CreateBatch(new[] { valueId }, old);

        Assert.That(world.DestroyBatch(old), Is.EqualTo(old.Length));
        Assert.That(world.AliveEntityCount, Is.Zero);

        var recreated = new Entity[old.Length];
        world.CreateBatch(new[] { valueId }, recreated);
        var oldByIndex = new Dictionary<int, Entity>();
        foreach (var entity in old)
        {
            oldByIndex[entity.Index] = entity;
        }

        foreach (var entity in recreated)
        {
            Assert.That(oldByIndex.TryGetValue(entity.Index, out var previous), Is.True);
            Assert.That(entity.Generation, Is.Not.EqualTo(previous.Generation));
            Assert.That(world.IsAlive(previous), Is.False);
            Assert.That(world.IsAlive(entity), Is.True);
        }
    }

    [Test]
    public void DestroyBatch_10KSmoke_UsesExactCountAndLeavesValidHandles()
    {
        var layouts = new ComponentLayoutRegistry();
        var valueId = layouts.Register<DestroyValue>(new SchemaId(30_003));
        var world = new World(layouts, chunkCapacity: 257);
        var entities = new Entity[10_000];
        world.CreateBatch(new[] { valueId }, entities);

        var requested = new List<Entity>(5_500);
        var expected = new HashSet<Entity>();
        for (var i = 0; i < entities.Length; i += 2)
        {
            requested.Add(entities[i]);
            expected.Add(entities[i]);
            if ((i % 10) == 0)
            {
                requested.Add(entities[i]);
            }
        }

        Assert.That(world.DestroyBatch(CollectionsMarshalCompat.AsSpan(requested)), Is.EqualTo(expected.Count));
        Assert.That(world.AliveEntityCount, Is.EqualTo(entities.Length - expected.Count));
        foreach (var entity in entities)
        {
            Assert.That(world.IsAlive(entity), Is.EqualTo(!expected.Contains(entity)));
        }
    }

    [Test]
    public void RandomizedBatchTransitions_MatchReferenceModelAndPreserveValues()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register<TransitionPosition>(new SchemaId(30_010));
        var velocityId = layouts.Register<TransitionVelocity>(new SchemaId(30_011));
        var healthId = layouts.Register<TransitionHealth>(new SchemaId(30_012));
        var world = new World(layouts, chunkCapacity: 9);
        var random = new Random(0x51A_7E);
        var model = new Dictionary<Entity, TransitionState>();
        var entities = new List<Entity>();

        for (var i = 0; i < 180; i++)
        {
            var entity = world.Create(new[] { positionId, velocityId, healthId });
            var state = new TransitionState
            {
                Entity = entity,
                Position = new TransitionPosition { Value = 1_000 + i },
                Velocity = new TransitionVelocity { Value = 2_000 + i },
                Health = new TransitionHealth { Value = 3_000 + i },
                HasVelocity = true,
                HasHealth = true
            };
            world.SetComponent(entity, positionId, state.Position);
            world.SetComponent(entity, velocityId, state.Velocity);
            world.SetComponent(entity, healthId, state.Health);
            model.Add(entity, state);
            entities.Add(entity);
        }

        for (var step = 0; step < 360; step++)
        {
            if (entities.Count > 40 && (step % 19) == 0)
            {
                var destroyIndex = random.Next(entities.Count);
                var removed = entities[destroyIndex];
                Assert.That(world.Destroy(removed), Is.True);
                entities.RemoveAt(destroyIndex);
                model.Remove(removed);
            }

            if ((step % 23) == 0)
            {
                var entity = world.Create(new[] { positionId });
                var state = new TransitionState
                {
                    Entity = entity,
                    Position = new TransitionPosition { Value = 10_000 + step },
                    HasVelocity = false,
                    HasHealth = false
                };
                world.SetComponent(entity, positionId, state.Position);
                entities.Add(entity);
                model.Add(entity, state);
            }

            var selected = SelectUnique(entities, random, random.Next(1, Math.Min(28, entities.Count) + 1));
            var addVelocity = (step & 1) == 0;
            world.AddComponents(addVelocity ? new[] { velocityId } : new[] { healthId }, CollectionsMarshalCompat.AsSpan(selected));
            foreach (var entity in selected)
            {
                var state = model[entity];
                if (addVelocity)
                {
                    state.HasVelocity = true;
                    state.Velocity = state.Velocity.Value == 0
                        ? new TransitionVelocity { Value = 0 }
                        : state.Velocity;
                }
                else
                {
                    state.HasHealth = true;
                    state.Health = state.Health.Value == 0
                        ? new TransitionHealth { Value = 0 }
                        : state.Health;
                }

                model[entity] = state;
            }

            if ((step % 3) == 0)
            {
                var second = SelectUnique(entities, random, random.Next(1, Math.Min(19, entities.Count) + 1));
                var removeVelocity = (step % 4) == 0;
                world.RemoveComponents(removeVelocity ? new[] { velocityId } : new[] { healthId }, CollectionsMarshalCompat.AsSpan(second));
                foreach (var entity in second)
                {
                    var state = model[entity];
                    if (removeVelocity)
                    {
                        state.HasVelocity = false;
                        state.Velocity = default;
                    }
                    else
                    {
                        state.HasHealth = false;
                        state.Health = default;
                    }

                    model[entity] = state;
                }
            }

            AssertTransitionModel(world, model, positionId, velocityId, healthId);
        }
    }

    [Test]
    public void OverlayTagChurn_ClusteredAndRandomQueriesMatchReferenceAfterDestroySwapBack()
    {
        var layouts = new ComponentLayoutRegistry();
        var payloadId = layouts.Register<TagPayload>(new SchemaId(30_020));
        var world = new World(layouts, chunkCapacity: 11);
        var random = new Random(0xA11_0F);
        var model = new Dictionary<Entity, TagState>();
        var entities = new List<Entity>();

        for (var i = 0; i < 220; i++)
        {
            var entity = world.Create(new[] { payloadId });
            var state = new TagState { A = (i % 3) == 0, B = (i % 5) == 0, C = (i % 7) == 0 };
            world.SetComponent(entity, payloadId, new TagPayload { Value = i });
            ApplyTags(world, entity, state);
            model.Add(entity, state);
            entities.Add(entity);
        }

        for (var step = 0; step < 260; step++)
        {
            var toggleCount = 1 + random.Next(7);
            for (var i = 0; i < toggleCount; i++)
            {
                var entity = entities[random.Next(entities.Count)];
                var state = model[entity];
                var tag = (step % 5) == 0 ? TagA : new[] { TagA, TagB, TagC }[random.Next(3)];
                if (tag == TagA)
                {
                    state.A = !state.A;
                }
                else if (tag == TagB)
                {
                    state.B = !state.B;
                }
                else
                {
                    state.C = !state.C;
                }

                if (state.A && tag == TagA || state.B && tag == TagB || state.C && tag == TagC)
                {
                    world.AddTag(entity, tag);
                }
                else
                {
                    world.RemoveTag(entity, tag);
                }

                model[entity] = state;
            }

            if ((step % 11) == 0)
            {
                var destroyCount = 1 + random.Next(3);
                var removed = SelectUnique(entities, random, Math.Min(destroyCount, entities.Count));
                var removedCount = world.DestroyBatch(CollectionsMarshalCompat.AsSpan(removed));
                Assert.That(removedCount, Is.EqualTo(removed.Count));
                foreach (var entity in removed)
                {
                    model.Remove(entity);
                    entities.Remove(entity);
                }
            }

            if ((step % 13) == 0)
            {
                var entity = world.Create(new[] { payloadId });
                var state = new TagState
                {
                    A = (step % 26) == 0,
                    B = random.Next(2) == 0,
                    C = random.Next(2) == 0
                };
                ApplyTags(world, entity, state);
                model.Add(entity, state);
                entities.Add(entity);
            }

            var allA = new QueryDescription(
                new[] { payloadId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(),
                new[] { TagA }, Array.Empty<TagId>(), Array.Empty<TagId>());
            var anyAB = new QueryDescription(
                new[] { payloadId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(),
                Array.Empty<TagId>(), new[] { TagA, TagB }, Array.Empty<TagId>());
            var noneC = new QueryDescription(
                new[] { payloadId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(),
                Array.Empty<TagId>(), Array.Empty<TagId>(), new[] { TagC });
            var combined = new QueryDescription(
                new[] { payloadId }, Array.Empty<ComponentId>(), Array.Empty<ComponentId>(),
                new[] { TagA }, new[] { TagB, TagC }, new[] { TagC });

            Assert.That(CountQuery(world, allA), Is.EqualTo(Count(model, static s => s.A)));
            Assert.That(CountQuery(world, anyAB), Is.EqualTo(Count(model, static s => s.A || s.B)));
            Assert.That(CountQuery(world, noneC), Is.EqualTo(Count(model, static s => !s.C)));
            Assert.That(CountQuery(world, combined), Is.EqualTo(Count(model, static s => s.A && s.B && !s.C)));
        }
    }

    [Test]
    public void HierarchyFixture_RandomizedStorageHasTopologicalOrderAndReferenceChecksum()
    {
        var layouts = new ComponentLayoutRegistry();
        var parentId = layouts.Register<ParentLink>(new SchemaId(30_030));
        var localId = layouts.Register<LocalTransform>(new SchemaId(30_031));
        var worldId = layouts.Register<WorldTransform>(new SchemaId(30_032));
        var world = new World(layouts, chunkCapacity: 8);
        const int count = 127;
        var random = new Random(0x1E_2); // fixed seed; no timing or ambient state
        var parentIndices = new int[count];
        var levels = new int[count];
        parentIndices[0] = -1;
        levels[0] = 0;
        for (var i = 1; i < count; i++)
        {
            parentIndices[i] = random.Next(i);
            levels[i] = levels[parentIndices[i]] + 1;
        }

        var storageOrder = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            storageOrder.Add(i);
        }

        Shuffle(storageOrder, random);
        var entities = new Entity[count];
        foreach (var node in storageOrder)
        {
            entities[node] = world.Create(new[] { parentId, localId, worldId });
        }

        var expectedWorld = new WorldTransform[count];
        var expectedLocal = new LocalTransform[count];
        for (var i = 0; i < count; i++)
        {
            expectedLocal[i] = new LocalTransform { Value = new TransformPod { X = 1 + i, Y = (i * 17) % 101 } };
            var parentWorld = parentIndices[i] < 0 ? default : expectedWorld[parentIndices[i]];
            expectedWorld[i] = new WorldTransform
            {
                Value = new TransformPod
                {
                    X = parentWorld.Value.X + expectedLocal[i].Value.X,
                    Y = parentWorld.Value.Y + expectedLocal[i].Value.Y
                }
            };

            var parent = parentIndices[i] < 0 ? Entity.Null : entities[parentIndices[i]];
            Assert.That(world.SetComponent(entities[i], parentId, new ParentLink { Parent = parent }), Is.True);
            Assert.That(world.SetComponent(entities[i], localId, expectedLocal[i]), Is.True);
            Assert.That(world.SetComponent(entities[i], worldId, expectedWorld[i]), Is.True);
        }

        var observed = new Dictionary<Entity, HierarchyObserved>();
        var query = QueryDescription.ForComponents(parentId, localId, worldId);
        world.Query(in query, QueryAccess.Read, lease =>
        {
            var parents = lease.GetComponentRow<ParentLink>(parentId);
            var locals = lease.GetComponentRow<LocalTransform>(localId);
            var worlds = lease.GetComponentRow<WorldTransform>(worldId);
            for (var slot = lease.SlotCount - 1; slot >= 0; slot--)
            {
                if (!lease.IsActiveSlot(slot))
                {
                    continue;
                }

                observed[lease.Entities[slot]] = new HierarchyObserved
                {
                    Parent = parents[slot].Parent,
                    Local = locals[slot],
                    World = worlds[slot]
                };
            }
        });

        Assert.That(observed.Count, Is.EqualTo(count));
        var topological = new List<int>(count);
        for (var level = 0; level <= Max(levels); level++)
        {
            for (var i = 0; i < count; i++)
            {
                if (levels[i] == level)
                {
                    topological.Add(i);
                }
            }
        }

        var position = new Dictionary<Entity, int>();
        for (var i = 0; i < topological.Count; i++)
        {
            position[entities[topological[i]]] = i;
        }

        long checksum = 0;
        for (var i = 0; i < count; i++)
        {
            var item = observed[entities[i]];
            Assert.That(item.Local.Value, Is.EqualTo(expectedLocal[i].Value));
            Assert.That(item.World.Value, Is.EqualTo(expectedWorld[i].Value));
            if (parentIndices[i] >= 0)
            {
                Assert.That(position[item.Parent], Is.LessThan(position[entities[i]]));
            }

            checksum += (long)(i + 1) * (expectedWorld[i].Value.X * 1_003L + expectedWorld[i].Value.Y);
        }

        long observedChecksum = 0;
        for (var i = 0; i < topological.Count; i++)
        {
            var node = topological[i];
            var value = observed[entities[node]].World.Value;
            observedChecksum += (long)(node + 1) * (value.X * 1_003L + value.Y);
        }

        Assert.That(observedChecksum, Is.EqualTo(checksum));
    }

    private static void AssertTransitionModel(
        World world,
        Dictionary<Entity, TransitionState> model,
        ComponentId positionId,
        ComponentId velocityId,
        ComponentId healthId)
    {
        Assert.That(world.AliveEntityCount, Is.EqualTo(model.Count));
        var alive = new Entity[Math.Max(1, model.Count)];
        var aliveCount = world.CollectAliveEntities(alive);
        Assert.That(aliveCount, Is.EqualTo(model.Count));
        for (var i = 0; i < aliveCount; i++)
        {
            var entity = alive[i];
            Assert.That(model.TryGetValue(entity, out var expected), Is.True);
            Assert.That(world.TryGetComponent<TransitionPosition>(entity, positionId, out var position), Is.True);
            Assert.That(position.Value, Is.EqualTo(expected.Position.Value));
            Assert.That(world.TryGetComponent<TransitionVelocity>(entity, velocityId, out var velocity), Is.EqualTo(expected.HasVelocity),
                $"velocity presence mismatch for {entity}; expected={expected.HasVelocity}, position={expected.Position.Value}");
            if (expected.HasVelocity)
            {
                Assert.That(velocity.Value, Is.EqualTo(expected.Velocity.Value));
            }

            Assert.That(world.TryGetComponent<TransitionHealth>(entity, healthId, out var health), Is.EqualTo(expected.HasHealth),
                $"health presence mismatch for {entity}; expected={expected.HasHealth}");
            if (expected.HasHealth)
            {
                Assert.That(health.Value, Is.EqualTo(expected.Health.Value));
            }
        }
    }

    private static int CountQuery(World world, in QueryDescription query)
    {
        var count = 0;
        world.Query(in query, QueryAccess.Read, lease =>
        {
            for (var slot = lease.SlotCount - 1; slot >= 0; slot--)
            {
                if (lease.IsActiveSlot(slot))
                {
                    count++;
                }
            }
        });

        return count;
    }

    private static int Count(Dictionary<Entity, TagState> model, Predicate<TagState> predicate)
    {
        var count = 0;
        foreach (var state in model.Values)
        {
            if (predicate(state))
            {
                count++;
            }
        }

        return count;
    }

    private static List<Entity> SelectUnique(List<Entity> entities, Random random, int requested)
    {
        var indexes = new HashSet<int>();
        while (indexes.Count < requested)
        {
            indexes.Add(random.Next(entities.Count));
        }

        var selected = new List<Entity>(requested);
        foreach (var index in indexes)
        {
            selected.Add(entities[index]);
        }

        return selected;
    }

    private static void ApplyTags(World world, Entity entity, TagState state)
    {
        if (state.A) world.AddTag(entity, TagA);
        if (state.B) world.AddTag(entity, TagB);
        if (state.C) world.AddTag(entity, TagC);
    }

    private static void Shuffle(List<int> values, Random random)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private static int Max(int[] values)
    {
        var max = 0;
        foreach (var value in values)
        {
            max = Math.Max(max, value);
        }

        return max;
    }

    private readonly struct DestroyValue
    {
        public int Value { get; init; }
    }

    private struct TransitionPosition
    {
        public int Value;
    }

    private struct TransitionVelocity
    {
        public int Value;
    }

    private struct TransitionHealth
    {
        public int Value;
    }

    private struct TagPayload
    {
        public int Value;
    }

    private struct ParentLink
    {
        public Entity Parent;
    }

    private struct TransformPod : IEquatable<TransformPod>
    {
        public int X;
        public int Y;

        public bool Equals(TransformPod other) => X == other.X && Y == other.Y;
    }

    private struct LocalTransform
    {
        public TransformPod Value;
    }

    private struct WorldTransform
    {
        public TransformPod Value;
    }

    private struct TransitionState
    {
        public Entity Entity;
        public TransitionPosition Position;
        public TransitionVelocity Velocity;
        public TransitionHealth Health;
        public bool HasVelocity;
        public bool HasHealth;
    }

    private struct TagState
    {
        public bool A;
        public bool B;
        public bool C;
    }

    private struct HierarchyObserved
    {
        public Entity Parent;
        public LocalTransform Local;
        public WorldTransform World;
    }

    // Span<T> cannot be obtained from List<T> on all target SDKs.  Keep the
    // conversion in one test-only helper so tests remain compatible with the
    // net8 SDK used by CI.
    private static class CollectionsMarshalCompat
    {
        public static Entity[] AsSpan(List<Entity> values) => values.ToArray();
    }
}
