namespace Delta.ECS.Tests;

using Delta.ECS;
using NUnit.Framework;

[TestFixture]
internal sealed class FunctorForEachTests
{
    private static int s_zeroArityWhereVisits;

    [Test]
    public void ZeroArityFunctorAnchorThrows()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_081));
        using var world = new World(layouts);
        world.Create(new[] { positionId });
        var query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var functor = new ZeroArityFunctor();

        Assert.That(() => world.ForEach(in query, functor).Invoke(), Throws.InvalidOperationException);

        Assert.That(functor.Count, Is.EqualTo(0));
    }

    [Test]
    public void DenseFunctorStateIsWrittenBackAfterTraversal()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_088));
        using var world = new World(layouts, initialEntityCapacity: 600);
        Entity[] entities = new Entity[600];
        world.Create([positionId], entities);

        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var functor = new CountPositionFunctor();
        world.ForEach(in query, ref functor).Invoke(ref functor);
        world.ForEach(entities.AsSpan(0, 2), in query, ref functor).Invoke(ref functor);

        Assert.That(functor.Count, Is.EqualTo(entities.Length + 2));
    }

    [Test]
    public void DeferredOperationIsReusableAndTracksNewQueryArchetypes()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_092));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_093));
        using var world = new World(layouts);
        Entity first = world.Create(positionId);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var operation = world.ForEach(in query, static (ref Position position) => position.X++);

        Assert.That(world.Get<Position>(first, positionId).X, Is.Zero);
        operation.Invoke();
        Entity second = world.Create(stackalloc ComponentId[] { positionId, velocityId });
        operation.Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(first, positionId).X, Is.EqualTo(2f));
            Assert.That(world.Get<Position>(second, positionId).X, Is.EqualTo(1f));
        });
    }

    [Test]
    public void DeferredFunctorOperationKeepsOwnedStateAndAcceptsCallerState()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_094));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var initialFunctor = new StatefulPositionFunctor();
        var operation = world.ForEach(in query, ref initialFunctor);

        operation.Invoke();
        operation.Invoke();
        var externalFunctor = new StatefulPositionFunctor();
        operation.Invoke(ref externalFunctor);
        operation.Invoke(ref externalFunctor);

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Position>(entity, positionId).X, Is.EqualTo(6f));
            Assert.That(initialFunctor.Count, Is.Zero);
            Assert.That(externalFunctor.Count, Is.EqualTo(2));
        });
    }

    [Test]
    public void DeferredOperationValidatesQueryOnlyWhenInvoked()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.Register<Position>(new SchemaId(60_095));
        using var world = new World(layouts);
        Query invalid = default;
        var operation = world.ForEach(in invalid, static (ref Position _) => { });

        Assert.Throws<ArgumentException>(() => operation.Invoke());
    }

    [Test]
    public void DenseFunctorStateIsNotWrittenBackWhenTraversalThrows()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId valueId = layouts.Register<FunctorProbeComponent>(new SchemaId(60_090));
        using var world = new World(layouts, initialEntityCapacity: 600);
        var entities = new Entity[600];
        world.Create([valueId], entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.GetRef<FunctorProbeComponent>(entities[index], valueId) = new FunctorProbeComponent { Index = index };
        }

        Query query = world.CreateQuery(QuerySpec.WhereAll(valueId));
        var functor = new ThrowingCountFunctor();

        Assert.That(() => world.ForEach(in query, ref functor).Invoke(ref functor), Throws.InvalidOperationException);
        Assert.That(functor.Count, Is.EqualTo(0));
    }

    [Test]
    public void ZeroArityEntityFunctorsVisitEveryEntity()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_086));
        using var world = new World(layouts);
        var entities = new Entity[3];
        world.Create([positionId], entities);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var functor = new ZeroArityEntityFunctor();
        var contextFunctor = new ZeroArityEntityContextFunctor();
        int context = 0;

        world.ForEachEntity(in query, ref functor).Invoke(ref functor);
        world.ForEachEntity(in query, ref context, ref contextFunctor).Invoke(ref context, ref contextFunctor);

        Assert.Multiple(() =>
        {
            Assert.That(functor.Count, Is.EqualTo(entities.Length));
            Assert.That(context, Is.EqualTo(entities.Length));
        });
    }

    [Test]
    public void WhereSupportsZeroArityEntityTerminals()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId healthId = layouts.Register<Health>(new SchemaId(60_087));
        ComponentId tagId = layouts.Register<WhereEntityTag>(new SchemaId(60_089));
        using var world = new World(layouts);
        Entity dead = world.Create([healthId]);
        Entity alive = world.Create([healthId]);
        world.GetRef<Health>(dead, healthId).Value = -1;
        world.GetRef<Health>(alive, healthId).Value = 10;
        world.Add(dead, tagId);
        Query query = world.CreateQuery(QuerySpec.WhereAll(healthId).WithAny(stackalloc[] { tagId }));
        var functor = new ZeroArityWhereEntityFunctor();
        s_zeroArityWhereVisits = 0;

        world.Where(in query, static (in Health health) => health.Value <= 0)
            .ForEachEntity(CountZeroArityWhereEntity).Invoke();
        world.Where(in query, static (in Health health) => health.Value <= 0)
            .ForEachEntity(ref functor).Invoke(ref functor);

        Assert.Multiple(() =>
        {
            Assert.That(s_zeroArityWhereVisits, Is.EqualTo(1));
            Assert.That(functor.Count, Is.EqualTo(1));
        });
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
            .ForEachEntity(ref actionState, ref action).Invoke(ref actionState, ref action);

        Assert.That(predicateState.Visited, Is.Zero);
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

        int destroyed = world.WhereEntity(in query, ref predicateState, ref predicate).Destroy().Invoke();

        Assert.That(destroyed, Is.EqualTo(1));
        Assert.That(predicateState.Visited, Is.Zero);
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
        world.Where(in query, ref predicate).Destroy().Invoke();
        world.Where(in query, static (in Health health) => health.Value <= 0).Destroy().Invoke();

        Assert.That(world.IsAlive(dead), Is.False);
        Assert.That(world.IsAlive(alive), Is.True);
    }

    [Test]
    public void WhereWritesFunctorStateBackAfterCompleteTraversal()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId healthId = layouts.Register<Health>(new SchemaId(60_089));
        using var world = new World(layouts, initialEntityCapacity: 600);
        Entity[] entities = new Entity[600];
        world.Create([healthId], entities);

        Query query = world.CreateQuery(QuerySpec.WhereAll(healthId));
        var predicate = new CountHealthPredicate();
        var action = new CountHealthAction();
        world.Where(in query, ref predicate).ForEach(ref action).Invoke(ref action);

        Assert.That(predicate.Count, Is.Zero);
        Assert.That(action.Count, Is.EqualTo(entities.Length));
    }

    [Test]
    public void WhereFunctorStateIsNotWrittenBackWhenTraversalThrows()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId healthId = layouts.Register<Health>(new SchemaId(60_091));
        using var world = new World(layouts, initialEntityCapacity: 600);
        var entities = new Entity[600];
        world.Create([healthId], entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.GetRef<Health>(entities[index], healthId) = new Health { Value = index };
        }

        Query query = world.CreateQuery(QuerySpec.WhereAll(healthId));
        var predicate = new CountHealthPredicate();
        var action = new ThrowingHealthAction();

        Assert.That(
            () => world.Where(in query, ref predicate).ForEach(ref action).Invoke(ref action),
            Throws.InvalidOperationException);

        Assert.Multiple(() =>
        {
            Assert.That(predicate.Count, Is.EqualTo(0));
            Assert.That(action.Count, Is.EqualTo(0));
        });
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
            .ForEach(ref action).Invoke(ref action);

        Assert.That(action.Count, Is.EqualTo(1));
        Assert.That(world.GetRef<Health>(entity, healthId).Value, Is.EqualTo(2));
    }

    internal struct ZeroArityFunctor : IForEach
    {
        public int Count;

        public void Invoke() => Count++;
    }

    internal struct ZeroArityEntityFunctor : IForEachEntity
    {
        public int Count;

        public void Invoke(EntityRef _) => Count++;
    }

    internal struct ZeroArityEntityContextFunctor : IForEachContextEntity<int>
    {
        public void Invoke(ref int context, EntityRef _) => context++;
    }

    internal struct CountPositionFunctor : IForEach
    {
        public int Count;

        public void Invoke(ref Position _) => Count++;
    }

    internal struct StatefulPositionFunctor : IForEach
    {
        public int Count;

        public void Invoke(ref Position position) => position.X += ++Count;
    }

    internal struct FunctorProbeComponent
    {
        public int Index;
    }

    internal struct ThrowingCountFunctor : IForEach
    {
        public int Count;

        public void Invoke(ref FunctorProbeComponent component)
        {
            Count++;
            if (component.Index == 512)
            {
                throw new InvalidOperationException();
            }
        }
    }

    internal struct ZeroArityWhereEntityFunctor : IForEachEntity
    {
        public int Count;

        public void Invoke(EntityRef entity)
        {
            Assert.That(entity.Get<Health>().Value, Is.EqualTo(-1));
            Count++;
        }
    }

    private static void CountZeroArityWhereEntity(EntityRef entity)
    {
        Assert.That(entity.Get<Health>().Value, Is.EqualTo(-1));
        s_zeroArityWhereVisits++;
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

    internal struct CountHealthPredicate : IWherePredicate
    {
        public int Count;

        public bool Invoke(in Health _)
        {
            Count++;
            return true;
        }
    }

    internal struct CountHealthAction : IForEach
    {
        public int Count;

        public void Invoke(ref Health _) => Count++;
    }

    internal struct ThrowingHealthAction : IForEach
    {
        public int Count;

        public void Invoke(ref Health health)
        {
            Count++;
            if (health.Value == 512)
            {
                throw new InvalidOperationException();
            }
        }
    }

    internal struct WhereActionState
    {
        public int Matched;
    }

    internal struct ResetHealthAction : IForEachContextEntity<WhereActionState>
    {
        public void Invoke(ref WhereActionState state, EntityRef entity, ref Health health)
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

    private readonly struct WhereEntityTag;
}
