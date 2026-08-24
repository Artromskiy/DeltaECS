using System.Reflection;
using Delta.ECS;
using Delta.ECS.Integration;
using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
public sealed class StampInvariantTests
{
    [Test]
    public void MutationStampSourceStartsAtDefaultAndExhaustionIsAtomic()
    {
        var source = default(MutationStampSource);
        Assert.That(source.Current, Is.EqualTo(default(Stamp)));
        Assert.That(source.Next(), Is.EqualTo(new Stamp(1)));
        Assert.That(source.Current, Is.EqualTo(new Stamp(1)));

        FieldInfo valueField = typeof(MutationStampSource).GetField(
            "_value",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException("MutationStampSource._value was not found.");
        object boxed = source;
        valueField.SetValue(boxed, ulong.MaxValue);
        source = (MutationStampSource)boxed;

        Assert.Throws<InvalidOperationException>(() => source.Next());
        Assert.That(source.Current, Is.EqualTo(new Stamp(ulong.MaxValue)));
    }

    [Test]
    public void ComponentStampStorageCopiesRangesAndClearsSlots()
    {
        using var source = new ComponentStampStorage(componentCount: 3, capacity: 5);
        var target = new ComponentStampStorage(componentCount: 3, capacity: 5);
        var componentZero = new Stamp(10);
        var componentOne = new Stamp(11);
        var componentTwo = new Stamp(12);

        source.SetComponentRange(0, 0, 5, componentZero);
        source.SetComponentRange(1, 0, 5, componentOne);
        source.SetComponentRange(2, 0, 5, componentTwo);
        source.SetSlot(3, new Stamp(30));
        source.CopySlot(3, 4);
        source.CopyComponentSlotTo(ref target, 4, 0, 2, 1);
        source.CopyComponentRangeTo(ref target, 0, 1, 3, 0, 2);

        Assert.Multiple(() =>
        {
            Assert.That(source.Get(0, 3), Is.EqualTo(new Stamp(30)));
            Assert.That(source.Get(1, 4), Is.EqualTo(new Stamp(30)));
            Assert.That(source.Get(2, 4), Is.EqualTo(new Stamp(30)));
            Assert.That(target.Get(1, 0), Is.EqualTo(new Stamp(30)));
            Assert.That(target.Get(2, 1), Is.EqualTo(componentZero));
            Assert.That(target.Get(2, 2), Is.EqualTo(componentZero));
            Assert.That(target.Get(2, 3), Is.EqualTo(componentZero));
            Assert.That(target.Get(0, 0), Is.EqualTo(default(Stamp)));
        });

        source.ClearSlot(3);
        source.ClearRange(0, 2);
        Assert.Multiple(() =>
        {
            Assert.That(source.Get(0, 0), Is.EqualTo(default(Stamp)));
            Assert.That(source.Get(1, 1), Is.EqualTo(default(Stamp)));
            Assert.That(source.Get(2, 2), Is.EqualTo(componentTwo));
            Assert.That(source.Get(0, 3), Is.EqualTo(default(Stamp)));
            Assert.That(source.Get(0, 4), Is.EqualTo(new Stamp(30)));
        });
        target.Dispose();
    }

    [Test]
    public void WorldAndCatalogStampsAreIndependent()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(61_001));
        using var storage = new World(layouts, chunkCapacity: 2);
        IEcsWorld integration = storage;

        ComponentCatalog firstCatalog = integration.Catalog;
        Stamp initialWorldStamp = integration.Stamp;
        Assert.That(firstCatalog.Stamp, Is.Not.EqualTo(default(Stamp)));
        Assert.That(integration.Catalog.Stamp, Is.EqualTo(firstCatalog.Stamp));

        integration.Initialize();
        Entity zeroComponent = integration.Create(ReadOnlySpan<ComponentId>.Empty);
        Assert.That(zeroComponent.IsAlive, Is.True);
        Stamp afterCreate = integration.Stamp;
        Assert.That(afterCreate, Is.EqualTo(new Stamp(initialWorldStamp.Value + 1)));
        Assert.That(integration.Catalog.Stamp, Is.EqualTo(firstCatalog.Stamp));

        ComponentId velocityId = layouts.Register(typeof(Velocity), new SchemaId(61_002));
        ComponentCatalog secondCatalog = integration.Catalog;
        Assert.Multiple(() =>
        {
            Assert.That(secondCatalog.Stamp, Is.Not.EqualTo(firstCatalog.Stamp));
            Assert.That(integration.Stamp, Is.EqualTo(afterCreate));
            Assert.That(secondCatalog.Components.Length, Is.EqualTo(2));
            Assert.That(secondCatalog.Components.Span[1].Id, Is.EqualTo(velocityId));
        });

        Assert.That(integration.Add(zeroComponent, stackalloc[] { positionId }), Is.True);
        Assert.That(integration.Stamp, Is.EqualTo(new Stamp(afterCreate.Value + 1)));
        integration.Shutdown();
    }

    [Test]
    public void CreateZeroAndMultipleComponentsStampEveryInitialSlotOnce()
    {
        var layouts = CreateLayouts();
        using var storage = new World(layouts, chunkCapacity: 2);
        IEcsWorld integration = storage;
        integration.Initialize();

        Entity zero = integration.Create(ReadOnlySpan<ComponentId>.Empty);
        Stamp zeroStamp = integration.Stamp;
        Assert.That(integration.TryGetComponents(zero, Span<ComponentId>.Empty, out int zeroCount), Is.True);
        Assert.That(zeroCount, Is.Zero);

        Entity multiple = storage.Create(PositionId, VelocityId, HealthId);
        Stamp multipleStamp = storage.Stamp;
        Assert.That(multipleStamp, Is.EqualTo(new Stamp(zeroStamp.Value + 1)));
        Assert.Multiple(() =>
        {
            Assert.That(storage.TryGetComponentStamp(multiple, PositionId, out Stamp position), Is.True);
            Assert.That(storage.TryGetComponentStamp(multiple, VelocityId, out Stamp velocity), Is.True);
            Assert.That(storage.TryGetComponentStamp(multiple, HealthId, out Stamp health), Is.True);
            Assert.That(position, Is.EqualTo(multipleStamp));
            Assert.That(velocity, Is.EqualTo(multipleStamp));
            Assert.That(health, Is.EqualTo(multipleStamp));
        });

        Span<Entity> batch = stackalloc Entity[3];
        Stamp beforeBatch = storage.Stamp;
        Assert.That(storage.CreateBatch(new[] { PositionId, VelocityId }, batch), Is.EqualTo(3));
        Assert.That(storage.Stamp, Is.EqualTo(new Stamp(beforeBatch.Value + 1)));
        foreach (Entity entity in batch)
        {
            Assert.That(storage.TryGetComponentStamp(entity, PositionId, out Stamp position), Is.True);
            Assert.That(position, Is.EqualTo(storage.Stamp));
            Assert.That(storage.TryGetComponentStamp(entity, VelocityId, out Stamp velocity), Is.True);
            Assert.That(velocity, Is.EqualTo(storage.Stamp));
        }

        integration.Shutdown();
    }

    [Test]
    public void SingleListAndQueryStructuralOperationsAdvanceOnce()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts, chunkCapacity: 2);
        Entity single = world.Create(PositionId);
        Entity[] listed = new Entity[4];
        world.CreateBatch(new[] { PositionId }, listed);
        Entity existingTarget = world.Create(PositionId, VelocityId);

        Stamp beforeSingleAdd = world.Stamp;
        world.AddComponents(new[] { VelocityId }, single);
        Assert.That(world.Stamp, Is.EqualTo(new Stamp(beforeSingleAdd.Value + 1)));
        Assert.That(world.TryGetComponentStamp(single, VelocityId, out Stamp singleVelocity), Is.True);
        Assert.That(singleVelocity, Is.EqualTo(world.Stamp));

        Stamp beforeListAdd = world.Stamp;
        Assert.That(world.AddComponents(new[] { VelocityId }, listed), Is.EqualTo(listed.Length));
        Assert.That(world.Stamp, Is.EqualTo(new Stamp(beforeListAdd.Value + 1)));
        foreach (Entity entity in listed)
        {
            Assert.That(world.TryGetComponentStamp(entity, VelocityId, out Stamp velocity), Is.True);
            Assert.That(velocity, Is.EqualTo(world.Stamp));
        }

        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        Stamp beforeQueryAdd = world.Stamp;
        Assert.That(world.AddComponents(in query, new[] { HealthId }), Is.EqualTo(listed.Length + 2));
        Assert.That(world.Stamp, Is.EqualTo(new Stamp(beforeQueryAdd.Value + 1)));
        Assert.That(world.TryGetComponentStamp(existingTarget, HealthId, out Stamp existingHealth), Is.True);
        Assert.That(existingHealth, Is.EqualTo(world.Stamp));

        Stamp beforeSingleRemove = world.Stamp;
        world.RemoveComponents(new[] { HealthId }, single);
        Assert.That(world.Stamp, Is.EqualTo(new Stamp(beforeSingleRemove.Value + 1)));

        Stamp beforeListRemove = world.Stamp;
        Assert.That(world.RemoveComponents(new[] { VelocityId }, listed), Is.EqualTo(listed.Length));
        Assert.That(world.Stamp, Is.EqualTo(new Stamp(beforeListRemove.Value + 1)));

        Stamp beforeQueryRemove = world.Stamp;
        Assert.That(world.RemoveComponents(in query, new[] { HealthId }), Is.EqualTo(listed.Length + 1));
        Assert.That(world.Stamp, Is.EqualTo(new Stamp(beforeQueryRemove.Value + 1)));

        Stamp beforeSingleDestroy = world.Stamp;
        Assert.That(world.Destroy(single), Is.True);
        Assert.That(world.Stamp, Is.EqualTo(new Stamp(beforeSingleDestroy.Value + 1)));

        Stamp beforeListDestroy = world.Stamp;
        Assert.That(world.DestroyBatch(listed), Is.EqualTo(listed.Length));
        Assert.That(world.Stamp, Is.EqualTo(new Stamp(beforeListDestroy.Value + 1)));

        Entity queryDestroyA = world.Create(PositionId);
        Entity queryDestroyB = world.Create(PositionId, VelocityId);
        var destroyQuery = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        Stamp beforeQueryDestroy = world.Stamp;
        Assert.That(world.Destroy(in destroyQuery), Is.EqualTo(3));
        Assert.That(world.Stamp, Is.EqualTo(new Stamp(beforeQueryDestroy.Value + 1)));
        Assert.Multiple(() =>
        {
            Assert.That(world.IsAlive(queryDestroyA), Is.False);
            Assert.That(world.IsAlive(queryDestroyB), Is.False);
            Assert.That(world.IsAlive(existingTarget), Is.False);
        });
    }

    [Test]
    public void NoOpsInvalidStaleAndForeignOperationsNeverAdvanceStamp()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts);
        using var foreign = new World(layouts);
        Entity entity = world.Create(PositionId);
        _ = foreign.Create(PositionId);
        Entity foreignEntity = foreign.Create(PositionId);
        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        var foreignQuery = foreign.CreateQuery(QuerySpec.ForComponents(PositionId));
        Stamp before = world.Stamp;

        world.AddComponents(Array.Empty<ComponentId>(), entity);
        world.RemoveComponents(new[] { VelocityId }, entity);
        world.AddComponents(new[] { PositionId }, entity);
        Assert.That(world.RemoveComponents(Array.Empty<ComponentId>(), new[] { entity }), Is.Zero);
        Assert.That(world.DestroyBatch(new[] { Entity.Null, new Entity(999, 0) }), Is.Zero);
        Assert.That(world.SetComponent(entity, new ComponentId(200), new Position()), Is.False);
        Assert.That(world.SetComponent(foreignEntity, PositionId, new Position()), Is.False);
        Assert.That(world.Destroy(foreignEntity), Is.False);
        Assert.That(world.TryGetComponentStamp(foreignEntity, PositionId, out Stamp foreignStamp), Is.False);
        Assert.That(foreignStamp, Is.EqualTo(default(Stamp)));
        Assert.That(world.TryGetComponentStamp(Entity.Null, PositionId, out Stamp nullStamp), Is.False);
        Assert.That(nullStamp, Is.EqualTo(default(Stamp)));
        Assert.That(world.Stamp, Is.EqualTo(before));

        Assert.Throws<ArgumentException>(() => world.AddComponents(in foreignQuery, new[] { VelocityId }));
        Query invalidQuery = default;
        Assert.Throws<ArgumentException>(() => world.RemoveComponents(in invalidQuery, new[] { VelocityId }));
        Assert.Throws<ArgumentException>(() => world.Destroy(in foreignQuery));
        Assert.That(world.Stamp, Is.EqualTo(before));

        Assert.That(world.AddComponents(in query, new[] { PositionId }), Is.Zero);
        Assert.That(world.RemoveComponents(in query, new[] { VelocityId }), Is.Zero);
        Assert.That(world.Stamp, Is.EqualTo(before));
    }

    [Test]
    public void StructuralMovesPreserveSurvivingStampsAndStampOnlyAddedRows()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts, chunkCapacity: 2);
        Entity entity = world.Create(PositionId, HealthId);
        Assert.That(world.SetComponent(entity, PositionId, new Position { X = 7 }), Is.True);
        Assert.That(world.SetComponent(entity, HealthId, new Health { Value = 9 }), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, PositionId, out Stamp positionBefore), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, HealthId, out Stamp healthBefore), Is.True);

        Stamp beforeAdd = world.Stamp;
        world.AddComponents(new[] { VelocityId }, entity);
        Stamp addStamp = world.Stamp;
        Assert.That(addStamp, Is.EqualTo(new Stamp(beforeAdd.Value + 1)));
        Assert.Multiple(() =>
        {
            Assert.That(world.TryGetComponentStamp(entity, PositionId, out Stamp positionAfter), Is.True);
            Assert.That(world.TryGetComponentStamp(entity, HealthId, out Stamp healthAfter), Is.True);
            Assert.That(world.TryGetComponentStamp(entity, VelocityId, out Stamp velocityAfter), Is.True);
            Assert.That(positionAfter, Is.EqualTo(positionBefore));
            Assert.That(healthAfter, Is.EqualTo(healthBefore));
            Assert.That(velocityAfter, Is.EqualTo(addStamp));
        });

        Stamp beforeRemove = world.Stamp;
        world.RemoveComponents(new[] { HealthId }, entity);
        Stamp removeStamp = world.Stamp;
        Assert.That(removeStamp, Is.EqualTo(new Stamp(beforeRemove.Value + 1)));
        Assert.Multiple(() =>
        {
            Assert.That(world.TryGetComponentStamp(entity, PositionId, out Stamp positionAfter), Is.True);
            Assert.That(world.TryGetComponentStamp(entity, VelocityId, out Stamp velocityAfter), Is.True);
            Assert.That(world.TryGetComponentStamp(entity, HealthId, out _), Is.False);
            Assert.That(positionAfter, Is.EqualTo(positionBefore));
            Assert.That(velocityAfter, Is.EqualTo(addStamp));
        });
    }

    [Test]
    public void QueryBlockAndRangeTransitionsPreserveExactStampsAcrossChunksAndArchetypes()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts, chunkCapacity: 2);
        Entity[] positionOnly = new Entity[5];
        Entity[] positionHealth = new Entity[3];
        world.CreateBatch(new[] { PositionId }, positionOnly);
        world.CreateBatch(new[] { PositionId, HealthId }, positionHealth);
        var before = new Dictionary<Entity, (Stamp Position, Stamp Health)>();
        foreach (Entity entity in positionOnly)
        {
            Assert.That(world.SetComponent(entity, PositionId, new Position { X = entity.Index }), Is.True);
            Assert.That(world.TryGetComponentStamp(entity, PositionId, out Stamp position), Is.True);
            before.Add(entity, (position, default));
        }

        foreach (Entity entity in positionHealth)
        {
            Assert.That(world.SetComponent(entity, PositionId, new Position { X = entity.Index }), Is.True);
            Assert.That(world.SetComponent(entity, HealthId, new Health { Value = entity.Index }), Is.True);
            Assert.That(world.TryGetComponentStamp(entity, PositionId, out Stamp position), Is.True);
            Assert.That(world.TryGetComponentStamp(entity, HealthId, out Stamp health), Is.True);
            before.Add(entity, (position, health));
        }

        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        Stamp beforeAdd = world.Stamp;
        Assert.That(world.AddComponents(in query, new[] { VelocityId }), Is.EqualTo(8));
        Stamp addStamp = world.Stamp;
        Assert.That(addStamp, Is.EqualTo(new Stamp(beforeAdd.Value + 1)));
        foreach (var pair in before)
        {
            Entity entity = pair.Key;
            Assert.That(world.TryGetComponentStamp(entity, PositionId, out Stamp position), Is.True);
            Assert.That(position, Is.EqualTo(pair.Value.Position));
            if (pair.Value.Health != default)
            {
                Assert.That(world.TryGetComponentStamp(entity, HealthId, out Stamp health), Is.True);
                Assert.That(health, Is.EqualTo(pair.Value.Health));
            }

            Assert.That(world.TryGetComponentStamp(entity, VelocityId, out Stamp velocity), Is.True);
            Assert.That(velocity, Is.EqualTo(addStamp));
        }

        Stamp beforeRemove = world.Stamp;
        Assert.That(world.RemoveComponents(in query, new[] { VelocityId }), Is.EqualTo(8));
        Assert.That(world.Stamp, Is.EqualTo(new Stamp(beforeRemove.Value + 1)));
        foreach (var pair in before)
        {
            Assert.That(world.TryGetComponentStamp(pair.Key, PositionId, out Stamp position), Is.True);
            Assert.That(position, Is.EqualTo(pair.Value.Position));
            Assert.That(world.TryGetComponentStamp(pair.Key, VelocityId, out _), Is.False);
        }
    }

    [Test]
    public void QueryReadIsStampFreeAndQueryWriteTouchesOnlyRequestedRows()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts, chunkCapacity: 2);
        Entity[] positionOnly = new Entity[3];
        Entity[] positionVelocity = new Entity[4];
        world.CreateBatch(new[] { PositionId }, positionOnly);
        world.CreateBatch(new[] { PositionId, VelocityId }, positionVelocity);
        foreach (Entity entity in positionOnly.Concat(positionVelocity))
        {
            Assert.That(world.SetComponent(entity, PositionId, new Position { X = entity.Index }), Is.True);
        }

        var positionQuery = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        ReadAccess readPosition = positionQuery.AccessRead(PositionId);
        Stamp beforeRead = world.Stamp;
        int readCount = 0;
        using (var scope = world.OpenQuery(in positionQuery))
        {
            ReadAccess bound = scope.Bind(readPosition);
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var slots = chunks.Current.Slots;
                    ReadValues values = slots.Get(bound);
                    while (slots.MoveNext())
                    {
                        _ = values.Ref<Position>(slots);
                        readCount++;
                    }
                }
            }
        }

        Assert.That(readCount, Is.EqualTo(7));
        Assert.That(world.Stamp, Is.EqualTo(beforeRead));

        WriteAccess writePosition = positionQuery.AccessWrite(PositionId);
        var velocityStamps = new Dictionary<Entity, Stamp>();
        foreach (Entity entity in positionVelocity)
        {
            Assert.That(world.TryGetComponentStamp(entity, VelocityId, out Stamp stamp), Is.True);
            velocityStamps.Add(entity, stamp);
        }

        Stamp beforeWrite = world.Stamp;
        using (var scope = world.OpenQuery(in positionQuery))
        {
            WriteAccess bound = scope.Bind(writePosition);
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var slots = chunks.Current.Slots;
                    WriteValues values = slots.Get(bound);
                    while (slots.MoveNext())
                    {
                        ref Position position = ref values.Ref<Position>(slots);
                        position.X += 1;
                    }
                }
            }
        }

        Stamp writeStamp = world.Stamp;
        Assert.That(writeStamp, Is.EqualTo(new Stamp(beforeWrite.Value + 1)));
        foreach (Entity entity in positionOnly.Concat(positionVelocity))
        {
            Assert.That(world.TryGetComponentStamp(entity, PositionId, out Stamp stamp), Is.True);
            Assert.That(stamp, Is.EqualTo(writeStamp));
        }

        foreach (var pair in velocityStamps)
        {
            Assert.That(world.TryGetComponentStamp(pair.Key, VelocityId, out Stamp stamp), Is.True);
            Assert.That(stamp, Is.EqualTo(pair.Value));
        }
    }

    [Test]
    public void SwapBackAndDestroyBatchKeepSurvivorStampsAndGenerationsExact()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts, chunkCapacity: 2);
        Entity first = world.Create(PositionId);
        Entity second = world.Create(PositionId);
        Entity third = world.Create(PositionId);
        Assert.That(world.SetComponent(second, PositionId, new Position { X = 2 }), Is.True);
        Assert.That(world.SetComponent(third, PositionId, new Position { X = 3 }), Is.True);
        Assert.That(world.TryGetComponentStamp(second, PositionId, out Stamp secondBefore), Is.True);
        Assert.That(world.TryGetComponentStamp(third, PositionId, out Stamp thirdBefore), Is.True);

        Stamp beforeDestroy = world.Stamp;
        Assert.That(world.Destroy(first), Is.True);
        Assert.That(world.Stamp, Is.EqualTo(new Stamp(beforeDestroy.Value + 1)));
        Assert.That(world.TryGetComponent<Position>(second, PositionId, out Position secondValue), Is.True);
        Assert.That(world.TryGetComponentStamp(second, PositionId, out Stamp secondAfter), Is.True);
        Assert.That(secondValue.X, Is.EqualTo(2));
        Assert.That(secondAfter, Is.EqualTo(secondBefore));
        Assert.That(world.TryGetComponentStamp(third, PositionId, out Stamp thirdAfter), Is.True);
        Assert.That(thirdAfter, Is.EqualTo(thirdBefore));

        Entity fourth = world.Create(PositionId);
        Entity fifth = world.Create(PositionId);
        Assert.That(world.SetComponent(fourth, PositionId, new Position { X = 4 }), Is.True);
        Assert.That(world.SetComponent(fifth, PositionId, new Position { X = 5 }), Is.True);
        Assert.That(world.TryGetComponentStamp(fourth, PositionId, out Stamp fourthBefore), Is.True);
        Assert.That(world.TryGetComponentStamp(fifth, PositionId, out Stamp fifthBefore), Is.True);
        Stamp beforeBatchDestroy = world.Stamp;
        Assert.That(world.DestroyBatch(new[] { fourth, Entity.Null, fifth }), Is.EqualTo(2));
        Assert.That(world.Stamp, Is.EqualTo(new Stamp(beforeBatchDestroy.Value + 1)));
        Assert.That(world.TryGetComponentStamp(fourth, PositionId, out _), Is.False);
        Assert.That(world.TryGetComponentStamp(fifth, PositionId, out _), Is.False);
        Assert.That(fourthBefore, Is.Not.EqualTo(default(Stamp)));
        Assert.That(fifthBefore, Is.Not.EqualTo(default(Stamp)));
    }

    [Test]
    public void DestroyedEntitiesCannotUseOldStampsAfterGenerationReuse()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts, chunkCapacity: 1);
        Entity stale = world.Create(PositionId);
        Assert.That(world.TryGetComponentStamp(stale, PositionId, out Stamp staleStamp), Is.True);
        Assert.That(world.Destroy(stale), Is.True);
        Stamp afterDestroy = world.Stamp;
        Entity replacement = world.Create(PositionId);
        Assert.That(replacement.Index, Is.EqualTo(stale.Index));
        Assert.That(replacement.Generation, Is.EqualTo(stale.Generation + 1));
        Assert.That(world.TryGetComponentStamp(replacement, PositionId, out Stamp replacementStamp), Is.True);
        Assert.That(replacementStamp, Is.EqualTo(world.Stamp));
        Assert.That(replacementStamp, Is.Not.EqualTo(staleStamp));

        Stamp beforeStaleOperations = world.Stamp;
        Assert.That(world.Destroy(stale), Is.False);
        Assert.That(world.SetComponent(stale, PositionId, new Position()), Is.False);
        Assert.That(world.TryGetComponentStamp(stale, PositionId, out Stamp hidden), Is.False);
        Assert.That(hidden, Is.EqualTo(default(Stamp)));
        Assert.That(world.Stamp, Is.EqualTo(beforeStaleOperations));
        Assert.That(afterDestroy, Is.Not.EqualTo(staleStamp));
    }

    [Test]
    public void ManagedComponentsRetainIdentityAndExactStampsThroughMovesAndObjectQueries()
    {
        var layouts = CreateLayouts();
        ComponentId referenceId = layouts.Register(typeof(ReferenceComponent), new SchemaId(61_101));
        using var world = new World(layouts, chunkCapacity: 2);
        Entity[] entities = new Entity[3];
        world.CreateBatch(new[] { referenceId }, entities);
        var values = new ReferenceComponent[entities.Length];
        var valuesByEntity = new Dictionary<Entity, ReferenceComponent>();
        var stamps = new Stamp[entities.Length];
        for (int index = 0; index < entities.Length; index++)
        {
            values[index] = new ReferenceComponent { Value = index + 1 };
            valuesByEntity.Add(entities[index], values[index]);
            Assert.That(world.SetComponent(entities[index], referenceId, values[index]), Is.True);
            Assert.That(world.TryGetComponentStamp(entities[index], referenceId, out stamps[index]), Is.True);
        }

        Stamp beforeMove = world.Stamp;
        world.AddComponents(new[] { PositionId }, entities);
        Stamp moveStamp = world.Stamp;
        Assert.That(moveStamp, Is.EqualTo(new Stamp(beforeMove.Value + 1)));
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.TryGetComponent<ReferenceComponent>(entities[index], referenceId, out ReferenceComponent? actual), Is.True);
            Assert.That(actual, Is.SameAs(values[index]));
            Assert.That(world.TryGetComponentStamp(entities[index], referenceId, out Stamp stamp), Is.True);
            Assert.That(stamp, Is.EqualTo(stamps[index]));
        }

        var query = world.CreateQuery(QuerySpec.ForComponents(referenceId));
        WriteAccess writeReference = query.AccessWrite(referenceId);
        Stamp beforeQueryWrite = world.Stamp;
        using (var scope = world.OpenQuery(in query))
        {
            WriteAccess bound = scope.Bind(writeReference);
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var slots = chunks.Current.Slots;
                    ObjectWriteValues objects = slots.GetObject(bound);
                    while (slots.MoveNext())
                    {
                        Entity currentEntity = chunks.Current.Entities[slots.CurrentIndex];
                        ReferenceComponent current = valuesByEntity[currentEntity];
                        current.Value += 10;
                        objects.Set(slots, current);
                    }
                }
            }
        }

        Stamp queryStamp = world.Stamp;
        Assert.That(queryStamp, Is.EqualTo(new Stamp(beforeQueryWrite.Value + 1)));
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(values[index].Value, Is.EqualTo(index + 11));
            Assert.That(world.TryGetComponentStamp(entities[index], referenceId, out Stamp stamp), Is.True);
            Assert.That(stamp, Is.EqualTo(queryStamp));
        }

        Stamp beforeRemove = world.Stamp;
        world.RemoveComponents(new[] { PositionId }, entities);
        Assert.That(world.Stamp, Is.EqualTo(new Stamp(beforeRemove.Value + 1)));
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.TryGetComponent<ReferenceComponent>(entities[index], referenceId, out ReferenceComponent? actual), Is.True);
            Assert.That(actual, Is.SameAs(values[index]));
            Assert.That(world.TryGetComponentStamp(entities[index], referenceId, out Stamp stamp), Is.True);
            Assert.That(stamp, Is.EqualTo(queryStamp));
        }
    }

    [Test]
    public void SuccessfulWritesAdvanceOnceAndStaleExpectedStampsAreRejected()
    {
        var layouts = CreateLayouts();
        using var storage = new World(layouts);
        IEcsWorld world = storage;
        world.Initialize();
        Entity entity = world.Create(stackalloc[] { PositionId });
        Assert.That(world.TryRead(entity, PositionId, out ComponentSnapshot initial, out _), Is.True);

        Assert.That(world.TryWrite(entity, PositionId, new Position { X = 1 }, initial.Stamp, out Stamp firstWrite, out EcsWriteError firstError), Is.True);
        Assert.That(firstError.Code, Is.EqualTo(EcsWriteErrorCode.None));
        Assert.That(firstWrite, Is.EqualTo(world.Stamp));
        Stamp beforeFailures = world.Stamp;
        Assert.That(world.TryWrite(entity, PositionId, new Position { X = 2 }, initial.Stamp, out Stamp staleWrite, out EcsWriteError staleError), Is.False);
        Assert.That(staleWrite, Is.EqualTo(default(Stamp)));
        Assert.That(staleError.Code, Is.EqualTo(EcsWriteErrorCode.StaleStamp));
        Assert.That(world.TryWrite(entity, PositionId, new Velocity { X = 3 }, firstWrite, out _, out EcsWriteError typeError), Is.False);
        Assert.That(typeError.Code, Is.EqualTo(EcsWriteErrorCode.InvalidValue));
        Assert.That(world.TryWrite(entity, new ComponentId(200), new Position(), default, out _, out EcsWriteError unknownError), Is.False);
        Assert.That(unknownError.Code, Is.EqualTo(EcsWriteErrorCode.ComponentUnknown));
        Assert.That(world.Stamp, Is.EqualTo(beforeFailures));

        Assert.That(world.TryWrite(entity, PositionId, new Position { X = 4 }, firstWrite, out Stamp secondWrite, out _), Is.True);
        Assert.That(secondWrite, Is.EqualTo(new Stamp(beforeFailures.Value + 1)));
        world.Shutdown();
    }

    [Test]
    public void EmptyWriteScopeDoesNotAdvanceWithoutAChunkWrite()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts, chunkCapacity: 2);
        var emptyQuery = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        WriteAccess write = emptyQuery.AccessWrite(PositionId);
        Stamp beforeScope = world.Stamp;

        using (var scope = world.OpenQuery(in emptyQuery))
        {
            WriteAccess bound = scope.Bind(write);
            var archetypes = scope.Archetypes;
            Assert.That(archetypes.MoveNext(), Is.False);
            _ = bound;
        }

        Assert.That(world.Stamp, Is.EqualTo(beforeScope), "An empty write scope is a no-op and has no row to stamp.");

    }

    [Test]
    public void EmptyWorldQueryDoesNotAdvanceWithoutAChunkWrite()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts, chunkCapacity: 2);
        var emptyQuery = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        _ = emptyQuery.AccessWrite(PositionId);
        int callbackCount = 0;
        Stamp beforeCallback = world.Stamp;
        world.Query(in emptyQuery, ref callbackCount, static (ref int count, ref QueryChunkCursor cursor) =>
        {
            count++;
            _ = cursor;
        });
        Assert.That(callbackCount, Is.Zero);
        Assert.That(world.Stamp, Is.EqualTo(beforeCallback), "World.Query must not reserve a stamp when no chunk callback runs.");
    }

    [Test]
    public void BoundWriteScopeWithoutIterationDoesNotAdvanceWithoutAWrite()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts, chunkCapacity: 2);
        Entity entity = world.Create(PositionId);
        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        WriteAccess write = query.AccessWrite(PositionId);
        Stamp before = world.Stamp;

        using (var scope = world.OpenQuery(in query))
        {
            _ = scope.Bind(write);
            var archetypes = scope.Archetypes;
            Assert.That(archetypes.MoveNext(), Is.True);
            // Deliberately do not descend to a chunk, iterate a slot, or obtain WriteValues.
        }

        Assert.That(world.Stamp, Is.EqualTo(before));
        Assert.That(world.TryGetComponentStamp(entity, PositionId, out Stamp stamp), Is.True);
        Assert.That(stamp, Is.EqualTo(before));
    }

    [Test]
    public void WorldQueryCallbackWithoutWriteAccessDoesNotAdvanceWithoutAWrite()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts, chunkCapacity: 2);
        Entity entity = world.Create(PositionId);
        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        WriteAccess write = query.AccessWrite(PositionId);
        int callbackCount = 0;
        Stamp before = world.Stamp;

        world.Query(in query, ref callbackCount, static (ref int count, ref QueryChunkCursor cursor) =>
        {
            count++;
            // Deliberately do not request a row from the write cursor.
            _ = cursor.SlotCount;
        });

        _ = write;
        Assert.That(callbackCount, Is.EqualTo(1));
        Assert.That(world.Stamp, Is.EqualTo(before));
        Assert.That(world.TryGetComponentStamp(entity, PositionId, out Stamp stamp), Is.True);
        Assert.That(stamp, Is.EqualTo(before));
    }

    [Test]
    public void WriteGetOnAnEmptyChunkDoesNotCreateAStampOrAComponentStamp()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts, chunkCapacity: 2);
        Entity entity = world.Create(PositionId);
        Assert.That(world.Destroy(entity), Is.True);
        Stamp before = world.Stamp;
        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        WriteAccess write = query.AccessWrite(PositionId);
        var archetype = world.Archetypes[0];
        var plan = new ArchetypePlan(archetype, new[] { 0 });
        var chunk = archetype.GetChunk(0);
        var chunkPlan = new ChunkPlan(chunk, new[] { chunk.GetRawComponentRow(0) });
        var slots = new QuerySlots(plan, chunkPlan, query.Cached, writeTick: 1, writeStamp: new Stamp(before.Value + 1));

        _ = slots.Get(write);
        Assert.That(world.Stamp, Is.EqualTo(before));
        Assert.That(chunk.GetComponentStamp(0, 0), Is.EqualTo(default(Stamp)));
    }

    [Test]
    public void ExhaustedStampSourceDoesNotMutateBeforeCreateThrows()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts);
        Entity entity = world.Create(PositionId);
        Stamp before = ExhaustWorldStamp(world);
        int archetypeVersion = world.ArchetypeVersion;

        Assert.Throws<InvalidOperationException>(() => world.Create(VelocityId));
        Assert.Multiple(() =>
        {
            Assert.That(world.Stamp, Is.EqualTo(before));
            Assert.That(world.ArchetypeVersion, Is.EqualTo(archetypeVersion));
            Assert.That(world.IsAlive(entity), Is.True);
            Assert.That(world.AliveEntityCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ExhaustedStampSourceDoesNotMutateBeforeAddThrows()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts);
        Entity entity = world.Create(PositionId);
        Stamp before = ExhaustWorldStamp(world);
        int archetypeVersion = world.ArchetypeVersion;

        Assert.Throws<InvalidOperationException>(() => world.AddComponents(new[] { VelocityId }, entity));
        Assert.Multiple(() =>
        {
            Assert.That(world.Stamp, Is.EqualTo(before));
            Assert.That(world.ArchetypeVersion, Is.EqualTo(archetypeVersion));
            Assert.That(world.IsAlive(entity), Is.True);
            Assert.That(world.TryGetComponentStamp(entity, PositionId, out _), Is.True);
            Assert.That(world.TryGetComponentStamp(entity, VelocityId, out _), Is.False);
        });
    }

    [Test]
    public void ExhaustedStampSourceDoesNotMutateBeforeRemoveThrows()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts);
        Entity entity = world.Create(PositionId);
        Stamp before = ExhaustWorldStamp(world);
        int archetypeVersion = world.ArchetypeVersion;

        Assert.Throws<InvalidOperationException>(() => world.RemoveComponents(new[] { PositionId }, entity));
        Assert.Multiple(() =>
        {
            Assert.That(world.Stamp, Is.EqualTo(before));
            Assert.That(world.ArchetypeVersion, Is.EqualTo(archetypeVersion));
            Assert.That(world.IsAlive(entity), Is.True);
            Assert.That(world.TryGetComponentStamp(entity, PositionId, out _), Is.True);
        });
    }

    [Test]
    public void ExhaustedStampSourceDoesNotMutateBeforeDestroyEntityThrows()
        => AssertDestroyExhaustionIsAtomic();

    [Test]
    public void ExhaustedStampSourceDoesNotMutateBeforeDestroyBatchThrows()
        => AssertDestroyBatchExhaustionIsAtomic();

    [Test]
    public void ExhaustedStampSourceDoesNotMutateBeforeDestroyQueryThrows()
        => AssertDestroyQueryExhaustionIsAtomic();

    [Test]
    public void DeterministicRandomizedStateMachineMatchesComponentStampModel()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts, chunkCapacity: 3);
        var random = new Random(0x5EED_2026);
        var states = new Dictionary<Entity, ModelEntity>();
        var stale = new List<Entity>();

        for (int step = 0; step < 240; step++)
        {
            int operation = random.Next(8);
            if (operation == 0 || states.Count == 0)
            {
                int count = random.Next(1, 4);
                ComponentId[] components = RandomComponents(random);
                Entity[] created = new Entity[count];
                Stamp before = world.Stamp;
                Assert.That(world.CreateBatch(components, created), Is.EqualTo(count), $"create at step {step}");
                Stamp operationStamp = world.Stamp;
                Assert.That(operationStamp, Is.EqualTo(new Stamp(before.Value + 1)), $"create stamp at step {step}");
                for (int index = 0; index < count; index++)
                {
                    var model = new ModelEntity(components.Distinct());
                    foreach (ComponentId component in model.Components)
                    {
                        model.Stamps[component] = operationStamp;
                    }

                    states.Add(created[index], model);
                }
            }
            else if (operation == 1)
            {
                Entity entity = Pick(states.Keys, random);
                ModelEntity model = states[entity];
                ComponentId component = PickComponent(random);
                Stamp before = world.Stamp;
                bool expected = model.Components.Contains(component);
                bool actual = component == PositionId
                    ? world.SetComponent(entity, component, new Position { X = step })
                    : component == VelocityId
                        ? world.SetComponent(entity, component, new Velocity { X = step })
                        : world.SetComponent(entity, component, new Health { Value = step });
                Assert.That(actual, Is.EqualTo(expected), $"set at step {step}");
                if (expected)
                {
                    model.Stamps[component] = world.Stamp;
                    Assert.That(world.Stamp, Is.EqualTo(new Stamp(before.Value + 1)), $"set stamp at step {step}");
                }
                else
                {
                    Assert.That(world.Stamp, Is.EqualTo(before), $"set no-op stamp at step {step}");
                }
            }
            else if (operation == 2 || operation == 3)
            {
                Entity[] candidates = PickDistinct(states.Keys, random, random.Next(1, Math.Min(3, states.Count) + 1));
                ComponentId component = PickComponent(random);
                bool isAdd = operation == 2;
                Stamp before = world.Stamp;
                int expectedChanged = 0;
                foreach (Entity candidate in candidates)
                {
                    ModelEntity model = states[candidate];
                    bool changes = isAdd ? model.Components.Add(component) : model.Components.Remove(component);
                    if (changes)
                    {
                        expectedChanged++;
                    }
                }

                int actualChanged = isAdd
                    ? world.AddComponents(new[] { component }, candidates)
                    : world.RemoveComponents(new[] { component }, candidates);
                Assert.That(actualChanged, Is.EqualTo(expectedChanged), $"list structural count at step {step}");
                if (expectedChanged != 0)
                {
                    Stamp operationStamp = world.Stamp;
                    Assert.That(operationStamp, Is.EqualTo(new Stamp(before.Value + 1)), $"list structural stamp at step {step}");
                    foreach (Entity candidate in candidates)
                    {
                        ModelEntity model = states[candidate];
                        if (isAdd && model.Components.Contains(component))
                        {
                            if (!model.Stamps.ContainsKey(component))
                            {
                                model.Stamps[component] = operationStamp;
                            }
                        }
                        else if (!isAdd)
                        {
                            model.Stamps.Remove(component);
                        }
                    }
                }
                else
                {
                    Assert.That(world.Stamp, Is.EqualTo(before), $"list structural no-op stamp at step {step}");
                }
            }
            else if (operation == 4)
            {
                Entity entity = Pick(states.Keys, random);
                Stamp before = world.Stamp;
                Assert.That(world.Destroy(entity), Is.True, $"destroy at step {step}");
                Assert.That(world.Stamp, Is.EqualTo(new Stamp(before.Value + 1)), $"destroy stamp at step {step}");
                stale.Add(entity);
                states.Remove(entity);
            }
            else if (operation == 5)
            {
                Entity entity = stale.Count == 0 || random.Next(2) == 0
                    ? new Entity(10_000 + step, 0)
                    : stale[random.Next(stale.Count)];
                Stamp before = world.Stamp;
                Assert.That(world.Destroy(entity), Is.False, $"stale destroy at step {step}");
                Assert.That(world.Stamp, Is.EqualTo(before), $"stale destroy stamp at step {step}");
            }
            else
            {
                Entity[] candidates = PickDistinct(states.Keys, random, random.Next(1, Math.Min(3, states.Count) + 1));
                Entity[] mixed = new Entity[candidates.Length + 1];
                candidates.CopyTo(mixed, 0);
                mixed[^1] = stale.Count == 0 ? Entity.Null : stale[random.Next(stale.Count)];
                Stamp before = world.Stamp;
                int expectedDestroyed = candidates.Length;
                Assert.That(world.DestroyBatch(mixed), Is.EqualTo(expectedDestroyed), $"batch destroy at step {step}");
                Assert.That(world.Stamp, Is.EqualTo(new Stamp(before.Value + 1)), $"batch destroy stamp at step {step}");
                foreach (Entity candidate in candidates)
                {
                    stale.Add(candidate);
                    states.Remove(candidate);
                }
            }

            AssertModel(world, states, step);
        }
    }

    private static ComponentLayoutRegistry CreateLayouts()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.Register(typeof(Position), new SchemaId(61_201));
        layouts.Register(typeof(Velocity), new SchemaId(61_202));
        layouts.Register(typeof(Health), new SchemaId(61_203));
        return layouts;
    }

    private static readonly ComponentId PositionId = new(0);
    private static readonly ComponentId VelocityId = new(1);
    private static readonly ComponentId HealthId = new(2);

    private static ComponentId[] RandomComponents(Random random)
    {
        var components = new List<ComponentId>();
        foreach (ComponentId component in new[] { PositionId, VelocityId, HealthId })
        {
            if (random.Next(2) == 0)
            {
                components.Add(component);
            }
        }

        if (components.Count == 0)
        {
            components.Add(PickComponent(random));
        }

        return components.ToArray();
    }

    private static ComponentId PickComponent(Random random)
        => new[] { PositionId, VelocityId, HealthId }[random.Next(3)];

    private static Stamp ExhaustWorldStamp(World world)
    {
        FieldInfo sourceField = typeof(World).GetField(
            "_mutationStamps",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException("World mutation stamp source was not found.");
        var source = (MutationStampSource)sourceField.GetValue(world)!;
        FieldInfo valueField = typeof(MutationStampSource).GetField(
            "_value",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException("Mutation stamp value was not found.");
        object boxed = source;
        valueField.SetValue(boxed, ulong.MaxValue);
        sourceField.SetValue(world, boxed);
        return world.Stamp;
    }

    private static void AssertDestroyExhaustionIsAtomic()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts);
        Entity entity = world.Create(PositionId);
        Stamp exhausted = ExhaustWorldStamp(world);
        Assert.Throws<InvalidOperationException>(() => world.Destroy(entity));
        Assert.That(world.Stamp, Is.EqualTo(exhausted));
        Assert.That(world.IsAlive(entity), Is.True, "Destroy(Entity) mutates before exhausting the stamp source.");
    }

    private static void AssertDestroyBatchExhaustionIsAtomic()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts);
        Entity entity = world.Create(PositionId);
        Stamp exhausted = ExhaustWorldStamp(world);
        Assert.Throws<InvalidOperationException>(() => world.DestroyBatch(new[] { entity }));
        Assert.That(world.Stamp, Is.EqualTo(exhausted));
        Assert.That(world.IsAlive(entity), Is.True, "DestroyBatch mutates before exhausting the stamp source.");
    }

    private static void AssertDestroyQueryExhaustionIsAtomic()
    {
        var layouts = CreateLayouts();
        using var world = new World(layouts);
        Entity entity = world.Create(PositionId);
        var query = world.CreateQuery(QuerySpec.ForComponents(PositionId));
        Stamp exhausted = ExhaustWorldStamp(world);
        Assert.Throws<InvalidOperationException>(() => world.Destroy(in query));
        Assert.That(world.Stamp, Is.EqualTo(exhausted));
        Assert.That(world.IsAlive(entity), Is.True, "Destroy(query) mutates before exhausting the stamp source.");
    }

    private static Entity Pick(IEnumerable<Entity> entities, Random random)
    {
        Entity[] values = entities.ToArray();
        return values[random.Next(values.Length)];
    }

    private static Entity[] PickDistinct(IEnumerable<Entity> entities, Random random, int count)
    {
        Entity[] values = entities.ToArray();
        for (int index = values.Length - 1; index > 0; index--)
        {
            int swap = random.Next(index + 1);
            (values[index], values[swap]) = (values[swap], values[index]);
        }

        return values[..count];
    }

    private static void AssertModel(World world, Dictionary<Entity, ModelEntity> states, int step)
    {
        Assert.That(world.AliveEntityCount, Is.EqualTo(states.Count), $"alive count at step {step}");
        foreach (var pair in states)
        {
            Assert.That(world.IsAlive(pair.Key), Is.True, $"alive entity at step {step}");
            foreach (ComponentId component in pair.Value.Components)
            {
                Assert.That(world.TryGetComponentStamp(pair.Key, component, out Stamp actual), Is.True, $"component presence at step {step}");
                Assert.That(actual, Is.EqualTo(pair.Value.Stamps[component]), $"component stamp at step {step}");
            }

            Assert.That(world.TryGetComponentStamp(pair.Key, new ComponentId(200), out _), Is.False);
        }
    }

    private sealed class ModelEntity
    {
        public ModelEntity(IEnumerable<ComponentId> components)
        {
            Components = new HashSet<ComponentId>(components);
            Stamps = new Dictionary<ComponentId, Stamp>();
        }

        public HashSet<ComponentId> Components { get; }
        public Dictionary<ComponentId, Stamp> Stamps { get; }
    }
}
