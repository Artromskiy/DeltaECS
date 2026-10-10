
---

# DeltaECS

**Fast execution. Expressive C#.**  
An archetype ECS for .NET and Unity, with generated typed iteration and composable queries.

- **Fast iteration** over dense component storage, with benchmarks against other C# ECS libraries.
- **Typed systems** generated at build time from the API shapes your code uses.
- **Clear access rules:** `in` to read a component, `ref` to change it.
- **Composable queries:** combine component filters, predicates and ordering.
- **Flexible iteration:** use lambdas, reusable functors, caller-owned context, entity-aware and parallel forms.
- **No component ceremony:** components are ordinary C# types; no ECS attributes are required.

## Install

Add the runtime and source generator to your project:

```xml
<ItemGroup>
  <PackageReference Include="DeltaECS" Version="*" />
  <PackageReference Include="DeltaECS.Generators" Version="*" PrivateAssets="all" />
</ItemGroup>
```

## Quickstart

Register components, create an entity, then describe a system with a query and a typed callback:

```csharp
using System;
using Delta.ECS;

var layouts = new ComponentLayoutRegistry();
using var world = new World(layouts);

layouts.Register<Position>(new SchemaId(1));
layouts.Register<Velocity>(new SchemaId(2));
layouts.Register<Paused>(new SchemaId(3));

Entity player = world.Create(new Position(0, 0);
world.Add(player, new Velocity(2, 1));

Query moving = world
    .WhereAll<Position, Velocity>()
    .WhereNone<Paused>();

world.ForEach(in moving,
    static (ref Position position, in Velocity velocity) =>
    {
        position.X += velocity.X;
        position.Y += velocity.Y;
    }).Invoke();

Console.WriteLine(world.Get<Position>(player).X); // 2

public record struct Position(float X, float Y);
public record struct Velocity(float X, float Y);
public record struct Paused;
```

The callback says exactly what the system does: it reads `Velocity`, writes `Position`, and processes only entities matching the query. `ForEach` returns a reusable operation; `Invoke()` runs it.

## Expressive by design

Build a query from component membership, then refine it with predicates or ordering:

```csharp
var candidates = world
    .WhereAll<Health, Enemy>()
    .WhereNone<Paused>();

var ordered = candidates
    .OrderBy(ref priorityComparer)
    .ThenBy(ref stableIdComparer);

Entity next = ordered.First().Invoke();
```

The same API also supports explicit `ComponentId`s, entity lists, struct functors, caller-owned context, tags, structural operations and parallel iteration. See the [API grammar](docs/API-GRAMMAR.md) for the available forms.

## Fast, and measured

DeltaECS is designed for very fast dense iteration. We publish comparative results across different component counts, entity compositions and execution modes; performance depends on the workload and hardware, so check the scenario closest to yours.

- [Full benchmark results and methodology](docs/benchmarks/ecs-csharp-results.md)
- [Benchmark runs and summaries on GitHub Actions](https://github.com/Artromskiy/DeltaECS/actions/workflows/ecs-csharp-benchmark.yml)

## Unity

See DeltaECS used in the [Sky Pirates Unity project](https://github.com/Artromskiy/SkyPirates), including its [entity-view system](https://github.com/Artromskiy/SkyPirates/blob/main/Assets/Scripts/SkyPirates/Client/Systems/EntityViewSystem.cs).

## Learn more

- [API cookbook](docs/usage-examples.md)
- [API grammar](docs/API-GRAMMAR.md)
- [GitHub Wiki](https://github.com/Artromskiy/DeltaECS/wiki)
- [Runtime package](docs/packages/DeltaECS.README.md)
- [Source generator](docs/packages/DeltaECS.Generators.README.md)

---

Файлы не менял. Я намеренно оставил заявление о скорости сильным, но подкрепил его ссылками на реальные сравнительные результаты вместо обещания «самая быстрая» для всех сценариев.
