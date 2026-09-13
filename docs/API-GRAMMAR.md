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
selector(I... | T...)?,
context(C)?,
callback(A | F)?,
options(N | O | W)?
```

`T...` selects typed component rows and `I...` selects the same rows by
position with `ComponentId` values. `Q` is required for world-wide query
iteration and optional after an explicit entity target `E`.

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

Functor forms use the same target, query, selector, and context order, with
`F` in the callback position:

```text
world.ForEach<T...>(Q | E, I..., C, F)
world.ForEachEntity<T...>(Q | E, I..., C, F)
world.ForEachParallel<T...>(Q | E, I..., C, F, W)
world.ForEachEntityParallel<T...>(Q | E, I..., C, F, W)
```

`ForEach` callbacks receive component rows. `ForEachEntity` callbacks also
receive the current `Entity` as their first row argument. Parallel callbacks
always use parallel execution; `W` is the caller's worker-count choice,
clamped to the supported range.

## Structural forms

Typed and non-generic structural operations have matching target shapes:

```text
world.Add<T...>(e | E | Q, I...)
world.Remove<T...>(e | E | Q, I...)
world.Set<T...>(e, V...)
world.Destroy(e | E | Q)

world.Add(e | E | Q, I...)
world.Remove(e | E | Q, I...)
world.Set(e, V...)

world.Create<T...>(N, O?)
world.Create<T...>(I..., N, O?)
world.Create(I..., N, O?)
```

The `I...` forms are positional `ComponentId` arguments. `Add` and `Remove`
accept them after the target; `Create` places them before `N` and `O`.

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
view.Remove<T...>()
view.ForEach(...)
view.ForEachEntity(...)
```

The ordinary `Where` predicate is component-only. `WhereEntity` adds the
current `Entity` as its first predicate parameter. Predicate component rows
use `in` or `ref readonly`. A structural terminal applies component writes
before it performs the structural change.

The zero-component delegate, parallel, and functor overloads throw
`InvalidOperationException`. Use a component-bearing callback or functor for
iteration; a functor must provide at least one component parameter.
