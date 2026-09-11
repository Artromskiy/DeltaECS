# DeltaECS API and code-generation cookbook

Start with the complete project and `Program.cs` in the [README](../README.md).
Each example below independently continues immediately after its `12` output,
using the same world, registrations, entity and query. Place additional types
at the end of `Program.cs`, after all top-level statements.

## What needs the generator?

| Operation | Provided by |
|---|---|
| Register layouts, create entities, `Get` / `Set` / `TryGet` | Runtime |
| Single-component `Add<T>(entity, value)` / `Remove<T>(entity)` | Runtime |
| `QuerySpec`, `CreateQuery`, `BeginScope` | Runtime |
| Typed `WhereAll<T...>` / `WhereAny<T...>` / `WhereNone<T...>` | Generator |
| Component-bearing `ForEach` / `ForEachEntity` | Generator |
| Struct functor overloads inferred from `Invoke` | Generator |
| Generic batch `Add<T...>` / `Remove<T...>` | Generator |
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
    static (Entity current) => Console.WriteLine(current.Index));
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

For combined conditions, construct a `QuerySpec` using explicit IDs:

```csharp
var spec = new QuerySpec(
    allComponents: new[] { positionId },
    anyComponents: Array.Empty<ComponentId>(),
    noneComponents: new[] { velocityId });
var stationary = world.CreateQuery(in spec);
world.Remove<Velocity>(entity);
world.ForEach(in stationary,
    static (ref Position position) => position.X = 0);
Console.WriteLine(world.Get<Position>(entity).X); // 0
```

## Pass time and accumulate results without a closure

```csharp
var step = new Step { DeltaTime = 0.5f };
world.ForEach<Step, Position, Velocity>(in query, ref step,
    static (ref Step state, ref Position position, in Velocity velocity) =>
    {
        position.X += velocity.X * state.DeltaTime;
        state.Updated++;
    });
Console.WriteLine(step.Updated); // 1
Console.WriteLine(world.Get<Position>(entity).X); // 13
```

Additional type:

```csharp
public struct Step
{
    public float DeltaTime;
    public int Updated;
}
```

Context comes before components and is passed by `ref`. For a callback that
also receives identity, use `ForEachEntity` and put `Entity` after context.
Functor equivalents implement `IForEachContext<TContext>` or
`IForEachContextEntity<TContext>`; component parameters still come from
`Invoke`. The [README](../README.md#generate-a-stateful-system) demonstrates a
functor carrying its own state instead.

The same functor contracts work after a query-wide `Where`. A predicate uses
`IWherePredicate` and remains read-only; a terminal functor can mutate
components and receive its own context:

```csharp
var predicateState = new PredicateState();
var predicate = new IsDeadPredicate();
var actionState = new ActionState();
var action = new ResetHealthAction();
world.WhereEntity(in query, ref predicateState, ref predicate)
    .ForEachEntity(ref actionState, ref action);
```

`Invoke` receives `ref TContext` when a context is supplied, then `Entity` for
`WhereEntity`, then predicate components. `Where` omits `Entity`. Terminal
`ForEach` uses the usual component-only form; `ForEachEntity` includes the
entity after its context. Static-lambda terminals can be lowered by the
optional interceptor path to the same chunk execution as ordinary generated
`ForEach`.

## Use explicit component IDs

This is the runtime query equivalent of `world.WhereAll<Position, Velocity>()`:

```csharp
var explicitQuery = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
world.Set(entity, positionId, new Position { X = 30 });
Console.WriteLine(world.Get<Position>(entity, positionId).X); // 30
```

Typed convenience calls use the primary registration of each CLR type. If a
type represents several distinct components, retain their individual IDs and
select the intended registration explicitly. Query selection and callback
access must refer to the same registrations.

`Set<T>` replaces an existing row and throws immediately when the entity is
stale or does not contain `T`. Use `TryGet` when the component may be absent;
`Add<T>` is the operation that creates a missing row.

## Change only a selected batch

```csharp
var candidates = new[] { entity };
int removed = world.From(candidates).Where(in query).Remove<Velocity>();
Console.WriteLine(removed); // 1
Console.WriteLine(world.TryGet<Velocity>(entity, out _)); // False
int added = world.From(candidates).Add<Velocity>();
Console.WriteLine(added); // 1
Console.WriteLine(world.Get<Velocity>(entity).X); // 0
```

The filtered sequence uses only the supplied candidates, not all world matches.
Do structural work after iteration has finished. Removing a component leaves
the entity alive; destroying it invalidates its handle. Adding a component
without an initialization value gives it the default value. To initialize
while adding, use `world.Add(entity, velocityId, new Velocity { X = 2 })`
when that component is absent.

## Filter the whole query before a mutation

`world.Where(in query, predicate)` creates a stack-only view over every entity
matched by `query`. Its predicate receives only read-only typed component
references. `WhereEntity` is the form that receives `Entity` first. Its
terminal completes the entire operation before returning:

These examples use the following component markers:

```csharp
public struct Health { public int Value; }
public struct Team { public int Id; public int DefaultHealth; }
public struct Dead { }
public struct Alive { }
```

```csharp
int destroyed = world.WhereEntity(
        in query,
        static (Entity current, in Health health) => health.Value <= 0)
    .Destroy();

int tagged = world.WhereEntity(
        in query,
        static (Entity current, in Health health, in Team team) =>
            health.Value <= 0 && team.Id == 1)
    .Add<Dead>();

world.WhereEntity(
        in query,
        static (Entity current, in Health health, in Team team) =>
            health.Value <= 0 && team.Id == 1)
    .Remove<Alive>();
```

Use either `in` or `ref readonly` for a zero-copy read-only component reference:

```csharp
world.WhereEntity(
        in query,
        static (Entity current, ref readonly Health health) => health.Value <= 0)
    .Destroy();
```

When identity is not needed, omit the entity parameter:

```csharp
int destroyed = world.Where(
        in query,
        static (in Health health) => health.Value <= 0)
    .Destroy();
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
        in query,
        static (Entity current, in Health health, in Team team) =>
            health.Value <= 0 && team.Id == 1)
    .ForEachEntity(static (Entity current, in Health health, in Team team) =>
        LogDeath(current, team));

world.Where(
        in query,
        static (in Health health, in Team team) =>
            health.Value <= 0 && team.Id == 1)
    .ForEach(static (ref Health health, in Team team) =>
        health.Value = team.DefaultHealth);
```

The intermediate view is a stack-only `ref struct`, so it cannot be stored in a
class, boxed, or returned. It holds the predicate only for the duration of the
terminal call; no command is retained. `Where` scans all query matches;
`world.From(items).Where(in query)` remains the API for filtering only an
explicit candidate sequence.

## Traverse chunks explicitly

For systems that need direct control over traversal, declare query accesses
and keep all borrowed rows inside a scope:

```csharp
{
    using var scope = world.BeginScope(in query);
    var positionAccess = query.AccessWrite(positionId);
    var velocityAccess = query.AccessRead(velocityId);
    var archetypes = scope.Archetypes;
    while (archetypes.MoveNext())
    {
        var chunks = archetypes.Current.Chunks;
        while (chunks.MoveNext())
        {
            var slots = chunks.Current.Slots;
            var positions = slots.GetRow(positionAccess);
            var velocities = slots.GetRow(velocityAccess);
            while (slots.MoveNext())
            {
                ref Position position = ref positions.Ref<Position>(slots);
                ref readonly Velocity velocity = ref velocities.Ref<Velocity>(slots);
                position.X += velocity.X;
            }
        }
    }
}
Console.WriteLine(world.Get<Position>(entity).X); // 14
```

Disposing the scope ends the structural lease. Accesses belong to their query;
rows and child iterators must not escape the scope. The callback and explicit
traversal APIs both enforce access intent and participate in change tracking.

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
source call. Capturing callbacks and sequence receivers keep ordinary delegate
execution. C# 9/10 consumers use ordinary generated overloads. `DECSGEN005`
explains an interceptor fallback and is informational.

To inspect generated C# in a consumer project, optionally enable:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Generated files belong under the intermediate output directory; do not copy
them into application source. See the [generator reference](src/DeltaECS.Generators/README.md)
for supported callback shapes and diagnostics, and the
[console / NativeAOT sample](../samples/DeltaECS.AotSample/Program.cs) for a
complete application combining callbacks, a functor and ordered sequences.
