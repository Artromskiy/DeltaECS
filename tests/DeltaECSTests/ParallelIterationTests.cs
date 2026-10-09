namespace Delta.ECS.Tests;

using System;
using System.Threading;
using NUnit.Framework;

internal struct ParallelState
{
    public float Delta;
}

internal struct ParallelIncrementFunctor : IForEach
{
    public int Count;

    public void Invoke(ref Position position, in Velocity velocity)
    {
        position.X += velocity.X;
        Count++;
    }
}

internal struct ParallelContextFunctor : IForEachContext<ParallelState>
{
    public void Invoke(in ParallelState state, ref Position position, in Velocity velocity) => position.X += state.Delta + velocity.X;
}

[TestFixture]
internal sealed class ParallelIterationTests
{
    private static readonly ForEachAction_WI<Position, Velocity> s_incrementAction = Increment;
    internal static int s_generatedCallbackThreadId;

    [Test]
    public void GeneratedForEachParallelSupportsReadOnlyAndValueState()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(70_090));
        using var world = new World(layouts, initialEntityCapacity: 2_048);
        var entities = new Entity[2_048];
        world.Create([positionId], entities);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var state = new ParallelState { Delta = 2 };
        Assert.That(world.TryGetComponentStamp(entities[0], positionId, out Stamp stampBefore), Is.True);

#pragma warning disable CS9198 // This test preserves the supported ref readonly lambda spelling for an in context parameter.
        world.ForEachParallel(
            in query,
            in state,
            static (ref readonly ParallelState value, ref Position position) => position.X += value.Delta,
            workerCount: 4).Invoke();
#pragma warning restore CS9198
        world.ForEachParallel(
            in query,
            in state,
            static (in ParallelState value, ref Position position) => position.X += value.Delta,
            workerCount: 4).Invoke();
        world.ForEachEntityParallel(
            in query,
            state,
            static (ParallelState value, EntityRef entity, ref Position position) =>
            {
                _ = entity;
                position.X += value.Delta;
            },
            workerCount: 4).Invoke();

        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.Get<Position>(entities[index], positionId).X, Is.EqualTo(6));
        }

        Assert.That(world.TryGetComponentStamp(entities[0], positionId, out Stamp stampAfter), Is.True);
        Assert.That(stampAfter, Is.EqualTo(new Stamp(stampBefore.Value + 3)));
    }

    [Test]
    public void ZeroArityEntityParallelCallbacksVisitEveryEntity()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(70_093));
        using var world = new World(layouts, initialEntityCapacity: 2_048);
        var entities = new Entity[2_048];
        world.Create([positionId], entities);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var state = new ParallelState();
        ForEachAction action = static () => { };
        int entityVisits = 0;
        int readOnlyContextVisits = 0;
        int valueContextVisits = 0;
        ForEachEntityAction entityAction = _ => Interlocked.Increment(ref entityVisits);
        ForEachContextActionIn<ParallelState> readOnlyAction = static (in ParallelState _) => { };
        ForEachContextActionValue<ParallelState> valueAction = static _ => { };
        ForEachContextEntityActionIn<ParallelState> readOnlyEntityAction = (in ParallelState _, EntityRef __) => Interlocked.Increment(ref readOnlyContextVisits);
        ForEachContextEntityActionValue<ParallelState> valueEntityAction = (ParallelState _, EntityRef __) => Interlocked.Increment(ref valueContextVisits);

        Assert.Multiple(() =>
        {
            Assert.That(() => world.ForEachParallel(in query, action, workerCount: 4).Invoke(), Throws.InvalidOperationException);
            Assert.That(() => world.ForEachParallel(in query, in state, readOnlyAction, workerCount: 4).Invoke(), Throws.InvalidOperationException);
            Assert.That(() => world.ForEachParallel(in query, state, valueAction, workerCount: 4).Invoke(), Throws.InvalidOperationException);
        });
        world.ForEachEntityParallel(in query, entityAction, workerCount: 4).Invoke();
        world.ForEachEntityParallel(in query, in state, readOnlyEntityAction, workerCount: 4).Invoke();
        world.ForEachEntityParallel(in query, state, valueEntityAction, workerCount: 4).Invoke();

        int[] entityListVisits = [0];
        ReadOnlySpan<Entity> selected = entities.AsSpan(0, entities.Length / 2);
        int selectedCount = selected.Length;
        world.ForEachEntityParallel(
            selected,
            in query,
            entityListVisits,
            static (int[] visits, EntityRef _) => Interlocked.Increment(ref visits[0]),
            workerCount: 4).Invoke();
        world.ForEachEntityParallel(
            selected,
            entityListVisits,
            static (int[] visits, EntityRef _) => Interlocked.Increment(ref visits[0]),
            workerCount: 4).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(entityVisits, Is.EqualTo(entities.Length));
            Assert.That(readOnlyContextVisits, Is.EqualTo(entities.Length));
            Assert.That(valueContextVisits, Is.EqualTo(entities.Length));
            Assert.That(entityListVisits[0], Is.EqualTo(selectedCount * 2));
        });
    }

    [Test]
    public void GeneratedForEachParallelSupportsExplicitFunctorsAndContext()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(70_091));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(70_092));
        using var world = new World(layouts, initialEntityCapacity: Chunk.Capacity * 4);
        var entities = new Entity[Chunk.Capacity * 4];
        world.Create([positionId, velocityId], entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.GetRef<Velocity>(entities[index], velocityId) = new Velocity { X = 1 };
        }

        Query query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { positionId, velocityId }));
        var singleWorkerAction = new ParallelIncrementFunctor();
        world.ForEachParallel(in query, ref singleWorkerAction, workerCount: 1).Invoke(ref singleWorkerAction);
        Assert.That(singleWorkerAction.Count, Is.EqualTo(entities.Length));

        var action = new ParallelIncrementFunctor();
        world.ForEachParallel(in query, ref action, workerCount: 4).Invoke(ref action);
        var state = new ParallelState { Delta = 2 };
        var contextual = new ParallelContextFunctor();
        world.ForEachParallel(in query, in state, ref contextual, workerCount: 4).Invoke(ref contextual);

        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.Get<Position>(entities[index], positionId).X, Is.EqualTo(5));
        }

        Assert.That(action.Count, Is.GreaterThan(0));
    }

    [Test]
    public void GeneratedForEachParallelProcessesEveryEntity()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register<Position>(new SchemaId(70_020));
        var velocityId = layouts.Register<Velocity>(new SchemaId(70_021));
        using var world = new World(layouts, initialEntityCapacity: 2_048);
        var entities = new Entity[2_048];
        world.Create(new[] { positionId, velocityId }, entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.GetRef<Position>(entities[index], positionId) = new Position { X = 1, Y = 2 };
            world.GetRef<Velocity>(entities[index], velocityId) = new Velocity { X = 3, Y = 4 };
        }

        var query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { positionId, velocityId }));
        world.ForEachParallel(
            in query,
            static (ref Position position, in Velocity velocity) =>
            {
                position.X += velocity.X;
                position.Y += velocity.Y;
            },
            workerCount: 4).Invoke();

        for (int index = 0; index < entities.Length; index++)
        {
            Position actual = world.Get<Position>(entities[index], positionId);
            Assert.That(actual.X, Is.EqualTo(4));
            Assert.That(actual.Y, Is.EqualTo(6));
        }
    }

    [Test]
    public void GeneratedForEachParallelUsesSingleThreadPathForSingleChunk()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(70_080));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(70_081));
        using var world = new World(layouts, initialEntityCapacity: 8);
        var entities = new Entity[2];
        world.Create([positionId, velocityId], entities);
        Query query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { positionId, velocityId }));
        s_generatedCallbackThreadId = 0;
        int callerThreadId = Environment.CurrentManagedThreadId;

        world.ForEachParallel(
            in query,
            static (ref Position position, in Velocity velocity) =>
            {
                Volatile.Write(ref s_generatedCallbackThreadId, Environment.CurrentManagedThreadId);
                position.X += velocity.X;
            },
            workerCount: 2).Invoke();

        Assert.That(Volatile.Read(ref s_generatedCallbackThreadId), Is.EqualTo(callerThreadId));

        int[] entityListThreadId = [0];
        world.ForEachEntityParallel(
            entities,
            in query,
            entityListThreadId,
            static (int[] threadId, EntityRef _) => Volatile.Write(ref threadId[0], Environment.CurrentManagedThreadId),
            workerCount: 2).Invoke();

        Assert.That(Volatile.Read(ref entityListThreadId[0]), Is.EqualTo(callerThreadId));
    }

    [Test]
    public void GeneratedForEachParallelRebuildsCachedRangesAfterTopologyChange()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register<Position>(new SchemaId(70_030));
        var velocityId = layouts.Register<Velocity>(new SchemaId(70_031));
        using var world = new World(layouts, initialEntityCapacity: 256);
        var firstBatch = new Entity[128];
        world.Create(new[] { positionId, velocityId }, firstBatch);
        var query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { positionId, velocityId }));
        var operation = world.ForEachParallel(in query, s_incrementAction, workerCount: 4);

        operation.Invoke();

        var secondBatch = new Entity[128];
        world.Create(new[] { positionId, velocityId }, secondBatch);
        operation.Invoke();

        for (int index = 0; index < firstBatch.Length; index++)
        {
            Assert.That(world.Get<Position>(firstBatch[index], positionId).X, Is.EqualTo(2));
            Assert.That(world.Get<Position>(firstBatch[index], positionId).Y, Is.EqualTo(2));
        }

        for (int index = 0; index < secondBatch.Length; index++)
        {
            Assert.That(world.Get<Position>(secondBatch[index], positionId).X, Is.EqualTo(1));
            Assert.That(world.Get<Position>(secondBatch[index], positionId).Y, Is.EqualTo(1));
        }
    }

    [Test]
    public void GeneratedForEachParallelIncludesChunksActivatedAfterQueryCreation()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(70_070));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(70_071));
        using var world = new World(layouts, initialEntityCapacity: 2_048);
        Query query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { positionId, velocityId }));
        var operation = world.ForEachParallel(in query, s_incrementAction, workerCount: 4);
        var entities = new Entity[2_048];
        world.Create([positionId, velocityId], entities);

        operation.Invoke();

        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.Get<Position>(entities[index], positionId).X, Is.EqualTo(1));
        }
    }

    [Test]
    public void GeneratedForEachParallelGrowsWorkerPoolWithoutLosingSignals()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register<Position>(new SchemaId(70_050));
        var velocityId = layouts.Register<Velocity>(new SchemaId(70_051));
        int entityCount = Chunk.Capacity * 4;
        using var world = new World(layouts, initialEntityCapacity: entityCount);
        var entities = new Entity[entityCount];
        world.Create(new[] { positionId, velocityId }, entities);
        var query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { positionId, velocityId }));
        var operation = world.ForEachParallel(in query, s_incrementAction, workerCount: 2);

        operation.Invoke();
        operation.Invoke();
        operation.Invoke();

        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.Get<Position>(entities[index], positionId).X, Is.EqualTo(3));
        }
    }

    [Test]
    public void GeneratedForEachParallelWarmPathDoesNotAllocateOnCallerThread()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register<Position>(new SchemaId(70_040));
        var velocityId = layouts.Register<Velocity>(new SchemaId(70_041));
        using var world = new World(layouts, initialEntityCapacity: 2_048);
        var entities = new Entity[2_048];
        world.Create(new[] { positionId, velocityId }, entities);
        var query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { positionId, velocityId }));
        var operation = world.ForEachParallel(in query, s_incrementAction, workerCount: 4);

        for (int warmup = 0; warmup < 8; warmup++)
        {
            operation.Invoke();
        }

        for (int measured = 0; measured < 3; measured++)
        {
            Assert.That(MeasureGeneratedParallelAllocation(operation), Is.EqualTo(0));
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long MeasureGeneratedParallelAllocation<TOperation>(TOperation operation)
        where TOperation : IOperation
    {
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        operation.Invoke();
        return GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    }

    private static void Increment(ref Position position, in Velocity velocity)
    {
        position.X += 1;
        position.Y += 1;
    }
}
