namespace Delta.ECS.Systems.Tests;

using System.Collections.Concurrent;
using Delta.ECS;
using Delta.ECS.Integration;
using Delta.ECS.Systems;
using NUnit.Framework;

[TestFixture]
public sealed class SystemSchedulerTests
{
    private static readonly ComponentId Position = new(0);
    private static readonly ComponentId Velocity = new(1);
    private static readonly ComponentId[] PositionComponents = { Position };
    private static readonly ComponentId[] VelocityComponents = { Velocity };
    private static readonly int[] ReaderWriterOrder = { 1, 2 };

    [Test]
    public void IndependentSystemsRunInParallel()
    {
        Assume.That(Environment.ProcessorCount, Is.GreaterThanOrEqualTo(2));
        using var world = new World();
        using var firstStarted = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        var first = new BarrierSystem(world, SystemAccess.None, firstStarted, secondStarted);
        var second = new BarrierSystem(world, SystemAccess.None, secondStarted, firstStarted);
        using var scheduler = new SystemScheduler(world, workerCount: 2);
        scheduler.Add(first);
        scheduler.Add(second);

        scheduler.Tick();

        Assert.That(first.Completed, Is.True);
        Assert.That(second.Completed, Is.True);
    }

    [Test]
    public void LinearSchedulePreservesRegistrationOrder()
    {
        using var world = new World();
        var order = new ConcurrentQueue<int>();
        using var scheduler = new SystemScheduler(world, optimizeSchedule: false);
        scheduler.Add(new RecordingSystem(world, SystemAccess.None, order, 1));
        scheduler.Add(new RecordingSystem(world, SystemAccess.None, order, 2));

        scheduler.Tick();

        Assert.That(order.ToArray(), Is.EqualTo(ReaderWriterOrder));
        Assert.That(scheduler.WorkerCount, Is.EqualTo(1));
    }

    [Test]
    public void LinearScheduleDoesNotRunIndependentSystemsConcurrently()
    {
        using var world = new World();
        using var state = new ConcurrencyState();
        using var scheduler = new SystemScheduler(world, workerCount: 2, optimizeSchedule: false);
        scheduler.Add(new ConcurrencySystem(world, SystemAccess.None, state));
        scheduler.Add(new ConcurrencySystem(world, SystemAccess.None, state));

        scheduler.Tick();

        Assert.That(state.MaximumConcurrency, Is.EqualTo(1));
        Assert.That(scheduler.WorkerCount, Is.EqualTo(1));
    }

    [Test]
    public void ConflictingSystemsPreserveRegistrationOrder()
    {
        using var world = new World();
        var order = new ConcurrentQueue<int>();
        var reader = new RecordingSystem(world, new SystemAccess(reads: PositionComponents), order, 1);
        var writer = new RecordingSystem(world, new SystemAccess(writes: PositionComponents), order, 2);
        using var scheduler = new SystemScheduler(world, workerCount: 2);
        scheduler.Add(reader);
        scheduler.Add(writer);

        scheduler.Tick();

        Assert.That(order.ToArray(), Is.EqualTo(ReaderWriterOrder));
    }

    [Test]
    public void StructuralSystemIsAnExclusivePhase()
    {
        using var world = new World();
        using var state = new ConcurrencyState();
        var ordinary = new ConcurrencySystem(world, new SystemAccess(writes: PositionComponents), state);
        var structural = new ConcurrencySystem(world, new SystemAccess(adds: VelocityComponents), state);
        using var scheduler = new SystemScheduler(world, workerCount: 2);
        scheduler.Add(ordinary);
        scheduler.Add(structural);

        scheduler.Tick();

        Assert.That(state.MaximumConcurrency, Is.EqualTo(1));
    }

    [Test]
    public void UnknownAccessIsAnExclusivePhase()
    {
        using var world = new World();
        var order = new ConcurrentQueue<int>();
        var unknown = new RecordingSystem(
            world,
            new SystemAccess(unknownWorldAccess: true),
            order,
            1);
        var independent = new RecordingSystem(world, SystemAccess.None, order, 2);
        using var scheduler = new SystemScheduler(world, workerCount: 2);
        scheduler.Add(unknown);
        scheduler.Add(independent);

        scheduler.Tick();

        Assert.That(order.ToArray(), Is.EqualTo(ReaderWriterOrder));
    }

    [Test]
    public void AddInvalidatesCompiledSchedule()
    {
        using var world = new World();
        var order = new ConcurrentQueue<int>();
        var first = new RecordingSystem(world, SystemAccess.None, order, 1);
        var second = new RecordingSystem(world, SystemAccess.None, order, 2);
        using var scheduler = new SystemScheduler(world, workerCount: 1);
        scheduler.Add(first);
        scheduler.Build();
        Assert.That(scheduler.IsBuilt, Is.True);

        scheduler.Add(second);

        Assert.That(scheduler.IsBuilt, Is.False);
        scheduler.Tick();
        Assert.That(scheduler.IsBuilt, Is.True);
        Assert.That(order.ToArray(), Is.EqualTo(ReaderWriterOrder));
    }

    [Test]
    public void WorldAndDuplicateRegistrationAreValidated()
    {
        using var world = new World();
        using var otherWorld = new World();
        var system = new RecordingSystem(world, SystemAccess.None, new ConcurrentQueue<int>(), 0);
        var foreign = new RecordingSystem(otherWorld, SystemAccess.None, new ConcurrentQueue<int>(), 0);
        using var scheduler = new SystemScheduler(world, workerCount: 1);

        scheduler.Add(system);
        Assert.That(() => scheduler.Add(system), Throws.ArgumentException);
        Assert.That(() => scheduler.Add(foreign), Throws.ArgumentException);
    }

    [Test]
    public void WorkerCountIsClampedToProcessorCount()
    {
        using var world = new World();
        using var scheduler = new SystemScheduler(world, int.MaxValue);

        Assert.That(scheduler.WorkerCount, Is.InRange(1, Environment.ProcessorCount));
    }

    [Test]
    public void AccessMetadataCopiesComponentIdsAndRejectsInvalidIds()
    {
        var ids = PositionComponents.ToArray();
        var access = new SystemAccess(reads: ids);
        ids[0] = Velocity;

        Assert.That(access.Reads[0], Is.EqualTo(Position));
        Assert.That(
            () => new SystemAccess(reads: new[] { ComponentId.Invalid }),
            Throws.ArgumentException);
    }

    [Test]
    public void AccessMetadataAcceptsExplicitComponentIdSpan()
    {
        ReadOnlySpan<ComponentId> ids = stackalloc ComponentId[] { Position, Velocity };

        var access = new SystemAccess(reads: ids);

        Assert.That(access.Reads.ToArray(), Is.EqualTo(new[] { Position, Velocity }));
    }

    [Test]
    public void QueryScopedAccessMustBelongToSchedulerWorld()
    {
        using var world = new World();
        using var otherWorld = new World();
        _ = otherWorld.Layouts.Register<SystemPosition>(new SchemaId(81011));
        Query foreignQuery = otherWorld.WhereAll<SystemPosition>();
        var access = new SystemAccess(
            queryAccesses: new[]
            {
                new SystemQueryAccess(in foreignQuery, writes: PositionComponents)
            });
        var system = new RecordingSystem(world, access, new ConcurrentQueue<int>(), 0);
        using var scheduler = new SystemScheduler(world, workerCount: 1);
        scheduler.Add(system);

        Assert.That(() => scheduler.Build(), Throws.ArgumentException);
    }

    [Test]
    public void OpenGenericFunctorRunsInsideScheduledSystem()
    {
        using var world = new World();
        ComponentId valueId = world.Layouts.Register<int>(new SchemaId(81001));
        ComponentId historyId = world.Layouts.Register(typeof(SystemHistory<>), valueId, new SchemaId(81002));
        Entity entity = world.Create(valueId, historyId);
        world.GetRef<int>(entity, valueId) = 42;
        Query query = world.WhereAll(valueId, historyId);
        var system = new SystemHistoryCaptureSystem(world, in query, valueId, historyId);
        using var scheduler = new SystemScheduler(world, optimizeSchedule: false);
        scheduler.Add(system);

        scheduler.Tick();

        Assert.That(world.Get<SystemHistory<int>>(entity, historyId).Value, Is.EqualTo(42));
        Assert.That(system.InvocationCount, Is.EqualTo(1));
    }

    [Test]
    public void OpenGenericFunctorWithoutExplicitAccessIsConservative()
    {
        using var world = new World();
        ComponentId valueId = world.Layouts.Register<int>(new SchemaId(81003));
        ComponentId historyId = world.Layouts.Register(typeof(SystemHistory<>), valueId, new SchemaId(81004));
        Query query = world.WhereAll(valueId, historyId);
        var system = new InferredSystemHistoryCaptureSystem(world, in query, valueId);

        Assert.That(system.Access.UnknownWorldAccess, Is.True);
        Assert.That(system.Access.RequiresExclusiveWorld, Is.True);
    }

    [Test]
    public void DisjointQueryArchetypesRunInParallelAndNewOverlapRebuildsSchedule()
    {
        Assume.That(Environment.ProcessorCount, Is.GreaterThanOrEqualTo(2));
        using var world = new World();
        ComponentId positionId = world.Layouts.Register<SystemPosition>(new SchemaId(81005));
        ComponentId playerId = world.Layouts.Register<PlayerUnit>(new SchemaId(81006));
        ComponentId enemyId = world.Layouts.Register<EnemyUnit>(new SchemaId(81007));
        Entity player = world.Create(positionId, playerId);
        Entity enemy = world.Create(positionId, enemyId);
        world.GetRef<SystemPosition>(player, positionId).Value = 1;
        world.GetRef<SystemPosition>(enemy, positionId).Value = 2;

        var probe = new QueryExecutionProbe();
        using var barrier = new Barrier(2);
        probe.Barrier = barrier;
        var playerSystem = new PlayerPositionSystem(world, world.WhereAll<SystemPosition, PlayerUnit>(), probe);
        var enemySystem = new EnemyPositionSystem(world, world.WhereAll<SystemPosition, EnemyUnit>(), probe);
        Assert.That(playerSystem.Access.QueryAccesses.Length, Is.EqualTo(1));
        Assert.That(enemySystem.Access.QueryAccesses.Length, Is.EqualTo(1));
        Assert.That(playerSystem.Access.QueryAccesses[0].Writes.ToArray(), Is.EqualTo(new[] { positionId }));
        Assert.That(playerSystem.Access.Writes.Length, Is.EqualTo(0));
        Assert.That(playerSystem.Access.Reads.Length, Is.EqualTo(0));

        using var scheduler = new SystemScheduler(world, workerCount: 2);
        scheduler.Add(playerSystem);
        scheduler.Add(enemySystem);

        scheduler.Tick();

        Assert.That(probe.InvocationCount, Is.EqualTo(2));
        probe.Barrier = null;
        probe.ResetMaximumConcurrency();
        _ = world.Create(positionId, playerId, enemyId);

        scheduler.Tick();

        Assert.That(probe.MaximumConcurrency, Is.EqualTo(1));
        Assert.That(scheduler.IsBuilt, Is.True);
    }

    [Test]
    public void NewMatchingArchetypeCreatedDuringTickSerializesRemainingSystems()
    {
        using var world = new World();
        ComponentId positionId = world.Layouts.Register<SystemPosition>(new SchemaId(81008));
        ComponentId playerId = world.Layouts.Register<PlayerUnit>(new SchemaId(81009));
        ComponentId enemyId = world.Layouts.Register<EnemyUnit>(new SchemaId(81010));
        Entity player = world.Create(positionId, playerId);
        _ = world.Create(positionId, enemyId);
        var probe = new QueryExecutionProbe();
        var playerSystem = new PlayerPositionSystem(world, world.WhereAll<SystemPosition, PlayerUnit>(), probe);
        var enemySystem = new EnemyPositionSystem(world, world.WhereAll<SystemPosition, EnemyUnit>(), probe);
        var mutate = new AddEnemyToEntitySystem(world, player, enemyId);
        using var scheduler = new SystemScheduler(world, workerCount: 2);
        scheduler.Add(mutate);
        scheduler.Add(playerSystem);
        scheduler.Add(enemySystem);

        scheduler.Tick();

        Assert.That(probe.MaximumConcurrency, Is.EqualTo(1));
        Assert.That(scheduler.IsBuilt, Is.False);

        probe.ResetMaximumConcurrency();
        scheduler.Tick();

        Assert.That(probe.MaximumConcurrency, Is.EqualTo(1));
        Assert.That(scheduler.IsBuilt, Is.True);
    }

    [Test]
    public void WorkerExceptionsArePropagatedAfterBatchCompletion()
    {
        using var world = new World();
        var failing = new RecordingSystem(
            world,
            SystemAccess.None,
            new ConcurrentQueue<int>(),
            0,
            new InvalidOperationException("failure"));
        var independent = new RecordingSystem(
            world,
            SystemAccess.None,
            new ConcurrentQueue<int>(),
            1);
        using var scheduler = new SystemScheduler(world, workerCount: 2);
        scheduler.Add(failing);
        scheduler.Add(independent);

        Assert.That(() => scheduler.Tick(), Throws.TypeOf<InvalidOperationException>());
        Assert.That(world.IsAlive(default), Is.False);
    }

    [Test]
    public void ExternalWorldAccessFailsFastDuringTick()
    {
        using var world = new World();
        _ = world.Layouts.Register<GeneratedSetComponent>(new SchemaId(1));
        _ = GeneratedForEachRuntime.GetGeneratedPrimaryComponentIds<GeneratedSetKey>(
            world,
            static _ => new[] { Position });
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var scheduler = new SystemScheduler(world, workerCount: 1);
        scheduler.Add(new BlockingSystem(world, started, release));

        Task tick = Task.Run(scheduler.Tick);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);
        Assert.That(
            () => Task.Run(() => world.IsAlive(default)).GetAwaiter().GetResult(),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(
            () => Task.Run(() => world.Has(default, Position)).GetAwaiter().GetResult(),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(
            () => Task.Run(() => world.TryGetComponentStamp(default, Position, out _)).GetAwaiter().GetResult(),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(
            () => Task.Run(() => GeneratedForEachRuntime.GetGeneratedPrimaryComponentIds<GeneratedSetKey>(
                world,
                static _ => new[] { Position }).Length).GetAwaiter().GetResult(),
            Throws.TypeOf<InvalidOperationException>());

        var integration = (IEcsWorld)world;
        Assert.That(
            () => Task.Run(() => integration.Catalog).GetAwaiter().GetResult(),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(
            () => Task.Run(integration.Initialize).GetAwaiter().GetResult(),
            Throws.TypeOf<InvalidOperationException>());

        release.Set();
        tick.GetAwaiter().GetResult();
    }

    [Test]
    public void SchedulerWorkersCanAccessTheirWorld()
    {
        Assume.That(Environment.ProcessorCount, Is.GreaterThanOrEqualTo(2));
        using var world = new World();
        using var scheduler = new SystemScheduler(world, workerCount: 2);
        scheduler.Add(new WorldAccessSystem(world));
        scheduler.Add(new WorldAccessSystem(world));

        scheduler.Tick();
    }

    [Test]
    public void NestedParallelExecutorWorkersCanAccessSchedulerWorld()
    {
        Assume.That(Environment.ProcessorCount, Is.GreaterThanOrEqualTo(2));
        using var world = new World();
        ComponentId component = world.Layouts.Register<GeneratedSetComponent>(new SchemaId(2));
        world.Create([component], 4);
        Query query = world.WhereAll(component);
        using var scheduler = new SystemScheduler(world, workerCount: 1);
        scheduler.Add(new NestedParallelSystem(world, query));

        scheduler.Tick();
    }

    [Test]
    public void ForeignSchedulerAndWorldDisposeFailFastDuringTick()
    {
        using var world = new World();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var scheduler = new SystemScheduler(world, workerCount: 1);
        using var foreignScheduler = new SystemScheduler(world, workerCount: 1);
        scheduler.Add(new BlockingSystem(world, started, release));

        Task tick = Task.Run(scheduler.Tick);
        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);
        Assert.That(() => foreignScheduler.Tick(), Throws.TypeOf<InvalidOperationException>());
        Assert.That(() => world.Dispose(), Throws.TypeOf<InvalidOperationException>());

        release.Set();
        tick.GetAwaiter().GetResult();
    }

    [Test]
    public void SchedulerGateIsReleasedAfterAnOrdinaryCompletion()
    {
        using var world = new World();
        using var scheduler = new SystemScheduler(world, workerCount: 1);
        scheduler.Add(new WorldAccessSystem(world));

        scheduler.Tick();

        Assert.That(world.IsAlive(default), Is.False);
    }

    [Test]
    public void DisposeReleasesWorkersAndRejectsFurtherUse()
    {
        using var world = new World();
        var scheduler = new SystemScheduler(world, workerCount: 2);
        scheduler.Dispose();

        Assert.That(() => scheduler.Tick(), Throws.TypeOf<ObjectDisposedException>());
        Assert.That(() => scheduler.Add(new RecordingSystem(world, SystemAccess.None, new ConcurrentQueue<int>(), 0)),
            Throws.TypeOf<ObjectDisposedException>());
    }

    private sealed class RecordingSystem : ISystem
    {
        private readonly ConcurrentQueue<int> _order;
        private readonly int _value;
        private readonly Exception? _failure;

        internal RecordingSystem(
            World world,
            SystemAccess access,
            ConcurrentQueue<int> order,
            int value,
            Exception? failure = null)
        {
            World = world;
            Access = access;
            _order = order;
            _value = value;
            _failure = failure;
        }

        public World World { get; init; }

        public SystemAccess Access { get; }

        public void Tick()
        {
            _order.Enqueue(_value);
            if (_failure is not null)
            {
                throw _failure;
            }
        }
    }

    private sealed class WorldAccessSystem : ISystem
    {
        internal WorldAccessSystem(World world) => World = world;

        public World World { get; init; }

        public SystemAccess Access => SystemAccess.None;

        public void Tick() => _ = World.IsAlive(default);
    }

    private sealed class BlockingSystem : ISystem
    {
        private readonly ManualResetEventSlim _started;
        private readonly ManualResetEventSlim _release;

        internal BlockingSystem(World world, ManualResetEventSlim started, ManualResetEventSlim release)
        {
            World = world;
            _started = started;
            _release = release;
        }

        public World World { get; init; }

        public SystemAccess Access => SystemAccess.None;

        public void Tick()
        {
            _started.Set();
            if (!_release.Wait(TimeSpan.FromSeconds(2)))
            {
                throw new TimeoutException("Tick was not released.");
            }
        }
    }

    private sealed class NestedParallelSystem : ISystem
    {
        private readonly Query _query;

        internal NestedParallelSystem(World world, Query query)
        {
            World = world;
            _query = query;
        }

        public World World { get; init; }

        public SystemAccess Access => new(usesParallelExecutor: true);

        public void Tick() => World.ForEachEntityParallel(in _query, OnEntity, workerCount: 2);

        private void OnEntity(Entity entity) => _ = World.IsAlive(entity);
    }

    private readonly struct GeneratedSetKey
    {
    }

    private readonly struct GeneratedSetComponent
    {
    }

    private sealed class BarrierSystem : ISystem
    {
        private readonly ManualResetEventSlim _started;
        private readonly ManualResetEventSlim _otherStarted;

        internal BarrierSystem(
            World world,
            SystemAccess access,
            ManualResetEventSlim started,
            ManualResetEventSlim otherStarted)
        {
            World = world;
            Access = access;
            _started = started;
            _otherStarted = otherStarted;
        }

        internal bool Completed { get; private set; }

        public World World { get; init; }

        public SystemAccess Access { get; }

        public void Tick()
        {
            _started.Set();
            if (!_otherStarted.Wait(TimeSpan.FromSeconds(2)))
            {
                throw new TimeoutException("Independent systems did not overlap.");
            }

            Completed = true;
        }
    }

    private sealed class ConcurrencySystem : ISystem
    {
        private readonly ConcurrencyState _state;

        internal ConcurrencySystem(World world, SystemAccess access, ConcurrencyState state)
        {
            World = world;
            Access = access;
            _state = state;
        }

        public World World { get; init; }

        public SystemAccess Access { get; }

        public void Tick()
        {
            _state.Enter();
            _state.BothStarted.Wait(TimeSpan.FromMilliseconds(100));
            _state.Exit();
        }
    }

    private sealed class ConcurrencyState : IDisposable
    {
        private readonly ManualResetEventSlim _bothStarted = new();
        private int _started;
        private int _active;

        internal ManualResetEventSlim BothStarted => _bothStarted;

        private int _maximumConcurrency;

        internal int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);

        internal void Enter()
        {
            int active = Interlocked.Increment(ref _active);
            int maximum;
            do
            {
                maximum = Volatile.Read(ref _maximumConcurrency);
                if (active <= maximum)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref _maximumConcurrency, active, maximum) != maximum);

            if (Interlocked.Increment(ref _started) == 2)
            {
                _bothStarted.Set();
            }
        }

        internal void Exit() => Interlocked.Decrement(ref _active);

        public void Dispose() => _bothStarted.Dispose();
    }
}

public struct SystemHistory<T>
{
    public T Value;
}

public struct SystemPosition
{
    public int Value;
}

public struct PlayerUnit
{
    public int Value;
}

public struct EnemyUnit
{
    public int Value;
}

public sealed class QueryExecutionProbe
{
    private int _activeInvocations;
    private int _maximumConcurrency;
    private int _invocationCount;

    public Barrier? Barrier;

    public int InvocationCount => Volatile.Read(ref _invocationCount);

    public int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);

    public void Execute(ref SystemPosition position)
    {
        Barrier? barrier = Barrier;
        if (barrier is not null && !barrier.SignalAndWait(TimeSpan.FromSeconds(2)))
        {
            throw new TimeoutException("Disjoint query systems did not enter their callbacks concurrently.");
        }

        int active = Interlocked.Increment(ref _activeInvocations);
        UpdateMaximumConcurrency(active);
        Interlocked.Increment(ref _invocationCount);
        Thread.Sleep(20);
        Interlocked.Decrement(ref _activeInvocations);
        position.Value++;
    }

    public void ResetMaximumConcurrency()
        => Volatile.Write(ref _maximumConcurrency, 0);

    private void UpdateMaximumConcurrency(int value)
    {
        int current;
        do
        {
            current = Volatile.Read(ref _maximumConcurrency);
            if (current >= value)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _maximumConcurrency, value, current) != current);
    }
}

public sealed partial class PlayerPositionSystem : ISystem
{
    private readonly Query _query;
    private QueryExecutionProbe _probe;

    public PlayerPositionSystem(World world, in Query query, QueryExecutionProbe probe)
    {
        World = world;
        _query = query;
        _probe = probe;
    }

    public World World { get; init; }

    public void Tick()
        => World.ForEach(in _query, ref _probe, static (ref QueryExecutionProbe probe, ref SystemPosition position) =>
            probe.Execute(ref position));
}

public sealed partial class EnemyPositionSystem : ISystem
{
    private readonly Query _query;
    private QueryExecutionProbe _probe;

    public EnemyPositionSystem(World world, in Query query, QueryExecutionProbe probe)
    {
        World = world;
        _query = query;
        _probe = probe;
    }

    public World World { get; init; }

    public void Tick()
        => World.ForEach(in _query, ref _probe, static (ref QueryExecutionProbe probe, ref SystemPosition position) =>
            probe.Execute(ref position));
}

public sealed class AddEnemyToEntitySystem : ISystem
{
    private readonly Entity _entity;
    private readonly ComponentId _enemyId;

    public AddEnemyToEntitySystem(World world, Entity entity, ComponentId enemyId)
    {
        World = world;
        _entity = entity;
        _enemyId = enemyId;
        Access = new SystemAccess(adds: stackalloc ComponentId[] { enemyId });
    }

    public World World { get; init; }

    public SystemAccess Access { get; }

    public void Tick() => World.Add<EnemyUnit>(_entity, _enemyId);
}

public struct SystemHistoryContext
{
    public int InvocationCount;
}

public struct CaptureSystemHistory<T> : IForEachContext<SystemHistoryContext>
{
    public void Invoke(ref SystemHistoryContext context, ref SystemHistory<T> history, in T value)
    {
        history.Value = value;
        context.InvocationCount++;
    }
}

public sealed class SystemHistoryCaptureSystem : ISystem
{
    private readonly Query _query;
    private readonly ComponentId _valueId;
    private SystemHistoryContext _context;

    public SystemHistoryCaptureSystem(World world, in Query query, ComponentId valueId, ComponentId historyId)
    {
        World = world;
        _query = query;
        _valueId = valueId;
        Access = new SystemAccess(
            reads: new[] { valueId },
            writes: new[] { historyId },
            readsTopology: true);
    }

    public World World { get; init; }

    public SystemAccess Access { get; }

    public int InvocationCount => _context.InvocationCount;

    public void Tick()
        => World.ForEach(in _query, ref _context, _valueId, typeof(CaptureSystemHistory<>));
}

public sealed partial class InferredSystemHistoryCaptureSystem : ISystem
{
    private readonly Query _query;
    private readonly ComponentId _valueId;

    public InferredSystemHistoryCaptureSystem(World world, in Query query, ComponentId valueId)
    {
        World = world;
        _query = query;
        _valueId = valueId;
    }

    public World World { get; init; }

    public void Tick()
        => World.ForEach(in _query, _valueId, typeof(CaptureSystemHistory<>));
}
