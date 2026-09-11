namespace Delta.ECS.Tests;

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
public sealed class ParallelIterationTests
{
    private static readonly ForEachAction_WI<Position, Velocity> s_incrementAction = Increment;
    internal static int s_generatedCallbackThreadId;
    private static int s_readOnlyContextVisits;
    private static int s_refReadonlyContextVisits;
    private static int s_valueEntityContextVisits;

    [Test]
    public void GeneratedForEachParallelSupportsReadOnlyAndValueState()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(70_090));
        using var world = new World(layouts, initialEntityCapacity: 2_048, chunkCapacity: 128);
        var entities = new Entity[2_048];
        world.Create([positionId], entities);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        var state = new ParallelState { Delta = 2 };
        Assert.That(world.TryGetComponentStamp(entities[0], positionId, out Stamp stampBefore), Is.True);

        world.ForEachParallel(
            in query,
            in state,
            static (in ParallelState value, ref Position position) => position.X += value.Delta,
            workerCount: 4);
        world.ForEachParallel(
            in query,
            in state,
            static (ref readonly ParallelState value, ref Position position) => position.X += value.Delta,
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
    public void GeneratedForEachParallelSupportsZeroComponentEntityAndNonEntityCallbacks()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(70_093));
        using var world = new World(layouts, initialEntityCapacity: 256, chunkCapacity: 128);
        var entities = new Entity[256];
        world.Create([positionId], entities);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        int visited = 0;
        int visitedEntities = 0;
        Volatile.Write(ref s_readOnlyContextVisits, 0);
        Volatile.Write(ref s_refReadonlyContextVisits, 0);
        Volatile.Write(ref s_valueEntityContextVisits, 0);

        world.ForEachParallel(in query, () => Interlocked.Increment(ref visited), workerCount: 4);
        world.ForEachEntityParallel(in query, entity =>
        {
            _ = entity;
            Interlocked.Increment(ref visitedEntities);
        }, workerCount: 4);
        var state = new ParallelState();
        world.ForEachParallel(
            in query,
            in state,
            static (in ParallelState _) => Interlocked.Increment(ref s_readOnlyContextVisits),
            workerCount: 4);
        world.ForEachParallel(
            in query,
            in state,
            static (ref readonly ParallelState _) => Interlocked.Increment(ref s_refReadonlyContextVisits),
            workerCount: 4);
        world.ForEachEntityParallel(
            in query,
            state,
            static (ParallelState _, Entity _) => Interlocked.Increment(ref s_valueEntityContextVisits),
            workerCount: 4);

        Assert.That(visited, Is.EqualTo(entities.Length));
        Assert.That(visitedEntities, Is.EqualTo(entities.Length));
        Assert.That(Volatile.Read(ref s_readOnlyContextVisits), Is.EqualTo(entities.Length));
        Assert.That(Volatile.Read(ref s_refReadonlyContextVisits), Is.EqualTo(entities.Length));
        Assert.That(Volatile.Read(ref s_valueEntityContextVisits), Is.EqualTo(entities.Length));
    }

    [Test]
    public void GeneratedForEachParallelSupportsExplicitFunctorsAndContext()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(70_091));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(70_092));
        using var world = new World(layouts, initialEntityCapacity: 2_048, chunkCapacity: 128);
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
    public void GeneratedForEachParallel_ProcessesEveryEntity()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register(typeof(Position), new SchemaId(70_020));
        var velocityId = layouts.Register(typeof(Velocity), new SchemaId(70_021));
        using var world = new World(layouts, initialEntityCapacity: 2_048, chunkCapacity: 128);
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
    public void GeneratedForEachParallel_UsesBackgroundWorkerForSmallQuery()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(70_080));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(70_081));
        using var world = new World(layouts, initialEntityCapacity: 8, chunkCapacity: 8);
        world.Create([positionId, velocityId], 2);
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
    public void GeneratedForEachParallel_RebuildsCachedRangesAfterTopologyChange()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register(typeof(Position), new SchemaId(70_030));
        var velocityId = layouts.Register(typeof(Velocity), new SchemaId(70_031));
        using var world = new World(layouts, initialEntityCapacity: 256, chunkCapacity: 128);
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
        using var world = new World(layouts, initialEntityCapacity: 2_048, chunkCapacity: 128);
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
    public void GeneratedForEachParallel_GrowsWorkerPoolWithoutLosingSignals()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register(typeof(Position), new SchemaId(70_050));
        var velocityId = layouts.Register(typeof(Velocity), new SchemaId(70_051));
        using var world = new World(layouts, initialEntityCapacity: 1_024, chunkCapacity: 128);
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
    public void GeneratedForEachParallel_WarmPathDoesNotAllocateOnCallerThread()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register(typeof(Position), new SchemaId(70_040));
        var velocityId = layouts.Register(typeof(Velocity), new SchemaId(70_041));
        using var world = new World(layouts, initialEntityCapacity: 2_048, chunkCapacity: 128);
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

    [Test]
    public void ForEachParallel_UsesBackgroundWorkerForSmallQuery()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register(typeof(Position), new SchemaId(70_060));
        var velocityId = layouts.Register(typeof(Velocity), new SchemaId(70_061));
        using var world = new World(layouts, initialEntityCapacity: 2_048, chunkCapacity: 128);
        var entities = new Entity[2_048];
        world.Create(new[] { positionId, velocityId }, entities);
        var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
        int callerThreadId = Environment.CurrentManagedThreadId;
        int callbackThreadId = 0;

        QueryChunkAction action = chunk =>
        {
            callbackThreadId = Environment.CurrentManagedThreadId;
        };
        world.ForEachParallel(in query, action, workerCount: 4);

        Assert.That(callbackThreadId, Is.Not.EqualTo(callerThreadId));
    }

    [Test]
    public void ForEachParallel_ProcessesEveryChunkExactlyOnce()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register(typeof(Position), new SchemaId(70_000));
        var velocityId = layouts.Register(typeof(Velocity), new SchemaId(70_001));
        using var world = new World(layouts, initialEntityCapacity: 2_048, chunkCapacity: 128);
        var entities = new Entity[2_048];
        world.Create(new[] { positionId, velocityId }, entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.Set(entities[index], positionId, new Position { X = 1, Y = 2 });
            world.Set(entities[index], velocityId, new Velocity { X = 3, Y = 4 });
        }

        var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
        var position = query.AccessWrite(positionId);
        var velocity = query.AccessRead(velocityId);
        QueryChunkAction action = Apply;
        world.ForEachParallel(in query, action, workerCount: 4);

        for (int index = 0; index < entities.Length; index++)
        {
            Position actual = world.Get<Position>(entities[index], positionId);
            Assert.That(actual.X, Is.EqualTo(4));
            Assert.That(actual.Y, Is.EqualTo(6));
        }

        void Apply(QueryChunk chunk)
        {
            var slots = chunk.Slots;
            var positions = slots.GetRow(position);
            var velocities = slots.GetRow(velocity);
            while (slots.MoveNext())
            {
                ref Position currentPosition = ref positions.Ref<Position>(slots);
                ref readonly Velocity currentVelocity = ref velocities.Ref<Velocity>(slots);
                currentPosition.X += currentVelocity.X;
                currentPosition.Y += currentVelocity.Y;
            }
        }
    }

    [Test]
    public void ForEachParallel_RejectsStructuralChangesDuringExecution()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register(typeof(Position), new SchemaId(70_010));
        using var world = new World(layouts, initialEntityCapacity: 1_024, chunkCapacity: 128);
        var entities = new Entity[1_024];
        world.Create(new[] { positionId }, entities);
        var query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        QueryChunkAction action = TryCreate;

        Assert.Throws<InvalidOperationException>(() => world.ForEachParallel(in query, action, workerCount: 4));
        Assert.That(world.IsAlive(world.Create(new[] { positionId })), Is.True);

        void TryCreate(QueryChunk _)
        {
            world.Create(new[] { positionId });
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
