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
        Assert.That(default(Entity).IsValid, Is.False);

        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);

        var e1 = world.Create(new[] { PositionId, VelocityId });
        Assert.That(e1.Generation, Is.GreaterThan(0));
        Assert.True(world.IsAlive(e1));

        var destroyed = world.Destroy(e1);
        Assert.True(destroyed);
        Assert.False(world.IsAlive(e1));

        var e2 = world.Create(new[] { PositionId, VelocityId });
        Assert.AreEqual(e1.Index, e2.Index);
        Assert.AreNotEqual(e1.Generation, e2.Generation);
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
        long sum = 0;
        world.ForEach<long, Position, Velocity>(
            in query,
            ref sum,
            static (ref long total, ref Position position, in Velocity velocity) =>
            {
                position = new Position { X = 1, Y = 2 };
                total += (long)position.X + (long)velocity.Y;
            });

        Assert.Greater(sum, 0);

        world.Destroy(created);
        Assert.AreEqual(0, world.AliveEntityCount);
    }


    [Test]
    public void ImmediateBatchTransition_CompletesBeforeReturn_AndIsIdempotent()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);
        var entities = new Entity[5];
        world.Create(new[] { PositionId }, entities);

        Assert.That(world.Add(entities, new[] { VelocityId }), Is.EqualTo(entities.Length));
        foreach (var entity in entities)
        {
            Assert.That(world.TryGet<Velocity>(entity, VelocityId, out _), Is.True);
        }

        Assert.That(world.Add(entities, new[] { VelocityId }), Is.EqualTo(0));
        Assert.That(world.Remove(entities, new[] { VelocityId }), Is.EqualTo(entities.Length));
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
            new[] { new ComponentId(129), new ComponentId(7), PositionId, new ComponentId(193), new ComponentId(65), new ComponentId(7) },
            new[] { VelocityId, VelocityId },
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
            new[] { ComponentId.Invalid },
            Array.Empty<ComponentId>(),
            Array.Empty<ComponentId>()));
        var dynamicQuery = new QuerySpec(
            new[] { new ComponentId(256) },
            Array.Empty<ComponentId>(), Array.Empty<ComponentId>());
        Assert.That(dynamicQuery.AllMask.Contains(new ComponentId(256)), Is.True);
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
    public void GeneratedQueryCompositionExtendsMasksAndReusesTheQueryCache()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        using var world = new World(layouts);
        QuerySpec expectedSpec = new(
            new[] { PositionId },
            new[] { VelocityId, HealthId },
            new[] { HealthId });
        Query expected = world.CreateQuery(in expectedSpec);

        Query composed = world
            .WhereAll<Position>()
            .WhereNone<Health>()
            .WhereAny<Velocity>()
            .WhereAny<Health>();
        Query repeated = world
            .WhereAll<Position>()
            .WhereNone<Health>()
            .WhereAny<Velocity>()
            .WhereAny<Health>();

        Assert.Multiple(() =>
        {
            Assert.That(composed.Description, Is.EqualTo(expectedSpec));
            Assert.That(composed.Cached, Is.SameAs(expected.Cached));
            Assert.That(repeated.Cached, Is.SameAs(composed.Cached));
        });
    }

    [Test]
    public void ComponentIdQueryFactoriesComposeFromWorldAndQuery()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        using var world = new World(layouts);

        QuerySpec expectedSpec = new(
            new[] { PositionId },
            new[] { VelocityId },
            new[] { HealthId });
        Query expected = world.CreateQuery(in expectedSpec);

        Query composed = world
            .WhereAll(PositionId)
            .WhereNone(HealthId)
            .WhereAny(VelocityId);
        Query repeated = world
            .WhereAll(PositionId)
            .WhereNone(HealthId)
            .WhereAny(VelocityId);

        Assert.Multiple(() =>
        {
            Assert.That(composed.Description, Is.EqualTo(expectedSpec));
            Assert.That(composed.Cached, Is.SameAs(expected.Cached));
            Assert.That(repeated.Cached, Is.SameAs(composed.Cached));
        });
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

        Assert.That(assembly.GetType("Delta.ECS.QueryScope"), Is.Null);
        Assert.That(assembly.GetType("Delta.ECS.QueryChunks"), Is.Null);
        Assert.That(assembly.GetType("Delta.ECS.QuerySlots"), Is.Null);
        Assert.That(assembly.GetType("Delta.ECS.QueryChunkAction"), Is.Null);
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
        var world = new World(layouts);

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
    public void Transition_Add_Remove_Preserves_Data()
    {
        var layouts = new ComponentLayoutRegistry();
        RegisterComponentLayouts(layouts);
        var world = new World(layouts);

        var first = world.Create(new[] { PositionId, VelocityId });
        world.Set(first, PositionId, new Position { X = 10, Y = 11 });
        world.Set(first, VelocityId, new Velocity { X = 20, Y = 21 });

        world.Add(first, new[] { HealthId });

        Assert.True(world.TryGet<Position>(first, PositionId, out var posAfterAdd));
        Assert.True(world.TryGet<Velocity>(first, VelocityId, out var velAfterAdd));
        Assert.True(world.TryGet<Health>(first, HealthId, out var healthAfterAdd));
        Assert.AreEqual(10, posAfterAdd.X);
        Assert.AreEqual(21, velAfterAdd.Y);
        Assert.AreEqual(0, healthAfterAdd.Value);

        world.Remove(first, new[] { VelocityId });

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
                    world.Add(entity, new[] { VelocityId });

                    if (!current.Velocity.HasValue)
                    {
                        current.Velocity = new Velocity();
                    }
                }
                else
                {
                    world.Remove(entity, new[] { VelocityId });
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

            ValidateInvariant(world, allEntities, model);

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
        foreach (ref readonly ChunkPlan chunk in handle.Cached.MatchingChunkPlans())
        {
            count += chunk.Chunk.Count;
        }

        return count;
    }

    private static void ValidateInvariant(World world, List<Entity> entities, Dictionary<int, EntityState> model)
    {
        if (world.AliveEntityCount != model.Count || entities.Count != model.Count)
        {
            Assert.Fail($"Entity count mismatch: model={model.Count}, tracked={entities.Count}, worldAlive={world.AliveEntityCount}");
        }

        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
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
        foreach (ref readonly ChunkPlan chunk in query.Cached.MatchingChunkPlans())
        {
            chunkIds.Add(chunk.Chunk.GlobalId);
        }

        return chunkIds;
    }

    private struct EntityState
    {
        public Position Position;
        public Velocity? Velocity;
        public Health? Health;
        public int ArchetypeKey;
    }

}
