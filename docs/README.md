# DeltaECS

DeltaECS is a standalone archetype-based entity-component-system library for
fast, typed iteration and immediate world updates in .NET applications.

## What it provides

- Entities with compact, reusable identities and generation safety.
- Components registered by `ComponentId`, including multiple registrations of
  the same CLR type.
- Queries that select entities by required and optional component sets.
- Typed `ForEach` callbacks and struct functors with read/write access modes.
- Explicit chunk traversal for systems that need lower-level control.
- Ordered entity sequences with filtering and structural batch operations.
- Mutation stamps for inexpensive change observation by integrations.

## Quick start

Install the runtime package and source generator in the consumer project:

```xml
<PackageReference Include="DeltaECS" Version="0.0.10" />
<PackageReference Include="DeltaECS.Generators" Version="0.0.10"
                  OutputItemType="Analyzer" />
```

```csharp
using Delta.ECS;

var world = new World();
var positionId = world.Register<Position>();
var velocityId = world.Register<Velocity>();
var entities = new Entity[1_000];
world.Create(stackalloc[] { positionId, velocityId }, entities);

var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
world.ForEach(in query,
    static (ref Position position, in Velocity velocity) =>
        position.X += velocity.X);

public struct Position { public float X; }
public struct Velocity { public float X; }
```

The callback updates every matching position. The generator emits the callback
shape used by the consumer; no handwritten overload for the component count is
required.

## Core concepts

`World` owns entities and components. A `QuerySpec` describes selection, and
`CreateQuery` produces a reusable query. `ForEach` is the convenient terminal
operation; `BeginScope` exposes borrowed chunk and slot views for systems that
need explicit traversal. Access tokens and row references are validated at
their public boundary and borrowed for the duration of the scope.

```text
World → QuerySpec → Query → ForEach callback
                       └── BeginScope → Chunks → Slots → typed row reference
```

Read parameters may be `in` or `ref readonly`; writable parameters use `ref`.
By-value parameters are read-only copies. A non-capturing static callback can
use the optional interceptor path for the fastest generated execution; ordinary
delegate and functor fallbacks remain available.

For ordered candidates, use `world.From(entities).Where(in query)` and finish
with `ForEachEntity`, `Add`, `Remove`, or `Destroy`. The sequence is non-owning:
the caller retains ownership of the input entity storage.

## Capabilities and limits

- Targets modern .NET runtimes supported by the package; the generator package
  is a build-time analyzer and is not deployed with the application.
- Structural changes are immediate; callers do not need a command-buffer phase.
- Query and sequence views are borrowed and must not outlive their scope.
- A component-bearing generated callback supports up to 256 component
  parameters. This is a source-generation limit, not a limit on registered
  component IDs.
- Mutation stamps track ECS writes. Mutating fields inside a reference-type
  component obtained by reference remains the caller's responsibility.
- Interceptors are optional and apply only to eligible call sites; unsupported
  callback shapes use the normal generated/delegate path.

## Packages and examples

- [Runtime package guide](packages/DeltaECS.README.md)
- [Source generator guide](packages/DeltaECS.Generators.README.md)
- [Runnable samples](../samples)

## Further reading

- [Public API map](APIMAP.md)
- [API map](APIMAP.md)
- [Integration API](src/DeltaECS/API/README.md)
- [License](../LICENSE)
