# DeltaECS.Systems

`DeltaECS.Systems` runs world-bound systems in deterministic dependency
batches. Systems that access different component ids can run together. Systems
that write the same component can also run together when their query scopes
match disjoint archetypes; structural and unknown world access forms an
exclusive phase.

The package contains the runtime contract and scheduler. When a system is
declared as a top-level `partial` class, the DeltaECS generator collects its
generated API calls and supplies the `Access` property automatically. Typed
`ForEach` calls that use a query stored in a `readonly` field keep their
component accesses scoped to that query. The scheduler compares the queries'
matching archetypes when it builds the schedule. Keep an explicit `Access`
property when access is dynamic or comes from an external source. The generator
treats explicit `ComponentId` selectors and calls outside the generated API as
unknown access; declare their actual component ids and query scopes explicitly
if you want the scheduler to distinguish those accesses.

Iteration calls return reusable deferred operations. Create one once and call
`Invoke()` from each system tick so the query plan and generated route stay
prepared between ticks.

```csharp
using Delta.ECS;
using Delta.ECS.Systems;

using var world = new World();
world.Layouts.Register<Velocity>(new SchemaId(1));
world.Layouts.Register<Position>(new SchemaId(2));

Query movementQuery = world.WhereAll<Position, Velocity>();
Entity mover = world.Create<Position>();
world.Add(mover, new Velocity());

using var scheduler = new SystemScheduler(world);
var movement = new MovementSystem(movementQuery) { World = world };
scheduler.Add(movement);
scheduler.Tick();

public partial class MovementSystem : ISystem
{
    private readonly Query _query;
    private EcsOperation? _operation;

    public World World { get; init; } = null!;

    public MovementSystem(Query query) => _query = query;

    public void Tick()
    {
        _operation ??= World.ForEach(in _query,
            static (ref Position p, in Velocity v) => p.X += v.X);
        _operation.Invoke();
    }
}

public struct Position { public float X; }
public struct Velocity { public float X; }
```

`SystemAccess` uses the `ComponentId` values registered in the system's world.
`Adds`, `Removes`, entity creation/destruction, unknown access and nested use of
the DeltaECS parallel executor are exclusive to keep immediate world mutation
safe. The scheduler compiles its graph when systems are added or removed and
reuses the resulting batches on subsequent ticks when dependency-aware
scheduling is enabled. If a system creates an archetype that changes a query's
matching set, the scheduler runs the rest of that tick serially and rebuilds
the optimized schedule on the next tick.

For systems with an explicit `Access` property, use `SystemQueryAccess` to
declare component ids within a query scope. This lets the scheduler run two
systems that write the same component together when their queries match
different archetypes:

```csharp
public SystemAccess Access => new(
    queryAccesses: new[]
    {
        new SystemQueryAccess(
            in _query,
            writes: stackalloc ComponentId[] { _positionId })
    },
    readsTopology: true);
```

Unscoped `reads`, `writes` and stamp reads remain world-wide and conflict with
the same component in any query scope. Query filters over overlay tags do not
separate archetypes, so they remain conservative when both queries can visit
the same archetype.

## Use an open generic functor in a system

Open generic functors can select their closed type with registered component
ids. This is useful for reusable operations such as copying a component into a
matching `History<T>` row. Register the generic component once, then call the
generated functor overload from `Tick`:

```csharp
using Delta.ECS;
using Delta.ECS.Systems;

using var world = new World();
ComponentId positionId = world.Layouts.Register<Position>(new SchemaId(10));
ComponentId historyId = world.Layouts.Register(typeof(History<>), positionId, new SchemaId(11));
Entity entity = world.Create(positionId, historyId);
world.GetRef<Position>(entity, positionId).X = 4f;
Query query = world.WhereAll(positionId, historyId);

using var scheduler = new SystemScheduler(world);
scheduler.Add(new CaptureHistorySystem(world, query, positionId, historyId));
scheduler.Tick();

public struct Position { public float X; }

public struct CaptureHistory<T> : IForEach
{
    public void Invoke(ref History<T> history, in T value) => history.Value = value;
}

public struct History<T> { public T Value; }

public sealed class CaptureHistorySystem : ISystem
{
    private readonly Query _query;
    private readonly ComponentId _positionId;
    private readonly SystemAccess _access;
    private EcsOperation? _captureOperation;

    public CaptureHistorySystem(World world, Query query, ComponentId positionId, ComponentId historyId)
    {
        World = world;
        _query = query;
        _positionId = positionId;
        _access = new SystemAccess(
            queryAccesses: new[]
            {
                new SystemQueryAccess(
                    in query,
                    reads: stackalloc ComponentId[] { positionId },
                    writes: stackalloc ComponentId[] { historyId })
            },
            readsTopology: true);
    }

    public World World { get; init; }
    public SystemAccess Access => _access;

    public void Tick()
    {
        _captureOperation ??= World.ForEach(in _query, _positionId, typeof(CaptureHistory<>));
        _captureOperation.Invoke();
    }
}
```

The open functor API uses the generator package and works inside scheduled
systems like the typed `ForEach` forms. Because its `ComponentId` arguments
select generic types at runtime, automatic access analysis cannot safely infer
the selected rows. Declare `SystemAccess` explicitly with the corresponding
IDs when the scheduler may run this system alongside independent systems; if
access metadata is generated, this call is conservatively treated as unknown
and runs in an exclusive phase. `SystemAccess` accepts explicit
`ReadOnlySpan<ComponentId>` lists and copies them when constructed.

To run every system sequentially in registration order, disable schedule
optimization:

```csharp
using var scheduler = new SystemScheduler(world, optimizeSchedule: false);
```

Linear mode does not inspect system access metadata or create scheduler worker
threads. `WorkerCount` is `1` in this mode.
