# DeltaECS.Systems

`DeltaECS.Systems` runs world-bound systems in deterministic dependency
batches. Systems that only access different component ids can run together;
structural and unknown world access forms an exclusive phase.

The package contains the runtime contract and scheduler. When a system is
declared as a top-level `partial` class, the DeltaECS generator collects its
generated API calls and supplies the `Access` property automatically. Keep an
explicit `Access` property when the access set is dynamic or comes from an
external source. Explicit `ComponentId` selectors and calls outside the
generated API are treated as unknown access, so the scheduler keeps them in an
exclusive phase.

```csharp
using Delta.ECS;
using Delta.ECS.Systems;

public partial class MovementSystem : ISystem
{
    private readonly Query _query;

    public World World { get; init; } = null!;

    public void Tick()
    {
        World.ForEach(in _query,
            static (ref Position p, in Velocity v) => p.X += v.X);
    }
}

using var world = new World();
using var scheduler = new SystemScheduler(world);
world.Layouts.Register<Velocity>(new SchemaId(1));
world.Layouts.Register<Position>(new SchemaId(2));
var movement = new MovementSystem { World = world };
scheduler.Add(movement);
scheduler.Tick();

public struct Position { public float X; }
public struct Velocity { public float X; }
```

`SystemAccess` uses the `ComponentId` values registered in the system's world.
`Adds`, `Removes`, entity creation/destruction, unknown access and nested use of
the DeltaECS parallel executor are exclusive to keep immediate world mutation
safe. The scheduler compiles its graph when systems are added or removed and
reuses the resulting batches on subsequent ticks when dependency-aware
scheduling is enabled.

To run every system sequentially in registration order, disable schedule
optimization:

```csharp
using var scheduler = new SystemScheduler(world, optimizeSchedule: false);
```

Linear mode does not inspect system access metadata or create scheduler worker
threads. `WorkerCount` is `1` in this mode.
