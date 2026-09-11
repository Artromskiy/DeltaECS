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
    public static void ApplyStaticMethodGroup(ref Position value) => value.Value++;

    public static void ApplyStaticMethodGroupWithContext(ref ConsumerContext context, ref Position value)
    {
        context.Value++;
        value.Value++;
    }

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

        Entity[] entities = { primary, secondary };
        EntitySequence sequence = world.From(entities);
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
        EntitySequence sequence = world.From(entities);
        var sequenceFunctor = new SequenceFunctor();
        sequence.ForEachEntity(ref sequenceFunctor);
    }

    public static int RunStructural()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(11));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(12));
        ComponentId accelerationId = layouts.Register<Acceleration>(new SchemaId(13));
        using var createWorld = new World(layouts);
        int total = createWorld.Create<Position, Velocity>(3);
        using var world = new World(layouts);
        Entity[] entities = world.Create(stackalloc[] { positionId }, 4);

        total += world.Add<Velocity, Acceleration>(entities);
        total += world.Remove<Velocity, Acceleration>(entities);

        Query query = world.CreateQuery(QuerySpec.WhereAll(stackalloc[] { positionId }));
        total += world.Add<Velocity, Acceleration>(in query);
        total += world.Remove<Velocity, Acceleration>(in query);

        EntitySequence sequence = world.From(entities);
        total += sequence.Add<Velocity, Acceleration>();
        total += sequence.Remove<Velocity, Acceleration>();

        FilteredEntitySequence filtered = sequence.Where(in query);
        total += filtered.Add<Velocity, Acceleration>();
        total += filtered.Remove<Velocity, Acceleration>();

        return total;
    }

    public static int RunGenericQueries()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(21));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(22));
        ComponentId accelerationId = layouts.Register<Acceleration>(new SchemaId(23));
        ComponentId lifetimeId = layouts.Register<Lifetime>(new SchemaId(24));
        using var world = new World(layouts, chunkCapacity: 2);
        world.Create(stackalloc[] { positionId, velocityId });
        world.Create(stackalloc[] { positionId, accelerationId });

        Query all = world.WhereAll<Position, Velocity>();
        Query any = world.WhereAny<Velocity, Acceleration>();
        Query none = world.WhereNone<Lifetime>();
        Query composed = world
            .WhereAll<Position>()
            .WhereNone<Lifetime>()
            .WhereAny<Velocity>()
            .WhereAny<Acceleration>();

        return Count(world, all) == 1
            && Count(world, any) == 2
            && Count(world, none) == 2
            && Count(world, composed) == 2
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
        using var world = new World(chunkCapacity: 2);
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
        using var world = new World(chunkCapacity: 2);
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
        using var world = new World(chunkCapacity: 2);
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
        using var world = new World(chunkCapacity: 2);
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
        world.ForEach(in query, () =>
        {
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
        Entity[] entities = world.Create(stackalloc[] { healthId, teamId, aliveId }, 3);
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
        using var scope = world.BeginScope(in query);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                while (slots.MoveNext())
                {
                    count++;
                }
            }
        }

        return count;
    }
}
