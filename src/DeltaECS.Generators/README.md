# DeltaECS consumer API generator

The analyzer emits only the `ForEach` and `ForEachEntity` shapes requested by a
consumer compilation. It does not generate storage, queries, archetypes or
structural kernels.

- Zero-component delegate overloads are handwritten in DeltaECS. Functor
  overloads, including zero-component forms, are generated from concrete
  marker implementations.
- Component-bearing arities start at one and may extend to 256.
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

The generator reports diagnostics for unsupported arity, ambiguous functor
`Invoke` shapes, invalid ref kinds and calls whose requested component pattern
cannot be represented safely. It does not use runtime reflection to choose a
callback overload.

The public source spelling remains `world.ForEach(...)`. Consumers must include
the DeltaECS analyzer reference for component-bearing generated overloads.
