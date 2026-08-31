# DeltaECS consumer API generator

The analyzer emits only the `ForEach` and `ForEachEntity` shapes requested by a
consumer compilation. It does not generate storage, queries, archetypes or
structural kernels.

- Zero-component delegate overloads are handwritten in DeltaECS. Component-
  bearing delegate and functor overloads are generated from the consumer's
  demand and start at arity one. The built-in marker-only functor entry points
  are not a generated zero-component execution path.
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
method. Both forms enter the same closed dense execution method as the
explicit functor API. Query ownership, leases, mutation stamps and write-row
marking therefore remain in the shared runtime path.

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
