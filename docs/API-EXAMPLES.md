# DeltaECS API examples

Use [API selection](API-SELECTION.md) to choose a form and the
[grammar](API-GRAMMAR.md) for exact overloads. These examples use the `DeltaECS`
and `DeltaECS.Generators` packages. Grouping, change tracking and visitors are
runtime APIs; typed variadic iteration needs the generator.

## Create and move an entity

Start with this world. Later snippets reuse its registrations but are independent
examples, not a sequence to execute against the same mutated state. Place type
declarations after all top-level statements in `Program.cs`.

```csharp
using System;
using System.Numerics;
using Delta.ECS;

using var world = new World();
world.Components.Register<Position>(new SchemaId(1));
world.Components.Register<Velocity>(new SchemaId(2));
world.Components.Register<Paused>(new SchemaId(3));
ComponentId healthId = world.Components.Register<Health>(new SchemaId(4));
world.Components.Register<MemberOf>(new SchemaId(5), group: true);

Entity player = world.Create<Position>();
world.GetRef<Position>(player) = new Position(Vector2.Zero);
world.Add(player, new Velocity(new Vector2(2, 1)));
Query moving = world.WhereAll<Position, Velocity>().WhereNone<Paused>();

var move = world.ForEach(in moving,
    static (ref Position position, in Velocity velocity) =>
        position.Value += velocity.Value);
move.Invoke();
Console.WriteLine(world.Get<Position>(player).Value.X); // 2

public record struct Position(Vector2 Value);
public record struct Velocity(Vector2 Value);
public record struct Health(int Value);
public readonly record struct MemberOf(int Squad);
public record struct Paused;
```

`ref` declares a write; `in`/`ref readonly` declares a read. Retain `move` and
call `Invoke()` again to process current matches. Use `ForEachEntity` with
`EntityRef` before the component parameters when identity is also needed.

## Select membership or values

```csharp
Query all = world.WhereAll<Position, Velocity>();
Query any = world.WhereAny<Position, Velocity>();
Query active = all.WhereNone<Paused>();

world.Where(in active,
        static (in Position position) => position.Value.X > 10)
    .ForEach(static (ref Velocity velocity) => velocity.Value = Vector2.Zero)
    .Invoke();
```

`All` requires every member, `Any` accepts alternatives, `None` excludes members.
Repeated `WhereAny` calls extend one shared OR set. Do not request both component
rows when the query guarantees only one. `Where` evaluates values per candidate;
for recurring equality keys, use the grouped query below.

A structural terminal collects matches and applies the change after traversal:

```csharp
Query damaged = world.WhereAll<Health>();
int destroyed = world.Where(in damaged,
        static (in Health health) => health.Value <= 0)
    .Destroy().Invoke();
```

Use `.Add<Paused>()` or `.Remove<Paused>()` similarly. Do not call immediate
structural operations from an active iteration callback.

## Pass state without a closure

```csharp
var step = new Step(0.5f, 0);
var timedMove = world.ForEach(in moving, ref step,
    static (ref Step state, ref Position position, in Velocity velocity) =>
    {
        position.Value += velocity.Value * state.DeltaTime;
        state.Updated++;
    });
timedMove.Invoke(ref step);
Console.WriteLine(step.Updated);

public record struct Step(float DeltaTime, int Updated);
```

A struct functor is an alternative for reusable behavior. Its `Invoke` signature
defines the component rows; it does not need a lambda or a per-entity interface call:

```csharp
var action = new Move(0.5f);
world.ForEach(in moving, ref action).Invoke(ref action);

public readonly record struct Move(float DeltaTime) : IForEach
{
    public void Invoke(ref Position position, in Velocity velocity) =>
        position.Value += velocity.Value * DeltaTime;
}
```

For independent work, the corresponding `ForEachParallel` and
`ForEachEntityParallel` forms accept `workerCount`. Their scheduling cost needs
enough work to amortize; mutable shared `ref` context is not supported.

## Select repeated CLR types with IDs

```csharp
ComponentId energyId = world.Components.Register<float>(new SchemaId(20));
ComponentId shieldId = world.Components.Register<float>(new SchemaId(21));
Entity unit = world.Create(energyId, 10f);
world.Add(unit, shieldId, 5f);
Query resources = world.WhereAll(energyId, shieldId);

world.ForEach(in resources, energyId, shieldId,
    static (ref float energy, in float shield) => energy += shield).Invoke();
```

Types choose their primary registration by default. Explicit IDs choose other
registrations while keeping direct typed rows. For dynamically assembled filters,
pass `ReadOnlySpan<ComponentId>` or use `QuerySpec` and `world.CreateQuery`.

## Apply and capture EntityData

```csharp
var data = new EntityData();
data.Add(healthId, new Health(100));
data.Add(world.Components.GetPrimary<Paused>()); // Presence only
Entity unit = world.Create(data);

data.Set(healthId, new Health(75));
int applied = world.Set(unit, data); // 2: the entire set exists
world.AddOrSet(unit, data);

data.Clear();
data.AddOrSet(world, unit);         // Capture current components and tags
foreach (ComponentId id in data.ComponentIds)
{
    Console.WriteLine(id.Value);
}
```

`World.Add` applies only missing entries and returns their count. `World.Set`
applies the entire set or returns zero without changing values if any entry is
missing. `AddOrSet` adds missing entries and updates supplied values. Presence-only
IDs preserve existing values. Retain the data instance to reuse world-owned plans;
changing its values keeps preparation reusable, while changing its IDs invalidates it.
`ComponentIds` borrows storage until the container is modified. Captures are shallow.

## Group entities and observe changes

`MemberOf` was registered with `group: true` above. Equal whole values share
archetypes; use a record struct for consistent `IEquatable<T>` equality.

```csharp
Entity unit = world.Create(healthId, new Health(100));
world.Add(unit, new MemberOf(1));
Query squad = world.WhereAll(new MemberOf(1)).WhereNone<Paused>();

var changes = world.ForEachChange<Health>(in squad, healthId,
    static (Entity entity, ChangeKind kind, ref Health health) =>
        Console.WriteLine(kind == ChangeKind.Removed
            ? $"{entity.Index}: removed from selection"
            : $"{entity.Index}: {kind}, health={health.Value}"));

changes.Invoke();                             // Added
world.GetRef<Health>(unit).Value = 75;
changes.Invoke();                             // Modified
world.GetRef<MemberOf>(unit) = new MemberOf(2);
changes.Invoke();                             // Removed; Health is still present
world.Set(unit, new MemberOf(1));
changes.Invoke();                             // Added
world.Add(unit, new Paused());
changes.Invoke();                             // Removed by the tag filter
```

Retain the change operation. Omit `in squad` to watch the registration across the
whole world. It observes one component plus membership in the filter, not writes
to every component mentioned by that filter. `WhereAny(value)` adds alternatives;
`WhereNone(value)` excludes a value and permits absence. Pass `(id, value)` for
an explicit registration.

A writable access counts as `Modified` even if the value stays equal. For
`Removed`, the ref is null and must not be read; the entity may already be dead.
Changes coalesce between invocations, with entity generations kept separate.
An operation's callback does not notify that same operation again.

Grouped writes defer relocation until the next world data operation, including
`changes.Invoke()`. Active iteration completes before regrouping; do not reuse
borrowed refs across a later operation that may move rows. Small chunks grow
from 16 to 1024 slots, but unique keys/combinations still retain metadata.
Choose grouping for recurring squad/owner keys; use ordinary `Where` for ranges,
distance or mostly unique frequently changing values. See the
[grouping trade-offs](API-SELECTION.md#choose-grouping-for-recurring-equality-filters).

## Visit a runtime-selected component

This standalone example applies interface behavior to a component chosen by ID.
The visitor works directly on its stored reference; no handler registry is needed
for a one-off command.

```csharp
using System;
using Delta.ECS;

using var world = new World();
world.Components.BindInterface<Health, IDamageable>();
world.Components.BindInterface<Durability, IDamageable>();
ComponentId healthId = world.Components.Register<Health>(new SchemaId(1));
world.Components.Register<Durability>(new SchemaId(2));
Entity target = world.Create(healthId, new Health(100));

world.Components.Visit(healthId, new DamageVisitor(world, target, 25));
Console.WriteLine(world.Get<Health>(target).Value); // 75

public interface IDamageable { void ApplyDamage(int amount); }
public record struct Health(int Value) : IDamageable
{
    public void ApplyDamage(int amount) => Value -= amount;
}
public record struct Durability(int Value) : IDamageable
{
    public void ApplyDamage(int amount) => Value -= amount;
}
public sealed class DamageVisitor(World world, Entity target, int amount)
    : IComponentVisitor<IDamageable>
{
    public void Visit<T>(ComponentId id) where T : IDamageable
    {
        if (world.Has(target, id))
        {
            world.GetRef<T>(target, id).ApplyDamage(amount);
        }
    }
}
```

`IUnconstrainedComponentVisitor` accepts any registered CLR type without interface
binding. `IComponentVisitor<TInterface>` supplies `ConstraintType` automatically;
`BindInterface` is explicit per CLR type and covers all its schema registrations.
`TryVisit` reports whether dispatch occurred; `Visit` silently skips unsupported
routes. For repeated commands prepare typed handlers once. For dense world work
prepare typed traversals once per ID rather than visiting each entity.

## Opt into interceptors

Eligible static non-capturing callbacks can use a generated direct functor path
on compilers supporting interceptors. Ordinary overloads remain available without it:

```xml
<PropertyGroup>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);Delta.ECS.Generated</InterceptorsNamespaces>
</PropertyGroup>
<ItemGroup>
  <CompilerVisibleProperty Include="InterceptorsNamespaces" />
</ItemGroup>
```

Keep `using Delta.ECS;` in the consumer and reference `DeltaECS.Generators` there
with `PrivateAssets="all"`. Generated files belong in intermediate output, not
application source. See the [grammar](API-GRAMMAR.md) for ordering, runtime-selected
generic functors and remaining overloads.
