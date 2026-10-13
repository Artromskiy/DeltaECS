namespace Delta.ECS.Tests;

using System;

using NUnit.Framework;

[TestFixture]
internal sealed class EntityRefTests
{
    [Test]
    public void EntityRefSupportsPrimaryComponentAccess()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_070));
        ComponentId tagId = layouts.Register<FirstTag>(new SchemaId(40_069));
        layouts.Register<Velocity>(new SchemaId(40_068));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, new Position { X = 1, Y = 2 });
        Assert.That(world.Add(entity, tagId), Is.True);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));

        world.ForEachEntity(in query, current =>
        {
            Entity currentEntity = current;
            Assert.That(currentEntity, Is.EqualTo(entity));
            Assert.That(current.Has<Position>(), Is.True);
            Assert.That(current.Has(positionId), Is.True);
            Assert.That(current.Has<Velocity>(), Is.False);
            Assert.That(current.TryGet(out Position value), Is.True);
            Assert.That(value, Is.EqualTo(new Position { X = 1, Y = 2 }));
            Assert.That(current.TryGet<Position>(positionId, out Position explicitValue), Is.True);
            Assert.That(explicitValue, Is.EqualTo(value));
            Assert.That(current.Get<Position>(), Is.EqualTo(value));
            Assert.That(current.Get<Position>(positionId), Is.EqualTo(value));

            ref readonly Position primaryRead = ref current.GetReadRef<Position>();
            ref readonly Position explicitRead = ref current.GetReadRef<Position>(positionId);
            Assert.That(primaryRead, Is.EqualTo(value));
            Assert.That(explicitRead, Is.EqualTo(value));

            Assert.That(current.Has<FirstTag>(), Is.True);
            Assert.That(current.TryGet<FirstTag>(out FirstTag tag), Is.True);
            Assert.That(tag, Is.EqualTo(default(FirstTag)));
            ref Position writable = ref current.GetRef<Position>();
            writable.X++;
            ref Position explicitWritable = ref current.GetRef<Position>(positionId);
            explicitWritable.Y++;

            Assert.That(current.TryGet<Velocity>(out _), Is.False);
        }).Invoke();

        Assert.That(world.Get<Position>(entity), Is.EqualTo(new Position { X = 2, Y = 3 }));
    }

    [Test]
    public void EntityRefUsesCurrentSlotAndChecksDynamicComponentAccess()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_071));
        ComponentId healthId = layouts.Register<Health>(new SchemaId(40_072));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(40_073));
        ComponentId firstTagId = layouts.Register<FirstTag>(new SchemaId(40_074));
        ComponentId secondTagId = layouts.Register<SecondTag>(new SchemaId(40_075));
        using var world = new World(layouts);

        var entities = new Entity[8];
        world.Create(stackalloc[] { positionId }, entities);
        world.Add(entities[0], healthId);
        world.Add(entities[2], healthId);
        world.GetRef<Health>(entities[0], healthId).Value = 10;
        world.GetRef<Health>(entities[2], healthId).Value = 20;
        world.Add(entities[1], firstTagId);
        world.Add(entities[3], firstTagId);
        world.Add(entities[3], secondTagId);
        world.Add(entities[5], firstTagId);
        world.Add(entities[2], secondTagId);
        world.Add(entities[6], secondTagId);

        Query query = world.CreateQuery(
            QuerySpec.WhereAll(positionId)
                .WithAny(stackalloc[] { firstTagId, secondTagId }));
        var state = new ProbeState
        {
            Entities = entities,
            PositionId = positionId,
            HealthId = healthId,
            VelocityId = velocityId,
            FirstTagId = firstTagId,
            SecondTagId = secondTagId,
            UnregisteredId = new ComponentId(1000)
        };

        world.ForEachEntity(in query, ref state, InspectEntity).Invoke(ref state);

        Assert.That(state.Visited, Is.EqualTo(5));
        Assert.That(state.HealthWrites, Is.EqualTo(1));
        Assert.That(state.MissingHealthReads, Is.EqualTo(4));
        Assert.That(world.Get<Health>(entities[2], healthId).Value, Is.EqualTo(21));
        Assert.That(world.Get<Health>(entities[0], healthId).Value, Is.EqualTo(10));
    }

    [Test]
    public void DelegateForEachEntityReusesEntityRefAcrossChunksAndTagSlots()
    {
        var layouts = new ComponentRegistry();
        ComponentId healthId = layouts.Register<Health>(new SchemaId(40_085));
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_076));
        ComponentId tagId = layouts.Register<FirstTag>(new SchemaId(40_077));
        ComponentId missingId = layouts.Register<Velocity>(new SchemaId(40_078));
        using var world = new World(layouts);

        int entityCount = Chunk.Capacity * 2 + 3;
        var entities = new Entity[entityCount];
        world.Create(stackalloc[] { positionId }, entities);
        for (int index = 1; index < entities.Length; index += 2)
        {
            world.Add(entities[index], healthId);
        }

        int taggedCount = 0;
        for (int index = 0; index < entities.Length; index += 2)
        {
            world.Add(entities[index], tagId);
            taggedCount++;
        }

        Query allPositions = world.CreateQuery(QuerySpec.WhereAll(positionId));
        int[] denseVisits = new int[entityCount];
        int denseVisitCount = 0;
        world.ForEachEntity(in allPositions, entity =>
        {
            denseVisits[entity.Index]++;
            entity.GetRef<Position>(positionId).X++;
            denseVisitCount++;
        }).Invoke();

        Assert.That(denseVisitCount, Is.EqualTo(entityCount));
        Assert.That(denseVisits, Is.All.EqualTo(1));
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.Get<Position>(entities[index], positionId).X, Is.EqualTo(1));
        }

        Query taggedPositions = world.CreateQuery(
            QuerySpec.WhereAll(positionId).WithAny(stackalloc[] { tagId }));
        int[] taggedVisits = new int[entityCount];
        int taggedVisitCount = 0;
        world.ForEachEntity(in taggedPositions, entity =>
        {
            Assert.That(entity.Has(tagId), Is.True);
            taggedVisits[entity.Index]++;
            taggedVisitCount++;
        }).Invoke();

        Assert.That(taggedVisitCount, Is.EqualTo(taggedCount));
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(taggedVisits[index], Is.EqualTo(index % 2 == 0 ? 1 : 0));
        }

        Query empty = world.CreateQuery(QuerySpec.WhereAll(missingId));
        int emptyVisits = 0;
        world.ForEachEntity(in empty, _ => emptyVisits++).Invoke();
        Assert.That(emptyVisits, Is.Zero);
    }

    [Test]
    public void ComponentEntityIterationUsesTheBoundEntityRefViewForTaggedSlots()
    {
        var layouts = new ComponentRegistry();
        ComponentId healthId = layouts.Register<Health>(new SchemaId(40_086));
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_079));
        ComponentId tagId = layouts.Register<FirstTag>(new SchemaId(40_080));
        using var world = new World(layouts);
        var entities = new Entity[8];
        world.Create(stackalloc[] { positionId }, entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.GetRef<Position>(entities[index], positionId).X = index;
            if (index % 4 == 0)
            {
                world.Add(entities[index], healthId);
            }

            if ((index & 1) == 0)
            {
                world.Add(entities[index], tagId);
            }
        }

        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId).WithAny(stackalloc[] { tagId }));
        int visits = 0;
        world.ForEachEntity(in query, (EntityRef entity, ref Position position) =>
        {
            Assert.That(entity.Has(tagId), Is.True);
            Assert.That(entity.TryGet(out Position value), Is.True);
            Assert.That(value.X, Is.EqualTo(position.X));
            ref readonly Position readOnly = ref entity.GetReadRef<Position>(positionId);
            Assert.That(readOnly.X, Is.EqualTo(position.X));
            entity.GetRef<Position>(positionId).X++;
            visits++;
        }).Invoke();

        Assert.That(visits, Is.EqualTo(entities.Length / 2));
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.Get<Position>(entities[index], positionId).X, Is.EqualTo(index + ((index & 1) == 0 ? 1 : 0)));
        }
    }

    [Test]
    public void EntityRefUsesPreparedAnyRowsAndRefreshesForNewArchetypes()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_081));
        ComponentId healthId = layouts.Register<Health>(new SchemaId(40_082));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(40_083));
        using var world = new World(layouts);
        Entity first = world.Create(
            stackalloc[] { positionId, healthId },
            new Position { X = 1 },
            new Health { Value = 10 });
        Query query = world.CreateQuery(
            QuerySpec.WhereAll(positionId)
                .WithAny(stackalloc[] { healthId, velocityId }));
        var state = new AnyComponentState
        {
            HealthId = healthId,
            VelocityId = velocityId
        };
        var operation = world.ForEachEntity(in query, ref state, InspectAnyComponents);

        operation.Invoke(ref state);

        Assert.That(state.Visited, Is.EqualTo(1));
        Assert.That(state.HealthEntities, Is.EqualTo(1));
        Assert.That(state.VelocityEntities, Is.Zero);
        Assert.That(state.HealthValue, Is.EqualTo(10));

        Entity second = world.Create(
            stackalloc[] { positionId, velocityId },
            new Position { X = 2 },
            new Velocity { X = 3 });

        operation.Invoke(ref state);

        Assert.That(state.Visited, Is.EqualTo(3));
        Assert.That(state.HealthEntities, Is.EqualTo(2));
        Assert.That(state.VelocityEntities, Is.EqualTo(1));
        Assert.That(state.HealthValue, Is.EqualTo(20));
        Assert.That(world.Get<Position>(first, positionId).X, Is.EqualTo(1));
        Assert.That(world.Get<Position>(second, positionId).X, Is.EqualTo(2));
    }

    internal static void InspectAnyComponents(ref AnyComponentState state, scoped EntityRef entity)
    {
        state.Visited++;
        if (entity.Has(state.HealthId))
        {
            Assert.That(entity.TryGet(state.HealthId, out Health health), Is.True);
            state.HealthEntities++;
            state.HealthValue += health.Value;
        }
        else
        {
            Assert.That(entity.TryGet(state.HealthId, out Health health), Is.False);
            Assert.That(health, Is.EqualTo(default(Health)));
        }

        if (entity.Has(state.VelocityId))
        {
            Assert.That(entity.TryGet(state.VelocityId, out Velocity velocity), Is.True);
            state.VelocityEntities++;
            state.VelocityValue += velocity.X;
        }
        else
        {
            Assert.That(entity.TryGet(state.VelocityId, out Velocity velocity), Is.False);
            Assert.That(velocity, Is.EqualTo(default(Velocity)));
        }
    }

    internal static void InspectEntity(ref ProbeState state, scoped EntityRef entity)
    {
        Entity handle = entity;
        Entity convertedHandle = entity;
        Assert.That(handle, Is.EqualTo(state.Entities[handle.Index]));
        Assert.That(convertedHandle, Is.EqualTo(handle));
        Assert.That(entity.Index, Is.EqualTo(handle.Index));
        Assert.That(entity.Generation, Is.EqualTo(handle.Generation));
        Assert.That(entity.Has(state.PositionId), Is.True);
        Assert.That(entity.Has(state.UnregisteredId), Is.False);

        Assert.That(entity.TryGet(state.PositionId, out Position _), Is.True);
        Assert.That(entity.TryGet<Position>(state.HealthId, out Position wrongType), Is.False);
        Assert.That(wrongType, Is.EqualTo(default(Position)));
        Assert.That(entity.TryGet<Health>(state.UnregisteredId, out Health unregistered), Is.False);
        Assert.That(unregistered, Is.EqualTo(default(Health)));
        Assert.That(entity.TryGet<Velocity>(ComponentId.Invalid, out Velocity invalid), Is.False);
        Assert.That(invalid, Is.EqualTo(default(Velocity)));

        bool expectedHealth = handle.Index == 2;
        Assert.That(entity.Has(state.HealthId), Is.EqualTo(expectedHealth));
        Assert.That(entity.TryGet(state.HealthId, out Health health), Is.EqualTo(expectedHealth));
        if (expectedHealth)
        {
            Assert.That(health.Value, Is.EqualTo(20));
            ref Health healthRef = ref entity.GetRef<Health>(state.HealthId);
            healthRef.Value++;
            state.HealthWrites++;
            bool typeMismatchThrows = false;
            try
            {
                entity.GetRef<Velocity>(state.HealthId).X++;
            }
            catch (ArgumentException)
            {
                typeMismatchThrows = true;
            }

            Assert.That(typeMismatchThrows, Is.True);
        }
        else
        {
            Assert.That(health, Is.EqualTo(default(Health)));
            bool missingThrows = false;
            try
            {
                entity.GetRef<Health>(state.HealthId).Value++;
            }
            catch (InvalidOperationException)
            {
                missingThrows = true;
            }

            Assert.That(missingThrows, Is.True);
            state.MissingHealthReads++;
        }

        bool hasFirstTag = handle.Index is 1 or 3 or 5;
        bool hasSecondTag = handle.Index is 2 or 3 or 6;
        Assert.That(entity.Has(state.FirstTagId), Is.EqualTo(hasFirstTag));
        Assert.That(entity.Has(state.SecondTagId), Is.EqualTo(hasSecondTag));
        Assert.That(entity.TryGet(state.FirstTagId, out FirstTag firstTag), Is.EqualTo(hasFirstTag));
        Assert.That(firstTag, Is.EqualTo(default(FirstTag)));
        Assert.That(entity.TryGet(state.SecondTagId, out SecondTag secondTag), Is.EqualTo(hasSecondTag));
        Assert.That(secondTag, Is.EqualTo(default(SecondTag)));
        if (hasFirstTag)
        {
            entity.GetRef<FirstTag>(state.FirstTagId) = default;
            Assert.That(entity.TryGet<FirstTag>(state.FirstTagId, out firstTag), Is.True);
            Assert.That(firstTag, Is.EqualTo(default(FirstTag)));
        }

        state.Visited++;
    }

    private static void IncrementPosition(ref ComponentId componentId, scoped EntityRef entity)
        => entity.GetRef<Position>(componentId).X++;

    internal struct ProbeState
    {
        internal Entity[] Entities;
        internal ComponentId PositionId;
        internal ComponentId HealthId;
        internal ComponentId VelocityId;
        internal ComponentId FirstTagId;
        internal ComponentId SecondTagId;
        internal ComponentId UnregisteredId;
        internal int Visited;
        internal int HealthWrites;
        internal int MissingHealthReads;
    }

    internal struct AnyComponentState
    {
        internal ComponentId HealthId;
        internal ComponentId VelocityId;
        internal int Visited;
        internal int HealthEntities;
        internal int VelocityEntities;
        internal int HealthValue;
        internal float VelocityValue;
    }

    private readonly struct FirstTag;

    private readonly struct SecondTag;
}
