# DeltaECS

`DeltaECS` is the runtime package for the DeltaECS archetype-based entity
component system.

```xml
<PackageReference Include="DeltaECS" Version="*" />
```

The runtime package contains the `Delta.ECS` assembly and does not depend on
the build-time source generator. Install `DeltaECS.Generators` separately when
consumer-assembly generation for typed callbacks is required.

The package ships `netstandard2.1` and `net10.0` runtime assets. Use the
`netstandard2.1` asset for hosts exposing that API profile, including Unity
projects whose selected compatibility profile is .NET Standard 2.1. The
`net10.0` asset uses the runtime's ref-backed row implementation; the
`netstandard2.1` asset uses the equivalent compatibility implementation while
preserving the same public API.

## Quick start

Register component types, create an entity, and run a typed query:

```csharp
using System;
using System.Numerics;
using Delta.ECS;

var layouts = new ComponentRegistry();
using var world = new World(layouts);

layouts.Register<Position>(new SchemaId(1));
layouts.Register<Velocity>(new SchemaId(2));
layouts.Register<Paused>(new SchemaId(3));

Entity player = world.Create<Position>();
world.GetRef<Position>(player) = new Position(Vector2.Zero);
world.Add(player, new Velocity(new Vector2(2, 1)));

Query moving = world.WhereAll<Position, Velocity>().WhereNone<Paused>();
world.ForEach(in moving,
    static (ref Position position, in Velocity velocity) =>
        position.Value += velocity.Value).Invoke();

Console.WriteLine(world.Get<Position>(player).Value.X); // 2

public record struct Position(Vector2 Value);
public record struct Velocity(Vector2 Value);
public record struct Paused;
```

The package also includes offline API guides under `docs/`:

- [Choose an API](docs/API-SELECTION.md)
- [API grammar](docs/API-GRAMMAR.md)
- [API examples](docs/API-EXAMPLES.md)

Start with the [API examples](docs/API-EXAMPLES.md) for query chains,
iteration, and structural operations.

Zero-component callbacks

Non-entity callbacks that receive no component parameters use zero-arity anchors
and throw `InvalidOperationException` when called. Entity-aware callbacks may
omit component parameters in `ForEachEntity` and `ForEachEntityParallel`
because they still receive the current `EntityRef`; the generator emits those
entity-only forms as well. The same rule applies to delegate, functor, and
parallel APIs.
