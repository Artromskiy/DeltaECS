# DeltaECS API grammar

This is the canonical public grammar for the DeltaECS API. Read it when using
the public entry points or documenting a consumer example. The same argument
order applies to generic, non-generic, delegate, functor, and parallel forms.

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
world.Layouts -> ComponentLayoutRegistry
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

`Register<T>(schemaId)` returns the component ID directly. The registry's marker-route overloads select the strongest standard constraint visible for `T` (`class, new()`, `unmanaged`, `struct`, `new()`, or `class`). A generic caller with no constraints uses the unconstrained route. To select a narrower route in generic code, declare that constraint on the helper method:

```csharp
ComponentId positionId = layouts.Register<Position>(new SchemaId(1));

static ComponentId RegisterNew<T>(ComponentLayoutRegistry layouts, SchemaId schemaId)
    where T : new()
    => layouts.Register<T>(schemaId);

ComponentId constructibleId = RegisterNew<Constructible>(layouts, new SchemaId(3));
```

`BindInterface<TComponent, TInterface>()` declares a route for the interface-based visitor contracts for one concrete component CLR type and one interface. It applies to every schema registration of that CLR type, including registrations made before or after the binding. The compiler checks that `TComponent` implements `TInterface`; binding another component type requires another call. This uses closed generic code directly and does not require generated interface routes:

```csharp
layouts.BindInterface<Position, IMovable>();

ComponentId positionId = layouts.Register<Position>(new SchemaId(4));
ComponentId localPositionId = layouts.Register<Position>(new SchemaId(5));

var visitor = new MovementVisitor();
layouts.Visit(positionId, visitor);
layouts.Visit(localPositionId, visitor);

if (layouts.TryVisit(positionId, visitor))
{
    // The registered component matched the visitor route.
}

public interface IMovable { }

public struct Position : IMovable { }

public sealed class MovementVisitor : IComponentVisitor<IMovable>
{
    public RuntimeTypeHandle ConstraintType => typeof(IMovable).TypeHandle;

    public void Visit<T>(ComponentId componentId) where T : IMovable { }
}
```

The binding is explicit for each component type when using the interface-based visitor contracts. For example, a `Velocity` component that also implements `IMovable` needs its own `layouts.BindInterface<Velocity, IMovable>()` call.

For virtual dispatch, derive from `GeneralComponentTypeVisitor<TConstraint>`. Override the named hooks for the registration shapes the visitor handles. The registry calls the matching hook through the explicitly bound interface route:

```csharp
layouts.BindInterface<Position, IMovable>();
ComponentId positionId = layouts.Register<Position>(new SchemaId(7));

public sealed class RoutedMovementVisitor : GeneralComponentTypeVisitor<IMovable>
{
    protected override void VisitStruct<T>(ComponentId componentId)
    {
        // T is a struct that implements IMovable.
    }

    protected override void VisitClass<T>(ComponentId componentId)
    {
        // T is a class that implements IMovable.
    }
}

layouts.TryVisit(positionId, new RoutedMovementVisitor());
```

The hooks are `Visit<T>` for an unconstrained route, `VisitUnmanaged<T>`, `VisitStruct<T>`, `VisitClass<T>`, `VisitConstructible<T>`, and `VisitClassConstructible<T>`. They are ordinary virtual methods with their constraints declared by the base class; no marker argument is needed in an override. This path uses the closed generic component route and virtual dispatch. Interface binding remains explicit for each component CLR type.

Every registration route also supports `IUnconstrainedVisitor`, which receives the registered CLR type and ID without requiring a marker or interface route. More constrained visitors still match only routes satisfying their constraints. Use `TryVisit` when the route may not match or the component IDs come from a dynamic list. It returns `false` for an invalid ID, a `null` visitor, or an unsupported constrained route. `Visit` has the same dispatch behavior but silently does nothing for those cases:

```csharp
ReadOnlySpan<ComponentId> componentIds = stackalloc ComponentId[] { positionId, localPositionId };
var visitor = new MovementVisitor();

foreach (ComponentId componentId in componentIds)
{
    if (layouts.TryVisit(componentId, visitor))
    {
        // This entry was visited as IMovable.
    }
}
```

The runtime APIs described here are available without adding the generator to the consuming project. The generated API grammar proof and runtime-only API grammar proof are kept in separate consumer projects so both dependency shapes are compiled and executed independently. The runtime-only proof covers registration and visitors, query construction, structural operations, typed single-component access, entity-only iteration, and the integration contract. Component-bearing iteration and other generated forms remain in the generated grammar proof. Both proofs are invoked by the grammar smoke application and the generator test suite.

`Register<T>(SchemaId)` and `BindInterface<T, TInterface>()` select a route from the generic constraints visible at the call site. The registry exposes a hidden marker-interface chain to let ordinary C# overload resolution choose the most specific applicable route. This works with C# 9 and does not require a consumer source generator:

```csharp
ComponentId id = layouts.Register<Position>(new SchemaId(1));
layouts.BindInterface<Position, IMovable>();
```

In a generic helper, declare any constraint that should select a narrower route:

```csharp
static ComponentId RegisterStruct<T>(ComponentLayoutRegistry layouts, SchemaId schemaId)
    where T : struct
    => layouts.Register<T>(schemaId);
```

An unconstrained `T` selects the unconstrained route even if its eventual closed type happens to be a struct.

The registry exposes the primary registration and CLR type for existing
components. `GetPrimary` throws if the type is not registered; `TryGetPrimary`
returns `false` and `ComponentId.Invalid`. Type overloads are useful when the
CLR type is known only at runtime:

```text
layouts.GetComponentType(I) -> Type
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

## Runtime integration contract

`World` implements `IEcsWorld` explicitly. Cast it to this neutral contract
when a tooling or runtime bridge needs registration IDs and object-based
component access:

```text
IEcsWorld integration = world
integration.Catalog -> RuntimeComponentCatalog
catalog.Components -> ReadOnlyMemory<ComponentDescriptor>
catalog.Stamp -> Stamp
descriptor.Id / Schema / Name / ValueType / Capabilities / AllowsNull / IsTag
ComponentCapabilities = None | Read | Write
integration.Initialize() / Update() / Shutdown()
integration.IsAlive(e) -> bool
integration.Create(D) -> Entity
integration.Destroy(e) -> bool
integration.Add(e, D) / Remove(e, D) -> bool
integration.TryGetComponents(e, Span<ComponentId>, out totalCount) -> bool
integration.TryRead(e, I, out ComponentSnapshot, out EcsReadError) -> bool
integration.TryWrite(e, I, object?, expectedStamp, out writtenStamp, out EcsWriteError) -> bool
snapshot.Value / snapshot.Stamp
EcsReadErrorCode = None | EntityNotAlive | ComponentUnknown | ComponentMissing | Unsupported
EcsWriteErrorCode = None | EntityNotAlive | ComponentUnknown | ComponentMissing | StaleStamp | InvalidValue | Unsupported
```

`TryGetComponents` writes the ascending prefix that fits in the destination
and reports the full count. `TryWrite` checks the expected stamp before
changing the component. The error enums distinguish missing entities or
components, unsupported object access, invalid values, and stale stamps. This
contract works without generated consumer code; the component catalog itself
is produced by the registration generator.

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
world.ForEachEntity(in query, static (EntityRef entity) =>
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
missing or its registration has another CLR type, and writes to a data
component update its stamp. A tag has no per-entity value: its `TryGet` result
is `default(T)`, and writes through `GetRef` are ignored.

For several query-specific processors, retain one archetype operation and add
its queries during setup. `Invoke` reuses the per-archetype query plan and
runs processors in registration order for each matching archetype:

```csharp
var restore = world
    .ForEachArchetype(restoreState)
    .Process(in healthQuery, new RestoreHealth())
    .Process(in positionQuery, new RestorePosition());

restore.Invoke(ref restoreState);
```

`IArchetypeForEachEntity<TContext>` processors receive the shared context and
the current borrowed `EntityRef`. This API is available without the source
generator and can be configured from a component visitor loop before the
operation is retained and invoked repeatedly.

For direct component access, bind the required data component's `ComponentId`
at setup. The row index is cached per matching archetype, and that processor
walks the typed component row directly instead of resolving every access
through `EntityRef`:

```csharp
restore.Process<Health, RestoreHealth>(in healthQuery, healthId, new RestoreHealth());
```

The component must be a data component in the query's `WhereAll` filter. Its
write stamp is updated for each processed entity.

`EntityRef` also supports primary-registration forms. Supply a `ComponentId`
when the entity has multiple registrations of the same CLR type:

```text
implicit Entity entity = entityRef; entityRef.Index; entityRef.Generation
entity.Has(I) | entity.Has<T>() | entity.Has<T>(I) -> bool
entity.TryGet<T>(out T) | entity.TryGet<T>(I, out T) -> bool
entity.Get<T>() | entity.Get<T>(I) -> T
entity.GetRef<T>() | entity.GetRef<T>(I) -> ref T
entity.GetReadRef<T>() | entity.GetReadRef<T>(I) -> ref readonly T
entity.TryGetComponentStamp<T>(out Stamp) -> bool
entity.TryGetComponentStamp(I, out Stamp) -> bool
entity.TryGetComponentStamp<T>(I, out Stamp) -> bool
```

Use `EntityRef` only while its callback is running and before any structural
change; assign it to `Entity` when the handle must be retained. The view already
contains the current world, chunk, archetype, and slot, so each access skips
entity-handle resolution. It does not pre-bind component rows from the query.
Parallel callbacks always use parallel execution; `W` is the caller's
worker-count choice, clamped to the supported range. A parallel context is
passed read-only or by value; mutable `ref` context is rejected.

## Stamp iteration forms

Stamp iteration uses the same target, query, selector, context, callback, and
worker ordering. It is read-only: callbacks receive `in Stamp` (or
`ref readonly Stamp` where the language supports it), and no stamp is changed.

Query-wide forms:

```text
world.ForEachStamp<T...>(Q, I... | D, C?, A | F)
world.ForEachEntityStamp<T...>(Q, I... | D, C?, A | F)
world.ForEachStamp(Q, I... | D, C?, A | F)
world.ForEachEntityStamp(Q, I... | D, C?, A | F)

world.ForEachStampParallel<T...>(Q, I... | D, C?, A | F, W)
world.ForEachEntityStampParallel<T...>(Q, I... | D, C?, A | F, W)
world.ForEachStampParallel(Q, I... | D, C?, A | F, W)
world.ForEachEntityStampParallel(Q, I... | D, C?, A | F, W)
```

Entity-list forms accept `E` first and an optional `Q` after it:

```text
world.ForEachStamp<T...>(E, Q?, I... | D, C?, A | F)
world.ForEachEntityStamp<T...>(E, Q?, I... | D, C?, A | F)
world.ForEachStamp(E, Q?, I... | D, C?, A | F)
world.ForEachEntityStamp(E, Q?, I... | D, C?, A | F)

world.ForEachStampParallel<T...>(E, Q?, I... | D, C?, A | F, W)
world.ForEachEntityStampParallel<T...>(E, Q?, I... | D, C?, A | F, W)
world.ForEachStampParallel(E, Q?, I... | D, C?, A | F, W)
world.ForEachEntityStampParallel(E, Q?, I... | D, C?, A | F, W)
```

`ForEachStamp` callbacks receive only the requested stamps. The
`ForEachEntityStamp` variants put the borrowed `EntityRef` first:

```csharp
world.ForEachEntityStamp<Health>(
    in query,
    static (EntityRef entity, in Stamp stamp) => Process(entity, stamp)).Invoke();

world.ForEachStampParallel<Health>(
    in query,
    static (in Stamp stamp) => Process(stamp),
    workerCount: 4).Invoke();

world.ForEachEntityStamp(
    entities,
    in query,
    healthId,
    static (EntityRef entity, in Stamp stamp) => Process(entity, stamp)).Invoke();
```

The zero-component stamp overloads are documentation anchors and throw
`InvalidOperationException`; a generated stamp form must name at least one
typed component or provide the corresponding `ComponentId` selector.

## Structural forms

Direct `World` structural calls execute immediately. Entity targets return
`bool` to report whether that entity changed; entity-list and query targets
return the number of changed entities. Generated typed forms accept primary
registrations or explicit selectors:

```text
world.Add<T...>(e | E | Q) -> bool | int
world.Add<T...>(e | E, I... | D) -> bool | int
world.Add<T...>(e | E, V...) -> bool | int
world.Add<T...>(e | E, I... | D, V...) -> bool | int
world.Remove<T...>(e | E | Q) -> bool | int
world.Remove<T...>(e | E, I... | D) -> bool | int
world.Destroy(e) -> bool
world.Destroy(E | Q) -> int

world.Add(e | E | Q, I... | D) -> bool | int
world.Remove(e | E | Q, I... | D) -> bool | int
world.Add(e | E, V...) -> bool | int

world.Create<T>() -> Entity
world.Create<T...>(N, O?) -> int
world.Create<T...>(I... | D) -> Entity
world.Create<T...>(I... | D, N, O?) -> int
world.Create<T...>(I... | D, V...) -> Entity
world.Create(I... | D) -> Entity
world.Create(I... | D, O) -> int
world.Create(I... | D, N, O?) -> int
```

`I...` forms are positional `ComponentId` arguments; `D` is one explicit
`ReadOnlySpan<ComponentId>` at the same selector position. `Add` and `Remove`
accept them after the target; `Create` places them before `N` and `O`.
Generic value types may be inferred from `V...`; multi-component value-based
`Create` and `Add` forms require explicit registrations, and the positions of
`I...` and `V...` correspond to component types in `T...`. Multi-value `Add`
applies all values during one combined structural transition per eligible
entity. The runtime's primary-registration `Create<T>()` is the single-component
convenience form.

`N` is the number of entities to create and `O` is optional caller-owned
output storage. Without `O`, created handles are not retained. Single-entity
`Create` returns its `Entity`; count forms return the number created. Direct
structural calls cannot run from an active traversal callback. A structural
terminal on a generated `Where` view instead returns a deferred operation that
runs on `Invoke()`.

## Single-entity typed access

Typed accessors use the primary registration unless an explicit ID is supplied.
`TryGet`, `Has`, and stamp lookups return `false` when the entity, component, or
CLR type does not match. `Get` and `GetRef` throw when the requested component
is unavailable; `GetReadRef` returns a read-only reference.

```text
world.TryGet<T>(e, out T) -> bool
world.TryGet<T>(e, I, out T) -> bool
world.Has(e, I) | world.Has<T>(e) | world.Has<T>(e, I) -> bool
world.Get<T>(e) | world.Get<T>(e, I) -> T
world.GetRef<T>(e) | world.GetRef<T>(e, I) -> ref T
world.GetReadRef<T>(e) | world.GetReadRef<T>(e, I) -> ref readonly T
world.TryGetComponentStamp<T>(e, out Stamp) -> bool
world.TryGetComponentStamp<T>(e, I, out Stamp) -> bool
world.TryGetComponentStamp(e, I, out Stamp) -> bool
```

`Add<T>(e, in value)` and `Add<T>(E, in value)` initialize the primary
registration, returning `bool` or a changed-entity count. Typed `Remove<T>`
also has primary and explicit-ID entity and entity-list forms. Generated
multi-component forms use the structural grammar above.

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

world.WhereAll<T...>(D) -> Query
world.WhereAny<T...>(D) -> Query
world.WhereNone<T...>(D) -> Query

query
    .WhereAll<T...>(D)
    .WhereNone<T...>(D)
    .WhereAny<T...>(D) -> Query
```

`WhereAll` appends components to `All`, `WhereNone` appends to `None`, and
`WhereAny` appends to the shared `Any` filter. Typed span forms require the
span length to match their generic arity and validate each registration's CLR
type.

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

Zero-component `ForEach` and all zero-component stamp forms throw
`InvalidOperationException`. `ForEachEntity`, `ForEachEntityParallel`, and a
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
Only stamp iteration is excluded: `ForEachStamp`, `ForEachEntityStamp`, and
their parallel forms do not currently accept an open generic functor type
token.

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
must match the marker contract. `Invoke` takes context first, then `Entity` for
the entity-aware form, followed by its component rows. The context can itself
be used to carry state such as counters or services:

```csharp
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

ComponentId valueId = layouts.Register<float>(new SchemaId(20));
ComponentId historyId = layouts.Register(typeof(History<>), valueId, new SchemaId(21));
Query query = world.WhereAll(valueId, historyId);
var context = new HistoryContext();
var saveHistory = world.ForEach(in query, ref context, valueId, typeof(SaveHistory<>));
saveHistory.Invoke(ref context);
saveHistory.Invoke(ref context);
```

The context is strongly typed through the generated adapter. No boxing or
reflection occurs per entity. Its `Invoke` parameter mode controls access:
`ref` changes the caller's context, while `in`/`ref readonly` and by-value
forms provide read-only or local-copy behavior. Parallel forms reject mutable
`ref` context.

Query-wide forms require `Q`:

```csharp
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
world.ForEach(in query, id0, id1, id2, typeof(Action<,,>)).Invoke();

ReadOnlySpan<ComponentId> genericArguments = stackalloc ComponentId[] { firstId, secondId };
world.ForEach(in query, genericArguments, typeof(CopyPair<,>)).Invoke();
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
```

Use `IComponentComparerEntity` when the comparison also needs the entity
handles. Its `Invoke` signature places the left `Entity` before the left
component values, then the right `Entity` before the right values. A context may
be the first parameter in either comparer contract:

```csharp
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

var entityComparer = default(SyncIdAndEntityComparer);
OrderedQuery byStableEntity = candidates.OrderBy(ref entityComparer);
```

For one-off comparisons, pass a typed delegate lambda instead of declaring a
functor. The callback receives values in the same left-then-right order. Static
lambdas are intercepted by the generator and emitted as direct comparer code;
capturing lambdas use the generated delegate adapter.

```csharp
struct OrderingContext
{
    public int Offset;
}

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
ComponentId priorityId = world.Layouts.Register<Priority>(new SchemaId(30));
ComponentId syncIdComponentId = world.Layouts.Register<SyncId>(new SchemaId(31));
ComponentId healthId = world.Layouts.Register<Health>(new SchemaId(32));
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

Void-returning deferred operations are mutable value types. Their concrete
executor type is inferred by the compiler:

```text
EcsOperation<TInvoker> : IOperation
EcsOperation<TState, TInvoker> : IOperation<TState>
EcsOperation<TContext, TFunctor, TInvoker> : IOperation<TContext, TFunctor>
EcsResultOperation<TResult>.Invoke() -> TResult
```

Stateful operations expose `Invoke` overloads for operation-owned state,
caller-owned state, or both. Invoke the concrete value directly to avoid
boxing. Store heterogeneous operations as `IOperation` (or a state-specific
interface) to box once, or pass the concrete operation to a generic method
constrained by the matching operation interface.

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
