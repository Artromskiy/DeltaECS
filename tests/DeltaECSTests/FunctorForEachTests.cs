namespace Delta.ECS.Tests;

using Delta.ECS;
using NUnit.Framework;

[TestFixture]
public sealed class FunctorForEachTests
{
    [Test]
    public void ZeroArityFunctorUsesTheGeneratedExecutionPath()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_081));
        using var world = new World(layouts);
        world.Create(new[] { positionId });
        var query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var functor = new ZeroArityFunctor();

        world.ForEach(in query, ref functor);

        Assert.That(functor.Count, Is.EqualTo(1));
    }

    internal struct ZeroArityFunctor : IForEach
    {
        public int Count;

        public void Invoke() => Count++;
    }
}
