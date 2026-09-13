using System;
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
public struct Health { public int Value; }
public struct Team { public int Id; public int DefaultHealth; }
public struct Dead { }
public struct Alive { }

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

/// <summary>
/// Consumer-side fixture. The demand-driven generator is attached to this
/// project as an analyzer; no runtime stubs or pre-generated matrix are used.
/// </summary>
public static class ConsumerProof
{
    public static void ApplyStaticMethodGroup(ref Position value) => value.Value++;

    public static void ApplyStaticMethodGroupWithContext(ref ConsumerContext context, ref Position value)
    {
        context.Value++;
        value.Value++;
    }

    public static void ApplyEntityMethodGroup(Entity entity, ref Position value)
        => value.Value += entity.Index;

    public static int Run()
    {
        using var world = new World();
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

        Query allNine = world.CreateQuery(QuerySpec.WhereAll(stackalloc[]
        {
            positionId, velocityId, accelerationId, lifetimeId, massId,
            sixId, sevenId, eightId, extraId
        }));
        Query secondaryFive = world.CreateQuery(QuerySpec.WhereAll(
            secondaryPositionId, velocityId, accelerationId, lifetimeId, massId));

        // Arity 1, no ID: resolves the primary registration by CLR type.
        world.ForEach<Position>(in allNine, static (ref Position value) => value.Value++);
        world.ForEach<Position>(in allNine, ApplyStaticMethodGroup);
        var methodGroupContext = new ConsumerContext();
        world.ForEach<ConsumerContext, Position>(in allNine, ref methodGroupContext, ApplyStaticMethodGroupWithContext);
        if (methodGroupContext.Value != 1)
        {
            throw new InvalidOperationException("Static method-group context callback was not invoked exactly once.");
        }

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

    }

    /// <summary>Compile-only coverage for caller-selected entity-list iteration.</summary>
    public static void CompileEntityListForms(World world, in Query query, ReadOnlySpan<Entity> entities)
    {
        world.ForEach<Position>(
            entities,
            in query,
            static (ref Position position) => position.Value++);
        world.ForEachEntity<Position>(
            entities,
            in query,
            static (Entity entity, ref Position position) => position.Value += entity.Index);
        world.ForEach<Position>(
            entities,
            in query,
            world.Layouts.GetPrimary<Position>(),
            static (ref Position position) => position.Value++);
        world.ForEachEntityParallel<Position>(
            entities,
            in query,
            static (Entity entity, ref Position position) => position.Value += entity.Index,
            2);
        world.ForEach<Position>(
            entities,
            static (ref Position position) => position.Value++);
        world.ForEachEntityParallel<Position>(
            entities,
            static (Entity entity, ref Position position) => position.Value += entity.Index,
            2);
        world.ForEach<Position>(
            entities,
            world.Layouts.GetPrimary<Position>(),
            static (ref Position position) => position.Value++);
        world.ForEach<Position>(entities, ApplyStaticMethodGroup);
        world.ForEachEntity<Position>(entities, ApplyEntityMethodGroup);
        var context = new ConsumerContext();
        var functor = new ContextEntityFunctor();
        world.ForEachEntity(entities, ref context, ref functor);
    }

    public static int RunStructural()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(11));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(12));
        ComponentId accelerationId = layouts.Register<Acceleration>(new SchemaId(13));
        using var createWorld = new World(layouts);
        int total = createWorld.Create<Position, Velocity>(3);
        total += createWorld.Create<Position, Velocity>(positionId, velocityId, 1);
        Entity created = createWorld.Create<Position, Velocity>();
        Span<Entity> createdOutput = stackalloc Entity[2];
        int outputCount = createWorld.Create<Position, Velocity>(2, createdOutput);
        Span<Entity> explicitGenericOutput = stackalloc Entity[1];
        total += createWorld.Create<Position, Velocity>(positionId, velocityId, 1, explicitGenericOutput);
        if (!createWorld.Set(created, new Position { Value = 1 }, new Velocity { Value = 2 })
            || !createWorld.Set<Position, Velocity>(created, new Position { Value = 3 }, new Velocity { Value = 4 })
            || createWorld.Get<Position>(created).Value != 3
            || createWorld.Get<Velocity>(created).Value != 4)
        {
            return 0;
        }

        if (outputCount != createdOutput.Length
            || !createWorld.Has<Position>(created)
            || !createWorld.Has(created, positionId)
            || !createWorld.TryGetComponentStamp<Position>(created, out _)
            || !createWorld.TryGetComponentStamp(created, positionId, out _)
            || !createWorld.Add<Acceleration, Position>(created)
            || !createWorld.Remove<Acceleration, Position>(created))
        {
            return 0;
        }

        Entity untypedCreated = createWorld.Create(stackalloc[] { positionId });
        if (!createWorld.Add(untypedCreated, stackalloc[] { velocityId, accelerationId })
            || !createWorld.Remove(untypedCreated, stackalloc[] { velocityId, accelerationId }))
        {
            return 0;
        }

        Entity explicitGenericTarget = createWorld.Create(positionId);
        if (!createWorld.Add<Velocity, Acceleration>(explicitGenericTarget, velocityId, accelerationId)
            || !createWorld.Remove<Velocity, Acceleration>(explicitGenericTarget, velocityId, accelerationId))
        {
            return 0;
        }

        Span<Entity> explicitOutput = stackalloc Entity[2];
        if (createWorld.Create(positionId, velocityId, 2, explicitOutput) != explicitOutput.Length
            || !createWorld.Add(untypedCreated, velocityId)
            || !createWorld.Remove(untypedCreated, velocityId)
            || !createWorld.Add(untypedCreated, velocityId, accelerationId)
            || !createWorld.Remove(untypedCreated, velocityId, accelerationId))
        {
            return 0;
        }

        using var world = new World(layouts);
        Entity[] entities = new Entity[4];
        world.Create(stackalloc[] { positionId }, entities.Length, entities);

        total += world.Add<Velocity, Acceleration>(entities);
        total += world.Remove<Velocity, Acceleration>(entities);

        Query query = world.CreateQuery(QuerySpec.WhereAll(stackalloc[] { positionId }));
        total += world.Add<Velocity, Acceleration>(in query);
        total += world.Remove<Velocity, Acceleration>(in query);

        return total;
    }

    public static int RunGenericQueries()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(21));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(22));
        ComponentId accelerationId = layouts.Register<Acceleration>(new SchemaId(23));
        ComponentId lifetimeId = layouts.Register<Lifetime>(new SchemaId(24));
        using var world = new World(layouts);
        world.Create(stackalloc[] { positionId, velocityId });
        world.Create(stackalloc[] { positionId, accelerationId });

        Query all = world.WhereAll<Position, Velocity>();
        Query any = world
            .WhereAll<Position>()
            .WhereAny<Velocity, Acceleration>();
        Query none = world
            .WhereAll<Position>()
            .WhereNone<Lifetime>();
        Query composed = world
            .WhereAll<Position>()
            .WhereNone<Lifetime>()
            .WhereAny<Velocity>()
            .WhereAny<Acceleration>();
        Query explicitQuery = world
            .WhereAll(positionId)
            .WhereNone(lifetimeId)
            .WhereAny(velocityId, accelerationId);

        return Count(world, all) == 1
            && Count(world, any) == 2
            && Count(world, none) == 2
            && Count(world, composed) == 2
            && Count(world, explicitQuery) == 2
            ? 1
            : 0;
    }

    public static int RunGeneratedWhere()
    {
        int destroyed = RunGeneratedWhereDestroy();
        int added = RunGeneratedWhereAdd();
        int removed = RunGeneratedWhereRemove();
        int callbacks = RunGeneratedWhereCallbacks();
        return destroyed == 1 && added == 1 && removed == 1 && callbacks == 3 ? 1 : 0;
    }

    private static int RunGeneratedWhereDestroy()
    {
        using var world = new World();
        (ComponentId healthId, ComponentId teamId, ComponentId aliveId, _) = RegisterMutationLayouts(world);
        Entity[] entities = CreateMutationEntities(world, healthId, teamId, aliveId);
        Query query = CreateMutationQuery(world, healthId, teamId, aliveId);

        int destroyed = world.WhereEntity(
                in query,
                static (Entity entity, in Health health, in Team team) => health.Value <= 0 && team.Id == 1)
            .Destroy();

        return destroyed == 1 && !world.IsAlive(entities[0]) && world.IsAlive(entities[1]) && world.IsAlive(entities[2])
            ? destroyed
            : 0;
    }

    private static int RunGeneratedWhereAdd()
    {
        using var world = new World();
        (ComponentId healthId, ComponentId teamId, ComponentId aliveId, _) = RegisterMutationLayouts(world);
        Entity[] entities = CreateMutationEntities(world, healthId, teamId, aliveId);
        Query query = CreateMutationQuery(world, healthId, teamId, aliveId);

        int added = world.WhereEntity(
                in query,
                static (Entity entity, in Health health) => health.Value <= 0)
            .Add<Dead>();

        return added == 2
            && world.TryGet<Dead>(entities[0], out _)
            && !world.TryGet<Dead>(entities[1], out _)
            && world.TryGet<Dead>(entities[2], out _)
            && world.Get<Health>(entities[0], healthId).Value == -1
            && world.Get<Health>(entities[2], healthId).Value == -1
            ? 1
            : 0;
    }

    private static int RunGeneratedWhereRemove()
    {
        using var world = new World();
        (ComponentId healthId, ComponentId teamId, ComponentId aliveId, _) = RegisterMutationLayouts(world);
        Entity[] entities = CreateMutationEntities(world, healthId, teamId, aliveId);
        Query query = CreateMutationQuery(world, healthId, teamId, aliveId);

        int removed = world.WhereEntity(
                in query,
                static (Entity entity, in Health health, in Team team) => health.Value <= 0 && team.Id == 1)
            .Remove<Alive>();

        return removed == 1 && !world.TryGet<Alive>(entities[0], out _) && world.TryGet<Alive>(entities[1], out _)
            ? removed
            : 0;
    }

    private static int RunGeneratedWhereCallbacks()
    {
        using var world = new World();
        (ComponentId healthId, ComponentId teamId, ComponentId aliveId, _) = RegisterMutationLayouts(world);
        Entity[] entities = CreateMutationEntities(world, healthId, teamId, aliveId);
        Query query = CreateMutationQuery(world, healthId, teamId, aliveId);
        int entityCallbackCount = 0;

        world.WhereEntity(
                in query,
                static (Entity entity, in Health health) => health.Value <= 0)
            .ForEachEntity(static (Entity entity, ref Health health, in Team team) =>
            {
                health.Value = team.DefaultHealth + entity.Index;
            });
        world.WhereEntity(
                in query,
                static (Entity entity, in Health health) => health.Value > 0)
            .ForEachEntity(static entity => _ = entity);
        world.WhereEntity(
                in query,
                static (Entity entity, in Health health) => health.Value >= 0)
            .ForEach(static (ref Health health, in Team team) => health.Value = team.DefaultHealth);

        bool rejectedStructuralNesting = false;
        world.ForEach(in query, (ref Health health) =>
        {
            _ = health;
            try
            {
                world.WhereEntity(
                        in query,
                        static (Entity entity, in Health health) => health.Value <= 0)
                    .Destroy();
            }
            catch (InvalidOperationException)
            {
                rejectedStructuralNesting = true;
            }
        });
        if (!rejectedStructuralNesting)
        {
            return 0;
        }

        for (int index = 0; index < entities.Length; index++)
        {
            if (world.Get<Health>(entities[index], healthId).Value == world.Get<Team>(entities[index], teamId).DefaultHealth)
            {
                entityCallbackCount++;
            }
        }

        return entityCallbackCount;
    }

    private static (ComponentId Health, ComponentId Team, ComponentId Alive, ComponentId Dead) RegisterMutationLayouts(World world)
        => (
            world.Layouts.Register<Health>(new SchemaId(31)),
            world.Layouts.Register<Team>(new SchemaId(32)),
            world.Layouts.Register<Alive>(new SchemaId(33)),
            world.Layouts.Register<Dead>(new SchemaId(34)));

    private static Entity[] CreateMutationEntities(World world, ComponentId healthId, ComponentId teamId, ComponentId aliveId)
    {
        Entity[] entities = new Entity[3];
        world.Create(stackalloc[] { healthId, teamId, aliveId }, entities.Length, entities);
        world.Set(entities[0], healthId, new Health { Value = -1 });
        world.Set(entities[1], healthId, new Health { Value = 5 });
        world.Set(entities[2], healthId, new Health { Value = -1 });
        world.Set(entities[0], teamId, new Team { Id = 1, DefaultHealth = 100 });
        world.Set(entities[1], teamId, new Team { Id = 1, DefaultHealth = 200 });
        world.Set(entities[2], teamId, new Team { Id = 2, DefaultHealth = 300 });
        return entities;
    }

    private static Query CreateMutationQuery(World world, ComponentId healthId, ComponentId teamId, ComponentId aliveId)
        => world.CreateQuery(QuerySpec.WhereAll(stackalloc[] { healthId, teamId, aliveId }));

    private static int Count(World world, in Query query)
    {
        int count = 0;
        world.ForEach<Position>(
            in query,
            (ref Position _) => count++);
        return count;
    }
}
