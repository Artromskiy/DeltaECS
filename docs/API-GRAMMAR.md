# DeltaECS API grammar

This is the canonical public grammar for the DeltaECS API. Read it when using
the public entry points or documenting a consumer example. The same argument
order applies to generic, non-generic, delegate, functor, and parallel forms.

## Grammar symbols

```text
T...  — one or more CLR component types
I...  — positional list of ComponentId values
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
```

The canonical argument order is:

```text
target(e | E)?,
query(Q)?,
component-types(T... | inferred)?,
registrations(I...)?,
context(C)?,
callback(A | F)?,
values(V...)?,
options(N | O | W)?
```

`T...` identifies the CLR row types. `I...` independently selects the
registration used for each row; omitting `I...` uses the primary registration.
Whenever both are present, their counts equal the component arity. Query
factories are the exception: their typed and `ComponentId` forms are separate.
`Q` is required for world-wide query iteration and optional after an explicit
entity target `E`.

## Generated iteration forms

Delegate and intercepted-lambda forms are generated with these shapes:

```text
world.ForEach<T...>(Q, I..., C, A)
world.ForEachEntity<T...>(Q, I..., C, A)

world.ForEach<T...>(E, Q?, I..., C, A)
world.ForEachEntity<T...>(E, Q?, I..., C, A)

world.ForEachParallel<T...>(Q, I..., C, A, W)
world.ForEachEntityParallel<T...>(Q, I..., C, A, W)

world.ForEachParallel<T...>(E, Q?, I..., C, A, W)
world.ForEachEntityParallel<T...>(E, Q?, I..., C, A, W)
```

Entity-aware iteration may omit the component selector entirely. The callback
still receives the current entity, so these are useful zero-arity forms:

```text
world.ForEachEntity(Q, C?, A | F)
world.ForEachEntity(E, Q?, C?, A | F)
world.ForEachEntityParallel(Q, C?, A | F, W)
world.ForEachEntityParallel(E, Q?, C?, A | F, W)
```

Functor forms use the same target, query, selector, and context order, with
`F` in the callback position:

```text
world.ForEach<T...>(Q, I..., C, F)
world.ForEachEntity<T...>(Q, I..., C, F)
world.ForEach<T...>(E, Q?, I..., C, F)
world.ForEachEntity<T...>(E, Q?, I..., C, F)

world.ForEachParallel<T...>(Q, I..., C, F, W)
world.ForEachEntityParallel<T...>(Q, I..., C, F, W)
world.ForEachParallel<T...>(E, Q?, I..., C, F, W)
world.ForEachEntityParallel<T...>(E, Q?, I..., C, F, W)
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
world.ForEachStamp<T...>(Q, C?, A | F)
world.ForEachEntityStamp<T...>(Q, C?, A | F)
world.ForEachStamp(Q, I..., C?, A | F)
world.ForEachEntityStamp(Q, I..., C?, A | F)

world.ForEachStampParallel<T...>(Q, C?, A | F, W)
world.ForEachEntityStampParallel<T...>(Q, C?, A | F, W)
world.ForEachStampParallel(Q, I..., C?, A | F, W)
world.ForEachEntityStampParallel(Q, I..., C?, A | F, W)
```

Entity-list forms accept `E` first and an optional `Q` after it:

```text
world.ForEachStamp<T...>(E, Q?, C?, A | F)
world.ForEachEntityStamp<T...>(E, Q?, C?, A | F)
world.ForEachStamp(E, Q?, I..., C?, A | F)
world.ForEachEntityStamp(E, Q?, I..., C?, A | F)

world.ForEachStampParallel<T...>(E, Q?, C?, A | F, W)
world.ForEachEntityStampParallel<T...>(E, Q?, C?, A | F, W)
world.ForEachStampParallel(E, Q?, I..., C?, A | F, W)
world.ForEachEntityStampParallel(E, Q?, I..., C?, A | F, W)
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
world.Add<T...>(e | E | Q, I...)
world.Remove<T...>(e | E | Q, I...)
world.Add<T...>(e, V...)
world.Set<T...>(e, V...)
world.Destroy(e | E | Q)

world.Add(e | E | Q, I...)
world.Remove(e | E | Q, I...)
world.Add(e, V...)
world.Set(e, V...)

world.Create<T...>(N, O?)
world.Create<T...>(I..., N, O?)
world.Create(I..., N, O?)
```

The `I...` forms are positional `ComponentId` arguments. `Add` and `Remove`
accept them after the target; `Create` places them before `N` and `O`.
For value forms, generic type arguments may be inferred from `V...`; all
component values are applied by one generated structural operation.

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

world.WhereAll(I...) -> Query
world.WhereAny(I...) -> Query
world.WhereNone(I...) -> Query

query
    .WhereAll<T...>()
    .WhereNone<T...>()
    .WhereAny<T...>() -> Query

query
    .WhereAll(I...)
    .WhereNone(I...)
    .WhereAny(I...) -> Query
```

`WhereAll` appends components to `All`, `WhereNone` appends to `None`, and
`WhereAny` appends to the shared `Any` filter.

## Where pipeline

`Where` evaluates a predicate over every entity selected by `Q`; the
entity-aware spelling is `WhereEntity`. The resulting view is stack-only and
the terminal completes the whole operation immediately:

```text
world.Where(Q, C?, Predicate) -> WhereView
world.WhereEntity(Q, C?, Predicate) -> WhereView

view.Destroy()
view.Add<T...>()
view.Add<T...>(I...)
view.Add<T...>(V...)
view.Add<T...>(I..., V...)
view.Remove<T...>()
view.Remove<T...>(I...)
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
