# DeltaECS

**Fast execution. Expressive C#.**

An archetype ECS for .NET and Unity, with generated typed iteration and composable queries.

- Fast iteration over dense component storage, with benchmarks against other C# ECS libraries.
- Typed systems generated at build time from the API shapes your code uses.
- Clear access rules: `in` to read a component, `ref` to change it.
- Composable queries: combine component filters, predicates and ordering.
- Flexible iteration: use lambdas, reusable functors, caller-owned context, entity-aware and parallel forms.
- No component ceremony: components are ordinary C# types; no ECS attributes are required.

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
using System.Numerics;
using Delta.ECS;

var layouts = new ComponentLayoutRegistry();
using var world = new World(layouts);

layouts.Register<Position>(new SchemaId(1));
layouts.Register<Velocity>(new SchemaId(2));
layouts.Register<Paused>(new SchemaId(3));

Entity player = world.Create(new Position(Vector2.Zero));
world.Add(player, new Velocity(new Vector2(2, 1)));

Query moving = world
    .WhereAll<Position, Velocity>()
    .WhereNone<Paused>();

world.ForEach(in moving,
    static (ref Position position, in Velocity velocity) =>
    {
        position.Value += velocity.Value;
    }).Invoke();

Console.WriteLine(world.Get<Position>(player).Value.X); // 2

public record struct Position(Vector2 Value);
public record struct Velocity(Vector2 Value);
public record struct Paused;
```

The callback says exactly what the system does: it reads `Velocity`, writes `Position`, and processes only entities matching the query. `ForEach` returns a reusable operation; `Invoke()` runs it.

## Expressive by design

Imagine you need to deal a fixed total amount of damage to enemies within 10 meters of your unit, prioritizing closer enemies.

```csharp
const float DamageZone = 10f;
float remainingDamage = 25f;
Vector2 playerPosition = world.Get<Position>(player).Value;

Query candidates = world
    .WhereAll<Health, Enemy, Position, UniqueId>()
    .WhereNone<Paused>();

world.Where(in candidates,
        (in Position position, in Health health) =>
            health.Value > 0 && Vector2.Distance(position.Value, playerPosition) < DamageZone)
    .OrderBy((in Position left, in Position right) =>
        Vector2.Distance(left.Value, playerPosition).CompareTo(Vector2.Distance(right.Value, playerPosition)))
    .ThenBy(static (in UniqueId left, in UniqueId right) => left.Value.CompareTo(right.Value))
    .ForEach((ref Health health) =>
    {
        if (remainingDamage <= 0)
        {
            return;
        }

        float damage = MathF.Min(health.Value, remainingDamage);
        health.Value -= damage;
        remainingDamage -= damage;
    }).Invoke();
```

The query excludes paused and defeated enemies, filters to the damage zone, then orders targets by distance and unique ID. The iteration spends the damage budget in that order, never reducing health below zero. The same API also supports explicit `ComponentId`s, entity lists, struct functors, caller-owned context, tags, structural operations and parallel iteration. See the [API grammar](docs/API-GRAMMAR.md) for the available forms.

## Fast, and measured

DeltaECS is designed for very fast dense iteration. We publish comparative results across different component counts, entity compositions and execution modes; performance depends on the workload and hardware, so check the scenario closest to yours.

- [Full benchmark results and methodology](docs/benchmarks/ecs-csharp-results.md)
- Benchmark runs and summaries on GitHub Actions

## Unity

See DeltaECS used in the Sky Pirates Unity project, including its entity-view system.

## Learn more

- [API cookbook](docs/usage-examples.md)
- [API grammar](docs/API-GRAMMAR.md)
- GitHub Wiki
- [Runtime package](docs/packages/DeltaECS.README.md)
- [Source generator](docs/packages/DeltaECS.Generators.README.md)
