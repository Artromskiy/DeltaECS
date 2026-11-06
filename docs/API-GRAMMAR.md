# DeltaECS API grammar

This is the canonical public grammar for the DeltaECS API. Read it when using
the public entry points or documenting a consumer example. The same argument
order applies to generic, non-generic, delegate, functor, and parallel forms.

For task-based recommendations, complexity and execution costs, start with
[Choosing the DeltaECS API](API-SELECTION.md).

## Grammar symbols

```text
T...  — one or more CLR component types
I...  — positional list of ComponentId values
D     — explicit ReadOnlySpan<ComponentId> selector (not a params argument)
e     — one Entity
E     — ReadOnlySpan<Entity> or an entity array
Q     — Query
C     — user context
A     — delegate or lambda callback
F     — functor
N     — entity count
O     — Span<Entity> output
W     — worker count
V...  — values corresponding positionally to T...
G...  — ComponentId arguments that close a generic functor type
```

## World and entity handles

The world owns its layouts, entity storage, and query cache. Use a `using`
declaration to release its native storage:

```text
new World(layouts?, initialEntityCapacity?) -> World
world.Components -> ComponentRegistry
world.AliveEntityCount -> int
world.Dispose()

new Entity(index, generation) -> Entity
entity.IsValid -> bool
world.IsAlive(entity) -> bool
query.IsValid -> bool
```

`Entity.IsValid` checks only the handle's index and generation shape. Liveness
is world-specific, so use `World.IsAlive`. A `Query` belongs to its creating
world and becomes invalid when that world is disposed.

The canonical argument order is:

```text
target(e | E)?,
query(Q)?,
component-types(T... | inferred)?,
registrations(I... | D)?,
context(C)?,
callback(A | F)?,
values(V...)?,
options(N | O | W)?
```

`T...` identifies the CLR row types. In ordinary typed iteration, `I...`
selects the registration used for each row; omitting it uses the primary
registration. `D` is the equivalent explicit dynamic selector. When both
`T...` and `I...` or `D` are present, the number of IDs must equal the row
arity, and each ID must identify a registration of the corresponding CLR
type. The runtime validates dynamic span length before executing the operation.
In runtime-selected generic functor calls, `G...` or `D` closes the open
functor type and is independent of the component rows accepted by `Invoke`.
Query factories also allow typed and `ComponentId` selectors. `Q` is required
for world-wide query iteration and optional after an explicit entity target
`E`.

Iteration entry points return reusable deferred operation objects. Calling a
`ForEach`, `ForEachParallel`, or generated terminal such as `Where(...).Add(...)`
only builds the operation; `Invoke()` executes it. `Where` and `OrderBy` return
composable views, while their terminal operations are deferred. An operation
retains its `Query` and live `QueryPlan`, not a snapshot of matching chunks.
Each invocation validates execution state and visits the query's current
matching chunks, including archetypes created after the operation was built.
`Query.First()` is a deferred result terminal: `query.First().Invoke()` returns
the first matching entity in query traversal order, or `default(Entity)` when
there is no match.

Functor operations keep a typed copy of the functor without converting it to an
interface. `Invoke()` uses and updates that operation-owned copy, while
`Invoke(ref functor)` uses caller-owned state. Context-bearing operations have
matching `Invoke` overloads. A generated `Where` view owns copies of its
predicate and predicate context and retains their updated state between
terminal invocations. Entity spans and dynamic component-ID spans are copied
when an operation is built because the returned object cannot retain a span
over caller-owned stack memory.

Use positional IDs when the registrations are known at the call site. Pass an
explicit `ReadOnlySpan<ComponentId>` when the list is assembled dynamically;
the span is an ordinary parameter, not a `params` argument. The old
`params ReadOnlySpan<ComponentId>` modifier has been removed. Since `params`
does not change the CLR method signature, an obsolete `params` overload cannot
coexist with the explicit-span overload under the same name and parameter
types.

## Component registration and type visitors

`Register<T>(schemaId, group: false)` returns a `ComponentId`. Registration
selects the strongest CLR constraint visible at the call site (`unmanaged`,
`struct`, `class`, `new()`, or `class, new()`). A generic helper must declare
its constraint to select that route; an unconstrained `T` uses the unconstrained
route even when its closed type is a struct. These APIs require no generator.

| Visitor contract | Accepted type / purpose |
| --- | --- |
| `IComponentVisitor` | Common marker; no callback by itself |
| `IUnconstrainedComponentVisitor` | Any CLR type; `Visit<T>(ComponentId)` |
| `IComponentVisitor<TConstraint>` | `T : TConstraint`; explicit interface binding |
| `IStructComponentVisitor`, `IUnmanagedComponentVisitor`, `IClassComponentVisitor`, `INewComponentVisitor`, `IClassNewComponentVisitor` | `struct`, `unmanaged`, `class`, `new()`, `class, new()` respectively |
| The same shape contracts with `<TConstraint>` | Both the shape and user constraint |
| `IConstrainedComponentVisitor` / `<TConstraint>` | Constraint identity base; the generic form supplies `ConstraintType` automatically |

```csharp
layouts.BindInterface<Health, IDamageable>();
ComponentId healthId = layouts.Register<Health>(new SchemaId(1));
layouts.Visit(healthId, new DamageVisitor(world, target, 25));

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

This fragment assumes `Health : IDamageable`, with `ApplyDamage(int)`; see the
[complete example](API-EXAMPLES.md#visit-a-runtime-selected-component).
`BindInterface<T, TInterface>()` covers every schema registration of that CLR
type, before or after binding. Bind each concrete CLR type separately.

Every registration supports an unconstrained visitor. Narrower visitors require
a compatible registration route. `TryVisit(id, visitor)` returns `false` for an
invalid ID, null visitor or unsupported route; `Visit` silently skips those cases.
For virtual hooks derive from `GeneralComponentTypeVisitor<TConstraint>` and
override only needed routes: `Visit`, `VisitUnmanaged`, `VisitStruct`, `VisitClass`,
`VisitConstructible`, or `VisitClassConstructible`. Constraints live on the base
methods; overrides need no marker arguments.

The registry exposes the primary registration and CLR type for existing
components. `GetPrimary` throws if the type is not registered; `TryGetPrimary`
returns `false` and `ComponentId.Invalid`. Type overloads are useful when the
CLR type is known only at runtime:

```text
layouts.GetComponentType(I) -> Type
layouts.GetSchemaId(I) -> SchemaId
layouts.IsTag(I) -> bool
layouts.GetComponentIds() -> ReadOnlySpan<ComponentId>
layouts.GetComponentIds<T>() -> ReadOnlySpan<ComponentId>
layouts.TryGetComponentId(SchemaId, out ComponentId) -> bool
layouts.GetPrimary<T>() -> ComponentId
layouts.TryGetPrimary<T>(out ComponentId) -> bool
layouts.GetPrimary(Type) -> ComponentId
layouts.TryGetPrimary(Type, out ComponentId) -> bool
```

When the generator emits a component catalog, register every entry or retrieve
one by type:

```csharp
foreach (IGeneratedComponentRegistration registration in GeneratedComponentCatalog.GetRegistrations())
{
    layouts.Register(registration);
}

IGeneratedComponentRegistration positionRegistration =
    GeneratedComponentCatalog.GetRegistration<Position>();
layouts.Register(positionRegistration);
```

`GetRegistrations` returns a deterministic array snapshot. These catalog calls
require generated registrations in the consumer assembly; ordinary
`Register<T>(schemaId)` and runtime visitor calls do not.

`ComponentSchema` takes no argument to derive a stable schema ID from the
component's qualified CLR name, or one non-zero `ulong` to assign it explicitly.
`ComponentAlias` can be repeated to assign ordinal string aliases to a generated
component. `ComponentGroupAttribute` accepts enum values and enum arrays;
repeated attributes are supported. For a `[Flags]` enum, generated registration
expands each set bit into its own group membership. Other enums use the exact
enum value. Register generated entries and manual metadata through the registry;
`World` exposes read APIs for its catalog. Registries can be shared by worlds,
so aliases and groups are shared as well:

```csharp
using System;
using Delta.ECS;

var layouts = new ComponentRegistry();
foreach (IGeneratedComponentRegistration registration in GeneratedComponentCatalog.GetRegistrations())
{
    layouts.Register(registration);
}

using var world = new World(layouts);
ComponentId healthId = world.GetAlias("health");
layouts.AddAlias("configured-health", healthId);
layouts.AddGroup(WorldComponentGroup.Rollback, healthId);
ReadOnlySpan<ComponentId> rollbackComponents = world.GetGroup(WorldComponentGroup.Rollback);
bool foundHealth = layouts.TryGetComponentId(new SchemaId(10), out ComponentId bySchemaId);

[Flags]
public enum WorldComponentGroup
{
    History = 1,
    Rollback = 2,
}

[ComponentSchema(10)]
[ComponentAlias("health")]
[ComponentGroup(WorldComponentGroup.History)]
public struct Health { public int Value; }
```

`GetComponentIds<T>()` returns all registrations for the exact CLR type in
registration order, or an empty span when none exist. `TryGetComponentId`
resolves the exact registration associated with a stable `SchemaId`; it returns
`false` and `ComponentId.Invalid` when that schema is unknown. This is a
registry lookup, not a check for a component on an entity. `AddAlias` accepts
repeated registration of the same alias/ID pair and rejects an alias mapped to
another component. `TryGetAlias` returns `false` for an unknown alias, while
`GetAlias` throws. `GetAliases(id)` returns aliases in
insertion order without copying, or an empty span when none are registered.
The borrowed span must not be retained across alias additions for that component.
Group membership is deduplicated.
`GetGroup` accepts exactly one enum value as its lookup key; it does not combine
multiple group values. Runtime `AddGroup` also adds one exact enum value at a
time, so call it once per desired flag when adding a flags-based component
manually.

```text
layouts.Register<T>(SchemaId) -> ComponentId
layouts.Register(IGeneratedComponentRegistration) -> ComponentId
layouts.TryGetComponentId(SchemaId, out ComponentId) -> bool
layouts.GetPrimary<T>() -> ComponentId
layouts.TryGetPrimary<T>(out ComponentId) -> bool
layouts.GetPrimary(Type) -> ComponentId
layouts.TryGetPrimary(Type, out ComponentId) -> bool
layouts.GetComponentType(ComponentId) -> Type
layouts.AddAlias(string, ComponentId)
layouts.TryGetAlias(string, out ComponentId) -> bool
layouts.GetAlias(string) -> ComponentId
layouts.GetAliases(ComponentId) -> ReadOnlySpan<string>
layouts.AddGroup<TEnum>(TEnum, ComponentId)
layouts.GetGroup<TEnum>(TEnum) -> ReadOnlySpan<ComponentId>
world.TryGetAlias(string, out ComponentId) -> bool
world.GetAlias(string) -> ComponentId
world.GetGroup<TEnum>(TEnum) -> ReadOnlySpan<ComponentId>
```

`ComponentGroup` attributes may list separate enum constants, pass an enum
array, or use a combined value from a `[Flags]` enum. Each generated flag bit is
registered as a separate group; querying with a combined flags value does not
union those groups.

## Experimental value grouping

```text
layouts.Register<T>(SchemaId, bool group = false) -> ComponentId
[ComponentSchema(group: true)]
[ComponentSchema(100UL, group: true)]

world/query.WhereAll<T>(in T value) -> Query
world/query.WhereAny<T>(in T value) -> Query
world/query.WhereNone<T>(in T value) -> Query
world/query.WhereAll<T>(ComponentId, in T value) -> Query
world/query.WhereAny<T>(ComponentId, in T value) -> Query
world/query.WhereNone<T>(ComponentId, in T value) -> Query
```

Grouped types must be structs implementing `IEquatable<T>`; record structs supply
it. Equal whole values share an archetype. The component keeps one ID/schema
and ordinary per-entity data; primary/explicit-ID access, iteration, `EntityData`
and serialization work normally. Empty grouped tags keep tag semantics. The
mode is fixed at registration; value filters reject ordinary registrations.
`ComponentSchema` passes the same flag through generated registration for both
explicit and name-derived schemas; invalid annotated types report `DECS0007`.

```csharp
ComponentId memberOfId = world.Components.Register<MemberOf>(new SchemaId(100), group: true);
Query squad = world.WhereAll(new MemberOf(1));
Query either = world.WhereAny(new MemberOf(1)).WhereAny(new MemberOf(2));
Query others = world.WhereNone(memberOfId, new MemberOf(1));

public readonly record struct MemberOf(int Squad);
```

`All(value)` requires equality, `Any(value)` extends the shared OR set alongside
IDs/tags, and `None(value)` excludes equality while permitting absence. Value
filters borrow `in T` without boxing and select archetypes rather than checking
every entity. They compose with ordinary component/tag filters. Parameterless
generic and ID/span forms still test presence. Equality/hash implementations
must be stable and consistent; normal string equality is ordinal.

Writable grouped access marks a chunk dirty. The **next world data operation**
resolves pending regrouping before obtaining locations, matches or row bindings;
the writing operation's completion does not trigger it. An active iteration
defers movement, so nested work uses that operation's existing placement.
Read-only access does not mark dirty. Borrowed refs must not be reused after
another operation can relocate data; the caller owns this lifetime. Partial
writes remain pending after a callback exception.

Chunks grow from 16 to 1024 slots and do not automatically shrink. Group keys
and archetype combinations retain metadata for the registry/world lifetime;
prefer bounded recurring keys to mostly unique values. See
[API selection](API-SELECTION.md#choose-grouping-for-recurring-equality-filters)
for costs and a complete group/change example.

## Runtime component-change iteration

```text
world.ForEachChange<T>(ComponentId, ComponentChangeAction<T>) -> ComponentChangeOperation<T>
world.ForEachChange<T>(in Query, ComponentId, ComponentChangeAction<T>) -> ComponentChangeOperation<T>
ComponentChangeOperation<T> : IOperation
ComponentChangeAction<T>(Entity, ChangeKind, ref T) -> void
ChangeKind = Added | Modified | Removed
```

This runtime API needs no generator. Retain its operation: the first invocation
reports existing matching instances as `Added`, later successful invocations
consume coalesced changes. The query must belong to the same world and may use
component/tag membership **and grouped values**. The observed registration must
be present even if omitted from the query. `Where` predicate views are not accepted.

```csharp
Query squad = world.WhereAll(new MemberOf(1)).WhereNone<Paused>();
var changes = world.ForEachChange<Health>(in squad, healthId,
    static (Entity entity, ChangeKind kind, ref Health health) =>
        Console.WriteLine(kind == ChangeKind.Removed
            ? $"{entity}: removed from selection"
            : $"{entity}: {kind}, health={health.Value}"));
changes.Invoke();
```

- Entry into the observed set reports `Added`; exit reports `Removed`, even when
  the component still exists. Switching group value can therefore report an exit
  in one observer and entry in another without writing their observed component.
- Writable access reports `Modified` even if the value is unchanged. Tags follow
  the same rule with a dummy ref. A filter-only change that leaves membership
  unchanged does not report `Modified`.
- `Removed` receives `Unsafe.NullRef<T>()` and the original, possibly stale entity.
  Never read that ref. Generations separate old removal from new addition at a reused index.
- Add then remove can cancel; remove then add for an already observed instance
  becomes `Modified`. Intermediate filter exits/reentries coalesce to final
  membership and writes to the tracked component.
- Writes through an operation's own callback do not notify itself again.
  Subsequent calls process pending membership and dirty chunks rather than
  rescanning the world; recreating the operation restarts initial reporting.

For complete code, see [grouping and changes](API-EXAMPLES.md#group-entities-and-observe-changes).

## Object-based component access

Tooling uses the ordinary `World` and its component registry:

```text
world.Components.GetComponentIds() -> ReadOnlySpan<ComponentId>
world.Components.GetSchemaId(I) -> SchemaId
world.Components.GetComponentType(I) -> Type
world.Components.IsTag(I) -> bool
world.TryRead(e, I, out object? value) -> bool
world.TryWrite(e, I, object? value) -> bool
```

`TryRead` returns a shallow copy of a component value or the default tag value.
`TryWrite` adds or replaces a value of the exact registered CLR type. A null
write value is rejected by an exception. Struct values are boxed; prefer typed
access in hot code. Neither method requires generated consumer code.

## Generated iteration forms

Delegate and intercepted-lambda forms are generated with these shapes:

```text
world.ForEach<T...>(Q, I... | D, C, A)
world.ForEachEntity<T...>(Q, I... | D, C, A)

world.ForEach<T...>(E, Q?, I... | D, C, A)
world.ForEachEntity<T...>(E, Q?, I... | D, C, A)

world.ForEachParallel<T...>(Q, I... | D, C, A, W)
world.ForEachEntityParallel<T...>(Q, I... | D, C, A, W)

world.ForEachParallel<T...>(E, Q?, I... | D, C, A, W)
world.ForEachEntityParallel<T...>(E, Q?, I... | D, C, A, W)
```

Entity-aware iteration may omit the component selector entirely. The callback
still receives the current entity, so these are useful zero-arity forms:

```text
world.ForEachEntity(Q, C?, A | F)
world.ForEachEntity(E, Q?, C?, A | F)
world.ForEachEntityParallel(Q, C?, A | F, W)
world.ForEachEntityParallel(E, Q?, C?, A | F, W)
```

Functor forms use the same target, query, registration selector, and context
order, with `F` in the callback position. Their CLR row types are inferred from
the functor's `Invoke` signature, so ordinary component functor calls do not
take explicit `<T...>` arguments:

```text
world.ForEach(Q, I... | D, C, F)
world.ForEachEntity(Q, I... | D, C, F)
world.ForEach(E, Q?, I... | D, C, F)
world.ForEachEntity(E, Q?, I... | D, C, F)

world.ForEachParallel(Q, I... | D, C, F, W)
world.ForEachEntityParallel(Q, I... | D, C, F, W)
world.ForEachParallel(E, Q?, I... | D, C, F, W)
world.ForEachEntityParallel(E, Q?, I... | D, C, F, W)
```

`ForEach` callbacks receive component rows. `ForEachEntity` callbacks receive
a borrowed `EntityRef` for the current chunk slot as their first argument. It
converts implicitly to the stable `Entity` handle and provides dynamic `Has`,
`TryGet<T>`, and `GetRef<T>` access without resolving that handle through the
world's entity table. This is useful when component registrations are selected
at runtime:

```csharp
world.ForEachEntity(in query, (EntityRef entity) =>
{
    if (entity.TryGet(positionId, out Position position))
    {
        Use(position);
    }

    if (entity.Has(healthId))
    {
        ref Health health = ref entity.GetRef<Health>(healthId);
        health.Value--;
    }
}).Invoke();
```

`Has` and `TryGet<T>` check membership because the requested component may not
be present. `TryGet<T>` also returns `false` for an unregistered ID or an ID
registered with another CLR type. `GetRef<T>` throws if the component is
missing or its registration has another CLR type. A tag has no per-entity
value: `TryGet` reports whether the tag is present and returns `default(T)` as
its output. `GetRef` returns a shared placeholder, not per-entity storage, so
writing through it does not persist a component payload. Writable access still
records a change for `ForEachChange<T>` observers.

`EntityRef` also supports primary-registration forms. Supply a `ComponentId`
when the entity has multiple registrations of the same CLR type:

```text
implicit Entity entity = entityRef; entityRef.Index; entityRef.Generation
entity.Has(I) | entity.Has<T>() -> bool
entity.TryGet<T>(out T) | entity.TryGet<T>(I, out T) -> bool
entity.Get<T>() | entity.Get<T>(I) -> T
entity.GetRef<T>() | entity.GetRef<T>(I) -> ref T
entity.GetReadRef<T>() | entity.GetReadRef<T>(I) -> ref readonly T
```

Use `EntityRef` only while its callback is running and before any structural
change; assign it to `Entity` when the handle must be retained. The view already
contains the current world, chunk, archetype, and slot, so each access skips
entity-handle resolution. It does not pre-bind component rows from the query.
Parallel callbacks always use parallel execution; `W` is the caller's
worker-count choice, clamped to the supported range. A parallel context is
passed read-only or by value; mutable `ref` context is rejected.

## Structural forms

Direct `World` structural calls execute immediately. Change-reporting single
entity forms return `bool`, while batch and query forms return the number of
changed entities. Direct typed `AddOrSet` returns `void` for one entity or an
entity batch. Generated typed forms accept primary registrations or explicit
selectors:

```text
world.Add<T...>(e | E | Q) -> bool | int
world.Add<T...>(e | E, V...) -> bool | int
world.Add<T...>(e | E, I... | D, V...) -> bool | int
world.AddOrSet<T...>(e | E, V...) -> void
world.AddOrSet<T...>(e | E, I... | D, V...) -> void
world.Set<T...>(e | E, V...) -> bool | int
world.Set<T...>(e | E, I... | D, V...) -> bool | int
world.Remove<T...>(e | E | Q) -> bool | int
world.Destroy(e) -> bool
world.Destroy(E | Q) -> int

world.Add(e | E | Q, I... | D) -> bool | int
world.Remove(e | E | Q, I... | D) -> bool | int
world.Add(e | E, V...) -> bool | int

world.Create<T>() -> Entity
world.Create<T...>(N, O?) -> int
world.Create<T...>(I... | D, V...) -> Entity
world.Create(I... | D) -> Entity
world.Create(I... | D, O) -> int
world.Create(I... | D, N, O?) -> int

world.Where(...).Add<T...>() / Add(I... | D) -> EcsResultOperation<int>
world.Where(...).Add<T...>(V... | I... | D, V...) -> EcsResultOperation<int>
world.Where(...).AddOrSet<T...>(V... | I... | D, V...) -> EcsOperation<TInvoker>
world.Where(...).Set<T...>(V... | I... | D, V...) -> EcsResultOperation<int>
```

`I...` forms are positional `ComponentId` arguments; `D` is one explicit
`ReadOnlySpan<ComponentId>` at the same selector position. Type-erased `Add`
and `Remove` accept IDs after the target; `Create` places them before `N` and
`O`. Generic selectors are reserved for primary-registration forms and forms
that also consume typed values.
Generic value types may be inferred from `V...`; multi-component value-based
`Create`, `Add`, `AddOrSet`, and `Set` forms require explicit registrations,
and the positions of `I...` and `V...` correspond to component types in `T...`.
`Add` inserts only missing components, initializes only those new rows from
`V...`, and leaves existing values untouched; it returns `true` for one entity
or the number of changed entities for a batch. `AddOrSet` inserts missing
components and overwrites existing values, returning `void`. `Set` updates only
entities that already have every selected component; it returns `false` for one
entity or excludes that entity from the batch count when any selected component
is missing. `Set` never adds a component. For tags, the typed value is ignored:
`Add` reports whether the tag was added, `AddOrSet` ensures it is present, and
`Set` succeeds only when it is already present. A `Where` terminal captures its
values and runs when its returned operation is invoked; `AddOrSet.Invoke()` has
no result and `Set.Invoke()` returns the number of entities updated. Direct
query-level `Add` without values remains the type-erased membership form. The
runtime's primary-registration `Create<T>()` is the single-component
convenience form.

Structural `World` batch methods that accept an entity span require unique
handles and throw `ArgumentException` for duplicates before changing the world.
Uniqueness compares the full entity handle, including its generation; a stale
handle and a different live generation with the same index are distinct inputs.

`N` is the number of entities to create and `O` is optional caller-owned
output storage. Without `O`, created handles are not retained. Single-entity
`Create` returns its `Entity`; count forms return the number created. Direct
structural calls cannot run from an active traversal callback. A structural
terminal on a generated `Where` view instead returns a deferred operation that
runs on `Invoke()`.

## Entity data

`EntityData` is a mutable, world-independent set of optional typed values keyed
by explicit `ComponentId` values. Build it once and pass it to a single-entity
`World` operation. The world validates that every ID is registered there and
that each stored CLR type matches its registration. `Add(I)` stores an ID
without knowing its CLR type; the world resolves the registration when the data
is applied. A data component without a stored value starts with its default
value when structurally added, while a tag contributes membership only. The
typed `Add<T>(I, V)` form automatically stores only presence when `T` is a tag.

```text
new EntityData() -> EntityData
data.Count -> int
data.ComponentIds -> ReadOnlySpan<ComponentId>
data.Add(world, entity) -> int // Adds missing entries; returns additions.
data.Set(world, entity) -> int // Updates matching entries; returns applications, including tags.
data.AddOrSet(world, entity) -> void // Adds or updates; preserves entries absent from the entity.
data.Add(I) -> bool // Stores an ID; the world resolves its registration.
data.Add<T>(I, V) -> bool // Stores a value, or presence when T is a tag.
data.AddOrSet<T>(I, V) -> void
data.Set<T>(I, V) -> bool
data.Has(I) -> bool
data.TryGet<T>(I, out T) -> bool
data.Get<T>(I) -> T
data.GetRef<T>(I) -> ref T
data.GetReadRef<T>(I) -> ref readonly T
data.Remove(I) -> bool
data.Clear()

world.Create(data) -> Entity
world.Add(e, data) -> int
world.AddOrSet(e, data) -> void
world.Set(e, data) -> int
```

The typed accessors treat tags like data components: a present tag returns
`default(T)` from `Get<T>` and succeeds in `TryGet<T>`. `GetRef<T>` and
`GetReadRef<T>` return the same shared dummy reference used by `World`; writing
through it does not change tag presence. These forms work for typed entries,
ID-only entries and tags captured from a world. A known CLR-type mismatch
returns `false` from `TryGet<T>` and throws from the other accessors. An ID-only
entry has no registered CLR type to check until it is typed or consumed by a
world; it can be read as a tag, but a data component still requires a stored
value.

`ComponentIds` enumerates stored IDs in insertion order. Its span
borrows the data's storage and must not be retained across changes to that
`EntityData`. `EntityData.Add/Set/AddOrSet(World, Entity)` copy values and
component presence from an existing entity into the data; they do not modify
the entity.

Capture resolves the source entity once and reuses world-owned typed read
routes. `Set(world, entity)` visits IDs already in the destination; `Add` and
`AddOrSet` visit the source's data components and the chunk's active tags,
copying only tags present on that source entity. These calls do not scan the
entire component registry. New entries from a full capture are appended in
registration order; existing destination entries keep their insertion order.

`Add` preserves an existing entry and returns `false`; `AddOrSet` inserts or
replaces a typed value and returns `void`; `Set` updates only an existing entry
and returns `false` if it is missing. Adding an ID without a value defers type
resolution until a world consumes the data. On `World`, `Add` adds only missing
components and tags, leaving existing component values untouched; it returns
the number of entries added. A presence-only data component starts with its
default value when structurally added. `AddOrSet` adds missing entries and
writes stored values for entries already present; a presence-only entry leaves
an existing value untouched. `Set` writes stored values only when the entity
already contains every entry in the data; it returns the number of entries
applied, or zero without writing any values when one is missing. Presence-only
entries count when present but do not write a value. An empty set cannot create
an entity; adding it returns zero, `AddOrSet` does nothing, and `Set` returns
zero.

`EntityData` can be reused as input for multiple single-entity operations. Its
values are applied to the world at each call; subsequent changes to the data
affect only later calls. These APIs accept one `Entity` at a time and do not
provide batch overloads.

## Single-entity typed access

Typed accessors use the primary registration unless an explicit ID is supplied.
`TryGet` and `Has` return `false` when the entity, component, or CLR type does
not match. `Get` and `GetRef` throw when the requested component
is unavailable; `GetReadRef` returns a read-only reference.

```text
world.TryGet<T>(e, out T) -> bool
world.TryGet<T>(e, I, out T) -> bool
world.Has(e, I) | world.Has<T>(e) -> bool
world.Get<T>(e) | world.Get<T>(e, I) -> T
world.GetRef<T>(e) | world.GetRef<T>(e, I) -> ref T
world.GetReadRef<T>(e) | world.GetReadRef<T>(e, I) -> ref readonly T
```

`Add<T>(e, in value)` and `Add<T>(E, in value)` initialize the primary
registration, returning `bool` or a changed-entity count. `Remove<T>` selects
the primary registration; use TypeErased `Remove(e | E | Q, I... | D)` when
the registration is selected by ID. Generated multi-component forms use the
structural grammar above.

`Set<T>` selects the primary registration or an explicit `ComponentId` and
writes the value only when the entity already has the component. It returns
`false` for a stale entity, a missing component, or an invalid/mismatched
explicit ID; it never adds the component. For a tag, `Set<T>` succeeds only
when the tag is already present; tags do not store the supplied value.

## Query factories

The query builder has three names only. A call returns a new `Query`, so the
chain can continue from either the world or the previous query. A specification
can also be built separately and passed to `CreateQuery`:

```text
world.CreateQuery(in QuerySpec) -> Query

world.WhereAll<T...>() -> Query
world.WhereAny<T...>() -> Query
world.WhereNone<T...>() -> Query

world.WhereAll(I... | D) -> Query
world.WhereAny(I... | D) -> Query
world.WhereNone(I... | D) -> Query

query
    .WhereAll<T...>()
    .WhereNone<T...>()
    .WhereAny<T...>() -> Query

query
    .WhereAll(I... | D)
    .WhereNone(I... | D)
    .WhereAny(I... | D) -> Query

```

`WhereAll` appends components to `All`, `WhereNone` appends to `None`, and
`WhereAny` appends to the shared `Any` filter. Type-erased ID forms select
registrations directly; use typed forms without IDs to select primary
registrations by CLR type.

The low-level `QuerySpec.WhereAll`, `WhereAny`, and `WhereNone` factories take
an explicit `ReadOnlySpan<ComponentId>` for dynamic lists of any length; each
also has a single-ID overload. Runtime composition uses `WithAll`, `WithAny`,
and `WithNone` with spans. For generated positional fluent filters, start with
`QuerySpec.Empty`; the generator emits `WhereAll`, `WhereAny`, and `WhereNone`
extension overloads for the arities used in the consumer project. There is no
fixed positional arity limit, and unused overloads are not generated. The
generated `World` and `Query` query factories likewise provide positional
overloads for call-site arities.

```text
QuerySpec.Empty.WithAll(D) -> QuerySpec
QuerySpec.Empty.WithAny(D) -> QuerySpec
QuerySpec.Empty.WithNone(D) -> QuerySpec
```

```csharp
QuerySpec runtimeSpec = QuerySpec.Empty
    .WithAll(stackalloc ComponentId[] { positionId, velocityId })
    .WithNone(stackalloc ComponentId[] { deadId });
Query runtimeQuery = world.CreateQuery(in runtimeSpec);

QuerySpec generatedSpec = QuerySpec.Empty
    .WhereAll(positionId, velocityId)
    .WhereNone(deadId, escapedId);
```

## Where pipeline

`Where` evaluates a predicate over every entity selected by `Q`; the
entity-aware spelling is `WhereEntity`. The resulting reusable view retains
the predicate and source query. Its terminal methods build deferred operations
that run only when invoked:

```text
world.Where(Q, C?, Predicate) -> WhereView
world.WhereEntity(Q, C?, Predicate) -> WhereView

view.Destroy().Invoke()
view.Add<T...>().Invoke()
view.Add<T...>(I... | D).Invoke()
view.Add<T...>(V...).Invoke()
view.Add<T...>(I..., V...).Invoke()
view.Remove<T...>().Invoke()
view.Remove<T...>(I... | D).Invoke()
view.ForEach(...).Invoke()
view.ForEachEntity(...).Invoke()     // EntityRef-only or EntityRef plus components
```

The ordinary `Where` predicate is component-only. `WhereEntity` adds the
current `Entity` as its first predicate parameter. Predicate component rows
use `in` or `ref readonly`. A structural terminal applies component writes
before it performs the structural change.

Zero-component `ForEach` forms throw `InvalidOperationException`.
`ForEachEntity`, `ForEachEntityParallel`, and a
`Where` view's `ForEachEntity` may omit component parameters because the
callback still receives `Entity`. Functor forms use the generated callback-slot pass mode so the caller can
choose `ref` (mutations returned), `in` / `ref readonly`, or by value. Value and
`in` forms accept temporaries such as `new Functor()` and keep the functor
eligible for full inlining when it has no observable state.

## Runtime-selected generic functors

An open generic component can be registered using the CLR types of existing
registrations. Supply its `SchemaId` explicitly. The runtime provides the
unary form; for two or more generic arguments, the generator emits the matching
`Register` overload:

```csharp
ComponentId historyId = layouts.Register(typeof(History<>), positionId, new SchemaId(100));
ComponentId pairId = layouts.Register(typeof(ComponentPair<,>), positionId, velocityId, new SchemaId(101));

ReadOnlySpan<ComponentId> pairArguments = stackalloc ComponentId[] { positionId, velocityId };
ComponentId dynamicPairId = layouts.Register(typeof(ComponentPair<,>), pairArguments, new SchemaId(102));
```

For the span form, the number of IDs must match the generic definition's arity.

The generated runtime-selected functor API supports the same targets and
execution modes for component-row iteration as the ordinary functor API. The
`ComponentId` values here close the open generic functor type; they are not
required to match the number of callback rows. In this section, `G...` denotes
positional IDs and `D` denotes an explicit dynamic span containing the generic
arguments. Its length must match the open functor's generic arity.
`typeof(Functor<>)` supplies the open generic definition.

The complete forms are:

```text
world.ForEach(Q, C?, G... | D, typeof(F<>))
world.ForEachEntity(Q, C?, G... | D, typeof(F<>))
world.ForEach(E, Q?, C?, G... | D, typeof(F<>))
world.ForEachEntity(E, Q?, C?, G... | D, typeof(F<>))

world.ForEachParallel(Q, C?, G... | D, typeof(F<>), W)
world.ForEachEntityParallel(Q, C?, G... | D, typeof(F<>), W)
world.ForEachParallel(E, Q?, C?, G... | D, typeof(F<>), W)
world.ForEachEntityParallel(E, Q?, C?, G... | D, typeof(F<>), W)
```

Open generic functors are supported by the ordinary parallel forms shown
above, including query-wide and entity-list calls, with or without context.
The generator emits a visitor stage for each generic parameter. Every component
registration retains its CLR type through a compiler-support token. Stages
support `struct`, `unmanaged`, `class`, `class?`, and `new()` constraints,
including `class, new()` combinations. At registration or on the first
functor call for an ordered `ComponentId` tuple, the runtime follows the
generated stages to close the type and checks each selected registration
against those constraints. The same path works when IDs arrive through helper
parameters or locals; generated code does not enumerate type combinations or
trace component IDs back to their source expressions. The selected executor
is cached before traversal, so type dispatch does not run per entity.

Open generic definitions with unsupported constraints produce a compiler
diagnostic at the registration or `ForEach` call. This includes `notnull`,
base-class and interface constraints, and constraints that relate two generic
parameters. Runtime-selected generic dispatch supports only the constraints
listed above.

For context forms, the functor implements `IForEachContext<C>` or
`IForEachContextEntity<C>`. Pass `ref C` after `Q` and before `G...`; its type
must match the marker contract. `Invoke` takes context first, then `EntityRef` for
the entity-aware form, followed by its component rows. The context can itself
be used to carry state such as counters or services:

```csharp
ComponentId valueId = layouts.Register<float>(new SchemaId(20));
ComponentId historyId = layouts.Register(typeof(History<>), valueId, new SchemaId(21));
Query query = world.WhereAll(valueId, historyId);
var context = new HistoryContext();
var saveHistory = world.ForEach(in query, ref context, valueId, typeof(SaveHistory<>));
saveHistory.Invoke(ref context);
saveHistory.Invoke(ref context);

public struct HistoryContext { public int Saved; }
public struct History<T> { public T Value; }

public struct SaveHistory<T> : IForEachContext<HistoryContext>
{
    public void Invoke(ref HistoryContext context, ref History<T> history, in T value)
    {
        history.Value = value;
        context.Saved++;
    }
}
```

The context is strongly typed through the generated adapter. No boxing or
reflection occurs per entity. Its `Invoke` parameter mode controls access:
`ref` changes the caller's context, while `in`/`ref readonly` and by-value
forms provide read-only or local-copy behavior. Parallel forms reject mutable
`ref` context.

The entity-aware context form uses the borrowed `EntityRef`, including in an
open generic functor. With `HistoryContext` and `valueId` from above:

```csharp
var countEntities = world.ForEachEntity(in query, ref context, valueId, typeof(CountEntities<>));
countEntities.Invoke(ref context);

public struct CountEntities<T> : IForEachContextEntity<HistoryContext>
{
    public void Invoke(ref HistoryContext context, EntityRef entity, in T value) => context.Saved++;
}
```

Declare functor and context types where generated code can access them; the
examples use public top-level types. This identity parameter differs from the
`Entity` parameter of a `WhereEntity` predicate.

Query-wide forms require `Q`:

```text
world.ForEach(in query, componentId0, typeof(SaveHistory<>)).Invoke();
world.ForEachEntity(in query, componentId0, typeof(VisitEntity<>)).Invoke();

world.ForEachParallel(in query, componentId0, typeof(SaveHistory<>), workerCount: 4).Invoke();
world.ForEachEntityParallel(in query, componentId0, typeof(VisitEntity<>), workerCount: 4).Invoke();
```

Entity-list forms accept an array or `ReadOnlySpan<Entity>`. `Q` may further
filter that list; omitting it derives a query from the component rows used by
`Invoke`:

```csharp
world.ForEach(entities, in query, componentId0, typeof(SaveHistory<>)).Invoke();
world.ForEach(entities, componentId0, typeof(SaveHistory<>)).Invoke();
world.ForEachEntity(entities, in query, componentId0, typeof(VisitEntity<>)).Invoke();
world.ForEachEntity(entities, componentId0, typeof(VisitEntity<>)).Invoke();

world.ForEachParallel(entities, in query, componentId0, typeof(SaveHistory<>), workerCount: 4).Invoke();
world.ForEachParallel(entities, componentId0, typeof(SaveHistory<>), workerCount: 4).Invoke();
world.ForEachEntityParallel(entities, in query, componentId0, typeof(VisitEntity<>), workerCount: 4).Invoke();
world.ForEachEntityParallel(entities, componentId0, typeof(VisitEntity<>), workerCount: 4).Invoke();
```

`IForEach` receives only the component rows declared by `Invoke`. Implement
`IForEachEntity` to also receive the current borrowed `EntityRef` as its first
argument.
Context marker variants prepend their typed context to the same signature. The
callback may use `ref`, `in`, `ref readonly`, or by-value component parameters,
as in the regular generated functor API. Parallel callbacks run concurrently
and must coordinate shared mutable state.

Generic functor argument lists are generated for every generic arity found in
the consumer assembly. The count and order of positional IDs or IDs in `D`
must match the open functor's generic parameters. For example, two generic
parameters can bind three callback rows, including a derived registration:

```csharp
world.ForEach(in query, firstId, secondId, typeof(CopyPair<,>)).Invoke();
world.ForEach(in query, id0, id1, id2, typeof(CopyTriple<,,>)).Invoke();

ReadOnlySpan<ComponentId> genericArguments = stackalloc ComponentId[] { firstId, secondId };
world.ForEach(in query, genericArguments, typeof(CopyPair<,>)).Invoke();

public struct CopyPair<TFirst, TSecond> : IForEach
{
    public void Invoke(in TFirst first, in TSecond second) { }
}

public struct CopyTriple<TFirst, TSecond, TThird> : IForEach
{
    public void Invoke(in TFirst first, in TSecond second, in TThird third) { }
}
```

Order and repeated IDs are preserved. Generic arity is independent of callback
row count: for example, `SaveHistory<T>.Invoke(ref History<T>, in T)` accesses
two rows with one generic argument. Register `History<T>` with an explicit
schema ID and the source component ID before iterating. Secondary registrations
of the same CLR type retain distinct derived registrations.

Each `Invoke` creates a default functor and keeps that instance for the whole
traversal. Its fields are pass-local functor state; use a context contract when
caller-owned state needs to be read or updated. The operation caches the closed
executor after its first invocation. The generator must be present
in the assembly defining the accessible generic functor struct. The
type-token dispatcher uses ordinary generic calls; AOT targets still need to
preserve the generated closed generic instantiations used by the application.

## Entity-Component-Linq: ordered query results

`OrderBy` creates an `OrderedQuery` view over an existing `Query`. Use it when
the order in which matching entities are processed matters, for example when
choosing a deterministic rollback order. `ThenBy` adds tie-breaking keys. Every
component used by a key must be included in the query's `WhereAll` filter.

Implement `IComponentComparer` for a reusable comparer functor. Its `int
Invoke` method receives the component values for the left entity followed by
the corresponding values for the right entity. Pass the functor by `ref`:

```csharp
Query candidates = world.WhereAll<Priority, SyncId, Health>();
var pairComparer = default(PriorityAndSyncIdComparer);
var healthComparer = default(HealthComparer);

OrderedQuery ordered = candidates
    .OrderBy(ref pairComparer)
    .ThenBy(ref healthComparer);

ordered.ForEachEntity(static (EntityRef entity, in Priority priority, in SyncId syncId) =>
{
    ProcessInOrder(entity, priority, syncId);
}).Invoke();

Entity first = ordered.First().Invoke();

public struct PriorityAndSyncIdComparer : IComponentComparer
{
    public int Invoke(
        in Priority leftPriority,
        in SyncId leftSyncId,
        in Priority rightPriority,
        in SyncId rightSyncId)
    {
        int priorityOrder = leftPriority.Value.CompareTo(rightPriority.Value);
        return priorityOrder != 0
            ? priorityOrder
            : leftSyncId.Value.CompareTo(rightSyncId.Value);
    }
}

public struct HealthComparer : IComponentComparer
{
    public int Invoke(in Health left, in Health right)
        => left.Value.CompareTo(right.Value);
}
```

Use `IComponentComparerEntity` when the comparison also needs the entity
handles. Its `Invoke` signature places the left `Entity` before the left
component values, then the right `Entity` before the right values. A context may
be the first parameter in either comparer contract:

```csharp
var entityComparer = default(SyncIdAndEntityComparer);
OrderedQuery byStableEntity = candidates.OrderBy(ref entityComparer);

public struct SyncIdAndEntityComparer : IComponentComparerEntity
{
    public int Invoke(
        Entity leftEntity,
        in SyncId leftSyncId,
        Entity rightEntity,
        in SyncId rightSyncId)
    {
        int order = leftSyncId.Value.CompareTo(rightSyncId.Value);
        return order != 0 ? order : leftEntity.Index.CompareTo(rightEntity.Index);
    }
}
```

For one-off comparisons, pass a typed delegate lambda instead of declaring a
functor. The callback receives values in the same left-then-right order. Static
lambdas are intercepted by the generator and emitted as direct comparer code;
capturing lambdas use the generated delegate adapter.

```csharp
var orderingContext = new OrderingContext { Offset = 3 };

OrderedQuery byPriority = candidates.OrderBy(
    static (in Priority left, in Priority right) => left.Value.CompareTo(right.Value));

OrderedQuery byEntity = candidates.OrderBy(
    static (Entity leftEntity, in SyncId left, Entity rightEntity, in SyncId right) =>
        left.Value != right.Value
            ? left.Value.CompareTo(right.Value)
            : leftEntity.Index.CompareTo(rightEntity.Index));

OrderedQuery withContext = candidates.OrderBy(
    in orderingContext,
    static (in OrderingContext context, in Priority left, in Priority right) =>
        (left.Value + context.Offset).CompareTo(right.Value + context.Offset));

OrderedQuery withContextAndEntity = candidates
    .OrderBy(static (in Priority left, in Priority right) => left.Value.CompareTo(right.Value))
    .ThenBy(
        in orderingContext,
        static (in OrderingContext context, Entity leftEntity, in SyncId left, Entity rightEntity, in SyncId right) =>
        {
            int syncOrder = left.Value.CompareTo(right.Value);
            return syncOrder != 0
                ? syncOrder
                : (leftEntity.Index + context.Offset).CompareTo(rightEntity.Index + context.Offset);
        });

struct OrderingContext
{
    public int Offset;
}
```

The delegate grammar also accepts a positional `ComponentId` for each key
component or one `ReadOnlySpan<ComponentId>`, with an optional context after
those selectors. Entity-aware delegates place `Entity` before each side's
component values, matching `IComponentComparerEntity`.

The context is copied into the `OrderedQuery` when the comparer key is added.
Treat it as comparer-owned state for subsequent comparisons; mutations to that
copy do not update the original variable.

The primary-registration form uses the primary registration for every
component in the functor's `Invoke` signature. When a CLR type has multiple
registrations, pass one positional `ComponentId` per compared component. A
`ReadOnlySpan<ComponentId>` can supply the same list dynamically:

```csharp
ComponentId priorityId = world.Components.Register<Priority>(new SchemaId(30));
ComponentId syncIdComponentId = world.Components.Register<SyncId>(new SchemaId(31));
ComponentId healthId = world.Components.Register<Health>(new SchemaId(32));
Query candidatesById = world.WhereAll(priorityId, syncIdComponentId, healthId);
var orderingContext = new OrderingContext { Offset = 3 };

OrderedQuery orderedByIds = candidatesById
    .OrderBy(priorityId, syncIdComponentId,
        static (in Priority leftPriority, in SyncId leftSync, in Priority rightPriority, in SyncId rightSync) =>
        {
            int priorityOrder = leftPriority.Value.CompareTo(rightPriority.Value);
            return priorityOrder != 0 ? priorityOrder : leftSync.Value.CompareTo(rightSync.Value);
        })
    .ThenBy(healthId, in orderingContext,
        static (in OrderingContext context, in Health left, in Health right) =>
            (left.Value + context.Offset).CompareTo(right.Value + context.Offset));

var pairComparerById = default(PriorityAndSyncIdComparer);
var healthComparerById = default(HealthComparer);
OrderedQuery orderedHealth = candidatesById
    .OrderBy(priorityId, syncIdComponentId, ref pairComparerById)
    .ThenBy(healthId, ref healthComparerById);

orderedHealth.ForEach<Health, SyncId>(healthId, syncIdComponentId,
    static (ref Health health, in SyncId syncId) => Apply(health, syncId)).Invoke();
```

The generated `OrderBy` and `ThenBy` forms also accept an explicit
`ReadOnlySpan<ComponentId>` selector. The generated comparer adapter validates
the selector length, each registration's CLR type, and membership in
`WhereAll` before sorting.

`ThenBy` adds another lexicographic comparer key and may be chained without a
fixed arity limit. One comparer may combine multiple components in a single
key; a later `ThenBy` compares only entities tied by all earlier keys. Ordering
keys may include tags; since tags have no stored value, their comparer receives
the tag's default value. Equal keys preserve the source query's iteration
order.

Compose `Query` component filters such as `WhereAll`, `WhereAny`, and
`WhereNone` before calling `OrderBy`. Predicate views created by the generated
`Where` and `WhereEntity` views can be ordered before selecting a result. The
view retains its predicate and source query: `First` evaluates it while
scanning the source query and compares only matching entities. Use this when a
selection has both a gameplay condition and a deterministic priority order:

```csharp
var pairComparer = default(PriorityAndSyncIdComparer);
var healthComparer = default(HealthComparer);
Entity target = world.WhereEntity(in candidates,
        static (Entity entity, in Health health) => health.Value > 0)
    .OrderBy(ref pairComparer)
    .ThenBy(ref healthComparer)
    .First().Invoke();
```

The filtered ordering supports `OrderBy`, `ThenBy`, `First`, `FirstEntity`,
`ForEach`, and `ForEachEntity`. It preserves the predicate view's context and
functor state. `First` scans matching entities and selects the minimum by the
ordering keys. `ForEach` collects only entities that pass the predicate, sorts
that list, then invokes the generated iteration path in that order. Neither
operation changes the source `Query`.

```text
world.Where(Q, C?, A | F) -> WhereView
world.WhereEntity(Q, C?, A | F) -> WhereView
WhereView.OrderBy(I... | D, C?, A | F) -> OrderedWhereView
OrderedWhereView.ThenBy(I... | D, C?, A | F) -> OrderedWhereView
OrderedWhereView.First(P?) -> EcsResultOperation<Entity>; Invoke() -> Entity
OrderedWhereView.ForEach(...) -> EcsOperation<TInvoker>; Invoke() executes
OrderedWhereView.ForEachEntity(...) -> EcsOperation<TInvoker>; Invoke() executes
```

`WhereView` and `OrderedWhereView` in this grammar name generated API families;
they are not literal consumer type names for field declarations. Use `var`
for a local chain. To retain the result in a field, build its terminal once
and store that operation as `IOperation` or `EcsResultOperation<TResult>`.
There is no `AsOperation()` conversion; assignment to the operation interface
performs the one-time boxing described below.

Void-returning deferred operations are mutable value types. Their concrete
executor type is inferred by the compiler:

```text
EcsOperation<TInvoker> : IOperation
EcsOperation<TState, TInvoker> : IOperation<TState>
EcsOperation<TContext, TFunctor, TInvoker> : IOperation<TContext, TFunctor>
EcsResultOperation<TResult> : IResultOperation<TResult>; Invoke() -> TResult
```

`IOperation<TState>` uses caller-owned mutable state; `IResultOperation<TResult>`
represents a deferred operation that returns a typed result. Stateful operations
expose `Invoke` overloads for operation-owned state, caller-owned state, or both.
Invoke the concrete value directly to avoid boxing. Store heterogeneous
operations as `IOperation` (or a state-specific interface) to box once, store
result operations as `IResultOperation<TResult>`, or pass the concrete operation
to a generic method constrained by the matching operation interface.

The ordering selectors and comparer can be omitted when the generated primary
registration and callback inference are sufficient. `P` is an optional
`Func<Entity, bool>` applied after the source `Where` predicate.

`OrderedQuery.ForEach` and `ForEachEntity` use the generated sequential
iteration grammar: callbacks or functors, optional context, component rows,
entity-aware callbacks, primary registrations, explicit positional IDs, and
dynamic `ReadOnlySpan<ComponentId>` forms. There are no parallel terminals for
an ordered view. The terminal collects the matching entity handles, performs a
stable full sort using `World`-owned reusable buffers, then passes the sorted
handles through the normal generated iteration path. Reuse the `OrderedQuery`
value to sort again after key values change. The source `Query` and its ordinary
iteration order are unchanged.

`First` returns the first entity in lexicographic order, or `default(Entity)`
when there is no match. Its `Func<Entity, bool>` overload returns the first
ordered entity accepted by the predicate. These terminals find the minimum in
one pass and do not sort the whole result.
