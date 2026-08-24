using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

public struct Position { public int Value; }
public struct Velocity { public int Value; }
public struct Acceleration { public int Value; }
public struct Lifetime { public int Value; }
public struct Mass { public int Value; }
public struct ComponentSix { public int Value; }
public struct ComponentSeven { public int Value; }
public struct ComponentEight { public int Value; }
public struct Extra { public int Value; }

public struct ConsumerContext { public int Value; }

public struct ContextEntityFunctor : IForEachContextEntity<ConsumerContext>
{
    public void Invoke(
        ref ConsumerContext context,
        Entity entity,
        in Position position,
        ref Velocity velocity,
        in Acceleration acceleration,
        ref Lifetime lifetime)
    {
        velocity.Value += position.Value + acceleration.Value + entity.Index;
        lifetime.Value += context.Value;
        context.Value++;
    }
}

public struct SequenceFunctor : IForEachEntity
{
    public void Invoke(
        Entity entity,
        in Position position,
        ref Velocity velocity,
        in Acceleration acceleration,
        ref Lifetime lifetime)
    {
        velocity.Value += position.Value + acceleration.Value + entity.Index;
        lifetime.Value++;
    }
}

/// <summary>
/// Consumer-side fixture. The demand-driven generator is attached to this
/// project as an analyzer; no runtime stubs or pre-generated matrix are used.
/// </summary>
public static class ConsumerProof
{
    public static int Run()
    {
        using var world = new World(chunkCapacity: 2);
        ComponentId positionId = world.Layouts.Register<Position>(new SchemaId(1));
        ComponentId secondaryPositionId = world.Layouts.Register<Position>(new SchemaId(2));
        ComponentId velocityId = world.Layouts.Register<Velocity>(new SchemaId(3));
        ComponentId accelerationId = world.Layouts.Register<Acceleration>(new SchemaId(4));
        ComponentId lifetimeId = world.Layouts.Register<Lifetime>(new SchemaId(5));
        ComponentId massId = world.Layouts.Register<Mass>(new SchemaId(6));
        ComponentId sixId = world.Layouts.Register<ComponentSix>(new SchemaId(7));
        ComponentId sevenId = world.Layouts.Register<ComponentSeven>(new SchemaId(8));
        ComponentId eightId = world.Layouts.Register<ComponentEight>(new SchemaId(9));
        ComponentId extraId = world.Layouts.Register<Extra>(new SchemaId(10));

        Entity primary = world.Create(stackalloc[]
        {
            positionId, velocityId, accelerationId, lifetimeId, massId,
            sixId, sevenId, eightId, extraId
        });
        Entity secondary = world.Create(stackalloc[]
        {
            secondaryPositionId, velocityId, accelerationId, lifetimeId, massId
        });

        world.Set(primary, positionId, new Position { Value = 1 });
        world.Set(primary, velocityId, new Velocity { Value = 2 });
        world.Set(primary, accelerationId, new Acceleration { Value = 3 });
        world.Set(primary, lifetimeId, new Lifetime { Value = 4 });
        world.Set(secondary, secondaryPositionId, new Position { Value = 5 });

        Query allNine = world.CreateQuery(QuerySpec.ForComponents(stackalloc[]
        {
            positionId, velocityId, accelerationId, lifetimeId, massId,
            sixId, sevenId, eightId, extraId
        }));
        Query secondaryFive = world.CreateQuery(QuerySpec.ForComponents(
            secondaryPositionId, velocityId, accelerationId, lifetimeId, massId));

        // Arity 1, no ID: resolves the primary registration by CLR type.
        world.ForEach<Position>(in allNine, static (ref Position value) => value.Value++);

        // Arity 4, no ID, mixed read/write access.
        world.ForEach<Position, Velocity, Acceleration, Lifetime>(
            in allNine,
            static (
                in Position position,
                ref Velocity velocity,
                in Acceleration acceleration,
                ref Lifetime lifetime) =>
            {
                velocity.Value += position.Value + acceleration.Value;
                lifetime.Value++;
            });

        // Arity 5, explicit secondary registration of Position.
        world.ForEach<Position, Velocity, Acceleration, Lifetime, Mass>(
            in secondaryFive,
            secondaryPositionId, velocityId, accelerationId, lifetimeId, massId,
            static (
                in Position position,
                ref Velocity velocity,
                in Acceleration acceleration,
                ref Lifetime lifetime,
                ref Mass mass) =>
            {
                velocity.Value += position.Value + acceleration.Value;
                lifetime.Value++;
                mass.Value++;
            });

        // Arity 8, no IDs, with an additional All component in the query.
        world.ForEach<Position, Velocity, Acceleration, Lifetime, Mass, ComponentSix, ComponentSeven, ComponentEight>(
            in allNine,
            static (
                ref Position position,
                in Velocity velocity,
                ref Acceleration acceleration,
                in Lifetime lifetime,
                ref Mass mass,
                in ComponentSix six,
                ref ComponentSeven seven,
                in ComponentEight eight) =>
            {
                position.Value += velocity.Value + lifetime.Value + six.Value + eight.Value;
                acceleration.Value += mass.Value;
                seven.Value++;
            });

        Entity[] entities = { primary, secondary };
        EntitySequence sequence = world.Entities(entities);
        sequence.ForEachEntity<Position>(
            static (Entity entity, ref Position position) => position.Value += entity.Index);

        FilteredEntitySequence filtered = sequence.Where(in allNine);
        filtered.ForEachEntity<Position, Velocity>(
            static (Entity entity, in Position position, ref Velocity velocity) =>
                velocity.Value += position.Value + entity.Index);

        return world.Get<Position>(primary, positionId).Value
            + world.Get<Velocity>(primary, velocityId).Value
            + world.Get<Position>(secondary, secondaryPositionId).Value;
    }

    /// <summary>Compile-only coverage for context and struct-functor forms.</summary>
    public static void CompileFunctorForms(
        World world,
        in Query query,
        ref ConsumerContext context)
    {
        world.ForEach<ConsumerContext, Position, Velocity, Acceleration, Lifetime>(
            in query,
            ref context,
            static (
                ref ConsumerContext state,
                in Position position,
                ref Velocity velocity,
                in Acceleration acceleration,
                ref Lifetime lifetime) =>
            {
                velocity.Value += position.Value + acceleration.Value;
                lifetime.Value += state.Value;
                state.Value++;
            });

        var functor = new ContextEntityFunctor();
        world.ForEachEntity(in query, ref context, ref functor);

        Entity[] entities = Array.Empty<Entity>();
        EntitySequence sequence = world.Entities(entities);
        var sequenceFunctor = new SequenceFunctor();
        sequence.ForEachEntity(ref sequenceFunctor);
    }
}
