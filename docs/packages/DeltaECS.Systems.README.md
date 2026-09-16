# DeltaECS.Systems

`DeltaECS.Systems` runs world-bound systems in deterministic dependency
batches. Systems that only access different component ids can run together;
structural and unknown world access forms an exclusive phase.

The package contains the runtime contract and scheduler. Access metadata is
declared by the system; it does not add a source generator or analyzer.

```csharp
using Delta.ECS;
using Delta.ECS.Systems;

public sealed class MovementSystem : ISystem
{
    private readonly ComponentId _position;
    private readonly ComponentId _velocity;

    public MovementSystem(ComponentId position, ComponentId velocity)
    {
        _position = position;
        _velocity = velocity;
    }

    public World World { get; init; } = null!;

    public SystemAccess Access => new(
        reads: new[] { _velocity },
        writes: new[] { _position });

    public void Tick()
    {
        // Use the regular generated DeltaECS ForEach API here.
    }
}

using var world = new World();
using var scheduler = new SystemScheduler(world);
var velocity = world.Layouts.Register<Velocity>(new SchemaId(1));
var position = world.Layouts.Register<Position>(new SchemaId(2));
var movement = new MovementSystem(
    position,
    velocity)
{
    World = world
};
scheduler.Add(movement);
scheduler.Tick();

public struct Position { public float X; }
public struct Velocity { public float X; }
```

`SystemAccess` uses the `ComponentId` values registered in the system's world.
`Adds`, `Removes`, entity creation/destruction, unknown access and nested use of
the DeltaECS parallel executor are exclusive to keep immediate world mutation
safe. The scheduler compiles its graph when systems are added or removed and
reuses the resulting batches on subsequent ticks.
