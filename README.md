# DeltaECS

DeltaECS is a standalone archetype-based entity-component-system library for
fast, typed iteration and immediate world updates in .NET applications.

## What it provides

- Compact entities with generation safety.
- Component registration by `ComponentId`.
- Reusable queries over required and optional component sets.
- Typed `ForEach` callbacks and struct functors with read/write modes.
- Explicit chunk traversal for systems needing lower-level control.
- Ordered entity sequences with filtering and batch structural operations.
- Mutation stamps for inexpensive change observation by integrations.

## Quick start

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

The callback updates every matching position. The source generator emits the
callback shape used by the consumer, so component count is not limited to a
handwritten overload set.

## Core concepts

`World` owns entities and components. `QuerySpec` describes selection and
`CreateQuery` produces a reusable query. `ForEach` is the convenient terminal
operation; `BeginScope` exposes borrowed chunk and slot views for explicit
traversal.

Read parameters use `in` or `ref readonly`; writable parameters use `ref`.
Non-capturing static callbacks can use the optional interceptor path for the
fastest generated execution. For ordered candidates, use
`world.From(entities).Where(in query)` and finish with `ForEachEntity`, `Add`,
`Remove`, or `Destroy`.

## Capabilities and limits

- Targets modern .NET runtimes supported by the package.
- Structural changes are immediate; no command-buffer playback phase is
  required.
- Query and sequence views are borrowed and must not outlive their scope.
- Generated component callbacks support up to 256 component parameters; this
  is a source-generation limit, not a limit on registered component IDs.
- Mutation stamps track ECS writes. Changes made inside reference-type
  components remain the caller's responsibility.
- Interceptors are optional and apply only to eligible call sites.

## Packages and examples

- [Runtime package guide](docs/packages/DeltaECS.README.md)
- [Source generator guide](docs/packages/DeltaECS.Generators.README.md)
- [Runnable samples](samples)

## Further reading

- [Public API map](docs/APIMAP.md)
- [API map](docs/APIMAP.md)
- [Integration API](docs/src/DeltaECS/API/README.md)
- [Documentation index](docs/README.md)
