# DeltaECS

DeltaECS is a standalone archetype-based entity-component-system library for
fast, typed iteration and immediate world updates in .NET applications.

## What it provides

- Generation-checked entity handles and registered component types.
- Reusable queries requiring, allowing or excluding component sets.
- Typed callbacks with explicit read/write access and caller-owned context.
- Source-generated query factories, callbacks and batch component operations.
- Source-generated query-wide mutation views with predicate terminals.
- Stateful struct functors and explicit chunk traversal.
- Ordered entity sequences with filtering and structural operations.
- Component revision stamps for change observation by integrations.

## Quick start

For a .NET 10 console application, use this project file and `Program.cs`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="DeltaECS" Version="*" />
    <PackageReference Include="DeltaECS.Generators" Version="*"
                      PrivateAssets="all" />
  </ItemGroup>
</Project>
```

```csharp
using System;
using Delta.ECS;

var layouts = new ComponentLayoutRegistry();
var positionId = layouts.Register<Position>(new SchemaId(1));
var velocityId = layouts.Register<Velocity>(new SchemaId(2));
using var world = new World(layouts);

var entity = world.Create(positionId, new Position { X = 10 });
world.Add(entity, velocityId, new Velocity { X = 2 });
var query = world.WhereAll<Position, Velocity>();
world.ForEach(in query,
    static (ref Position position, in Velocity velocity) =>
        position.X += velocity.X);
Console.WriteLine(world.Get<Position>(entity).X); // 12

public struct Position { public float X; }
public struct Velocity { public float X; }
```

Register types before using them. `ComponentId` identifies a registration in
this world; `SchemaId` is the application's stable identity for that component
layout. Generic calls select a type's primary registration. Use explicit IDs
when one CLR type has several registrations.

`World` owns component storage and is disposable. `Get<T>` returns a value;
use `Set` to replace an existing component or a `ref` callback to modify a
stored struct in place. `Set<T>` fails fast when the entity is stale or the
component row is missing; use `TryGet` when the row is optional.
`default(Entity)` (`[0:0]`) is the invalid sentinel; valid entity handles use a
positive generation. Use `World.IsAlive` for world-specific liveness checks.
The generator runs during compilation in the consuming project: no attributes
or hand-written generated files are needed for these calls.

Each variation reuses the quick-start world independently. Put statements
before its component declarations.

### Read, replace and remove a component

```csharp
world.Set(entity, new Position { X = 20 });
if (world.TryGet<Position>(entity, out var position))
    Console.WriteLine(position.X); // 20
world.Remove<Velocity>(entity);
world.Destroy(entity);
Console.WriteLine(world.IsAlive(entity)); // False
```

### Create and process a batch

```csharp
var entities = new Entity[3];
world.Create(stackalloc[] { positionId }, entities);
int added = world.From(entities).Add<Velocity>(); // 3; default values
world.From(entities).Where(in query).ForEachEntity(
    static (Entity current, ref Position position, in Velocity velocity) =>
        position.X = current.Index + velocity.X);
int removed = world.Remove<Position, Velocity>(entities); // 3
world.From(entities).Destroy();
```

Sequence callbacks follow candidate order, preserve duplicates and skip stale
handles. `Where(in query)` filters those candidates. Batch structural methods
return the number of entities changed; adding an existing component preserves
its value. Components added without values start at `default`.

### Generate a stateful system

```csharp
var movement = new Movement { DeltaTime = 0.5f };
world.ForEach(in query, ref movement);
Console.WriteLine(movement.Updated); // 1
Console.WriteLine(world.Get<Position>(entity).X); // 13
```

Add this type alongside the component declarations:

```csharp
public struct Movement : IForEach
{
    public float DeltaTime;
    public int Updated;
    public void Invoke(ref Position position, in Velocity velocity)
    {
        position.X += velocity.X * DeltaTime;
        Updated++;
    }
}
```

The generator reads `Invoke` and emits the required overload. Passing the
functor by `ref` keeps its accumulated state. Lambda parameters must be typed:
`ref T` writes, `in T` / `ref readonly T` read, and `T` reads a value copy.

## Benchmark snapshot

[Complete `Ecs.CSharp.Benchmark` result table](docs/benchmarks/ecs-csharp-results.md)

## Capabilities and limits

- Runtime targets: `netstandard2.1` and `net10.0`; the console example uses
  .NET 10. Compatible Unity profiles can use the .NET Standard 2.1 asset.
- Queries are reusable selections; callback components must exist on every
  match. An `Any` match alone does not guarantee each requested component.
- Structural changes are immediate. Perform create/add/remove/destroy outside
  active iteration scopes and callbacks; collect handles for a later batch.
- Chunk and sequence views borrow storage. Keep them within their valid scope.
- Generated callbacks, structural operations and query factories support up
  to 256 component type parameters per call; registered IDs have no such cap.
- Mutation stamps track ECS writes, not mutations inside reference objects.
- Interceptors are optional; ordinary generated callbacks work without them.

## Packages and examples

- [API and code-generation cookbook](docs/usage-examples.md): query filters,
  context callbacks, explicit IDs, chunk traversal and interceptor setup.
- [Runtime package](docs/packages/DeltaECS.README.md) and
  [generator package](docs/packages/DeltaECS.Generators.README.md).
- [Runnable console / NativeAOT sample](samples/DeltaECS.AotSample/Program.cs).

- [Public behavior and documentation index](docs/README.md)
- [Generator reference](docs/src/DeltaECS.Generators/README.md)
- [Integration API](docs/src/DeltaECS/API/README.md)
