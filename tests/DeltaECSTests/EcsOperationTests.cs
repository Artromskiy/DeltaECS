namespace Delta.ECS.Tests;

using Delta.ECS;
using NUnit.Framework;

[TestFixture]
internal sealed class EcsOperationTests
{
    [Test]
    public void ConcreteOperationIsAValueTypeAndCanBeInvokedThroughGenericConstraint()
    {
        var count = new Counter();
        var operation = new EcsOperation<IncrementInvoker>(new IncrementInvoker(count));

        InvokeTwice(ref operation);

        Assert.That(count.Value, Is.EqualTo(2));
    }

    [Test]
    public void StoredOperationBoxesOnceAndRetainsItsState()
    {
        var count = new Counter();
        IOperation operation = new EcsOperation<IncrementInvoker>(new IncrementInvoker(count));

        operation.Invoke();
        operation.Invoke();

        Assert.That(count.Value, Is.EqualTo(2));
    }

    [Test]
    public void InvokeUsesOwnedOrCallerSuppliedState()
    {
        var probe = new StateProbe();
        var operation = new EcsOperation<int, StateInvoker>(3, new StateInvoker(probe));
        int external = 8;

        operation.Invoke();
        operation.Invoke(ref external);
        operation.Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(external, Is.EqualTo(9));
            Assert.That(probe.LastValue, Is.EqualTo(4));
        });
    }

    [Test]
    public void InvokeUsesOwnedOrCallerSuppliedContextAndFunctor()
    {
        var operation = new EcsOperation<int, int, ContextFunctorInvoker>(2, 5, default);
        int externalContext = 10;
        int externalFunctor = 1;

        operation.Invoke();
        operation.Invoke(ref externalFunctor);
        operation.InvokeWithContext(ref externalContext);
        operation.Invoke(ref externalContext, ref externalFunctor);

        Assert.Multiple(() =>
        {
            Assert.That(externalContext, Is.EqualTo(12));
            Assert.That(externalFunctor, Is.EqualTo(17));
        });
    }

    [Test]
    public void ResultIsProducedOnlyWhenInvoked()
    {
        int count = 0;
        var operation = new EcsResultOperation<int>(() => ++count);

        Assert.That(count, Is.Zero);
        Assert.That(operation.Invoke(), Is.EqualTo(1));
        Assert.That(operation.Invoke(), Is.EqualTo(2));
    }

    private static void InvokeTwice<TOperation>(ref TOperation operation)
        where TOperation : IOperation
    {
        operation.Invoke();
        operation.Invoke();
    }

    private sealed class Counter
    {
        internal int Value;
    }

    private readonly struct IncrementInvoker : IEcsOperationInvoker
    {
        private readonly Counter _counter;

        internal IncrementInvoker(Counter counter) => _counter = counter;

        public void Invoke() => _counter.Value++;
    }

    private sealed class StateProbe
    {
        internal int LastValue;
    }

    private readonly struct StateInvoker : IEcsOperationInvoker<int>
    {
        private readonly StateProbe _probe;

        internal StateInvoker(StateProbe probe) => _probe = probe;

        public void Invoke(ref int state)
        {
            _probe.LastValue = state;
            state++;
        }
    }

    private readonly struct ContextFunctorInvoker : IEcsOperationInvoker<int, int>
    {
        public void Invoke(ref int context, ref int functor)
        {
            context++;
            functor += context;
        }
    }
}
