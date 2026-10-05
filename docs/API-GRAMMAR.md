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

Use positional IDs when the registrations are known at the call site. Pass an
explicit `ReadOnlySpan<ComponentId>` when the list is assembled dynamically;
the span is an ordinary parameter, not a `params` argument. The old
`params ReadOnlySpan<ComponentId>` modifier has been removed. Since `params`
does not change the CLR method signature, an obsolete `params` overload cannot
coexist with the explicit-span overload under the same name and parameter
types.

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

`ForEach` callbacks receive component rows. `ForEachEntity` callbacks also
receive the current `Entity` as their first row argument. Parallel callbacks
always use parallel execution; `W` is the caller's worker-count choice,
clamped to the supported range. A parallel context is passed read-only or by
value; mutable `ref` context is rejected.

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
`ForEachEntityStamp` variants put `Entity` first:

```csharp
world.ForEachEntityStamp<Health>(
    in query,
    static (Entity entity, in Stamp stamp) => Process(entity, stamp));

world.ForEachStampParallel<Health>(
    in query,
    static (in Stamp stamp) => Process(stamp),
    workerCount: 4);

world.ForEachEntityStamp(
    entities,
    in query,
    healthId,
    static (Entity entity, in Stamp stamp) => Process(entity, stamp));
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
without retaining handles. Structural terminals are immediate operations and
cannot run from an active traversal callback.

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
entity-aware spelling is `WhereEntity`. The resulting view is stack-only and
the terminal completes the whole operation immediately:

```text
world.Where(Q, C?, Predicate) -> WhereView
world.WhereEntity(Q, C?, Predicate) -> WhereView

view.Destroy()
view.Add<T...>()
view.Add<T...>(I... | D)
view.Add<T...>(V...)
view.Add<T...>(I..., V...)
view.Remove<T...>()
view.Remove<T...>(I... | D)
view.ForEach(...)
view.ForEachEntity(...)              // Entity-only or Entity plus components
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
world.ForEach(in query, ref context, valueId, typeof(SaveHistory<>));
```

The context is strongly typed through the generated adapter. No boxing or
reflection occurs per entity. Its `Invoke` parameter mode controls access:
`ref` changes the caller's context, while `in`/`ref readonly` and by-value
forms provide read-only or local-copy behavior. Parallel forms reject mutable
`ref` context.

Query-wide forms require `Q`:

```csharp
world.ForEach(in query, componentId0, typeof(SaveHistory<>));
world.ForEachEntity(in query, componentId0, typeof(VisitEntity<>));

world.ForEachParallel(in query, componentId0, typeof(SaveHistory<>), workerCount: 4);
world.ForEachEntityParallel(in query, componentId0, typeof(VisitEntity<>), workerCount: 4);
```

Entity-list forms accept an array or `ReadOnlySpan<Entity>`. `Q` may further
filter that list; omitting it derives a query from the component rows used by
`Invoke`:

```csharp
world.ForEach(entities, in query, componentId0, typeof(SaveHistory<>));
world.ForEach(entities, componentId0, typeof(SaveHistory<>));
world.ForEachEntity(entities, in query, componentId0, typeof(VisitEntity<>));
world.ForEachEntity(entities, componentId0, typeof(VisitEntity<>));

world.ForEachParallel(entities, in query, componentId0, typeof(SaveHistory<>), workerCount: 4);
world.ForEachParallel(entities, componentId0, typeof(SaveHistory<>), workerCount: 4);
world.ForEachEntityParallel(entities, in query, componentId0, typeof(VisitEntity<>), workerCount: 4);
world.ForEachEntityParallel(entities, componentId0, typeof(VisitEntity<>), workerCount: 4);
```

`IForEach` receives only the component rows declared by `Invoke`. Implement
`IForEachEntity` to also receive the current `Entity` as its first argument.
Context marker variants prepend their typed context to the same signature. The
callback may use `ref`, `in`, `ref readonly`, or by-value component parameters,
as in the regular generated functor API. Parallel callbacks run concurrently
and must coordinate shared mutable state.

Generic functor argument lists are generated for every generic arity found in
the consumer assembly. The count and order of positional IDs or IDs in `D`
must match the open functor's generic parameters. For example, two generic
parameters can bind three callback rows, including a derived registration:

```csharp
world.ForEach(in query, firstId, secondId, typeof(CopyPair<,>));
world.ForEach(in query, id0, id1, id2, typeof(Action<,,>));

ReadOnlySpan<ComponentId> genericArguments = stackalloc ComponentId[] { firstId, secondId };
world.ForEach(in query, genericArguments, typeof(CopyPair<,>));
```

Order and repeated IDs are preserved. Generic arity is independent of callback
row count: for example, `SaveHistory<T>.Invoke(ref History<T>, in T)` accesses
two rows with one generic argument. Register `History<T>` with an explicit
schema ID and the source component ID before iterating. Secondary registrations
of the same CLR type retain distinct derived registrations.

Each invocation creates a default functor and keeps that instance for the whole
traversal. Its fields are pass-local functor state; use a context contract when
caller-owned state needs to be read or updated. The generator must be present
in the assembly defining the accessible generic functor struct. The
type-token dispatcher uses ordinary generic calls; AOT targets still need to
preserve the generated closed generic instantiations used by the application.
