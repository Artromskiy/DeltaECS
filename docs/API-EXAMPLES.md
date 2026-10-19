# DeltaECS API and code-generation cookbook

Start with this initialized world. Each later code block is an independent
example; do not run every mutating example sequentially against the same world.
The examples reuse the `world`, `layouts`, `player`, and `moving` values and
the component registrations declared here:

```csharp
using System;
using System.Numerics;
using Delta.ECS;

var layouts = new ComponentRegistry();
using var world = new World(layouts);

layouts.Register<Position>(new SchemaId(1));
layouts.Register<Velocity>(new SchemaId(2));
layouts.Register<Paused>(new SchemaId(3));
layouts.Register<Health>(new SchemaId(4));
layouts.Register<Human>(new SchemaId(5));
layouts.Register<Dead>(new SchemaId(6));
layouts.Register<Escaped>(new SchemaId(7));
layouts.Register<Armed>(new SchemaId(8));
layouts.Register<Berserk>(new SchemaId(9));
layouts.Register<Alive>(new SchemaId(10));
layouts.Register<Team>(new SchemaId(11));

Entity player = world.Create<Position>();
world.GetRef<Position>(player) = new Position(Vector2.Zero);
world.Add(player, new Velocity(new Vector2(2, 1)));

Query moving = world
    .WhereAll<Position, Velocity>()
    .WhereNone<Paused>();
world.ForEach(in moving,
    static (ref Position position, in Velocity velocity) =>
        position.Value += velocity.Value).Invoke();
Console.WriteLine(world.Get<Position>(player).Value.X); // 2

ComponentId positionId = layouts.GetPrimary<Position>();
ComponentId velocityId = layouts.GetPrimary<Velocity>();
ComponentId healthId = layouts.GetPrimary<Health>();
ComponentId deadId = layouts.GetPrimary<Dead>();
ComponentId aliveId = layouts.GetPrimary<Alive>();
ComponentId armedId = layouts.GetPrimary<Armed>();
ComponentId berserkId = layouts.GetPrimary<Berserk>();
Entity entity = player;
Query query = moving;
Query healthQuery = world.WhereAll<Health, Team>();
```

Place any additional component or functor types at the end of `Program.cs`,
after all top-level statements.

## What needs the generator?

| Operation | Provided by |
|---|---|
| Register components, create entities, `Get` / `GetRef` / `TryGet` | Runtime |
| Single-component `Add<T>(entity, value)` / `Remove<T>(entity)` | Runtime |
| `QuerySpec`, `CreateQuery` | Runtime |
| Typed `WhereAll<T...>` / `WhereAny<T...>` / `WhereNone<T...>` | Generator |
| `WhereAll` / `WhereAny` / `WhereNone` with `ComponentId` values | Runtime |
| Component-bearing `ForEach` / `ForEachEntity` | Generator |
| Positional `ComponentId` selectors for `ForEach` / `ForEachEntity` | Generator |
| Struct functor overloads inferred from `Invoke` | Generator |
| Generic batch `Add<T...>` / `Remove<T...>` | Generator |
| Positional `ComponentId` `Create(I..., N, O?)` forms | Generator |
| Query-wide `Where(...).Destroy/Add/Remove/ForEach` | Generator |

Keep `using Delta.ECS;` in the consuming source and reference the analyzer in
that same project. It generates only the shapes used there. A runtime-only
reference does not supply generated extensions. The analyzer package is build
input, not a runtime dependency; `PrivateAssets="all"` keeps it private to the
consumer. Component types need no generator-specific attributes.

## Select entities by their component sets

```csharp
var all = world.WhereAll<Position, Velocity>();
var any = world.WhereAny<Position, Velocity>();
var none = world.WhereNone<Velocity>();

world.ForEachEntity(in any,
    static (EntityRef current) => Console.WriteLine(current.Index)).Invoke();
```

`All` requires every listed component, `Any` requires at least one, and `None`
excludes every listed component. These are reusable queries, not entity
snapshots. The next execution sees current matching entities. Do not request
both component values in a callback on `any`: some matches may have only one.

Compose a typed query by extending the previous `Query`:

```csharp
var humanCombatants = world
    .WhereAll<Position, Health, Human>()
    .WhereNone<Dead, Escaped>()
    .WhereAny<Armed, Berserk>();
```

The first factory is an extension on `World`; every following call is an
extension on `Query` and returns a new query handle. `WhereAll` adds to the
`All` mask, `WhereNone` to `None`, and `WhereAny` to the shared `Any` mask.
Each composed specification goes through `World.CreateQuery`, so equivalent
chains reuse the existing query-plan cache.

The same factories accept positional `ComponentId` values when the component
registration is selected at runtime:

```csharp
var explicitCombatants = world
    .WhereAll(positionId, healthId)
    .WhereNone(deadId)
    .WhereAny(armedId, berserkId);
```

These runtime overloads use the same `World`/`Query` chaining and query-plan
cache as the generated typed factories.

The same positional IDs can select callback rows when a CLR type has multiple
registrations:

```csharp
world.ForEach(in query, positionId, velocityId,
    static (ref Position position, in Velocity velocity) =>
        position.Value += velocity.Value).Invoke();
```

For combined conditions, continue the query chain with explicit IDs:

```csharp
var stationary = world
    .WhereAll(positionId)
    .WhereNone(velocityId);
world.Remove<Velocity>(entity);
world.ForEach(in stationary,
    static (ref Position position) => position.Value = Vector2.Zero).Invoke();
Console.WriteLine(world.Get<Position>(entity).Value.X); // 0
```

## Pass time and accumulate results without a closure

```csharp
var step = new Step { DeltaTime = 0.5f };
var operation = world.ForEach<Step, Position, Velocity>(in query, ref step,
    static (ref Step state, ref Position position, in Velocity velocity) =>
    {
        position.Value += velocity.Value * state.DeltaTime;
        state.Updated++;
    });
operation.Invoke(ref step);
Console.WriteLine(step.Updated); // 1
Console.WriteLine(world.Get<Position>(entity).Value.X); // 3
```

Context comes before components and is passed by `ref`. For a callback that
also receives identity, use `ForEachEntity` and put `EntityRef` after context.
Functor equivalents implement `IForEachContext<TContext>` or
`IForEachContextEntity<TContext>`; component parameters still come from
`Invoke`. The [API grammar](API-GRAMMAR.md) lists the available context and
entity-aware callback shapes.

The same functor contracts work after a query-wide `Where`. A predicate uses
`IWherePredicate` and remains read-only; a terminal functor can mutate
components. Prepare a matching entity for this example:

```csharp
Entity injured = world.Create<Health, Team>();
world.GetRef<Health>(injured).Value = 0;
world.GetRef<Team>(injured) = new Team { Id = 1, DefaultHealth = 100 };

var predicate = new IsDeadPredicate();
var action = new ResetHealthAction();
world.Where(in healthQuery, ref predicate)
    .ForEach(ref action)
    .Invoke(ref action);
```

The predicate and terminal functors receive only their declared component
parameters. Use `WhereEntity` when the predicate also needs the current `Entity`,
and `ForEachEntity` when the terminal needs an `EntityRef`. Static-lambda
terminals can be lowered by the optional interceptor path to the same chunk
execution as ordinary generated `ForEach`.

## Use explicit component IDs

This is the runtime query equivalent of `world.WhereAll<Position, Velocity>()`:

```csharp
var explicitQuery = world.WhereAll(positionId, velocityId);
ref Position position = ref world.GetRef<Position>(entity, positionId);
position.Value = new Vector2(30, position.Value.Y);
Console.WriteLine(world.Get<Position>(entity, positionId).Value.X); // 30
```

Typed convenience calls use the primary registration of each CLR type. If a
type represents several distinct components, retain their individual IDs and
select the intended registration explicitly. Query selection and callback
access must refer to the same registrations.

`GetRef<T>` returns a writable reference to an existing row and throws when the
entity is stale or does not contain `T`. Use `TryGet` when the component may be
absent; `Add<T>` is the operation that creates a missing row.

## Build entity data and apply it

`EntityData` collects values and tags without holding a `World`. Use explicit
component IDs when building the set, then pass it to a single-entity operation:

```csharp
var initialData = new EntityData();
initialData.Add(positionId, new Position(new Vector2(4, 8)));
initialData.AddTag(aliveId);
Entity spawned = world.Create(initialData);

var movementData = new EntityData();
movementData.Add(positionId, new Position(new Vector2(12, 8)));
movementData.Add(velocityId, new Velocity(new Vector2(2, 0)));
world.AddOrSet(spawned, movementData);

var positionUpdate = new EntityData();
positionUpdate.Add(positionId, new Position(new Vector2(20, 8)));
bool updated = world.Set(spawned, positionUpdate); // true: Position is present
```

`EntityData.Add` returns `false` for an ID already in the set and leaves its
value unchanged. `AddOrSet` replaces a value already in the set. When applying
data to a world, `Add` initializes only missing components, `AddOrSet` also
replaces values already present, and `Set` writes only if every entry is
already present. Use `AddTag` for tags; they carry no value. The same data set
can be reused for later entities, and the world checks registrations and CLR
types when each operation is invoked.

## Change an explicit batch

```csharp
var candidates = new[] { entity };
int removed = world.Remove<Velocity>(candidates);
Console.WriteLine(removed); // 1
Console.WriteLine(world.TryGet<Velocity>(entity, out _)); // False
int added = world.Add<Velocity>(candidates);
Console.WriteLine(added); // 1
Console.WriteLine(world.Get<Velocity>(entity).Value.X); // 0
```

The batch overload uses only the supplied candidates. Removing a component leaves
the entity alive; destroying it invalidates its handle. Adding a component
without an initialization value gives it the default value. To initialize
while adding, use `world.Add(entity, velocityId, new Velocity(new Vector2(2, 1)))`
when that component is absent.

For a runtime-selected component set with a batch count, keep the IDs before
the count; the generator emits this positional form:

```csharp
Span<Entity> output = stackalloc Entity[2];
int created = world.Create(positionId, velocityId, 2, output);
```

## Filter the whole query before a mutation

`world.Where(in healthQuery, predicate)` creates a reusable view over entities
matched by `healthQuery`. Its predicate receives only read-only typed component
references. `WhereEntity` is the form that receives `Entity` first. Terminals
return deferred operations and execute only after `Invoke()`:

Each structural terminal below is an independent example. Its entity fixture
must match the `healthQuery`; the removal example also adds `Alive`.

```csharp
Entity target = world.Create<Health, Team>();
world.GetRef<Health>(target).Value = 0;
world.GetRef<Team>(target).Id = 1;
int destroyed = world.WhereEntity(
        in healthQuery,
        static (Entity current, in Health health) => health.Value <= 0)
    .Destroy().Invoke();
```

```csharp
Entity target = world.Create<Health, Team>();
world.GetRef<Health>(target).Value = 0;
world.GetRef<Team>(target).Id = 1;
int tagged = world.WhereEntity(
        in healthQuery,
        static (Entity current, in Health health, in Team team) =>
            health.Value <= 0 && team.Id == 1)
    .Add<Dead>().Invoke();
```

```csharp
Entity target = world.Create<Health, Team>();
world.GetRef<Health>(target).Value = 0;
world.GetRef<Team>(target).Id = 1;
world.Add(target, new Alive());
world.WhereEntity(
        in healthQuery,
        static (Entity current, in Health health, in Team team) =>
            health.Value <= 0 && team.Id == 1)
    .Remove<Alive>().Invoke();
```

Use either `in` or `ref readonly` for a zero-copy read-only component reference:

```csharp
Entity target = world.Create<Health, Team>();
world.GetRef<Health>(target).Value = 0;
world.GetRef<Team>(target).Id = 1;
world.WhereEntity(
        in healthQuery,
        static (Entity current, ref readonly Health health) => health.Value <= 0)
    .Destroy().Invoke();
```

When identity is not needed, omit the entity parameter:

```csharp
Entity target = world.Create<Health, Team>();
world.GetRef<Health>(target).Value = 0;
int destroyed = world.Where(
        in healthQuery,
        static (in Health health) => health.Value <= 0)
    .Destroy().Invoke();
```

`Destroy`, `Add` and `Remove` first collect matching handles in query order,
close the query scope, then call the normal immediate world operation. The
predicate is read-only: `in` and `ref readonly` component parameters cannot
mutate storage. Use a terminal callback such as `ForEach` when the selected
components need to be changed. Because the scope is closed first, a structural terminal
called from an already active traversal callback still raises the runtime's
active-lease error.

For non-structural work, use `ForEachEntity` when the callback needs identity,
or `ForEach` when it needs only components:

```csharp
world.WhereEntity(
        in healthQuery,
        static (Entity current, in Health health, in Team team) =>
            health.Value <= 0 && team.Id == 1)
    .ForEachEntity(static (EntityRef current, in Health health, in Team team) =>
        Console.WriteLine($"{current.Index}: {team.Id}")).Invoke();

world.Where(
        in healthQuery,
        static (in Health health, in Team team) =>
            health.Value <= 0 && team.Id == 1)
    .ForEach(static (ref Health health, in Team team) =>
        health.Value = team.DefaultHealth).Invoke();
```

The view stores the query and predicate for reuse. Each terminal returns an
operation object; keeping that object lets the caller reuse its prepared route
with `Invoke`. Each invocation reevaluates the predicate against the query's
current matches. A structural terminal collects the matching entity handles,
closes the query scope, then applies the structural change; do not issue an
immediate structural operation inside an active traversal callback.

## Opt into interceptors

Ordinary source generation requires only the package reference shown in the
README. On a compiler supporting interceptors, the optional configuration is:

```xml
<PropertyGroup>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);Delta.ECS.Generated</InterceptorsNamespaces>
</PropertyGroup>
<ItemGroup>
  <CompilerVisibleProperty Include="InterceptorsNamespaces" />
</ItemGroup>
```

Eligible synchronous static, non-capturing `World.ForEach` / `ForEachEntity`
callbacks can then use generated functor execution while keeping the same
source call. Capturing callbacks keep ordinary delegate execution. C# 9/10
consumers use ordinary generated overloads. `DECSGEN005`
explains an interceptor fallback and is informational.

To inspect generated C# in a consumer project, optionally enable:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Generated files belong under the intermediate output directory; do not copy
them into application source. See the [API grammar](API-GRAMMAR.md) for
supported callback shapes and the [API selection guide](API-SELECTION.md) for
choosing between runtime and generated forms.

The additional example types are:

```csharp
public struct Step
{
    public float DeltaTime;
    public int Updated;
}

public record struct Position(Vector2 Value);
public record struct Velocity(Vector2 Value);
public record struct Paused;
public struct Health { public int Value; }
public struct Team { public int Id; public int DefaultHealth; }
public struct Dead { }
public struct Alive { }
public struct Human { }
public struct Escaped { }
public struct Armed { }
public struct Berserk { }

public struct IsDeadPredicate : IWherePredicate
{
    public readonly bool Invoke(in Health health) => health.Value <= 0;
}

public struct ResetHealthAction : IForEach
{
    public readonly void Invoke(ref Health health) => health.Value = 100;
}
```
