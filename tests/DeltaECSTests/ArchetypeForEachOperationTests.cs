namespace Delta.ECS.Tests;

using System.Collections.Generic;
using Delta.ECS;
using NUnit.Framework;

[TestFixture]
internal sealed class ArchetypeForEachOperationTests
{
    [Test]
    public void ProcessRunsMatchingQueriesDuringOneReusableTraversal()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_301));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_302));
        ComponentId pausedId = layouts.Register<Paused>(new SchemaId(60_303));
        using var world = new World(layouts);
        Span<Entity> entities = stackalloc Entity[3];
        world.Create([positionId], entities);
        world.Add(entities[1], pausedId);

        Query allPositions = world.WhereAll(positionId);
        Query activePositions = allPositions.WhereNone(pausedId);
        var state = new IterationState();
        var operation = world
            .ForEachArchetype(state)
            .Process(in allPositions, new CollectEntities(false))
            .Process(in activePositions, new CollectEntities(true));

        operation.Invoke(ref state);

        Assert.That(state.All, Is.EqualTo(new[] { entities[0].Index, entities[1].Index, entities[2].Index }));
        Assert.That(state.Active, Is.EqualTo(new[] { entities[0].Index, entities[2].Index }));

        state.All.Clear();
        state.Active.Clear();
        world.Add(entities[0], velocityId);
        operation.Invoke(ref state);

        Assert.That(state.All, Is.EqualTo(new[] { entities[0].Index, entities[1].Index, entities[2].Index }));
        Assert.That(state.Active, Is.EqualTo(new[] { entities[0].Index, entities[2].Index }));
    }

    [Test]
    public void ProcessBindsRequiredComponentRowAndPassesWritableReference()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_304));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_305));
        using var world = new World(layouts);
        Span<Entity> entities = stackalloc Entity[2];
        world.Create([positionId, velocityId], entities);
        world.TryGetComponentStamp(entities[0], positionId, out Stamp stampBefore);
        world.TryGetComponentStamp(entities[1], positionId, out Stamp secondStampBefore);

        Query positions = world.WhereAll(positionId);
        var operation = world.ForEachArchetype(new IterationState())
            .Process<Position, SetPosition>(in positions, positionId, new SetPosition(17))
            .Process<Position, SetPosition>(in positions, positionId, new SetPosition(17));

        operation.Invoke();

        Assert.That(world.Get<Position>(entities[0], positionId).Value, Is.EqualTo(17));
        Assert.That(world.Get<Position>(entities[1], positionId).Value, Is.EqualTo(17));
        world.TryGetComponentStamp(entities[0], positionId, out Stamp stampAfter);
        world.TryGetComponentStamp(entities[1], positionId, out Stamp secondStampAfter);
        Assert.That(stampAfter.Value, Is.EqualTo(stampBefore.Value + 1));
        Assert.That(secondStampAfter.Value, Is.EqualTo(secondStampBefore.Value + 1));
    }

    [Test]
    public void ProcessComponentRequiresWhereAllMembership()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_306));
        using var world = new World(layouts);
        Query all = world.CreateQuery(QuerySpec.Empty);

        Assert.Throws<System.ArgumentException>(() => world
            .ForEachArchetype(new IterationState())
            .Process<Position, SetPosition>(in all, positionId, new SetPosition(1)));
    }

    [Test]
    public void ProcessComponentRespectsTagFilters()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_307));
        ComponentId activeId = layouts.Register<Active>(new SchemaId(60_308));
        using var world = new World(layouts);
        Span<Entity> entities = stackalloc Entity[2];
        world.Create([positionId], entities);
        world.Add(entities[1], activeId);

        Query activePositions = world.WhereAll(positionId).WhereAll(activeId);
        var operation = world.ForEachArchetype(new IterationState())
            .Process<Position, SetPosition>(in activePositions, positionId, new SetPosition(9));

        operation.Invoke();

        Assert.That(world.Get<Position>(entities[0], positionId).Value, Is.Zero);
        Assert.That(world.Get<Position>(entities[1], positionId).Value, Is.EqualTo(9));
    }

    [Test]
    public void InvokeCopiesStructContextBackOnlyAfterSuccessfulExecution()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_309));
        using var world = new World(layouts);
        Span<Entity> entities = stackalloc Entity[2];
        world.Create([positionId], entities);
        Query positions = world.WhereAll(positionId);
        var context = new CounterContext(4);
        var operation = world.ForEachArchetype(context)
            .Process(in positions, new IncrementContext());

        operation.Invoke(ref context);

        Assert.That(context.Value, Is.EqualTo(6));

        context.Value = 10;
        var throwingOperation = world.ForEachArchetype(context)
            .Process(in positions, new ThrowAfterIncrement());

        Assert.Throws<System.InvalidOperationException>(() => throwingOperation.Invoke(ref context));

        Assert.That(context.Value, Is.EqualTo(10));
    }

    private sealed class IterationState
    {
        internal readonly List<int> All = new();
        internal readonly List<int> Active = new();
    }

    private struct CounterContext(int value)
    {
        internal int Value = value;
    }

    private readonly struct IncrementContext : IArchetypeForEachEntity<CounterContext>
    {
        public void Invoke(ref CounterContext context, EntityRef entity) => context.Value++;
    }

    private readonly struct ThrowAfterIncrement : IArchetypeForEachEntity<CounterContext>
    {
        public void Invoke(ref CounterContext context, EntityRef entity)
        {
            context.Value++;
            throw new System.InvalidOperationException();
        }
    }

    private readonly struct CollectEntities : IArchetypeForEachEntity<IterationState>
    {
        private readonly bool _active;

        internal CollectEntities(bool active) => _active = active;

        public void Invoke(ref IterationState context, EntityRef entity)
            => (_active ? context.Active : context.All).Add(entity.Index);
    }

    private readonly struct SetPosition(int value) : IArchetypeForEachComponent<IterationState, Position>
    {
        public void Invoke(ref IterationState context, EntityRef entity, ref Position component) => component.Value = value;
    }

    private struct Position
    {
        internal int Value;
    }
    private struct Velocity;
    private readonly struct Paused;
    private readonly struct Active;
}
