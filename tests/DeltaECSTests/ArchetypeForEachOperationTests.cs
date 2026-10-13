namespace Delta.ECS.Tests;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Delta.ECS;
using NUnit.Framework;

internal struct ComponentChangePosition
{
    internal int Value;
}

internal struct ComponentChangeVelocity
{
    internal int Value;
}

[TestFixture]
internal sealed class ArchetypeForEachOperationTests
{
    [Test]
    public void ProcessRunsMatchingQueriesDuringOneReusableTraversal()
    {
        var layouts = new ComponentRegistry();
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
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_304));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(60_305));
        using var world = new World(layouts);
        Span<Entity> entities = stackalloc Entity[2];
        world.Create([positionId, velocityId], entities);

        Query positions = world.WhereAll(positionId);
        var operation = world.ForEachArchetype(new IterationState())
            .ProcessRef<Position, SetPosition>(in positions, positionId, new SetPosition(17))
            .ProcessRef<Position, SetPosition>(in positions, positionId, new SetPosition(17));

        operation.Invoke();

        Assert.That(world.Get<Position>(entities[0], positionId).Value, Is.EqualTo(17));
        Assert.That(world.Get<Position>(entities[1], positionId).Value, Is.EqualTo(17));
    }

    [Test]
    public void ReadOnlyComponentFormsPreserveValues()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_313));
        using var world = new World(layouts);
        Span<Entity> entities = stackalloc Entity[2];
        world.Create([positionId], entities);
        world.GetRef<Position>(entities[0], positionId).Value = 3;
        world.GetRef<Position>(entities[1], positionId).Value = 7;

        Query positions = world.WhereAll(positionId);
        var state = new ReadComponentModes();
        var operation = world.ForEachArchetype(state)
            .ProcessIn<Position, SumIn>(in positions, positionId, new SumIn())
            .ProcessReadRef<Position, SumRefReadonly>(in positions, positionId, new SumRefReadonly())
            .Process<Position, SumValue>(in positions, positionId, new SumValue());

        operation.Invoke(ref state);

        Assert.That(state.InTotal, Is.EqualTo(10));
        Assert.That(state.RefReadonlyTotal, Is.EqualTo(10));
        Assert.That(state.ValueTotal, Is.EqualTo(10));
        Assert.That(world.Get<Position>(entities[0], positionId).Value, Is.EqualTo(3));
        Assert.That(world.Get<Position>(entities[1], positionId).Value, Is.EqualTo(7));
    }

    [Test]
    public void ForEachChangeReportsInitialAddsModificationsRemovalsAndDestroyedEntities()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<ComponentChangePosition>(new SchemaId(60_317));
        using var world = new World(layouts);
        Span<Entity> entities = stackalloc Entity[2];
        world.Create([positionId], entities);
        var changes = new List<(Entity Entity, ChangeKind Kind, bool HasComponent, int Value)>();
        var operation = world.ForEachChange<ComponentChangePosition>(positionId,
            (Entity entity, ChangeKind kind, ref ComponentChangePosition position) =>
                changes.Add((entity, kind, !Unsafe.IsNullRef(ref position), Unsafe.IsNullRef(ref position) ? 0 : position.Value)));

        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[]
        {
            (entities[0], ChangeKind.Added, true, 0),
            (entities[1], ChangeKind.Added, true, 0)
        }));

        changes.Clear();
        operation.Invoke();
        Assert.That(changes, Is.Empty);

        ref ComponentChangePosition unchanged = ref world.GetRef<ComponentChangePosition>(entities[0], positionId);
        _ = unchanged.Value;
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entities[0], ChangeKind.Modified, true, 0) }));

        changes.Clear();
        world.GetRef<ComponentChangePosition>(entities[1], positionId).Value = 12;
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entities[1], ChangeKind.Modified, true, 12) }));

        changes.Clear();
        world.Remove(entities[0], positionId);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entities[0], ChangeKind.Removed, false, 0) }));
        Assert.That(world.IsAlive(entities[0]), Is.True);

        changes.Clear();
        world.Add(entities[0], positionId);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entities[0], ChangeKind.Added, true, 0) }));

        changes.Clear();
        world.Destroy(entities[1]);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entities[1], ChangeKind.Removed, false, 0) }));
        Assert.That(world.IsAlive(changes[0].Entity), Is.False);
    }

    [Test]
    public void ForEachChangeDoesNotTreatReusedEntityIndexAsModifiedOldEntity()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<ComponentChangePosition>(new SchemaId(60_322));
        using var world = new World(layouts);
        Entity original = world.Create([positionId]);
        var changes = new List<(Entity Entity, ChangeKind Kind)>();
        var operation = world.ForEachChange<ComponentChangePosition>(positionId,
            (Entity entity, ChangeKind kind, ref ComponentChangePosition component) => changes.Add((entity, kind)));

        operation.Invoke();
        changes.Clear();
        world.GetRef<ComponentChangePosition>(original, positionId).Value++;
        world.Destroy(original);
        Entity replacement = world.Create([positionId]);

        Assert.That(replacement.Index, Is.EqualTo(original.Index));
        Assert.That(replacement.Generation, Is.Not.EqualTo(original.Generation));

        operation.Invoke();

        Assert.That(changes, Is.EqualTo(new[]
        {
            (replacement, ChangeKind.Added),
            (original, ChangeKind.Removed)
        }));
    }

    [Test]
    public void ForEachChangeFoldsStructuralAndWriteEventsByEntityAndComponent()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<ComponentChangePosition>(new SchemaId(60_323));
        ComponentId velocityId = layouts.Register<ComponentChangeVelocity>(new SchemaId(60_325));
        using var world = new World(layouts);
        Entity existing = world.Create([positionId]);
        Entity initiallyAbsent = world.Create([velocityId]);
        var changes = new List<(Entity Entity, ChangeKind Kind)>();
        var operation = world.ForEachChange<ComponentChangePosition>(positionId,
            (Entity entity, ChangeKind kind, ref ComponentChangePosition _) => changes.Add((entity, kind)));

        operation.Invoke();
        changes.Clear();

        world.Remove(existing, positionId);
        world.Add(existing, positionId);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (existing, ChangeKind.Modified) }));

        changes.Clear();
        world.Add(initiallyAbsent, positionId);
        world.GetRef<ComponentChangePosition>(initiallyAbsent, positionId).Value++;
        world.Remove(initiallyAbsent, positionId);
        operation.Invoke();
        Assert.That(changes, Is.Empty);

        world.Add(existing, positionId);
        operation.Invoke();
        changes.Clear();
        world.GetRef<ComponentChangePosition>(existing, positionId).Value++;
        world.Remove(existing, positionId);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (existing, ChangeKind.Removed) }));
    }

    [Test]
    public void ForEachChangeKeepsReusedEntityIndexGenerationsSeparate()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<ComponentChangePosition>(new SchemaId(60_329));
        using var world = new World(layouts);
        Entity oldEntity = world.Create([positionId]);
        var changes = new List<(Entity Entity, ChangeKind Kind)>();
        var operation = world.ForEachChange<ComponentChangePosition>(positionId,
            (Entity entity, ChangeKind kind, ref ComponentChangePosition _) => changes.Add((entity, kind)));

        operation.Invoke();
        changes.Clear();
        world.Destroy(oldEntity);
        Entity newEntity = world.Create([positionId]);
        Assert.That(newEntity.Index, Is.EqualTo(oldEntity.Index));
        Assert.That(newEntity.Generation, Is.Not.EqualTo(oldEntity.Generation));
        operation.Invoke();

        Assert.That(changes, Is.EquivalentTo(new[]
        {
            (oldEntity, ChangeKind.Removed),
            (newEntity, ChangeKind.Added)
        }));

        changes.Clear();
        world.Destroy(newEntity);
        Entity transientEntity = world.Create([positionId]);
        Assert.That(transientEntity.Index, Is.EqualTo(oldEntity.Index));
        world.Destroy(transientEntity);
        operation.Invoke();

        Assert.That(changes, Is.EqualTo(new[] { (newEntity, ChangeKind.Removed) }));
    }

    [Test]
    public void ForEachChangeTracksTagWritesAndFoldsTagMembershipChanges()
    {
        var layouts = new ComponentRegistry();
        ComponentId activeId = layouts.Register<Active>(new SchemaId(60_324));
        using var world = new World(layouts);
        Entity entity = world.Create<Active>();
        var changes = new List<(Entity Entity, ChangeKind Kind)>();
        var operation = world.ForEachChange<Active>(activeId,
            (Entity changedEntity, ChangeKind kind, ref Active _) => changes.Add((changedEntity, kind)));

        operation.Invoke();
        changes.Clear();
        world.GetRef<Active>(entity, activeId) = default;
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entity, ChangeKind.Modified) }));

        changes.Clear();
        Query active = world.WhereAll(activeId);
        world.ForEach<Active>(in active, activeId, static (ref Active _) => { }).Invoke();
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entity, ChangeKind.Modified) }));

        changes.Clear();
        world.Remove(entity, activeId);
        world.Add(entity, activeId);
        operation.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { (entity, ChangeKind.Modified) }));
    }

    [Test]
    public void ForEachChangeIntersectsGeneratedTagWritesWithQuerySelection()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<ComponentChangePosition>(new SchemaId(60_326));
        ComponentId activeId = layouts.Register<Active>(new SchemaId(60_327));
        ComponentId blockedId = layouts.Register<Paused>(new SchemaId(60_328));
        using var world = new World(layouts);
        Span<Entity> entities = stackalloc Entity[3];
        world.Create([positionId], entities);
        world.Add(entities[0], activeId);
        world.Add(entities[1], activeId);
        world.Add(entities[0], blockedId);
        var changes = new List<(Entity Entity, ChangeKind Kind)>();
        var operation = world.ForEachChange<Active>(activeId,
            (Entity entity, ChangeKind kind, ref Active _) => changes.Add((entity, kind)));

        operation.Invoke();
        changes.Clear();
        Query selected = world.WhereAll([positionId, activeId]).WhereNone(blockedId);
        world.ForEach<Active>(in selected, activeId, static (ref Active _) => { }).Invoke();
        operation.Invoke();

        Assert.That(changes, Is.EqualTo(new[] { (entities[1], ChangeKind.Modified) }));
    }

    [Test]
    public void ForEachChangeConsumesWritesMadeThroughItsCallbackReference()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<ComponentChangePosition>(new SchemaId(60_318));
        using var world = new World(layouts);
        Entity entity = world.Create([positionId]);
        int callbackCount = 0;
        var operation = world.ForEachChange<ComponentChangePosition>(positionId,
            (Entity entity, ChangeKind kind, ref ComponentChangePosition position) =>
            {
                callbackCount++;
                position.Value++;
            });

        operation.Invoke();
        operation.Invoke();

        Assert.That(callbackCount, Is.EqualTo(1));
        Assert.That(world.Get<ComponentChangePosition>(entity, positionId).Value, Is.EqualTo(1));
    }

    [Test]
    public void ForEachChangeObservesGeneratedWritableIteration()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<ComponentChangePosition>(new SchemaId(60_319));
        ComponentId velocityId = layouts.Register<ComponentChangeVelocity>(new SchemaId(60_320));
        using var world = new World(layouts);
        Entity entity = world.Create([positionId, velocityId]);
        Query query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { positionId, velocityId }));
        var positionChanges = new List<(Entity Entity, ChangeKind Kind, int Value)>();
        var velocityChanges = new List<(Entity Entity, ChangeKind Kind, int Value)>();
        var positionOperation = world.ForEachChange<ComponentChangePosition>(positionId,
            (Entity changedEntity, ChangeKind kind, ref ComponentChangePosition position) => positionChanges.Add((changedEntity, kind, position.Value)));
        var velocityOperation = world.ForEachChange<ComponentChangeVelocity>(velocityId,
            (Entity changedEntity, ChangeKind kind, ref ComponentChangeVelocity velocity) => velocityChanges.Add((changedEntity, kind, velocity.Value)));

        positionOperation.Invoke();
        velocityOperation.Invoke();
        positionChanges.Clear();
        velocityChanges.Clear();
        world.ForEach<ComponentChangePosition, ComponentChangeVelocity>(in query, positionId, velocityId,
            static (ref ComponentChangePosition position, ref ComponentChangeVelocity velocity) =>
            {
                position.Value++;
                velocity.Value++;
            }).Invoke();
        positionOperation.Invoke();
        velocityOperation.Invoke();

        Assert.That(positionChanges, Is.EqualTo(new[] { (entity, ChangeKind.Modified, 1) }));
        Assert.That(velocityChanges, Is.EqualTo(new[] { (entity, ChangeKind.Modified, 1) }));
    }

    [Test]
    public void ForEachChangeKeepsChunkWritesWhenEntityMovesBeforeObservation()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<ComponentChangePosition>(new SchemaId(60_330));
        ComponentId velocityId = layouts.Register<ComponentChangeVelocity>(new SchemaId(60_331));
        using var world = new World(layouts);
        Entity entity = world.Create([positionId]);
        Query query = world.WhereAll(positionId);
        var changes = new List<(Entity Entity, ChangeKind Kind)>();
        var operation = world.ForEachChange<ComponentChangePosition>(positionId,
            (Entity changedEntity, ChangeKind kind, ref ComponentChangePosition _) => changes.Add((changedEntity, kind)));

        operation.Invoke();
        changes.Clear();
        world.ForEach<ComponentChangePosition>(in query, positionId, static (ref ComponentChangePosition position) => position.Value++).Invoke();
        world.Add(entity, velocityId);
        operation.Invoke();

        Assert.That(changes, Is.EqualTo(new[] { (entity, ChangeKind.Modified) }));
    }

    [Test]
    public void ForEachChangeObservesEntityListAndArchetypeIteration()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<ComponentChangePosition>(new SchemaId(60_321));
        using var world = new World(layouts);
        Entity[] entities = new Entity[2];
        world.Create([positionId], entities);
        Query query = world.WhereAll(positionId);
        var changes = new List<(Entity Entity, ChangeKind Kind, int Value)>();
        var observer = world.ForEachChange<ComponentChangePosition>(positionId,
            (Entity entity, ChangeKind kind, ref ComponentChangePosition position) => changes.Add((entity, kind, position.Value)));

        observer.Invoke();
        changes.Clear();
        world.ForEach(entities.AsSpan(), in query, positionId,
            static (ref ComponentChangePosition position) => position.Value++).Invoke();
        observer.Invoke();

        Assert.That(changes, Is.EqualTo(new[]
        {
            (entities[0], ChangeKind.Modified, 1),
            (entities[1], ChangeKind.Modified, 1)
        }));

        changes.Clear();
        var archetypeOperation = world.ForEachArchetype(new IterationState())
            .ProcessRef<ComponentChangePosition, SetChangePosition>(in query, positionId, new SetChangePosition(9));
        archetypeOperation.Invoke();
        observer.Invoke();

        Assert.That(changes, Is.EqualTo(new[]
        {
            (entities[0], ChangeKind.Modified, 9),
            (entities[1], ChangeKind.Modified, 9)
        }));
    }

    [Test]
    public void ProcessComponentRequiresWhereAllMembership()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_306));
        using var world = new World(layouts);
        Query all = world.CreateQuery(QuerySpec.Empty);

        Assert.Throws<System.ArgumentException>(() => world
            .ForEachArchetype(new IterationState())
            .ProcessRef<Position, SetPosition>(in all, positionId, new SetPosition(1)));
    }

    [Test]
    public void ProcessComponentRespectsTagFilters()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_307));
        ComponentId activeId = layouts.Register<Active>(new SchemaId(60_308));
        using var world = new World(layouts);
        Span<Entity> entities = stackalloc Entity[2];
        world.Create([positionId], entities);
        world.Add(entities[1], activeId);

        Query activePositions = world.WhereAll(positionId).WhereAll(activeId);
        var operation = world.ForEachArchetype(new IterationState())
            .ProcessRef<Position, SetPosition>(in activePositions, positionId, new SetPosition(9));

        operation.Invoke();

        Assert.That(world.Get<Position>(entities[0], positionId).Value, Is.Zero);
        Assert.That(world.Get<Position>(entities[1], positionId).Value, Is.EqualTo(9));
    }

    [Test]
    public void ProcessTagPassesPlaceholderReferenceForMatchingEntities()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(60_310));
        ComponentId activeId = layouts.Register<Active>(new SchemaId(60_311));
        using var world = new World(layouts);
        Span<Entity> entities = stackalloc Entity[3];
        world.Create([positionId], entities);
        world.Add(entities[0], activeId);
        world.Add(entities[2], activeId);

        Query active = world.WhereAll(positionId).WhereAll(activeId);
        var state = new IterationState();
        var operation = world.ForEachArchetype(state)
            .ProcessRef<Active, VisitActiveTag>(in active, activeId, new VisitActiveTag());

        operation.Invoke(ref state);

        Assert.That(state.Active, Is.EqualTo(new[] { entities[0].Index, entities[2].Index }));
        Assert.That(world.Has<Active>(entities[0]), Is.True);
        Assert.That(world.Has<Active>(entities[1]), Is.False);
        Assert.That(world.Has<Active>(entities[2]), Is.True);
    }

    [Test]
    public void ProcessTagRequiresWhereAllMembership()
    {
        var layouts = new ComponentRegistry();
        ComponentId activeId = layouts.Register<Active>(new SchemaId(60_312));
        using var world = new World(layouts);
        Query all = world.CreateQuery(QuerySpec.Empty);

        Assert.Throws<System.ArgumentException>(() => world
            .ForEachArchetype(new IterationState())
            .ProcessRef<Active, VisitActiveTag>(in all, activeId, new VisitActiveTag()));
    }

    [Test]
    public void InvokeCopiesStructContextBackOnlyAfterSuccessfulExecution()
    {
        var layouts = new ComponentRegistry();
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

    private readonly struct SetChangePosition(int value) : IArchetypeForEachComponent<IterationState, ComponentChangePosition>
    {
        public void Invoke(ref IterationState context, EntityRef entity, ref ComponentChangePosition component) => component.Value = value;
    }

    private struct ReadComponentModes
    {
        internal int InTotal;
        internal int RefReadonlyTotal;
        internal int ValueTotal;
    }

    private readonly struct SumIn : IArchetypeForEachComponentIn<ReadComponentModes, Position>
    {
        public void Invoke(ref ReadComponentModes context, EntityRef entity, in Position component) => context.InTotal += component.Value;
    }

    private readonly struct SumRefReadonly : IArchetypeForEachComponentRefReadonly<ReadComponentModes, Position>
    {
        public void Invoke(ref ReadComponentModes context, EntityRef entity, ref readonly Position component) => context.RefReadonlyTotal += component.Value;
    }

    private readonly struct SumValue : IArchetypeForEachComponentValue<ReadComponentModes, Position>
    {
        public void Invoke(ref ReadComponentModes context, EntityRef entity, Position component)
        {
            context.ValueTotal += component.Value;
            component.Value++;
        }
    }

    private readonly struct VisitActiveTag : IArchetypeForEachComponent<IterationState, Active>
    {
        public void Invoke(ref IterationState context, EntityRef entity, ref Active component)
        {
            component = default;
            context.Active.Add(entity.Index);
        }
    }

    private struct Position
    {
        internal int Value;
    }
    private struct Velocity;
    private readonly struct Paused;
    private readonly struct Active;
}
