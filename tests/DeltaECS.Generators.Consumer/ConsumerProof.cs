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
public struct NeedsRespawn { public int Value; }
public struct Alive { }
internal struct ClosedGenericListComponent { public int Value; }
internal struct GenericListHistory<T> { public T Value; }

internal struct PositionOrderComparer : IComponentComparer
{
    public int Invoke(in Position left, in Position right)
        => left.Value.CompareTo(right.Value);
}

internal struct VelocityOrderComparer : IComponentComparer
{
    public int Invoke(in Velocity left, in Velocity right)
        => left.Value.CompareTo(right.Value);
}

internal struct OrderedWhereContext { public int Evaluated; }

internal sealed class OrderedEntitySink
{
    public int[] Indices = new int[3];
    public int Count;
}

internal interface IClosedGenericStructAction
{
    void Invoke<T>() where T : struct;
}

internal static class ClosedGenericStructComponentList
{
    public static void ForEachData<TAction>(ref TAction action)
        where TAction : struct, IClosedGenericStructAction
        => action.Invoke<ClosedGenericListComponent>();
}

internal struct RegisterClosedGenericListComponent : IClosedGenericStructAction
{
    private readonly ComponentLayoutRegistry _layouts;

    public RegisterClosedGenericListComponent(ComponentLayoutRegistry layouts)
    {
        _layouts = layouts;
    }

    public void Invoke<T>() where T : struct
        => _ = _layouts.Register<T>(new SchemaId(91901));
}

internal struct CopyGenericListHistory<T> : IForEach
{
    public void Invoke(ref GenericListHistory<T> history, in T component)
        => history.Value = component;
}

public struct ConsumerContext { public int Value; }
public struct RuntimeGenericContext { public int Count; public int LastEntityIndex; public int[]? Calls; }

public struct RuntimeContextCount<T> : IForEachContext<RuntimeGenericContext>
{
    public void Invoke(ref RuntimeGenericContext context, in T value) => context.Count++;
}

public struct RuntimeEntityContextCount<T> : IForEachContextEntity<RuntimeGenericContext>
{
    public void Invoke(ref RuntimeGenericContext context, EntityRef entity, in T value)
    {
        context.Count++;
        context.LastEntityIndex = entity.Index;
    }
}

public struct RuntimeEntityOnly<T> : IForEachEntity
{
    public void Invoke(EntityRef entity) => System.Threading.Interlocked.Increment(ref RuntimeEntityCounter.Count);
}

public struct RuntimePairContextCount<TFirst, TSecond> : IForEachContext<RuntimeGenericContext>
{
    public void Invoke(ref RuntimeGenericContext context, in TFirst first, in TSecond second)
        => context.Count++;
}

public struct RuntimeParallelContextCount<T> : IForEachContext<RuntimeGenericContext>
{
    public void Invoke(in RuntimeGenericContext context, in T value)
        => System.Threading.Interlocked.Increment(ref context.Calls![0]);
}

public struct RuntimeParallelEntityContextCount<T> : IForEachContextEntity<RuntimeGenericContext>
{
    public void Invoke(in RuntimeGenericContext context, EntityRef entity, in T value)
        => System.Threading.Interlocked.Increment(ref context.Calls![0]);
}

public struct RuntimeValueContextCount<T> : IForEachContext<RuntimeGenericContext>
{
    public void Invoke(RuntimeGenericContext context, in T value)
        => System.Threading.Interlocked.Increment(ref context.Calls![0]);
}

public struct ContextEntityFunctor : IForEachContextEntity<ConsumerContext>
{
    public void Invoke(
        ref ConsumerContext context,
        EntityRef entity,
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
public static partial class ConsumerProof
{
    public static int RunOrderedWhereFirst()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(91801));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(91802));
        using var world = new World(layouts);
        var entities = new Entity[4];
        world.Create(stackalloc ComponentId[] { positionId, velocityId }, entities.Length, entities);
        world.GetRef<Position>(entities[0], positionId).Value = 2;
        world.GetRef<Velocity>(entities[0], velocityId).Value = 0;
        world.GetRef<Position>(entities[1], positionId).Value = 1;
        world.GetRef<Velocity>(entities[1], velocityId).Value = 5;
        world.GetRef<Position>(entities[2], positionId).Value = 1;
        world.GetRef<Velocity>(entities[2], velocityId).Value = 2;
        world.GetRef<Position>(entities[3], positionId).Value = 3;
        world.GetRef<Velocity>(entities[3], velocityId).Value = 0;

        Query query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { positionId, velocityId }));
        var positionOrder = default(PositionOrderComparer);
        var velocityOrder = default(VelocityOrderComparer);
        Entity first = world.Where(in query, static (in Position position) => position.Value > 0)
            .OrderBy(positionId, ref positionOrder)
            .ThenBy(velocityId, ref velocityOrder)
            .First().Invoke();
        Entity selected = world.Where(in query, static (in Position position) => position.Value > 0)
            .OrderBy(positionId, ref positionOrder)
            .First(static entity => entity.Index == 3).Invoke();
        Entity firstEntity = world.WhereEntity(in query,
                static (Entity entity, in Position position) => entity.Index == 1 || entity.Index == 2)
            .OrderBy(positionId, ref positionOrder)
            .ThenBy(velocityId, ref velocityOrder)
            .FirstEntity().Invoke();
        Entity noMatch = world.Where(in query, static (in Position position) => position.Value < 0)
            .OrderBy(positionId, ref positionOrder)
            .First().Invoke();

        return first == entities[2]
            && selected == entities[3]
            && firstEntity == entities[2]
            && noMatch == default ? 1 : 0;
    }

    public static int RunOrderedWhereForEach()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(91811));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(91812));
        using var world = new World(layouts);
        var entities = new Entity[4];
        world.Create(stackalloc ComponentId[] { positionId, velocityId }, entities.Length, entities);
        world.GetRef<Position>(entities[0], positionId).Value = 2;
        world.GetRef<Velocity>(entities[0], velocityId).Value = 0;
        world.GetRef<Position>(entities[1], positionId).Value = 1;
        world.GetRef<Velocity>(entities[1], velocityId).Value = 5;
        world.GetRef<Position>(entities[2], positionId).Value = 1;
        world.GetRef<Velocity>(entities[2], velocityId).Value = 2;
        world.GetRef<Position>(entities[3], positionId).Value = 0;
        world.GetRef<Velocity>(entities[3], velocityId).Value = 0;

        Query query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { positionId, velocityId }));
        var filterContext = new OrderedWhereContext();
        var sink = new OrderedEntitySink();
        var positionOrder = default(PositionOrderComparer);
        var velocityOrder = default(VelocityOrderComparer);
        world.Where(in query, ref filterContext,
                static (ref OrderedWhereContext context, in Position position) =>
                {
                    context.Evaluated++;
                    return position.Value > 0;
                })
            .OrderBy(positionId, ref positionOrder)
            .ThenBy(velocityId, ref velocityOrder)
            .ForEachEntity(ref sink,
                static (ref OrderedEntitySink output, EntityRef entity, in Position position) =>
                {
                    output.Indices[output.Count++] = entity.Index;
                }).Invoke(ref sink);

        return sink.Count == 3
            && sink.Indices[0] == entities[2].Index
            && sink.Indices[1] == entities[1].Index
            && sink.Indices[2] == entities[0].Index
                ? 1
                : 0;
    }

    public static int RunRuntimeGenericFunctorEntityForms()
    {
        using var world = new World();
        ComponentId value = world.Layouts.Register<int>(new SchemaId(91101));
        Entity[] entities = new Entity[2];
        world.Create<int>(value, 2, entities);
        Query query = world.WhereAll(value);
        RuntimeEntityCounter.Count = 0;

        world.ForEachEntity(in query, value, typeof(RuntimeEntityCount<>)).Invoke();
        if (RuntimeEntityCounter.Count != 2)
        {
            return 0;
        }

        RuntimeEntityCounter.Count = 0;
        world.ForEachEntity(entities, in query, value, typeof(RuntimeEntityCount<>)).Invoke();
        if (RuntimeEntityCounter.Count != 2)
        {
            return 0;
        }

        RuntimeEntityCounter.Count = 0;
        world.ForEachEntity(entities, value, typeof(RuntimeEntityCount<>)).Invoke();
        if (RuntimeEntityCounter.Count != 2)
        {
            return 0;
        }

        RuntimeEntityCounter.Count = 0;
        world.ForEachEntityParallel(in query, value, typeof(RuntimeEntityCount<>), workerCount: 2).Invoke();
        if (RuntimeEntityCounter.Count != 2)
        {
            return 0;
        }

        RuntimeEntityCounter.Count = 0;
        world.ForEachEntityParallel(entities, in query, value, typeof(RuntimeEntityCount<>), workerCount: 2).Invoke();
        if (RuntimeEntityCounter.Count != 2)
        {
            return 0;
        }

        RuntimeEntityCounter.Count = 0;
        world.ForEachEntityParallel(entities, value, typeof(RuntimeEntityCount<>), workerCount: 2).Invoke();
        return RuntimeEntityCounter.Count == 2 ? 1 : 0;
    }

    public static int RunRuntimeGenericFunctorComponentForms()
    {
        using var world = new World();
        ComponentId value = world.Layouts.Register<int>(new SchemaId(91102));
        Entity[] entities = new Entity[2];
        world.Create<int>(value, 2, entities);
        Query query = world.WhereAll(value);
        RuntimeEntityCounter.Count = 0;
        world.ForEachEntity(in query, value, typeof(RuntimeEntityOnly<>)).Invoke();
        if (RuntimeEntityCounter.Count != 2)
        {
            return 0;
        }

        RuntimeComponentCounter.Count = 0;
        world.ForEach(in query, value, typeof(RuntimeComponentCount<>)).Invoke();
        if (RuntimeComponentCounter.Count != 2)
        {
            return 0;
        }

        ReadOnlySpan<ComponentId> genericArguments = stackalloc ComponentId[] { value };
        RuntimeComponentCounter.Count = 0;
        world.ForEach<int>(in query, genericArguments, static (in int component) =>
            System.Threading.Interlocked.Increment(ref RuntimeComponentCounter.Count)).Invoke();
        if (RuntimeComponentCounter.Count != 2)
        {
            return 0;
        }

        RuntimeComponentCounter.Count = 0;
        try
        {
            world.ForEach<int>(in query, stackalloc ComponentId[] { value, value }, static (in int component) =>
                System.Threading.Interlocked.Increment(ref RuntimeComponentCounter.Count)).Invoke();
            return 0;
        }
        catch (ArgumentException)
        {
            if (RuntimeComponentCounter.Count != 0)
            {
                return 0;
            }
        }

        ComponentId wrongTypeId = world.Layouts.Register<float>(new SchemaId(91107));
        try
        {
            world.ForEach<int>(in query, stackalloc ComponentId[] { wrongTypeId }, static (in int component) =>
                System.Threading.Interlocked.Increment(ref RuntimeComponentCounter.Count)).Invoke();
            return 0;
        }
        catch (ArgumentException)
        {
        }

        RuntimeComponentCounter.Count = 0;
        world.ForEach(in query, genericArguments, typeof(RuntimeComponentCount<>)).Invoke();
        if (RuntimeComponentCounter.Count != 2)
        {
            return 0;
        }

        ReadOnlySpan<ComponentId> wrongArity = stackalloc ComponentId[] { value, value };
        try
        {
            world.ForEach(in query, wrongArity, typeof(RuntimeComponentCount<>)).Invoke();
            return 0;
        }
        catch (ArgumentException)
        {
        }

        RuntimeComponentCounter.Count = 0;
        world.ForEach(entities, in query, value, typeof(RuntimeComponentCount<>)).Invoke();
        if (RuntimeComponentCounter.Count != 2)
        {
            return 0;
        }

        RuntimeComponentCounter.Count = 0;
        world.ForEach(entities, value, typeof(RuntimeComponentCount<>)).Invoke();
        if (RuntimeComponentCounter.Count != 2)
        {
            return 0;
        }

        RuntimeComponentCounter.Count = 0;
        world.ForEachParallel(in query, value, typeof(RuntimeComponentCount<>), workerCount: 2).Invoke();
        if (RuntimeComponentCounter.Count != 2)
        {
            return 0;
        }

        RuntimeComponentCounter.Count = 0;
        world.ForEachParallel(entities, in query, value, typeof(RuntimeComponentCount<>), workerCount: 2).Invoke();
        if (RuntimeComponentCounter.Count != 2)
        {
            return 0;
        }

        RuntimeComponentCounter.Count = 0;
        world.ForEachParallel(entities, value, typeof(RuntimeComponentCount<>), workerCount: 2).Invoke();
        return RuntimeComponentCounter.Count == 2 ? 1 : 0;
    }

    public static int RunRuntimeGenericFunctorContextForms()
    {
        using var world = new World();
        ComponentId value = world.Layouts.Register<int>(new SchemaId(91103));
        Entity[] entities = new Entity[2];
        world.Create<int>(value, 2, entities);
        Query query = world.WhereAll(value);

        var context = new RuntimeGenericContext();
        try
        {
            world.ForEach(in query, value, typeof(RuntimeContextCount<>)).Invoke();
            return 0;
        }
        catch (InvalidOperationException)
        {
        }

        int wrongContext = 0;
        try
        {
            world.ForEach(in query, ref wrongContext, value, typeof(RuntimeContextCount<>)).Invoke(ref wrongContext);
            return 0;
        }
        catch (ArgumentException)
        {
        }

        try
        {
            world.ForEachParallel(in query, ref context, value, typeof(RuntimeContextCount<>), workerCount: 2).Invoke(ref context);
            return 0;
        }
        catch (ArgumentException)
        {
        }

        world.ForEach(in query, ref context, value, typeof(RuntimeContextCount<>)).Invoke(ref context);
        if (context.Count != 2)
        {
            return 0;
        }

        context.Count = 0;
        world.ForEach(entities, in query, ref context, value, typeof(RuntimeContextCount<>)).Invoke(ref context);
        if (context.Count != 2)
        {
            return 0;
        }

        context.Count = 0;
        world.ForEach(entities, ref context, value, typeof(RuntimeContextCount<>)).Invoke(ref context);
        if (context.Count != 2)
        {
            return 0;
        }

        context.Count = 0;
        world.ForEachEntity(in query, ref context, value, typeof(RuntimeEntityContextCount<>)).Invoke(ref context);
        if (context.Count != 2)
        {
            return 0;
        }

        context.Count = 0;
        world.ForEachEntity(entities, in query, ref context, value, typeof(RuntimeEntityContextCount<>)).Invoke(ref context);
        if (context.Count != 2)
        {
            return 0;
        }

        context.Count = 0;
        world.ForEachEntity(entities, ref context, value, typeof(RuntimeEntityContextCount<>)).Invoke(ref context);
        if (context.Count != 2)
        {
            return 0;
        }

        context.Calls = new int[1];
        world.ForEachParallel(in query, ref context, value, typeof(RuntimeParallelContextCount<>), workerCount: 2).Invoke(ref context);
        if (context.Calls![0] != 2)
        {
            return 0;
        }

        context.Calls![0] = 0;
        world.ForEachParallel(entities, in query, ref context, value, typeof(RuntimeParallelContextCount<>), workerCount: 2).Invoke(ref context);
        if (context.Calls![0] != 2)
        {
            return 0;
        }

        context.Calls![0] = 0;
        world.ForEachParallel(entities, ref context, value, typeof(RuntimeParallelContextCount<>), workerCount: 2).Invoke(ref context);
        if (context.Calls![0] != 2)
        {
            return 0;
        }

        context.Calls![0] = 0;
        world.ForEachEntityParallel(in query, ref context, value, typeof(RuntimeParallelEntityContextCount<>), workerCount: 2).Invoke(ref context);
        if (context.Calls![0] != 2)
        {
            return 0;
        }

        context.Calls![0] = 0;
        world.ForEachEntityParallel(entities, in query, ref context, value, typeof(RuntimeParallelEntityContextCount<>), workerCount: 2).Invoke(ref context);
        if (context.Calls![0] != 2)
        {
            return 0;
        }

        context.Calls![0] = 0;
        world.ForEachEntityParallel(entities, ref context, value, typeof(RuntimeParallelEntityContextCount<>), workerCount: 2).Invoke(ref context);
        if (context.Calls![0] != 2)
        {
            return 0;
        }

        context.Calls![0] = 0;
        world.ForEach(in query, ref context, value, typeof(RuntimeValueContextCount<>)).Invoke(ref context);
        if (context.Calls![0] != 2)
        {
            return 0;
        }

        ComponentId secondValue = world.Layouts.Register<int>(new SchemaId(91104));
        world.Create(value, secondValue);
        Query pairQuery = world.WhereAll(value, secondValue);
        var pairContext = new RuntimeGenericContext();
        world.ForEach(in pairQuery, ref pairContext, value, secondValue, typeof(RuntimePairContextCount<,>)).Invoke(ref pairContext);
        return pairContext.Count == 1 ? 1 : 0;
    }

    public static int RunRuntimeGenericFunctor()
    {
        using var world = new World();
        ComponentId first = world.Layouts.Register<float>(new SchemaId(91001));
        ComponentId second = world.Layouts.Register<float>(new SchemaId(91002));
        ComponentId historyFirst = world.Layouts.Register(typeof(RuntimeHistory<>), first, new SchemaId(91003));
        ComponentId historySecond = world.Layouts.Register(typeof(RuntimeHistory<>), second, new SchemaId(91004));
        Entity entity = world.Create(first, second, historyFirst, historySecond);
        world.GetRef<float>(entity, first) = 11f;
        world.GetRef<float>(entity, second) = 23f;
        Query query = world.WhereAll(first, second, historyFirst, historySecond);
        Type functorType = typeof(RuntimeSave<,,>);
        world.ForEach(in query, first, second, first, functorType).Invoke();
        world.ForEach(in query, second, first, second, functorType).Invoke();
        world.ForEach(in query, first, second, first, functorType).Invoke();
        return (int)(world.Get<RuntimeHistory<float>>(entity, historyFirst).Value
            + world.Get<RuntimeHistory<float>>(entity, historySecond).Value);
    }

    public static int RunUnaryGenericBindings()
    {
        using var world = new World();
        ComponentId valueId = world.Layouts.Register<int>(new SchemaId(91201));
        ComponentId historyId = RegisterUnaryHistory(world.Layouts, valueId);
        Entity entity = world.Create(valueId, historyId);
        Query query = world.WhereAll(valueId, historyId);

        ApplyUnaryHistory(world, in query, valueId);
        return world.Get<UnaryHistory<int>>(entity, historyId).Value == 1 ? 1 : 0;
    }

    private static ComponentId RegisterUnaryHistory(ComponentLayoutRegistry layouts, ComponentId componentId)
        => layouts.Register(typeof(UnaryHistory<>), componentId, new SchemaId(91202));

    private static void ApplyUnaryHistory(World world, in Query query, ComponentId componentId)
        => world.ForEach(in query, componentId, typeof(UnaryHistoryWriter<>)).Invoke();

    public static void ApplyStaticMethodGroup(ref Position value) => value.Value++;

    public static void ApplyStaticMethodGroupWithContext(ref ConsumerContext context, ref Position value)
    {
        context.Value++;
        value.Value++;
    }

    public static void ApplyEntityMethodGroup(EntityRef entity, ref Position value)
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

        world.GetRef<Position>(primary, positionId) = new Position { Value = 1 };
        world.GetRef<Velocity>(primary, velocityId) = new Velocity { Value = 2 };
        world.GetRef<Acceleration>(primary, accelerationId) = new Acceleration { Value = 3 };
        world.GetRef<Lifetime>(primary, lifetimeId) = new Lifetime { Value = 4 };
        world.GetRef<Position>(secondary, secondaryPositionId) = new Position { Value = 5 };

        Query allNine = world.CreateQuery(QuerySpec.WhereAll(stackalloc[]
        {
            positionId, velocityId, accelerationId, lifetimeId, massId,
            sixId, sevenId, eightId, extraId
        }));
        Query secondaryFive = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[]
        {
            secondaryPositionId, velocityId, accelerationId, lifetimeId, massId
        }));

        // Arity 1, no ID: resolves the primary registration by CLR type.
        world.ForEach<Position>(in allNine, static (ref Position value) => value.Value++).Invoke();
        world.ForEach<Position>(in allNine, ApplyStaticMethodGroup).Invoke();
        var methodGroupContext = new ConsumerContext();
        ref int c2 = ref global::System.Runtime.CompilerServices.Unsafe.NullRef<int>();

        world.ForEach<ConsumerContext, Position>(in allNine, ref methodGroupContext, ApplyStaticMethodGroupWithContext).Invoke(ref methodGroupContext);
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
            }).Invoke();

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
            }).Invoke();

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
            }).Invoke();

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
            }).Invoke(ref context);

        var functor = new ContextEntityFunctor();
        world.ForEachEntity(in query, ref context, ref functor).Invoke(ref context, ref functor);

    }

    /// <summary>Compile-only coverage for caller-selected entity-list iteration.</summary>
    public static void CompileEntityListForms(World world, in Query query, ReadOnlySpan<Entity> entities)
    {
        world.ForEach<Position>(
            entities,
            in query,
            static (ref Position position) => position.Value++).Invoke();
        world.ForEachEntity<Position>(
            entities,
            in query,
            static (EntityRef entity, ref Position position) => position.Value += entity.Index).Invoke();
        world.ForEach<Position>(
            entities,
            in query,
            world.Layouts.GetPrimary<Position>(),
            static (ref Position position) => position.Value++).Invoke();
        world.ForEachEntityParallel<Position>(
            entities,
            in query,
            static (EntityRef entity, ref Position position) => position.Value += entity.Index,
            2).Invoke();
        world.ForEach<Position>(
            entities,
            static (ref Position position) => position.Value++).Invoke();
        world.ForEachEntityParallel<Position>(
            entities,
            static (EntityRef entity, ref Position position) => position.Value += entity.Index,
            2).Invoke();
        world.ForEach<Position>(
            entities,
            world.Layouts.GetPrimary<Position>(),
            static (ref Position position) => position.Value++).Invoke();
        world.ForEach<Position>(entities, ApplyStaticMethodGroup).Invoke();
        world.ForEachEntity<Position>(entities, ApplyEntityMethodGroup).Invoke();
        var context = new ConsumerContext();
        var functor = new ContextEntityFunctor();
        world.ForEachEntity(entities, ref context, ref functor).Invoke(ref context, ref functor);
    }

    /// <summary>Compile-only coverage for the canonical typed and ComponentId selector matrix.</summary>
    public static void CompileGrammarSelectorForms(
        World world,
        in Query query,
        ReadOnlySpan<Entity> entities,
        Entity entity,
        ComponentId positionId,
        ComponentId velocityId)
    {
        world.ForEach<Position, Velocity>(in query, positionId, velocityId,
            static (ref Position position, in Velocity velocity) => position.Value += velocity.Value).Invoke();
        world.ForEachEntityParallel<Position, Velocity>(entities, in query, positionId, velocityId,
            static (EntityRef current, ref Position position, in Velocity velocity) => position.Value += current.Index + velocity.Value,
            workerCount: 2).Invoke();

        world.Add<Position, Velocity>(entity, positionId, velocityId);
        world.Add<Position, Velocity>(entities, positionId, velocityId);
        world.Remove<Position, Velocity>(in query, positionId, velocityId);

        world.Create<Position, Velocity>(positionId, velocityId, 2);
        Span<Entity> output = stackalloc Entity[2];
        world.Create<Position, Velocity>(positionId, velocityId, 2, output);
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
        Span<Entity> createdHandleOutput = stackalloc Entity[1];
        createWorld.Create<Position, Velocity>(1, createdHandleOutput);
        Entity created = createdHandleOutput[0];
        Span<Entity> createdOutput = stackalloc Entity[2];
        int outputCount = createWorld.Create<Position, Velocity>(2, createdOutput);
        Span<Entity> explicitGenericOutput = stackalloc Entity[1];
        total += createWorld.Create<Position, Velocity>(positionId, velocityId, 1, explicitGenericOutput);
        createWorld.GetRef<Position>(created) = new Position { Value = 1 };
        createWorld.GetRef<Velocity>(created) = new Velocity { Value = 2 };
        createWorld.GetRef<Position>(created) = new Position { Value = 3 };
        createWorld.GetRef<Velocity>(created) = new Velocity { Value = 4 };
        if (createWorld.Get<Position>(created).Value != 3
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

        if (!ValidateGenericComponentSpanRegistration()
            || !ValidateStructuralSelectorSpanCount(createWorld, untypedCreated, velocityId))
        {
            return 0;
        }

        return total;
    }

    private static bool ValidateGenericComponentSpanRegistration()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(91301));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(91302));
        ReadOnlySpan<ComponentId> arguments = stackalloc ComponentId[] { positionId, velocityId };
        ComponentId spanId = layouts.Register(typeof(RuntimeComponentPair<,>), arguments, new SchemaId(91303));
        ComponentId positionalId = layouts.Register(typeof(RuntimeComponentPair<,>), positionId, velocityId, new SchemaId(91303));
        if (spanId != positionalId
            || layouts.GetComponentType(spanId) != typeof(RuntimeComponentPair<Position, Velocity>))
        {
            return false;
        }

        ReadOnlySpan<ComponentId> wrongArity = stackalloc ComponentId[] { positionId };
        try
        {
            layouts.Register(typeof(RuntimeComponentPair<,>), wrongArity, new SchemaId(91305));
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static bool ValidateStructuralSelectorSpanCount(World world, Entity entity, ComponentId velocityId)
    {
        ReadOnlySpan<ComponentId> wrongArity = stackalloc ComponentId[] { velocityId };
        try
        {
            world.Add<Velocity, Acceleration>(entity, wrongArity);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
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

        ReadOnlySpan<ComponentId> typedIds = stackalloc ComponentId[] { positionId, velocityId };
        Query typedSpan = world.WhereAll<Position, Velocity>(typedIds);
        ReadOnlySpan<ComponentId> wrongArity = stackalloc ComponentId[] { positionId };
        try
        {
            _ = world.WhereAll<Position, Velocity>(wrongArity);
            return 0;
        }
        catch (ArgumentException)
        {
        }

        ReadOnlySpan<ComponentId> wrongTypes = stackalloc ComponentId[] { velocityId, positionId };
        try
        {
            _ = world.WhereAll<Position, Velocity>(wrongTypes);
            return 0;
        }
        catch (ArgumentException)
        {
        }

        return Count(world, all) == 1
            && Count(world, any) == 2
            && Count(world, none) == 2
            && Count(world, composed) == 2
            && Count(world, explicitQuery) == 2
            && Count(world, typedSpan) == 1
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
            .Destroy().Invoke();

        return destroyed == 1 && !world.IsAlive(entities[0]) && world.IsAlive(entities[1]) && world.IsAlive(entities[2])
            ? destroyed
            : 0;
    }

    private static int RunGeneratedWhereAdd()
    {
        using var world = new World();
        (ComponentId healthId, ComponentId teamId, ComponentId aliveId, _) = RegisterMutationLayouts(world);
        ComponentId needsRespawnId = world.Layouts.Register<NeedsRespawn>(new SchemaId(35));
        Entity[] entities = CreateMutationEntities(world, healthId, teamId, aliveId);
        Query query = CreateMutationQuery(world, healthId, teamId, aliveId);

        int added = world.Where(
                in query,
                static (in Health health) => health.Value <= 0)
            .Add<Dead, NeedsRespawn>(new Dead(), new NeedsRespawn { Value = 42 }).Invoke();
        added += world.WhereEntity(
                in query,
                static (Entity entity, in Health health) => health.Value > 0)
            .Add<Dead, NeedsRespawn>(
                world.Layouts.GetPrimary<Dead>(),
                needsRespawnId,
                new Dead(),
                new NeedsRespawn { Value = 7 }).Invoke();

        return added == 3
            && world.TryGet<Dead>(entities[0], out _)
            && world.Get<NeedsRespawn>(entities[0], needsRespawnId).Value == 42
            && world.TryGet<Dead>(entities[1], out _)
            && world.Get<NeedsRespawn>(entities[1], needsRespawnId).Value == 7
            && world.TryGet<Dead>(entities[2], out _)
            && world.Get<NeedsRespawn>(entities[2], needsRespawnId).Value == 42
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
            .Remove<Alive>().Invoke();

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
            .ForEachEntity(static (EntityRef entity, ref Health health, in Team team) =>
            {
                health.Value = team.DefaultHealth + entity.Index;
            }).Invoke();
        world.WhereEntity(
                in query,
                static (Entity entity, in Health health) => health.Value >= 0)
            .ForEach(static (ref Health health, in Team team) => health.Value = team.DefaultHealth).Invoke();

        bool rejectedStructuralNesting = false;
        world.ForEach(in query, (ref Health health) =>
        {
            _ = health;
            try
            {
                world.WhereEntity(
                        in query,
                        static (Entity entity, in Health health) => health.Value <= 0)
                    .Destroy().Invoke();
            }
            catch (InvalidOperationException)
            {
                rejectedStructuralNesting = true;
            }
        }).Invoke();
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
        world.GetRef<Health>(entities[0], healthId) = new Health { Value = -1 };
        world.GetRef<Health>(entities[1], healthId) = new Health { Value = 5 };
        world.GetRef<Health>(entities[2], healthId) = new Health { Value = -1 };
        world.GetRef<Team>(entities[0], teamId) = new Team { Id = 1, DefaultHealth = 100 };
        world.GetRef<Team>(entities[1], teamId) = new Team { Id = 1, DefaultHealth = 200 };
        world.GetRef<Team>(entities[2], teamId) = new Team { Id = 2, DefaultHealth = 300 };
        return entities;
    }

    private static Query CreateMutationQuery(World world, ComponentId healthId, ComponentId teamId, ComponentId aliveId)
        => world.CreateQuery(QuerySpec.WhereAll(stackalloc[] { healthId, teamId, aliveId }));

    private static int Count(World world, in Query query)
    {
        int count = 0;
        world.ForEach<Position>(
            in query,
            (ref Position _) => count++).Invoke();
        return count;
    }
}

public struct RuntimeHistory<T> { public T Value; }

public struct RuntimeComponentPair<TFirst, TSecond> { public TFirst First; public TSecond Second; }

public struct UnaryHistory<T> { public int Value; }

public struct UnaryHistoryWriter<T> : IForEach
{
    public void Invoke(ref UnaryHistory<T> history, ref T component) => history.Value++;
}

public struct RuntimeSave<T0, T1, T2> : IForEach
{
    public void Invoke(ref RuntimeHistory<T2> history, in T0 first, in T1 second, in T2 third)
        => history.Value = third;
}

public struct RuntimeEntityCount<T> : IForEachEntity
{
    public void Invoke(EntityRef entity, in T component)
        => System.Threading.Interlocked.Increment(ref RuntimeEntityCounter.Count);
}

internal static class RuntimeEntityCounter
{
    internal static int Count;
}

public struct RuntimeComponentCount<T> : IForEach
{
    public void Invoke(in T component)
        => System.Threading.Interlocked.Increment(ref RuntimeComponentCounter.Count);
}

public struct StructConstraintFunctor<T> : IForEach where T : struct
{
    public void Invoke(in T component) => ConstraintFunctorCounter.Count++;
}

public struct UnmanagedConstraintFunctor<T> : IForEach where T : unmanaged
{
    public void Invoke(in T component) => ConstraintFunctorCounter.Count++;
}

public struct ClassConstraintFunctor<T> : IForEach where T : class
{
    public void Invoke(in T component) => ConstraintFunctorCounter.Count++;
}

public struct NewConstraintFunctor<T> : IForEach where T : new()
{
    public void Invoke(in T component) => ConstraintFunctorCounter.Count++;
}

public struct ClassNewConstraintFunctor<T> : IForEach where T : class, new()
{
    public void Invoke(in T component) => ConstraintFunctorCounter.Count++;
}

public struct StructBox<T> where T : struct { public T Value; }
public struct UnmanagedBox<T> where T : unmanaged { public T Value; }
public class ClassBox<T> where T : class { public T? Value; }
public struct NewBox<T> where T : new() { public T Value; }

public sealed class ConstructibleComponent
{
    public ConstructibleComponent() { }
}

internal static class ConstraintFunctorCounter
{
    internal static int Count;
}

public static partial class ConsumerProof
{
    public static int RunStructGenericListTokenRegistration()
    {
        var layouts = new ComponentLayoutRegistry();
        var register = new RegisterClosedGenericListComponent(layouts);
        ClosedGenericStructComponentList.ForEachData(ref register);

        ComponentId component = layouts.GetPrimary<ClosedGenericListComponent>();
        ComponentId history = layouts.Register(typeof(GenericListHistory<>), component, new SchemaId(91902));
        using var world = new World(layouts);
        Entity entity = world.Create(component, history);
        world.GetRef<ClosedGenericListComponent>(entity, component).Value = 42;
        Query query = world.WhereAll(component, history);

        world.ForEach(in query, component, typeof(CopyGenericListHistory<>)).Invoke();

        return world.Get<GenericListHistory<ClosedGenericListComponent>>(entity, history).Value.Value == 42
            ? 1
            : 0;
    }

    public static int RunStandardGenericConstraints()
    {
        using var world = new World();
        ComponentId positionId = world.Layouts.Register<Position>(new SchemaId(91201));
        ComponentId intId = world.Layouts.Register<int>(new SchemaId(91202));
        ComponentId classId = world.Layouts.Register<ConstructibleComponent>(new SchemaId(91203));
        ComponentId structBoxId = world.Layouts.Register(typeof(StructBox<>), positionId, new SchemaId(91204));
        ComponentId unmanagedBoxId = world.Layouts.Register(typeof(UnmanagedBox<>), intId, new SchemaId(91205));
        ComponentId classBoxId = world.Layouts.Register(typeof(ClassBox<>), classId, new SchemaId(91206));
        ComponentId newBoxId = world.Layouts.Register(typeof(NewBox<>), classId, new SchemaId(91207));

        Query structQuery = CreateConstraintQuery(world, structBoxId);
        ConstraintFunctorCounter.Count = 0;
        world.ForEach(in structQuery, structBoxId, typeof(StructConstraintFunctor<>)).Invoke();
        if (ConstraintFunctorCounter.Count != 1)
        {
            return 0;
        }

        Query unmanagedQuery = CreateConstraintQuery(world, unmanagedBoxId);
        ConstraintFunctorCounter.Count = 0;
        world.ForEach(in unmanagedQuery, unmanagedBoxId, typeof(UnmanagedConstraintFunctor<>)).Invoke();
        if (ConstraintFunctorCounter.Count != 1)
        {
            return 0;
        }

        Query classBoxQuery = CreateConstraintQuery(world, classBoxId);
        ConstraintFunctorCounter.Count = 0;
        world.ForEach(in classBoxQuery, classBoxId, typeof(ClassConstraintFunctor<>)).Invoke();
        if (ConstraintFunctorCounter.Count != 1)
        {
            return 0;
        }

        Query newQuery = CreateConstraintQuery(world, classId);
        ConstraintFunctorCounter.Count = 0;
        world.ForEach(in newQuery, classId, typeof(NewConstraintFunctor<>)).Invoke();
        if (ConstraintFunctorCounter.Count != 1)
        {
            return 0;
        }

        ConstraintFunctorCounter.Count = 0;
        world.ForEach(in newQuery, classId, typeof(ClassNewConstraintFunctor<>)).Invoke();
        if (ConstraintFunctorCounter.Count != 1)
        {
            return 0;
        }

        Query newBoxQuery = CreateConstraintQuery(world, newBoxId);
        ConstraintFunctorCounter.Count = 0;
        world.ForEach(in newBoxQuery, newBoxId, typeof(StructConstraintFunctor<>)).Invoke();
        if (ConstraintFunctorCounter.Count != 1)
        {
            return 0;
        }

        Query classQuery = world.WhereAll(classId);
        try
        {
            world.ForEach(in classQuery, classId, typeof(StructConstraintFunctor<>)).Invoke();
            return 0;
        }
        catch (ArgumentException)
        {
            return 1;
        }
    }

    private static Query CreateConstraintQuery(World world, ComponentId componentId)
    {
        Entity[] entities = new Entity[1];
        world.Create(stackalloc ComponentId[] { componentId }, entities.Length, entities);
        return world.WhereAll(componentId);
    }
}

internal static class RuntimeComponentCounter
{
    internal static int Count;
}
