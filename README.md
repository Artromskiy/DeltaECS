# DeltaECS

DeltaECS is an archetype-based entity-component system for .NET. It combines
ordinary CLR component types with generated typed APIs for queries, iteration
and structural changes, so systems can describe their component access directly
in C#.

```csharp
var moving = world
    .WhereAll<Position, Velocity>()
    .WhereNone<Paused>();

world.ForEach(in moving,
    static (ref Position position, in Velocity velocity) =>
        position.X += velocity.X);
```

## What DeltaECS provides

- Archetype queries that select entities by the components they have.
- Generated typed `ForEach` callbacks with explicit read and write access.
- Matching API forms for CLR component types and explicit `ComponentId`s.
- Query-wide, entity-list and single-entity operations with a shared argument
  order.
- Caller-owned callback context, tuple context and reusable struct functors.
- Entity-aware, parallel, stamp-only and predicate-filtered iteration.
- Immediate `Add`, `Remove`, `Set`, `Create` and `Destroy` operations.
- Data-less tag components for composition filters and entity membership tests.
- Optional Roslyn interception for eligible static callbacks.

Components are regular C# types; no DeltaECS attributes are required.

## Install

Reference the runtime and generator from the project that contains your systems.
Keep both packages on the same version. The generator is a build-time dependency
and is not copied to the application at runtime.

```xml
<ItemGroup>
  <PackageReference Include="DeltaECS" Version="0.0.32" />
  <PackageReference Include="DeltaECS.Generators" Version="0.0.32" PrivateAssets="all" />
</ItemGroup>
```

`DeltaECS.Systems` is an optional package for applications that want the
system-scheduler API. A project can use the ECS runtime and generator without
it.

## Quick start

This complete example registers two component types, creates an entity, runs a
system and prints `12`:

```csharp
using System;
using Delta.ECS;

var layouts = new ComponentLayoutRegistry();
ComponentId positionId = layouts.Register<Position>(new SchemaId(1));
ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(2));

using var world = new World(layouts);
Entity entity = world.Create(positionId, new Position { X = 10 });
world.Add(entity, velocityId, new Velocity { X = 2 });

Query moving = world.WhereAll<Position, Velocity>();
world.ForEach(in moving,
    static (ref Position position, in Velocity velocity) =>
        position.X += velocity.X);

Console.WriteLine(world.Get<Position>(entity).X); // 12

public struct Position
{
    public float X;
}

public struct Velocity
{
    public float X;
}
```

The component layouts must be registered before creating entities that use
them. The generator sees API calls in the consuming project and emits the
typed overloads used by that project.

## Components, registrations and entities

A CLR type describes the data layout used by a callback. A `ComponentId`
identifies a registration of that type in a world, and a `SchemaId` identifies
the registered layout. Most applications use one registration per CLR type and
can use the generic convenience APIs. Pass `ComponentId`s when a type has
multiple registrations or when the selected registration is known only at
runtime.

```csharp
ComponentId localPositionId = layouts.Register<Position>(new SchemaId(10));
ComponentId worldPositionId = layouts.Register<Position>(new SchemaId(11));

world.Add(entity, worldPositionId, new Position { X = 100 });
Position local = world.Get<Position>(entity, localPositionId);
```

Entity handles are world-independent values; use `world.IsAlive(entity)` to
check whether a handle is alive in a particular world. `default(Entity)` is
invalid.

### Data-less tags

Register a value type with no instance fields as a tag. It participates in
archetype composition without storing a value for each entity:

```csharp
ComponentId deadId = layouts.RegisterTag<Dead>(new SchemaId(20));

world.Add<Dead>(entity);
bool isDead = world.Has<Dead>(entity);
world.Remove<Dead>(entity);

public struct Dead { }
```

Tags work with `WhereAll`, `WhereAny` and `WhereNone` just like other
components. Include a tag in a query filter when you need membership, and omit
it from the iteration selector when you do not need a value.

## One API grammar

DeltaECS keeps a common argument order across the generated API families:

```text
target (e | E)?, query (Q)?, component-types (T... | inferred)?,
registrations (I...)?, context (C)?, callback (A | F)?,
values (V...)?, options (N | O | W)?
```

| Symbol | Meaning |
| --- | --- |
| `e` | One `Entity` |
| `E` | An entity array or `ReadOnlySpan<Entity>` |
| `Q` | A `Query` |
| `T...` | CLR component types |
| `I...` | Positional `ComponentId` registrations |
| `C` | Caller-owned callback context |
| `A` | Delegate or lambda callback |
| `F` | Struct functor |
| `V...` | Component values |
| `N` | Entity count |
| `O` | Caller-owned `Span<Entity>` output |
| `W` | Requested worker count |

`T...` describes the CLR component types used by the callback. Positional
`I...` values optionally select a specific registration for each component;
when omitted, the primary registration for each type is used. The query is
required for world-wide traversal; an entity-list target can optionally add a
query filter.

The full canonical grammar, including generated overload shapes, is in the
[API grammar](docs/API-GRAMMAR.md).

### Iteration forms

These forms share the same target, query, type, registration, context and
callback order:

```text
world.ForEach<T...>(Q, C?, A | F)
world.ForEach(Q, I..., C?, A | F)
world.ForEach<T...>(Q, I..., C?, A | F)

world.ForEach<T...>(E, Q?, C?, A | F)
world.ForEach(E, Q?, I..., C?, A | F)
world.ForEach<T...>(E, Q?, I..., C?, A | F)

world.ForEachParallel<T...>(Q, C?, A | F, W)
world.ForEachParallel(Q, I..., C?, A | F, W)
world.ForEachEntityParallel<T...>(Q, I..., C?, A | F, W)

world.ForEachParallel<T...>(E, Q?, I..., C?, A | F, W)
world.ForEachEntityParallel<T...>(E, Q?, I..., C?, A | F, W)
```

The same forms exist for `ForEachEntity`, `ForEachStamp`, and
`ForEachEntityStamp`. `ForEachEntity` adds the current `Entity` as the first
callback row argument, ahead of selected component rows. Entity-only
`ForEachEntity` is supported; ordinary `ForEach` requires at least one
component.

### Generated API matrix

`ForEach` passes component rows to the callback. `ForEachEntity` passes the
current `Entity` first, then the component rows. Both families support a
callback delegate or struct functor, optional context, typed component rows,
and positional registration IDs.

```text
world.ForEach<T...>(Q, I..., C?, A | F)
world.ForEachEntity<T...>(Q, I..., C?, A | F)

world.ForEach<T...>(E, Q?, I..., C?, A | F)
world.ForEachEntity<T...>(E, Q?, I..., C?, A | F)

world.ForEachParallel<T...>(Q, I..., C?, A | F, W)
world.ForEachEntityParallel<T...>(Q, I..., C?, A | F, W)

world.ForEachParallel<T...>(E, Q?, I..., C?, A | F, W)
world.ForEachEntityParallel<T...>(E, Q?, I..., C?, A | F, W)
```

`T...` and `I...` are positional: each ID must identify a registration whose
runtime type matches the corresponding callback component type. If callback
types are inferable, the non-generic form can take `I...` without explicit
type arguments.

Stamp iteration follows the same target and selector grammar but passes
`Stamp` values instead of component values:

```text
world.ForEachStamp<T...>(Q, I..., C?, A | F)
world.ForEachEntityStamp<T...>(Q, I..., C?, A | F)
world.ForEachStamp<T...>(E, Q?, I..., C?, A | F)
world.ForEachEntityStamp<T...>(E, Q?, I..., C?, A | F)

world.ForEachStampParallel<T...>(Q, I..., C?, A | F, W)
world.ForEachEntityStampParallel<T...>(Q, I..., C?, A | F, W)
world.ForEachStampParallel<T...>(E, Q?, I..., C?, A | F, W)
world.ForEachEntityStampParallel<T...>(E, Q?, I..., C?, A | F, W)
```

`ForEachStamp` callbacks receive only stamps. `ForEachEntityStamp` callbacks
receive `Entity` first. Stamp parameters are read-only (`in` or
`ref readonly`), and stamp iteration does not mark components written.

Structural forms use the same targets and registration selection:

```text
world.Add<T...>(e | E | Q, I...)
world.Remove<T...>(e | E | Q, I...)
world.Destroy(e | E | Q)

world.Add<T...>(e, V...)
world.Set<T...>(e, V...)
world.Create<T...>(N, O?)
world.Create<T...>(I..., N, O?)
world.Create(I..., N, O?)
```

For query-wide value predicates, use `Where` or `WhereEntity` and then choose
one terminal:

```text
world.Where(Q, C?, Predicate) -> view
world.WhereEntity(Q, C?, Predicate) -> view

view.Destroy()
view.Add<T...>()
view.Add<T...>(I...)
view.Remove<T...>()
view.Remove<T...>(I...)
view.ForEach(...)
view.ForEachEntity(...)
```

The full grammar also documents inferred-value forms, argument modifiers and
zero-arity anchor methods.

## Queries and filters

Queries describe component composition; they are reusable handles, not saved
lists of entity handles. The three query operators express the `All`, `Any` and
`None` sets:

```csharp
Query combatants = world
    .WhereAll<Position, Health, Human>()
    .WhereNone<Dead, Escaped>()
    .WhereAny<Armed, Berserk>();
```

- `WhereAll<A, B>()` selects entities that contain both `A` and `B`.
- `WhereAny<A, B>()` selects entities that contain at least one of `A` or `B`.
- `WhereNone<A, B>()` excludes entities that contain either `A` or `B`.

Each call returns a new query handle, and the chain can continue from that
handle. The first call is made on `World`; later calls are made on `Query`.
The same factories accept explicit IDs:

```csharp
Query selected = world
    .WhereAll(positionId, healthId)
    .WhereNone(deadId)
    .WhereAny(armedId, berserkId);
```

A query filter does not mean every entity has every callback component. In
particular, `WhereAny<A, B>()` allows matches that contain only one of them;
request only components that are present for every selected entity.

## Iteration and access modes

Use `ForEach` when a system needs component values, and `ForEachEntity` when it
also needs identity:

```csharp
world.ForEach(in combatants,
    static (ref Position position, in Health health) =>
        position.X += health.Value);

world.ForEachEntity(in combatants,
    static (Entity entity, ref Position position, in Health health) =>
    {
        position.X += health.Value;
        Report(entity);
    });
```

Callback parameter modifiers declare access:

- `ref T` provides writable access to a component row. The write is visible in
  the world immediately.
- `in T` and `ref readonly T` provide read-only access without copying the
  component value.
- A value parameter receives a value copy.

Select only component rows that the callback actually needs. For a query with
tags, the tag can stay in the query filter without appearing in the callback.

### Iterate a caller-provided entity list

Use an entity-list target when processing a known set of candidates. An
optional query further filters that set:

```csharp
ReadOnlySpan<Entity> candidates = entities;

world.ForEach<Position>(candidates,
    static (ref Position position) => position.X = 0);

world.ForEachEntity<Position>(candidates, in combatants,
    static (Entity entity, in Position position) => Log(entity, position));
```

The entity-list form preserves the caller's target set; a world-wide query
iteration processes all entities matching the query. Use the form that states
which population the system owns.

### Pass state without a closure

Pass context before the callback. The context can be a small struct or a tuple:

```csharp
var state = (DeltaTime: 0.016f, Updated: 0);

world.ForEach(in moving, ref state,
    static (ref (float DeltaTime, int Updated) state,
        ref Position position, in Velocity velocity) =>
    {
        position.X += velocity.X * state.DeltaTime;
        state.Updated++;
    });

Console.WriteLine(state.Updated);
```

The caller passes mutable state with `ref`; its changes are visible when the
call returns. A static callback can use that state without capturing locals.
Entity-aware callbacks receive `Entity` after the context parameter. Tuple
element names help readability but are not part of the CLR type; use distinct
named context structs when separate callback shapes need distinct context
types.

### Use a struct functor

For reusable callback logic, implement the matching functor contract and pass
the value explicitly:

```csharp
public struct Move : IForEach
{
    public float DeltaTime;

    public void Invoke(ref Position position, in Velocity velocity)
    {
        position.X += velocity.X * DeltaTime;
    }
}

var move = new Move { DeltaTime = 0.016f };
world.ForEach(in moving, ref move);
```

The generator derives component types and access modes from `Invoke` and emits
the corresponding typed call shape. Entity-aware functors use the
`IForEachEntity` family. Context functors are available when callback context
and functor state are separate concerns.

## Stamp iteration and change tracking

Stamp iteration reads component revision tokens instead of component values.
Use it when the consumer needs to detect changes but does not need the row
itself:

```csharp
world.ForEachStamp<Health>(in combatants,
    static (in Stamp stamp) => Observe(stamp));

world.ForEachEntityStamp<Health>(in combatants,
    static (Entity entity, in Stamp stamp) => Observe(entity, stamp));
```

Stamp callbacks are read-only. They do not update stamps. Type selectors and
explicit `ComponentId` selectors follow the same query and entity-list forms
as ordinary iteration; parallel stamp variants are available as well.

Stamps are revision/equality tokens, not wall-clock timestamps. Compare an old
token with a new token to determine whether the tracked component changed.
See the [stamp guide](docs/src/DeltaECS/Stamps/README.md) for the precise
contract.

## Parallel iteration

The parallel family mirrors ordinary typed and entity-aware iteration:

```csharp
world.ForEachParallel<Position, Velocity>(in moving,
    static (ref Position position, in Velocity velocity) =>
        position.X += velocity.X,
    workerCount: 4);

world.ForEachEntityParallel(in combatants,
    static (Entity entity, in Health health) => Report(entity, health),
    workerCount: 4);
```

The caller chooses `workerCount`; zero selects the runtime default. The
requested count is clamped to the supported limit and available work. Parallel
callbacks can use read-only or by-value context; mutable shared `ref` context
is intentionally not part of this API. Make sure writes from different
entities do not race through shared state or referenced objects.

The low-level chunk API is documented separately. Most systems should start
with the generated typed callback forms above.

## Structural operations

Structural methods can target one entity, a supplied entity list, or a query.
They take effect before returning:

```csharp
world.Add<Dead>(entity);
world.Remove<Paused>(entity);
world.Destroy(entity);

int removed = world.Remove<Velocity>(entities);
int destroyed = world.Destroy(in combatants);
```

Batch and query operations return the number of entities changed. Components
added without an explicit value start at their default value. Multi-component
forms are generated too:

```csharp
world.Add<Position, Velocity>(entities);
world.Remove<Position, Velocity>(entities);

world.Add(entity,
    new Weapon { Id = equippedWeapon },
    new Damage { Value = baseDamage },
    new Attack { Cooldown = 0 });
```

Use positional `ComponentId`s if the target registration is selected at
runtime:

```csharp
world.Add(entity, velocityId);
world.Remove(entities, velocityId);
world.Add(in combatants, velocityId);
```

Do not perform structural changes from inside an active iteration callback.
Finish traversal first, then issue the structural operation; `Where` terminals
handle their own query scope as described below.

### Create entities

Create one typed entity or a batch:

```csharp
Entity entity = world.Create<Position, Velocity>();

Span<Entity> createdEntities = stackalloc Entity[128];
int created = world.Create<Position, Velocity>(128, createdEntities);
```

When only the component registrations are known at runtime, supply IDs before
the count:

```csharp
int createdById = world.Create(
    positionId,
    velocityId,
    128,
    createdEntities);
```

Output storage is optional. Supply it when the caller needs to retain the
created handles; otherwise use the count-only form.

### Add, Set and Has

`Add` creates a missing component; the generated multi-value form can add and
initialize several components in one call. `Set` replaces the value of a
component already present on the entity and fails fast when the entity is
stale or the component is absent. Use `TryGet` when presence is optional.

```csharp
world.Add(entity, new Position { X = 5 }, new Velocity { X = 2 });
world.Set(entity, new Position { X = 10 });

if (world.Has<Velocity>(entity))
{
    ref Velocity velocity = ref world.GetRef<Velocity>(entity);
    velocity.X *= 2;
}

ref readonly Position position = ref world.GetReadRef<Position>(entity);
Console.WriteLine(position.X);
```

Keep a reference returned by `GetRef` or `GetReadRef` within the period where
the entity's component storage remains in place. A structural change can move
the entity to another archetype; reacquire the reference afterwards.

## Predicate-filtered operations

`Where` applies a read-only predicate to every entity matched by the query.
Use `WhereEntity` when the predicate needs the entity handle:

```csharp
int destroyed = world.Where(in combatants,
        static (in Health health) => health.Value <= 0)
    .Destroy();

int marked = world.WhereEntity(in combatants,
        static (Entity entity, in Health health, in Team team) =>
            health.Value <= 0 && team.Id == 1)
    .Add<Dead>();
```

Predicates receive component references as `in` or `ref readonly`. They may
inspect values, not mutate components. If a callback must update selected
components, use a terminal iteration:

```csharp
world.Where(in combatants,
        static (in Health health) => health.Value > 0)
    .ForEach(static (ref Health health) => health.Value++);
```

Available terminals include `Destroy`, `Add`, `Remove`, `ForEach` and
`ForEachEntity`. The view is stack-only, and every terminal completes the
operation immediately. Structural terminals finish the query scope before
applying ordinary immediate structural changes; no command is queued for a
later update.

## Source generation and interception

`DeltaECS.Generators` emits only the API shapes needed by calls in the
consuming assembly. It supplies typed query factories and generated
multi-component iteration and structural overloads. You do not edit generated
files or add component attributes.

Static callbacks work with the ordinary generated API. On a compiler that
supports C# interceptors, eligible static non-capturing callbacks can use the
optional interception path while keeping the same source call. Interception is
an optimization choice, not a requirement for using DeltaECS. See the
[generator guide](docs/packages/DeltaECS.Generators.README.md) for configuration
and callback coverage.

## Runtime and packages

| Package | Purpose |
| --- | --- |
| [`DeltaECS`](docs/packages/DeltaECS.README.md) | ECS world, components, queries and operations |
| [`DeltaECS.Generators`](docs/packages/DeltaECS.Generators.README.md) | Consumer-side generated typed API |
| [`DeltaECS.Systems`](docs/packages/DeltaECS.Systems.README.md) | Optional system scheduler |

The runtime targets .NET Standard 2.1 and .NET 10. The generator targets .NET
Standard 2.0 as a compiler tool. `World` owns its component storage and should
be disposed when the application no longer needs it.

## Performance and benchmark results

DeltaECS is designed for dense component iteration and explicit component
access. Performance depends on the workload, runtime, component layout and
callback body. The benchmark page contains the measured workload matrix,
methodology and complete results; consult those tables rather than treating a
single headline number as representative.

- [Complete ECS C# benchmark results](docs/benchmarks/ecs-csharp-results.md)
- [Benchmark methodology](docs/benchmarks/ecs-csharp-benchmark.md)

## Documentation

- [GitHub Wiki](https://github.com/Artromskiy/DeltaECS/wiki) — tutorials and
  user-facing topic pages.
- [API grammar](docs/API-GRAMMAR.md) — canonical overload shapes and argument
  order.
- [API cookbook](docs/usage-examples.md) — focused examples and code-generation
  scenarios.
- [Documentation index](docs/README.md) — package and topic guides.
- [Runnable .NET / NativeAOT sample](samples/DeltaECS.AotSample/Program.cs).
