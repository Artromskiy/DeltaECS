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

    internal static void InspectEntity(ref ProbeState state, EntityRef entity)
    {
        Entity handle = entity.Handle;
        Assert.That(handle, Is.EqualTo(state.Entities[handle.Index]));
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

    private readonly struct FirstTag;

    private readonly struct SecondTag;
}
