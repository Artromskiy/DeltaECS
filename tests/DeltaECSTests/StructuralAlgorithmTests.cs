using System;
using System.Collections.Generic;
using Delta.ECS;
using NUnit.Framework;

namespace Delta.ECS.Tests;

/// <summary>
/// Deterministic regression coverage for the structural algorithms.  These tests
/// intentionally use enough entities in the cases that need several chunks so
/// that swap-back is exercised with the fixed runtime chunk size.
/// </summary>
[TestFixture]
internal sealed class StructuralAlgorithmTests
{
    [Test]
    public void DestroyBatchRandomContiguousDuplicateAndStaleHandlesPreservesSurvivors()
    {
        var layouts = new ComponentLayoutRegistry();
        var valueId = layouts.Register<DestroyValue>(new SchemaId(30_001));
        using var world = new World(layouts);
        var entities = new Entity[96];
        world.Create(new[] { valueId }, entities);
        for (var i = 0; i < entities.Length; i++)
        {
            world.Set(entities[i], valueId, new DestroyValue { Value = 100_000 + i });
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

        var random = new DeterministicRandom(0xD35_701);
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
        var destroyed = world.Destroy(CollectionsMarshalCompat.AsSpan(requested));

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

            Assert.That(world.TryGet<DestroyValue>(entity, valueId, out var value), Is.True);
            Assert.That(value.Value, Is.EqualTo(100_000 + entity.Index));
        }
    }

    [Test]
    public void DestroyBatchRecreateRecyclesAllDestroyedRecordsWithNewGenerations()
    {
        var layouts = new ComponentLayoutRegistry();
        var valueId = layouts.Register<DestroyValue>(new SchemaId(30_002));
        using var world = new World(layouts);
        var old = new Entity[64];
        world.Create(new[] { valueId }, old);

        Assert.That(world.Destroy(old), Is.EqualTo(old.Length));
        Assert.That(world.AliveEntityCount, Is.Zero);

        var recreated = new Entity[old.Length];
        world.Create(new[] { valueId }, recreated);
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
    public void DestroyBatch10KSmokeUsesExactCountAndLeavesValidHandles()
    {
        var layouts = new ComponentLayoutRegistry();
        var valueId = layouts.Register<DestroyValue>(new SchemaId(30_003));
        using var world = new World(layouts);
        var entities = new Entity[10_000];
        world.Create(new[] { valueId }, entities);

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

        Assert.That(world.Destroy(CollectionsMarshalCompat.AsSpan(requested)), Is.EqualTo(expected.Count));
        Assert.That(world.AliveEntityCount, Is.EqualTo(entities.Length - expected.Count));
        foreach (var entity in entities)
        {
            Assert.That(world.IsAlive(entity), Is.EqualTo(!expected.Contains(entity)));
        }
    }

    [Test]
    public void RandomizedBatchTransitionsMatchReferenceModelAndPreserveValues()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register<TransitionPosition>(new SchemaId(30_010));
        var velocityId = layouts.Register<TransitionVelocity>(new SchemaId(30_011));
        var healthId = layouts.Register<TransitionHealth>(new SchemaId(30_012));
        using var world = new World(layouts);
        var random = new DeterministicRandom(0x51A_7E);
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
            world.Set(entity, positionId, state.Position);
            world.Set(entity, velocityId, state.Velocity);
            world.Set(entity, healthId, state.Health);
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
                world.Set(entity, positionId, state.Position);
                entities.Add(entity);
                model.Add(entity, state);
            }

            var selected = SelectUnique(entities, random, random.Next(1, Math.Min(28, entities.Count) + 1));
            var addVelocity = (step & 1) == 0;
            world.Add(CollectionsMarshalCompat.AsSpan(selected), addVelocity ? new[] { velocityId } : new[] { healthId });
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
                world.Remove(CollectionsMarshalCompat.AsSpan(second), removeVelocity ? new[] { velocityId } : new[] { healthId });
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

    private static void AssertTransitionModel(
        World world,
        Dictionary<Entity, TransitionState> model,
        ComponentId positionId,
        ComponentId velocityId,
        ComponentId healthId)
    {
        Assert.That(world.AliveEntityCount, Is.EqualTo(model.Count));
        foreach (var pair in model)
        {
            var entity = pair.Key;
            var expected = pair.Value;
            Assert.That(world.TryGet<TransitionPosition>(entity, positionId, out var position), Is.True);
            Assert.That(position.Value, Is.EqualTo(expected.Position.Value));
            Assert.That(world.TryGet<TransitionVelocity>(entity, velocityId, out var velocity), Is.EqualTo(expected.HasVelocity),
                $"velocity presence mismatch for {entity}; expected={expected.HasVelocity}, position={expected.Position.Value}");
            if (expected.HasVelocity)
            {
                Assert.That(velocity.Value, Is.EqualTo(expected.Velocity.Value));
            }

            Assert.That(world.TryGet<TransitionHealth>(entity, healthId, out var health), Is.EqualTo(expected.HasHealth),
                $"health presence mismatch for {entity}; expected={expected.HasHealth}");
            if (expected.HasHealth)
            {
                Assert.That(health.Value, Is.EqualTo(expected.Health.Value));
            }
        }
    }

    private static List<Entity> SelectUnique(List<Entity> entities, DeterministicRandom random, int requested)
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

    private struct TransitionState
    {
        public Entity Entity;
        public TransitionPosition Position;
        public TransitionVelocity Velocity;
        public TransitionHealth Health;
        public bool HasVelocity;
        public bool HasHealth;
    }

    // Span<T> cannot be obtained from List<T> on all target SDKs.  Keep the
    // conversion in one test-only helper so tests remain compatible with the
    // net10 SDK used by CI.
    private static class CollectionsMarshalCompat
    {
        public static Entity[] AsSpan(List<Entity> values) => values.ToArray();
    }

    private sealed class DeterministicRandom(int seed)
    {
        private uint _state = unchecked((uint)seed);

        public int Next(int maxValue)
            => (int)(NextUInt32() % (uint)maxValue);

        public int Next(int minValue, int maxValue)
            => minValue + Next(maxValue - minValue);

        private uint NextUInt32()
        {
            uint value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }
    }
}
