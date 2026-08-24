# DeltaECS consumer API generator

The analyzer emits only the `ForEach` and `ForEachEntity` shapes requested by a
consumer compilation. It does not generate storage, queries, archetypes or
structural kernels.

- Zero-component delegate overloads are handwritten in DeltaECS. Functor
  overloads, including zero-component forms, are generated from concrete
  marker implementations.
- Component-bearing arities start at one and may extend to 256.
- `in T` parameters declare reads; `ref T` parameters declare writes.
- Component-bearing callbacks may omit the method type list when lambda
  parameters are explicitly typed; the generator infers the component types
  from `ref T`/`in T` parameters. For example:

  ```csharp
  sequence.ForEach(static (ref Position position, in Velocity velocity) =>
      position.X += velocity.X);
  ```

- Calls may include an `Entity`, mutable caller context, primary registrations,
  or explicit `ComponentId` arguments.
- Generated extensions live in the consumer assembly while execution enters a
  shared non-generic DeltaECS runtime bridge.
- Functors implement only `IForEach`, `IForEachEntity`,
  `IForEachContext<TContext>`, or `IForEachContextEntity<TContext>`; generated
  interface names never contain component types or read/write patterns.

The generator reports diagnostics for unsupported arity, ambiguous functor
`Invoke` shapes, invalid ref kinds and calls whose requested component pattern
cannot be represented safely. It does not use runtime reflection to choose a
callback overload.

The public source spelling remains `world.ForEach(...)`. Consumers must include
the DeltaECS analyzer reference for component-bearing generated overloads.
