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
    public void Invoke(ref Position position, in Velocity velocity) => position.X += velocity.X;
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
            workerCount: 4);
#pragma warning restore CS9198
        world.ForEachParallel(
            in query,
            in state,
            static (in ParallelState value, ref Position position) => position.X += value.Delta,
            workerCount: 4);
        world.ForEachEntityParallel(
            in query,
            state,
            static (ParallelState value, Entity entity, ref Position position) =>
            {
                _ = entity;
                position.X += value.Delta;
            },
            workerCount: 4);

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
        ForEachContextEntityActionIn<ParallelState> readOnlyEntityAction = (in ParallelState _, Entity __) => Interlocked.Increment(ref readOnlyContextVisits);
        ForEachContextEntityActionValue<ParallelState> valueEntityAction = (ParallelState _, Entity __) => Interlocked.Increment(ref valueContextVisits);

        Assert.Multiple(() =>
        {
            Assert.That(() => world.ForEachParallel(in query, action, workerCount: 4), Throws.InvalidOperationException);
            Assert.That(() => world.ForEachParallel(in query, in state, readOnlyAction, workerCount: 4), Throws.InvalidOperationException);
            Assert.That(() => world.ForEachParallel(in query, state, valueAction, workerCount: 4), Throws.InvalidOperationException);
        });
        world.ForEachEntityParallel(in query, entityAction, workerCount: 4);
        world.ForEachEntityParallel(in query, in state, readOnlyEntityAction, workerCount: 4);
        world.ForEachEntityParallel(in query, state, valueEntityAction, workerCount: 4);

        int[] entityListVisits = [0];
        ReadOnlySpan<Entity> selected = entities.AsSpan(0, entities.Length / 2);
        int selectedCount = selected.Length;
        world.ForEachEntityParallel(
            selected,
            in query,
            entityListVisits,
            static (int[] visits, Entity _) => Interlocked.Increment(ref visits[0]),
            workerCount: 4);
        world.ForEachEntityParallel(
            selected,
            entityListVisits,
            static (int[] visits, Entity _) => Interlocked.Increment(ref visits[0]),
            workerCount: 4);

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
        using var world = new World(layouts, initialEntityCapacity: 2_048);
        var entities = new Entity[2_048];
        world.Create([positionId, velocityId], entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.Set(entities[index], velocityId, new Velocity { X = 1 });
        }

        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
        var action = new ParallelIncrementFunctor();
        world.ForEachParallel(in query, ref action, workerCount: 4);
        var state = new ParallelState { Delta = 2 };
        var contextual = new ParallelContextFunctor();
        world.ForEachParallel(in query, in state, ref contextual, workerCount: 4);

        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.Get<Position>(entities[index], positionId).X, Is.EqualTo(4));
        }
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
            world.Set(entities[index], positionId, new Position { X = 1, Y = 2 });
            world.Set(entities[index], velocityId, new Velocity { X = 3, Y = 4 });
        }

        var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
        world.ForEachParallel(
            in query,
            static (ref Position position, in Velocity velocity) =>
            {
                position.X += velocity.X;
                position.Y += velocity.Y;
            },
            workerCount: 4);

        for (int index = 0; index < entities.Length; index++)
        {
            Position actual = world.Get<Position>(entities[index], positionId);
            Assert.That(actual.X, Is.EqualTo(4));
            Assert.That(actual.Y, Is.EqualTo(6));
        }
    }

    [Test]
    public void GeneratedForEachParallelUsesBackgroundWorkerForSmallQuery()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(70_080));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(70_081));
        using var world = new World(layouts, initialEntityCapacity: 8);
        world.Create([positionId, velocityId], 2, Span<Entity>.Empty);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
        s_generatedCallbackThreadId = 0;
        int callerThreadId = Environment.CurrentManagedThreadId;

        world.ForEachParallel(
            in query,
            static (ref Position position, in Velocity velocity) =>
            {
                Volatile.Write(ref s_generatedCallbackThreadId, Environment.CurrentManagedThreadId);
                position.X += velocity.X;
            },
            workerCount: 2);

        Assert.That(Volatile.Read(ref s_generatedCallbackThreadId), Is.Not.EqualTo(callerThreadId));
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
        var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));

        RunGeneratedParallel(world, in query);

        var secondBatch = new Entity[128];
        world.Create(new[] { positionId, velocityId }, secondBatch);
        RunGeneratedParallel(world, in query);

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
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
        var entities = new Entity[2_048];
        world.Create([positionId, velocityId], entities);

        RunGeneratedParallel(world, in query);

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
        using var world = new World(layouts, initialEntityCapacity: 1_024);
        var entities = new Entity[1_024];
        world.Create(new[] { positionId, velocityId }, entities);
        var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));

        RunGeneratedParallel(world, in query, workerCount: 2);
        RunGeneratedParallel(world, in query, workerCount: 4);
        RunGeneratedParallel(world, in query, workerCount: 2);

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
        var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));

        for (int warmup = 0; warmup < 8; warmup++)
        {
            RunGeneratedParallel(world, in query);
        }

        for (int measured = 0; measured < 3; measured++)
        {
            Assert.That(MeasureGeneratedParallelAllocation(world, in query), Is.EqualTo(0));
        }
    }

    private static void RunGeneratedParallel(World world, in Query query, int workerCount = 4) =>
        world.ForEachParallel(in query, s_incrementAction, workerCount);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long MeasureGeneratedParallelAllocation(World world, in Query query)
    {
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        RunGeneratedParallel(world, in query);
        return GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    }

    private static void Increment(ref Position position, in Velocity velocity)
    {
        position.X += 1;
        position.Y += 1;
    }
}
