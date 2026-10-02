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
G...  — ComponentId arguments that close a generic functor type
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

`T...` identifies the CLR row types. In ordinary typed iteration, `I...`
selects the registration used for each row; omitting it uses the primary
registration. When both `T...` and `I...` are present, their counts equal the
row arity. In runtime-selected generic functor calls, `G...` closes the open
functor type and is independent of the component rows accepted by `Invoke`.
Query factories are the exception: their typed and `ComponentId` forms are
separate. `Q` is required for world-wide query iteration and optional after an
explicit entity target `E`.

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
world.Add<T...>(e | E, I..., V...)
world.Destroy(e | E | Q)

world.Add(e | E | Q, I...)
world.Remove(e | E | Q, I...)
world.Add(e, V...)

world.Create<T...>(N, O?)
world.Create<T...>(I..., N, O?)
world.Create<T...>(I..., V...) -> Entity
world.Create(I..., N, O?)
```

The `I...` forms are positional `ComponentId` arguments. `Add` and `Remove`
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

## Runtime-selected generic functors

An open generic component can be registered using the CLR types of existing
registrations. Supply its `SchemaId` explicitly. Positional overloads support
one or two generic arguments directly; when a consumer uses a higher-arity
generic component, the generator emits the matching `Register` overload:

```csharp
ComponentId historyId = layouts.Register(typeof(History<>), new SchemaId(100), positionId);
ComponentId pairId = layouts.Register(typeof(ComponentPair<,>), new SchemaId(101), positionId, velocityId);
```

The generated runtime-selected functor API supports the same targets and
execution modes for component-row iteration as the ordinary functor API. The
`ComponentId` values here close the open generic functor type; they are not
required to match the number of callback rows. In this section, `G...` denotes
those positional `ComponentId` arguments. `typeof(Functor<>)` supplies the open
generic definition.

The complete forms are:

```text
world.ForEach(Q, C?, G..., typeof(F<>))
world.ForEachEntity(Q, C?, G..., typeof(F<>))
world.ForEach(E, Q?, C?, G..., typeof(F<>))
world.ForEachEntity(E, Q?, C?, G..., typeof(F<>))

world.ForEachParallel(Q, C?, G..., typeof(F<>), W)
world.ForEachEntityParallel(Q, C?, G..., typeof(F<>), W)
world.ForEachParallel(E, Q?, C?, G..., typeof(F<>), W)
world.ForEachEntityParallel(E, Q?, C?, G..., typeof(F<>), W)
```

These forms cover component-row `ForEach` calls only. The separate
`ForEachStamp`, `ForEachEntityStamp`, and parallel stamp APIs do not currently
accept an open generic functor type token.

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
ComponentId historyId = layouts.Register(typeof(History<>), new SchemaId(21), valueId);
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
the consumer assembly. The count and order of `ComponentId` arguments must
match the open functor's generic parameters. For example, two generic
parameters can bind three callback rows, including a derived registration:

```csharp
world.ForEach(in query, firstId, secondId, typeof(CopyPair<,>));
world.ForEach(in query, id0, id1, id2, typeof(Action<,,>));
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
