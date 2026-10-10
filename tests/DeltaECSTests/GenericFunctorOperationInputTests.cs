namespace Delta.ECS.Tests;

using System;
using NUnit.Framework;

public sealed class GenericFunctorOperationInputTests
{
    [Test]
    public void OperationCopiesSpanInputsIntoWorldOwnedMemory()
    {
        ProbeFunctor.CallCount = 0;
        using var world = new World();
        ComponentId componentId = world.Layouts.Register<ProbeComponent>(new SchemaId(1));
        Entity first = world.Create(componentId);
        Entity second = world.Create(componentId);
        GeneratedGenericBindingRegistry.RegisterFunctorDispatcher(
            typeof(OpenProbeFunctor<>),
            1,
            static _ => ProbeFunctor.Instance);

        Span<Entity> entities = stackalloc Entity[] { first, second };
        Span<ComponentId> arguments = stackalloc ComponentId[] { componentId };
        var operation = world.CreateGenericFunctorOperation(
            default,
            entities,
            false,
            GeneratedGenericFunctorMode.EntityList,
            typeof(OpenProbeFunctor<>),
            0,
            arguments);
        entities.Clear();
        arguments.Clear();

        operation.Invoke();
        operation.Invoke();

        Assert.That(ProbeFunctor.CallCount, Is.EqualTo(2));
        Assert.That(ProbeFunctor.FirstEntity, Is.EqualTo(first));
        Assert.That(ProbeFunctor.SecondEntity, Is.EqualTo(second));
        Assert.That(ProbeFunctor.ComponentArgument, Is.EqualTo(componentId));

        world.Dispose();
        Assert.Throws<ObjectDisposedException>(() => operation.Invoke());
    }

    private struct ProbeComponent
    {
    }

    private struct OpenProbeFunctor<T>
    {
    }

    private sealed class ProbeFunctor : IGeneratedGenericFunctor
    {
        internal static readonly ProbeFunctor Instance = new();
        internal static int CallCount;
        internal static Entity FirstEntity;
        internal static Entity SecondEntity;
        internal static ComponentId ComponentArgument;

        public Query CreateQuery(World world, ReadOnlySpan<ComponentId> genericArguments)
            => world.WhereAll(genericArguments);

        public void Execute(
            World world,
            in Query query,
            ReadOnlySpan<Entity> entities,
            bool hasQuery,
            GeneratedGenericFunctorMode mode,
            int workerCount,
            ReadOnlySpan<ComponentId> genericArguments)
        {
            CallCount++;
            FirstEntity = entities[0];
            SecondEntity = entities[1];
            ComponentArgument = genericArguments[0];
        }
    }
}
