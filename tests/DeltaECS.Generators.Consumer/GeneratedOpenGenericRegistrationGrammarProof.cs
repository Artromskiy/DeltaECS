using System;
using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

internal static class GeneratedOpenGenericRegistrationGrammarProof
{
    internal static void Run()
    {
        VerifyGenericListRegistration();
        VerifyUnaryGenericRegistration();
        VerifyMultiArgumentGenericRegistration();
        VerifyStandardConstraints();
    }

    private static void VerifyGenericListRegistration()
    {
        var layouts = new ComponentLayoutRegistry();
        var register = new RegisterGenericListComponent(layouts);
        GenericListComponents.ForEachData(ref register);

        ComponentId componentId = layouts.GetPrimary<GenericListComponent>();
        ComponentId historyId = layouts.Register(
            typeof(GenericListHistory<>),
            componentId,
            new SchemaId(921_001));
        using var world = new World(layouts);
        Entity entity = world.Create(componentId, historyId);
        world.GetRef<GenericListComponent>(entity, componentId).Value = 42;
        Query query = world.WhereAll(componentId, historyId);

        world.ForEach(in query, componentId, typeof(CopyGenericListHistory<>)).Invoke();

        Require(world.Get<GenericListHistory<GenericListComponent>>(entity, historyId).Value.Value == 42);
    }

    private static void VerifyUnaryGenericRegistration()
    {
        using var world = new World();
        ComponentId valueId = world.Layouts.Register<int>(new SchemaId(921_002));
        ComponentId historyId = world.Layouts.Register(typeof(UnaryHistory<>), valueId, new SchemaId(921_003));
        Entity entity = world.Create(valueId, historyId);
        Query query = world.WhereAll(valueId, historyId);

        world.ForEach(in query, valueId, typeof(UnaryHistoryWriter<>)).Invoke();

        Require(world.Get<UnaryHistory<int>>(entity, historyId).Value == 1);
    }

    private static void VerifyMultiArgumentGenericRegistration()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId firstId = layouts.Register<Cmp1>(new SchemaId(921_012));
        ComponentId secondId = layouts.Register<Cmp2>(new SchemaId(921_013));

        ComponentId positionalId = layouts.Register(
            typeof(GenericPair<,>),
            firstId,
            secondId,
            new SchemaId(921_014));
        ReadOnlySpan<ComponentId> arguments = stackalloc ComponentId[] { firstId, secondId };
        ComponentId spanId = layouts.Register(typeof(GenericPair<,>), arguments, new SchemaId(921_014));

        Require(spanId == positionalId);
        Require(layouts.GetComponentType(positionalId) == typeof(GenericPair<Cmp1, Cmp2>));
        Require(layouts.GetComponentType(spanId) == typeof(GenericPair<Cmp1, Cmp2>));
    }

    private static void VerifyStandardConstraints()
    {
        using var world = new World();
        ComponentId structId = world.Layouts.Register<Cmp1>(new SchemaId(921_004));
        ComponentId intId = world.Layouts.Register<int>(new SchemaId(921_005));
        ComponentId classId = world.Layouts.Register<ConstructibleGenericComponent>(new SchemaId(921_006));
        ComponentId structBoxId = world.Layouts.Register(typeof(StructBox<>), structId, new SchemaId(921_007));
        ComponentId unmanagedBoxId = world.Layouts.Register(typeof(UnmanagedBox<>), intId, new SchemaId(921_008));
        ComponentId classBoxId = world.Layouts.Register(typeof(ClassBox<>), classId, new SchemaId(921_009));
        ComponentId newBoxId = world.Layouts.Register(typeof(NewBox<>), classId, new SchemaId(921_010));

        CreateEntity(world, structBoxId);
        CreateEntity(world, unmanagedBoxId);
        CreateEntity(world, classBoxId);
        CreateEntity(world, newBoxId);
        CreateEntity(world, classId);

        var context = new GenericConstraintContext();
        Query structQuery = world.WhereAll(structBoxId);
        world.ForEach(in structQuery, ref context, structBoxId, typeof(StructConstraintFunctor<>)).Invoke(ref context);
        Require(context.Count == 1);

        context.Count = 0;
        Query unmanagedQuery = world.WhereAll(unmanagedBoxId);
        world.ForEach(in unmanagedQuery, ref context, unmanagedBoxId, typeof(UnmanagedConstraintFunctor<>)).Invoke(ref context);
        Require(context.Count == 1);

        context.Count = 0;
        Query classBoxQuery = world.WhereAll(classBoxId);
        world.ForEach(in classBoxQuery, ref context, classBoxId, typeof(ClassConstraintFunctor<>)).Invoke(ref context);
        Require(context.Count == 1);

        context.Count = 0;
        Query classQuery = world.WhereAll(classId);
        world.ForEach(in classQuery, ref context, classId, typeof(NewConstraintFunctor<>)).Invoke(ref context);
        Require(context.Count == 1);

        context.Count = 0;
        world.ForEach(in classQuery, ref context, classId, typeof(ClassNewConstraintFunctor<>)).Invoke(ref context);
        Require(context.Count == 1);

        context.Count = 0;
        Query newBoxQuery = world.WhereAll(newBoxId);
        world.ForEach(in newBoxQuery, ref context, newBoxId, typeof(StructConstraintFunctor<>)).Invoke(ref context);
        Require(context.Count == 1);

        try
        {
            world.ForEach(in classQuery, ref context, classId, typeof(StructConstraintFunctor<>)).Invoke(ref context);
            throw new InvalidOperationException("An incompatible open-generic constraint was accepted.");
        }
        catch (ArgumentException)
        {
        }
    }

    private static void CreateEntity(World world, ComponentId componentId)
    {
        Span<Entity> output = stackalloc Entity[1];
        Require(world.Create(stackalloc ComponentId[] { componentId }, output) == 1);
    }

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Generated open-generic registration grammar proof failed.");
        }
    }

}

internal struct GenericPair<TFirst, TSecond>
{
    internal TFirst First;
    internal TSecond Second;
}

internal interface IGenericListAction
{
    void Register<T>() where T : struct;
}

internal static class GenericListComponents
{
    internal static void ForEachData<TAction>(ref TAction action)
        where TAction : struct, IGenericListAction
        => action.Register<GenericListComponent>();
}

internal struct RegisterGenericListComponent(ComponentLayoutRegistry layouts) : IGenericListAction
{
    public void Register<T>() where T : struct
        => _ = layouts.Register<T>(new SchemaId(921_011));
}

internal struct GenericListComponent
{
    public int Value;
}

internal struct GenericListHistory<T>
{
    public T Value;
}

internal struct CopyGenericListHistory<T> : IForEach
{
    public void Invoke(ref GenericListHistory<T> history, in T component)
        => history.Value = component;
}

internal struct UnaryHistory<T>
{
    public int Value;
}

internal struct UnaryHistoryWriter<T> : IForEach
{
    public void Invoke(ref UnaryHistory<T> history, ref T component)
        => history.Value++;
}

internal struct StructBox<T> where T : struct
{
    public T Value;
}

internal struct UnmanagedBox<T> where T : unmanaged
{
    public T Value;
}

internal sealed class ClassBox<T> where T : class
{
    public T? Value;
}

internal struct NewBox<T> where T : new()
{
    public T Value;
}

internal sealed class ConstructibleGenericComponent
{
    public ConstructibleGenericComponent() { }
}

public struct GenericConstraintContext
{
    public int Count;
}

public struct StructConstraintFunctor<T> : IForEachContext<GenericConstraintContext> where T : struct
{
    public void Invoke(ref GenericConstraintContext context, in T component) => context.Count++;
}

public struct UnmanagedConstraintFunctor<T> : IForEachContext<GenericConstraintContext> where T : unmanaged
{
    public void Invoke(ref GenericConstraintContext context, in T component) => context.Count++;
}

public struct ClassConstraintFunctor<T> : IForEachContext<GenericConstraintContext> where T : class
{
    public void Invoke(ref GenericConstraintContext context, in T component) => context.Count++;
}

public struct NewConstraintFunctor<T> : IForEachContext<GenericConstraintContext> where T : new()
{
    public void Invoke(ref GenericConstraintContext context, in T component) => context.Count++;
}

public struct ClassNewConstraintFunctor<T> : IForEachContext<GenericConstraintContext> where T : class, new()
{
    public void Invoke(ref GenericConstraintContext context, in T component) => context.Count++;
}
