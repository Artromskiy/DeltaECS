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

    [Test]
    public void WhereFunctorSupportsPredicateAndTerminalContext()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId healthId = layouts.Register<Health>(new SchemaId(60_082));
        using var world = new World(layouts);
        Entity dead = world.Create(new[] { healthId });
        Entity alive = world.Create(new[] { healthId });
        world.GetRef<Health>(dead, healthId).Value = -1;
        world.GetRef<Health>(alive, healthId).Value = 10;
        var query = world.CreateQuery(QuerySpec.WhereAll(healthId));
        var predicateState = new WherePredicateState();
        var predicate = new DeadHealthPredicate();
        var actionState = new WhereActionState();
        var action = new ResetHealthAction();

        world.WhereEntity(in query, ref predicateState, ref predicate)
            .ForEachEntity(ref actionState, ref action);

        Assert.That(predicateState.Visited, Is.EqualTo(2));
        Assert.That(actionState.Matched, Is.EqualTo(1));
        Assert.That(world.GetRef<Health>(dead, healthId).Value, Is.EqualTo(0));
        Assert.That(world.GetRef<Health>(alive, healthId).Value, Is.EqualTo(10));
    }

    [Test]
    public void WhereFunctorSupportsStructuralTerminal()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId healthId = layouts.Register<Health>(new SchemaId(60_083));
        using var world = new World(layouts);
        Entity dead = world.Create(new[] { healthId });
        Entity alive = world.Create(new[] { healthId });
        world.GetRef<Health>(dead, healthId).Value = -1;
        world.GetRef<Health>(alive, healthId).Value = 10;
        var query = world.CreateQuery(QuerySpec.WhereAll(healthId));
        var predicateState = new WherePredicateState();
        var predicate = new DeadHealthPredicate();

        int destroyed = world.WhereEntity(in query, ref predicateState, ref predicate).Destroy();

        Assert.That(destroyed, Is.EqualTo(1));
        Assert.That(predicateState.Visited, Is.EqualTo(2));
        Assert.That(world.AliveEntityCount, Is.EqualTo(1));
        Assert.That(world.IsAlive(dead), Is.False);
        Assert.That(world.IsAlive(alive), Is.True);
    }

    [Test]
    public void WhereSupportsPredicateWithoutEntity()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId healthId = layouts.Register<Health>(new SchemaId(60_085));
        using var world = new World(layouts);
        Entity dead = world.Create(new[] { healthId });
        Entity alive = world.Create(new[] { healthId });
        world.GetRef<Health>(dead, healthId).Value = -1;
        world.GetRef<Health>(alive, healthId).Value = 10;
        var query = world.CreateQuery(QuerySpec.WhereAll(healthId));

        var predicate = new DeadHealthPredicateWithoutEntity();
        world.Where(in query, ref predicate).Destroy();
        world.Where(in query, static (in Health health) => health.Value <= 0).Destroy();

        Assert.That(world.IsAlive(dead), Is.False);
        Assert.That(world.IsAlive(alive), Is.True);
    }

    [Test]
    public void WhereLambdaSupportsInterceptedFunctorTerminal()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId healthId = layouts.Register<Health>(new SchemaId(60_084));
        using var world = new World(layouts);
        Entity entity = world.Create(new[] { healthId });
        world.GetRef<Health>(entity, healthId).Value = 1;
        var query = world.CreateQuery(QuerySpec.WhereAll(healthId));
        var action = new IncrementHealthAction();

        world.WhereEntity(in query, static (Entity current, in Health health) => health.Value > 0)
            .ForEach(ref action);

        Assert.That(action.Count, Is.EqualTo(1));
        Assert.That(world.GetRef<Health>(entity, healthId).Value, Is.EqualTo(2));
    }

    internal struct ZeroArityFunctor : IForEach
    {
        public int Count;

        public void Invoke() => Count++;
    }

    internal struct WherePredicateState
    {
        public int Visited;
    }

    internal struct DeadHealthPredicate : IWherePredicate
    {
        public bool Invoke(ref WherePredicateState state, Entity entity, in Health health)
        {
            state.Visited++;
            return health.Value <= 0;
        }
    }

    internal struct DeadHealthPredicateWithoutEntity : IWherePredicate
    {
        public bool Invoke(in Health health) => health.Value <= 0;
    }

    internal struct WhereActionState
    {
        public int Matched;
    }

    internal struct ResetHealthAction : IForEachContextEntity<WhereActionState>
    {
        public void Invoke(ref WhereActionState state, Entity entity, ref Health health)
        {
            state.Matched++;
            health.Value = 0;
        }
    }

    internal struct IncrementHealthAction : IForEach
    {
        public int Count;

        public void Invoke(ref Health health)
        {
            Count++;
            health.Value++;
        }
    }
}
