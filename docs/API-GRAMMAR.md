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

`Register<T>(schemaId)` returns the component ID directly. The compiler selects the strongest standard constraint known for `T` (`class, new()`, `unmanaged`, `struct`, `new()`, or `class`) when overload-priority support is available. A generic caller with no constraints uses the unconstrained form; pass a marker token only when deliberately selecting a less-specific route:

```csharp
ComponentId positionId = layouts.Register<Position>(new SchemaId(1));

NewConstraint newConstraint = default;
ComponentId constructibleId = layouts.Register<Constructible>(new SchemaId(3), in newConstraint);
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

Use `TryVisit` when the route may not match or the component IDs come from a dynamic list. It returns `false` for an invalid ID, a `null` visitor, or an unsupported visitor route. `Visit` has the same dispatch behavior but silently does nothing for those cases:

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

The runtime APIs described here are available without adding the generator to the consuming project. The generated API proof and runtime-only API proof are kept in separate consumer projects so both dependency shapes are compiled and executed independently. The runtime-only proof covers registration and visitors, query construction, structural operations, typed single-component access, entity-only iteration, and the integration contract. Component-bearing iteration and other generated forms remain in the generated consumer proof.

When compiling with a language version that does not apply `OverloadResolutionPriority`, supply the marker explicitly to avoid relying on automatic selection among the constrained `Register<T>` overloads.

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
exposes the stable handle through `Handle`, plus dynamic `Has`, `TryGet<T>`,
and `GetRef<T>` access without resolving the handle through the world's entity
table. This is useful when the component registrations are selected at runtime:

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

Use `EntityRef` only while its callback is running and before any structural
change; store `entity.Handle` when the entity must be retained. The view already
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
    static (EntityRef entity, in Stamp stamp) => Process(entity.Handle, stamp)).Invoke();

world.ForEachStampParallel<Health>(
    in query,
    static (in Stamp stamp) => Process(stamp),
    workerCount: 4).Invoke();

world.ForEachEntityStamp(
    entities,
    in query,
    healthId,
    static (EntityRef entity, in Stamp stamp) => Process(entity.Handle, stamp)).Invoke();
```

The zero-component stamp overloads are documentation anchors and throw
`InvalidOperationException`; a generated stamp form must name at least one
typed component or provide the corresponding `ComponentId` selector.

## Structural forms

Typed and non-generic structural operations have matching target shapes:

```text
world.Add<T...>(e | E | Q, I... | D)
world.Remove<T...>(e | E | Q, I... | D)
world.Add<T...>(e | E, I..., V...)
world.Destroy(e | E | Q)

world.Add(e | E | Q, I... | D)
world.Remove(e | E | Q, I... | D)
world.Add(e, V...)

world.Create<T...>(N, O?)
world.Create<T...>(I... | D, N, O?)
world.Create<T...>(I... | D, V...) -> Entity
world.Create(I... | D, N, O?)
```

The `I...` forms are positional `ComponentId` arguments; `D` is one explicit
`ReadOnlySpan<ComponentId>` at the same selector position. `Add` and `Remove`
accept them after the target; `Create` places them before `N` and `O`.
For value forms, generic type arguments may be inferred from `V...`. The
positions of `I...` and `V...` correspond to the component types in `T...`.
Multi-value `Add` applies the values while performing one combined structural
transition per eligible entity; `Create` initializes one entity with the
selected registrations.

`O` is optional caller-owned output storage. Omitting it creates entities
without retaining handles. Structural terminals execute synchronously when
their operation is invoked and cannot run from an active traversal callback.

## Query factories

The query builder has three names only. A call returns a new `Query`, so the
chain can continue from either the world or the previous query:

```text
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
an explicit `ReadOnlySpan<ComponentId>` for dynamic lists of any length. For
positional fluent filters, start with `QuerySpec.Empty`; the generator emits
extension overloads for the `WhereAll`, `WhereAny`, and `WhereNone` arities
used by calls in the consumer project. There is no fixed positional arity
limit, and unused overloads are not generated. The generated `World` and
`Query` query factories likewise provide positional overloads for the call-site
arities used by the consumer.

```csharp
QuerySpec spec = QuerySpec.Empty
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
    ProcessInOrder(entity.Handle, priority, syncId);
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
OrderedWhereView.ForEach(...) -> EcsOperation; Invoke() executes
OrderedWhereView.ForEachEntity(...) -> EcsOperation; Invoke() executes
```

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
