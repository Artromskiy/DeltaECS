namespace Delta.ECS.Tests;

using Delta.ECS;
using NUnit.Framework;

[TestFixture]
internal sealed class EcsOperationTests
{
    [Test]
    public void InvokeRepeatsAction()
    {
        int count = 0;
        var operation = new EcsOperation(() => count++);

        operation.Invoke();
        operation.Invoke();

        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void InvokeUsesOwnedOrCallerSuppliedState()
    {
        int lastValue = 0;
        var operation = new EcsOperation<int>(3, (ref int value) =>
        {
            lastValue = value;
            value++;
        });
        int external = 8;

        operation.Invoke();
        operation.Invoke(ref external);
        operation.Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(external, Is.EqualTo(9));
            Assert.That(lastValue, Is.EqualTo(4));
        });
    }

    [Test]
    public void InvokeUsesOwnedOrCallerSuppliedContextAndFunctor()
    {
        var operation = new EcsOperation<int, int>(2, 5, static (ref int context, ref int functor) =>
        {
            context++;
            functor += context;
        });
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
}
