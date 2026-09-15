using Delta.ECS;
using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
public sealed class PipelineApiTests
{
    [Test]
    public void ZeroArityEntityCallbacksIterateWithoutComponentRows()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_004));
        using var world = new World(layouts);
        var entities = new Entity[3];
        world.Create([positionId], entities);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        int context = 0;
        ForEachAction action = static () => { };
        int entityVisits = 0;
        ForEachEntityAction entityAction = _ => entityVisits++;
        ForEachContextAction<int> contextAction = static (ref int _) => { };
        ForEachContextEntityAction<int> contextEntityAction = static (ref int value, Entity _) => value++;

        Assert.Multiple(() =>
        {
            Assert.That(() => world.ForEach(in query, action), Throws.InvalidOperationException);
            Assert.That(() => world.ForEach(in query, ref context, contextAction), Throws.InvalidOperationException);
        });
        world.ForEachEntity(in query, entityAction);
        world.ForEachEntity(in query, ref context, contextEntityAction);

        int entityListVisits = 0;
        ReadOnlySpan<Entity> selected = entities.AsSpan(1);
        int selectedCount = selected.Length;
        world.ForEachEntity(
            selected,
            in query,
            ref entityListVisits,
            static (ref int visits, Entity _) => visits++);
        world.ForEachEntity(
            selected,
            ref entityListVisits,
            static (ref int visits, Entity _) => visits++);

        Assert.Multiple(() =>
        {
            Assert.That(entityVisits, Is.EqualTo(entities.Length));
            Assert.That(context, Is.EqualTo(entities.Length));
            Assert.That(entityListVisits, Is.EqualTo(selectedCount * 2));
        });

        ForEachContextAction_In<int> readOnlyContextAction = static (in int _) => { };
        ForEachContextAction_Value<int> valueContextAction = static _ => { };
        int readOnlyContextVisits = 0;
        int valueContextVisits = 0;
        ForEachContextEntityAction_In<int> readOnlyEntityContextAction = (in int _, Entity __) => readOnlyContextVisits++;
        ForEachContextEntityAction_Value<int> valueEntityContextAction = (int _, Entity __) => valueContextVisits++;

        Assert.Multiple(() =>
        {
            Assert.That(() => world.ForEachParallel(in query, action, workerCount: 1), Throws.InvalidOperationException);
            Assert.That(() => world.ForEachParallel(in query, in context, readOnlyContextAction, workerCount: 1), Throws.InvalidOperationException);
            Assert.That(() => world.ForEachParallel(in query, context, valueContextAction, workerCount: 1), Throws.InvalidOperationException);
        });
        world.ForEachEntityParallel(in query, entityAction, workerCount: 1);
        world.ForEachEntityParallel(in query, in context, readOnlyEntityContextAction, workerCount: 1);
        world.ForEachEntityParallel(in query, context, valueEntityContextAction, workerCount: 1);

        Assert.Multiple(() =>
        {
            Assert.That(entityVisits, Is.EqualTo(entities.Length * 2));
            Assert.That(readOnlyContextVisits, Is.EqualTo(entities.Length));
            Assert.That(valueContextVisits, Is.EqualTo(entities.Length));
        });
    }

    [Test]
    public void InferredForEachUsesPreparedWriteRoutes()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_005));
        ComponentId velocityId = layouts.Register<PipelineVelocity>(new SchemaId(70_006));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, velocityId);
        Assert.That(world.Set(entity, positionId, new PipelinePosition { Value = 1 }), Is.True);
        Assert.That(world.Set(entity, velocityId, new PipelineVelocity { Value = 2 }), Is.True);

        var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
        world.ForEach(in query, static (ref PipelinePosition position, in PipelineVelocity velocity) =>
            position.Value += velocity.Value);

        world.ForEach(in query, static (ref PipelinePosition position, in PipelineVelocity velocity) =>
            position.Value += velocity.Value);

        Assert.That(world.Get<PipelinePosition>(entity, positionId).Value, Is.EqualTo(5));
    }

    [Test]
    public void EntityListForEachFiltersByQueryAndUsesTheRequestedSlots()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_020));
        ComponentId velocityId = layouts.Register<PipelineVelocity>(new SchemaId(70_021));
        using var world = new World(layouts);
        var entities = new Entity[3];
        world.Create(stackalloc[] { positionId, velocityId }, entities.Length, entities);
        Entity withoutVelocity = world.Create(positionId);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));

        world.ForEachEntity(
            entities.AsSpan(1),
            in query,
            static (Entity entity, ref PipelinePosition position, in PipelineVelocity velocity) =>
            {
                position.Value = entity.Index + velocity.Value;
            });
        world.ForEach(
            new[] { entities[0], withoutVelocity },
            in query,
            static (ref PipelinePosition position, in PipelineVelocity velocity) =>
            {
                position.Value += velocity.Value;
            });

        Assert.That(world.Get<PipelinePosition>(entities[0], positionId).Value, Is.EqualTo(0));
        Assert.That(world.Get<PipelinePosition>(entities[1], positionId).Value, Is.EqualTo(entities[1].Index));
        Assert.That(world.Get<PipelinePosition>(entities[2], positionId).Value, Is.EqualTo(entities[2].Index));
        Assert.That(world.Get<PipelinePosition>(withoutVelocity, positionId).Value, Is.EqualTo(0));
    }

    [Test]
    public void EntityListForEachCanBuildItsQueryFromTheSelector()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_023));
        using var world = new World(layouts);
        Entity[] entities = new Entity[2];
        world.Create(stackalloc[] { positionId }, entities.Length, entities);

        world.ForEach(
            entities,
            positionId,
            static (ref PipelinePosition position) => position.Value++);

        Assert.That(world.Get<PipelinePosition>(entities[0], positionId).Value, Is.EqualTo(1));
        Assert.That(world.Get<PipelinePosition>(entities[1], positionId).Value, Is.EqualTo(1));
    }

    [Test]
    public void EntityListParallelForEachUsesTheGeneratedParallelExecutor()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_026));
        using var world = new World(layouts);
        var entities = new Entity[8];
        world.Create(stackalloc[] { positionId }, entities.Length, entities);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));

        world.ForEachEntityParallel(
            entities,
            in query,
            static (Entity entity, ref PipelinePosition position) => position.Value = entity.Index + 1,
            workerCount: 2);

        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.Get<PipelinePosition>(entities[index], positionId).Value, Is.EqualTo(entities[index].Index + 1));
        }
    }

    [Test]
    public void WhereInterceptionPreservesPredicateContext()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_022));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        world.Set(entity, positionId, new PipelinePosition { Value = 2 });
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var context = new WhereContext { Minimum = 3 };

        world.Where(
            in query,
            ref context,
            static (ref WhereContext state, in PipelinePosition position) => position.Value < state.Minimum)
            .ForEach(static (ref PipelinePosition position) => position.Value++);

        Assert.That(world.Get<PipelinePosition>(entity, positionId).Value, Is.EqualTo(3));
    }

    [Test]
    public void WhereStructuralInterceptionPreservesPredicateContext()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_024));
        layouts.Register<PipelineMarker>(new SchemaId(70_025));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        world.Set(entity, positionId, new PipelinePosition { Value = 2 });
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var context = new WhereContext { Minimum = 3 };

        world.Where(
            in query,
            ref context,
            static (ref WhereContext state, in PipelinePosition position) => position.Value < state.Minimum)
            .Add<PipelineMarker>();

        Assert.That(world.Has<PipelineMarker>(entity), Is.True);
    }

    [Test]
    public void StaticLambdaDelegatePathInvokesOnceAndPreservesRefWriteSemantics()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_008));
        ComponentId velocityId = layouts.Register<PipelineVelocity>(new SchemaId(70_009));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, velocityId);
        Assert.That(world.Set(entity, positionId, new PipelinePosition { Value = 1 }), Is.True);
        Assert.That(world.Set(entity, velocityId, new PipelineVelocity { Value = 2 }), Is.True);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));

        int calls = 0;
        world.ForEach(in query, static (ref PipelinePosition position, in PipelineVelocity velocity) =>
        {
            position.Value += velocity.Value;
        });
        world.ForEach(in query, (ref PipelinePosition position, in PipelineVelocity velocity) =>
        {
            calls++;
            position.Value += velocity.Value;
        });

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(world.Get<PipelinePosition>(entity, positionId).Value, Is.EqualTo(5));
    }

    [Test]
    public void PrecreatedDelegateFallbackStillExecutesOnce()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_010));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        Assert.That(world.Set(entity, positionId, new PipelinePosition { Value = 3 }), Is.True);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        int calls = 0;
        world.ForEach(in query, static (ref PipelinePosition _) => { });
        ForEachAction<PipelinePosition> action = (ref PipelinePosition position) =>
        {
            calls++;
            position.Value++;
        };

        world.ForEach(in query, action);

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(world.Get<PipelinePosition>(entity, positionId).Value, Is.EqualTo(4));
    }

    [Test]
    public void MethodGroupDelegateFallbackStillExecutesOnce()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_011));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        Assert.That(world.Set(entity, positionId, new PipelinePosition { Value = 3 }), Is.True);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        s_methodGroupCalls = 0;

        world.ForEach(in query, ApplyMethodGroup);

        Assert.That(s_methodGroupCalls, Is.EqualTo(1));
        Assert.That(world.Get<PipelinePosition>(entity, positionId).Value, Is.EqualTo(4));
    }

    internal struct PipelinePosition
    {
        public int Value;
    }

    internal struct PipelineVelocity
    {
        public int Value;
    }

    internal struct PipelineMarker
    {
    }

    internal struct WhereContext
    {
        public int Minimum;
    }

    private static int s_methodGroupCalls;

    private static void ApplyMethodGroup(ref PipelinePosition position)
    {
        s_methodGroupCalls++;
        position.Value++;
    }
}
