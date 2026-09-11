# DeltaECS consumer API generator

The generator is published separately as `DeltaECS.Generators`. Add it as a
`PrivateAssets="all"` analyzer package beside the `DeltaECS` runtime package;
it is build-time input and must not be deployed as a runtime dependency. The
package README is at
[docs/packages/DeltaECS.Generators.README.md](../../packages/DeltaECS.Generators.README.md).

The analyzer emits only the callback and generic structural-operation shapes
requested by a consumer compilation. It does not generate storage, queries,
archetypes or structural kernels.

- Zero-component delegate overloads are handwritten in DeltaECS. Component-
  bearing delegate and functor overloads are generated from the consumer's
  demand and start at arity one. A zero-component functor `Invoke()` is also
  generated on demand; there is no no-op runtime fallback for a functor call.
- Component-bearing callback arities start at one and may extend to 256. This
  is a generator limit, not a limit on the dynamically sized component mask.
- Component parameters use four access literals in generated callback names:
  `R` for `ref readonly T`, `W` for `ref T`, `I` for `in T`, and `V` for a
  by-value `T` copy. `W` is the only writing mode; the other three use a read
  row.
- Component-bearing callbacks may omit the method type list when lambda
  parameters are explicitly typed; the generator infers the component types
  from `ref readonly T`/`ref T`/`in T`/`T` parameters. For example:

  ```csharp
  sequence.ForEach(static (ref Position position, in Velocity velocity) =>
      position.X += velocity.X);

  world.ForEach(in query,
      static (ref readonly Position position, ref Velocity velocity,
              in Acceleration acceleration, Scale scale) =>
      velocity.Value += position.Value + acceleration.Value + scale.Value);
  ```

- Calls may include an `Entity`, mutable caller context, primary registrations,
  or explicit `ComponentId` arguments.
- Parallel typed callbacks use `ForEachParallel` and `ForEachEntityParallel`.
  Their state parameter is `in`, `ref readonly`, or by value; the old parallel
  `ref` state form is not generated. Read-only/value state is copied into each
  worker invoker. Implement `IForEach`/`IForEachEntity` for an explicit
  non-intercepted functor call.
- Query-wide `world.Where(in query, predicate)` predicates are read-only and
  receive only typed components. `WhereEntity` is the corresponding form that
  also receives `Entity` first. Use `in T` or `ref readonly T` component
  parameters; a writable `ref T` reports `DECSGEN006`. Use the terminal
  `ForEach` callback for component mutations:

  ```csharp
  world.WhereEntity(in query,
      static (Entity entity, ref readonly Health health) => health.Value <= 0)
      .ForEach(static (ref Health health) => health.Value = 0);
  ```
- Generated extensions live in the consumer assembly while execution enters a
  shared non-generic DeltaECS runtime bridge.
- Dense generated callbacks enter a closed execution method. The runtime
  validates the query once, resolves each row once per chunk, and the generated
  loop advances direct typed references. Sequence callbacks use direct trusted
  reference endpoints over the current entity chunk instead of creating a row
  view for each callback.
- Functors implement only `IForEach`, `IForEachEntity`,
  `IForEachContext<TContext>`, or `IForEachContextEntity<TContext>`; generated
  interface names never contain component types or read/write patterns.
- Query predicate functors implement `IWherePredicate`. The no-entity form
  starts with typed read-only components; `WhereEntity` adds `Entity` before
  them. When a separate caller-owned context is needed, pass it by `ref` and
  put `ref TContext` first in `Invoke`, just as with a `ForEach` callback. A
  terminal functor can keep its own context too:

  ```csharp
  struct IsDead : IWherePredicate
  {
      public bool Invoke(ref PredicateState state, Entity entity, in Health health)
      {
          state.Visited++;
          return health.Value <= 0;
      }
  }

  struct Reset : IForEachContextEntity<ActionState>
  {
      public void Invoke(ref ActionState state, Entity entity, ref Health health)
      {
          state.Matched++;
          health.Value = 0;
      }
  }

  var predicateState = new PredicateState();
  var predicate = new IsDead();
  var actionState = new ActionState();
  var action = new Reset();
  world.WhereEntity(in query, ref predicateState, ref predicate)
      .ForEachEntity(ref actionState, ref action);
  ```

  The lambda form follows the same ordering:

  ```csharp
  world.Where(in query,
      ref predicateState,
      static (ref PredicateState state, in Health health) =>
      {
          state.Visited++;
          return health.Value <= 0;
      }).Destroy();
  ```

  `Where` predicates remain read-only; component writes belong in the terminal
  functor. The generated view and functors stay stack-only and copy caller
  state back before the terminal returns.

## Generic structural operations

The generator also emits only the generic `Add`/`Remove` arities used by the
consumer. The type arguments resolve each type's primary registration and the
runtime receives a stack-only `ReadOnlySpan<ComponentId>`; no `ComponentId[]`
is allocated by the generated façade:

```csharp
int added = world.Add<Position, Velocity>(entities);
int removed = world.Remove<Position, Velocity>(entities);

int queryAdded = world.Add<Position, Velocity>(in query);
int queryRemoved = world.Remove<Position, Velocity>(in query);

int sequenceAdded = world.From(entities).Add<Position, Velocity>();
int sequenceRemoved = world.From(entities).Remove<Position, Velocity>();
```

The same sequence terminals are available after `Where(in query)`. Structural
operations add or remove the primary component registrations and return the
number of entities changed. Newly added rows are default-initialized; use the
existing typed `World.Add<T>(..., ComponentId, in T)` overload when a value must
be initialized during the transition. Generic structural forms support arity
one through 256 on demand; this generator limit is independent of the dynamic
number of registered component IDs.

## Generic query factories

The generator also emits only the typed query factories used by a consumer.
Initial factories are extensions on `World`; the same names are emitted as
extensions on `Query` for fluent composition. The runtime query and storage
types remain non-generic:

```csharp
Query combatants = world
    .WhereAll<Position, Health, Human>()
    .WhereNone<Dead, Escaped>()
    .WhereAny<Armed, Berserk>();
```

Each generated factory resolves primary component registrations, fills a
stack-only `ComponentId` span and calls the existing `World.CreateQuery` path.
The initial call uses `world.Layouts.GetPrimary<T>()`; a `Query` extension
resolves the same registration through the query's owning world and composes
the masks into a new `QuerySpec`. Equivalent specifications continue to share
the existing query-plan cache. Factories are emitted on demand for arities one
through 256; no generic query plan or runtime type dictionary is introduced.

`DeltaECS.Generators` targets `netstandard2.0`. The analyzer package keeps this
broadly compatible assembly in `analyzers/dotnet/cs`; the target of the
consumer project remains independent from the target of the analyzer.

## Optional Roslyn interceptor path

Consumers targeting an SDK with Roslyn interceptor support may opt in per
project by exposing the library-owned namespace to the compiler:

```xml
<PropertyGroup>
  <InterceptorsNamespaces>Delta.ECS.Generated</InterceptorsNamespaces>
</PropertyGroup>
<ItemGroup>
  <CompilerVisibleProperty Include="InterceptorsNamespaces" />
</ItemGroup>
```

This opt-in is part of the recommended high-performance configuration for
delegate-shaped hot loops. It preserves the public `world.ForEach(...)` call
while allowing supported static callbacks to use the generated trusted
struct-functor execution path instead of the ordinary delegate callback path.
The setting is a consumer-project build feature; it is not a runtime package
dependency and it does not add the `netstandard2.0` analyzer to a published
NativeAOT application.

The generator keeps this opt-in isolated to `Delta.ECS.Generated`; it does not
enable a global preview switch or add `InterceptorsPreviewNamespaces`. When
enabled, a supported `World.ForEach`/`ForEachEntity` call with a synchronous
static non-capturing lambda or an unambiguous static method group receives a
generated interceptor. A lambda body is copied into a generated struct
functor; a method group functor forwards directly to its resolved static
method. The same lowering is used for a static-lambda
`world.Where(...).ForEach(...)` or `world.WhereEntity(...).ForEach(...)` terminal
and for a functor terminal with a
static-lambda predicate. These forms enter a closed dense execution method with
chunk-level row resolution. Query ownership, leases, mutation stamps and
write-row marking therefore remain in the shared runtime path.

When the consumer uses C# 9 or C# 10, including Unity projects with a
`netstandard2.1` API profile, the generator skips interceptor source because
the language cannot parse the required interceptor/file declarations. Ordinary
demand-generated `ForEach` overloads remain available and the public API is
unchanged. Interception is used only when the consumer language version
supports it.

Capturing and async lambdas, instance or ambiguous method groups, pre-created
delegates, generic method-group targets, generic containing types/methods,
sequence receivers, and call sites without an interceptable Roslyn location
stay on the ordinary delegate path. The generator reports `DECSGEN005` at
informational severity with the fallback reason; the diagnostic never turns a
fallback call into a build failure.

The generator reports diagnostics for unsupported arity, ambiguous functor
`Invoke` shapes, invalid ref kinds and calls whose requested component pattern
cannot be represented safely. It does not use runtime reflection to choose a
callback overload.

Fixed multi-line source templates in the generator use C# raw string literals.
Dynamic symbols, callback bodies and access lists are still appended separately,
so the generated source remains demand-driven without turning runtime callback
execution into a template or reflection path.

The public source spelling remains `world.ForEach(...)`. Consumers must include
the DeltaECS analyzer reference for component-bearing generated overloads.
