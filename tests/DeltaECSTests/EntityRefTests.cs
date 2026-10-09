namespace Delta.ECS.Tests;

using System;
using NUnit.Framework;

[TestFixture]
internal sealed class EntityRefTests
{
    [Test]
    public void EntityRefUsesCurrentSlotAndChecksDynamicComponentAccess()
    {
        var layouts = new ComponentLayoutRegistry();
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
            World = world,
            Entities = entities,
            PositionId = positionId,
            HealthId = healthId,
            VelocityId = velocityId,
            FirstTagId = firstTagId,
            SecondTagId = secondTagId,
            UnregisteredId = new ComponentId(1000),
            HealthStamps = new Stamp[entities.Length]
        };

        world.ForEachEntity(in query, ref state, InspectEntity).Invoke(ref state);

        Assert.That(state.Visited, Is.EqualTo(5));
        Assert.That(state.HealthWrites, Is.EqualTo(1));
        Assert.That(state.MissingHealthReads, Is.EqualTo(4));
        Assert.That(world.Get<Health>(entities[2], healthId).Value, Is.EqualTo(21));
        Assert.That(world.Get<Health>(entities[0], healthId).Value, Is.EqualTo(10));
        Assert.That(world.TryGetComponentStamp(entities[2], healthId, out Stamp after), Is.True);
        Assert.That(after, Is.EqualTo(new Stamp(state.HealthStamps[2].Value + 1)));
    }

    [Test]
    public void DelegateForEachEntityReusesEntityRefAcrossChunksAndTagSlots()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_076));
        ComponentId tagId = layouts.Register<FirstTag>(new SchemaId(40_077));
        ComponentId missingId = layouts.Register<Velocity>(new SchemaId(40_078));
        using var world = new World(layouts);

        int entityCount = Chunk.Capacity * 2 + 3;
        var entities = new Entity[entityCount];
        world.Create(stackalloc[] { positionId }, entities);
        int taggedCount = 0;
        for (int index = 0; index < entities.Length; index += 2)
        {
            world.Add(entities[index], tagId);
            taggedCount++;
        }

        Query allPositions = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var denseVisits = new int[entityCount];
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
        var taggedVisits = new int[entityCount];
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
    public void EntityRefUsesPreparedAnyRowsAndRefreshesForNewArchetypes()
    {
        var layouts = new ComponentLayoutRegistry();
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

    [Test]
    public void EntityRefWritesStampsImmediatelyAcrossAllChunks()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_084));
        using var world = new World(layouts);
        var entities = new Entity[Chunk.Capacity + 7];
        world.Create(stackalloc[] { positionId }, entities);

        for (int index = 0; index < entities.Length; index += 3)
        {
            world.GetRef<Position>(entities[index], positionId).X = index;
        }

        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var before = new Stamp[entities.Length];
        var entityTerms = new Stamp[entities.Length];
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.TryGetComponentStamp(entities[index], positionId, out before[index]), Is.True);
            int slot = index % Chunk.Capacity;
            entityTerms[index] = world.Archetypes[0].GetChunk(index / Chunk.Capacity).GetComponentStampTrusted(0, slot);
        }

        Stamp archetypeStampBefore = world.GetArchetypeComponentStamps(world.Archetypes[0].Id)[0];
        var state = new DirectStampState(world, positionId, before);

        world.ForEachEntity(in query, ref state, WriteEveryEntity).Invoke(ref state);

        Assert.That(state.StampsWereVisible, Is.True);
        Assert.That(
            world.GetArchetypeComponentStamps(world.Archetypes[0].Id)[0],
            Is.EqualTo(archetypeStampBefore));
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.TryGetComponentStamp(entities[index], positionId, out Stamp after), Is.True);
            Assert.That(after, Is.EqualTo(new Stamp(before[index].Value + 1)));
            int slot = index % Chunk.Capacity;
            Stamp entityTermAfter = world.Archetypes[0].GetChunk(index / Chunk.Capacity).GetComponentStampTrusted(0, slot);
            Assert.That(entityTermAfter, Is.EqualTo(new Stamp(entityTerms[index].Value + 1)));
        }
    }

    [Test]
    public void EntityRefStampIndicesFollowArchetypePlanRefreshes()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_088));
        using var world = new World(layouts);
        var initialEntities = new Entity[(Chunk.Capacity * 2) + 5];
        world.Create(stackalloc[] { positionId }, initialEntities);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var operation = world.ForEachEntity(in query, ref positionId, IncrementPosition);

        operation.Invoke(ref positionId);

        var expectedStamps = new Stamp[initialEntities.Length];
        for (int index = 0; index < initialEntities.Length; index++)
        {
            Assert.That(world.TryGetComponentStamp(initialEntities[index], positionId, out expectedStamps[index]), Is.True);
        }

        Assert.That(world.Destroy(initialEntities.AsSpan(0, Chunk.Capacity)), Is.EqualTo(Chunk.Capacity));
        operation.Invoke(ref positionId);

        for (int index = Chunk.Capacity; index < initialEntities.Length; index++)
        {
            expectedStamps[index] = new Stamp(expectedStamps[index].Value + 1);
            Assert.That(world.TryGetComponentStamp(initialEntities[index], positionId, out Stamp actual), Is.True);
            Assert.That(actual, Is.EqualTo(expectedStamps[index]));
        }

        var addedEntities = new Entity[Chunk.Capacity + 7];
        world.Create(stackalloc[] { positionId }, addedEntities);
        var liveEntities = new Entity[(initialEntities.Length - Chunk.Capacity) + addedEntities.Length];
        initialEntities.AsSpan(Chunk.Capacity).CopyTo(liveEntities);
        addedEntities.CopyTo(liveEntities, initialEntities.Length - Chunk.Capacity);
        var stampsBeforeThirdInvoke = new Stamp[liveEntities.Length];
        for (int index = 0; index < liveEntities.Length; index++)
        {
            Assert.That(world.TryGetComponentStamp(liveEntities[index], positionId, out stampsBeforeThirdInvoke[index]), Is.True);
        }

        operation.Invoke(ref positionId);

        for (int index = 0; index < liveEntities.Length; index++)
        {
            Assert.That(world.TryGetComponentStamp(liveEntities[index], positionId, out Stamp actual), Is.True);
            Assert.That(actual, Is.EqualTo(new Stamp(stampsBeforeThirdInvoke[index].Value + 1)));
        }
    }

    [Test]
    public void EntityRefWritesOnlyStampWrittenEntities()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_087));
        using var world = new World(layouts);
        var entities = new Entity[6];
        world.Create(stackalloc[] { positionId }, entities);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var before = new Stamp[entities.Length];
        var entityTerms = new Stamp[entities.Length];
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.TryGetComponentStamp(entities[index], positionId, out before[index]), Is.True);
            entityTerms[index] = world.Archetypes[0].GetChunk(0).GetComponentStampTrusted(0, index);
        }

        Stamp archetypeStampBefore = world.GetArchetypeComponentStamps(world.Archetypes[0].Id)[0];
        var state = new DirectStampState(world, positionId, before);

        world.ForEachEntity(in query, ref state, WriteFirstFourOnce).Invoke(ref state);

        Assert.That(state.StampsWereVisible, Is.True);
        Assert.That(world.GetArchetypeComponentStamps(world.Archetypes[0].Id)[0], Is.EqualTo(archetypeStampBefore));
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.TryGetComponentStamp(entities[index], positionId, out Stamp after), Is.True);
            Assert.That(after, Is.EqualTo(new Stamp(before[index].Value + (index < 4 ? 1UL : 0UL))));
            Stamp expectedEntityTerm = index < 4
                ? new Stamp(entityTerms[index].Value + 1)
                : entityTerms[index];
            Assert.That(world.Archetypes[0].GetChunk(0).GetComponentStampTrusted(0, index), Is.EqualTo(expectedEntityTerm));
        }
    }

    [Test]
    public void EntityRefWritesSparseStampsImmediatelyWithoutChangingUnwrittenEntities()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_085));
        using var world = new World(layouts);
        var entities = new Entity[6];
        world.Create(stackalloc[] { positionId }, entities);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var before = new Stamp[entities.Length];
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.TryGetComponentStamp(entities[index], positionId, out before[index]), Is.True);
        }

        Stamp archetypeStampBefore = world.GetArchetypeComponentStamps(world.Archetypes[0].Id)[0];
        var state = new DirectStampState(world, positionId, before);

        world.ForEachEntity(in query, ref state, WriteSparseRepeatedly).Invoke(ref state);

        Assert.That(state.StampsWereVisible, Is.True);
        Assert.That(world.GetArchetypeComponentStamps(world.Archetypes[0].Id)[0], Is.EqualTo(archetypeStampBefore));
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.TryGetComponentStamp(entities[index], positionId, out Stamp after), Is.True);
            ulong expected = before[index].Value + (index % 2 == 0 ? 2UL : 0UL);
            Assert.That(after, Is.EqualTo(new Stamp(expected)));
        }
    }

    [Test]
    public void EntityRefKeepsImmediateStampWhenCallbackThrows()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_086));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp before), Is.True);
        var state = new DirectStampState(world, positionId, new[] { before });

        var operation = world.ForEachEntity(in query, ref state, WriteThenThrow);
        bool callbackThrew = false;
        try
        {
            operation.Invoke(ref state);
        }
        catch (InvalidOperationException)
        {
            callbackThrew = true;
        }

        Assert.That(callbackThrew, Is.True);
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp after), Is.True);
        Assert.That(after, Is.EqualTo(new Stamp(before.Value + 1)));
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
        Entity handle = entity.Handle;
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
            Assert.That(state.World.TryGetComponentStamp(handle, state.HealthId, out Stamp stamp), Is.True);
            state.HealthStamps[handle.Index] = stamp;
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

    internal struct ProbeState
    {
        internal World World;
        internal Entity[] Entities;
        internal ComponentId PositionId;
        internal ComponentId HealthId;
        internal ComponentId VelocityId;
        internal ComponentId FirstTagId;
        internal ComponentId SecondTagId;
        internal ComponentId UnregisteredId;
        internal Stamp[] HealthStamps;
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

    internal struct DirectStampState
    {
        internal DirectStampState(World world, ComponentId componentId, Stamp[] before)
        {
            World = world;
            ComponentId = componentId;
            Before = before;
            StampsWereVisible = true;
        }

        internal World World;
        internal ComponentId ComponentId;
        internal Stamp[] Before;
        internal bool StampsWereVisible;
    }

    private static void WriteEveryEntity(ref DirectStampState state, scoped EntityRef entity)
    {
        ulong before = state.Before[entity.Index].Value;
        entity.GetRef<Position>(state.ComponentId).X++;
        state.StampsWereVisible &= state.World.TryGetComponentStamp(
                entity.Handle,
                state.ComponentId,
                out Stamp pending)
            && pending.Value == before + 1;
    }

    private static void WriteSparseRepeatedly(ref DirectStampState state, scoped EntityRef entity)
    {
        if (entity.Index % 2 != 0)
        {
            return;
        }

        ulong before = state.Before[entity.Index].Value;
        entity.GetRef<Position>(state.ComponentId).X++;
        entity.GetRef<Position>(state.ComponentId).X++;
        state.StampsWereVisible &= state.World.TryGetComponentStamp(
                entity.Handle,
                state.ComponentId,
                out Stamp pending)
            && pending.Value == before + 2;
    }

    private static void WriteFirstFourOnce(ref DirectStampState state, scoped EntityRef entity)
    {
        if (entity.Index >= 4)
        {
            return;
        }

        ulong before = state.Before[entity.Index].Value;
        entity.GetRef<Position>(state.ComponentId).X++;
        state.StampsWereVisible &= state.World.TryGetComponentStamp(
                entity.Handle,
                state.ComponentId,
                out Stamp pending)
            && pending.Value == before + 1;
    }

    private static void WriteThenThrow(ref DirectStampState state, scoped EntityRef entity)
    {
        entity.GetRef<Position>(state.ComponentId).X++;
        throw new InvalidOperationException();
    }

    private static void IncrementPosition(ref ComponentId componentId, scoped EntityRef entity)
        => entity.GetRef<Position>(componentId).X++;

    private readonly struct FirstTag;

    private readonly struct SecondTag;
}
