# Sequence execution API

Status: planned. There is no public `Sequence` type, `World.Entities` facade,
or `World.ForEach` method in the current source tree. This note records the
agreed shape and invariants for the future implementation; it is not an
implementation promise beyond those boundaries.

This folder is reserved for the type-erased implementation of explicit entity-sequence
execution. Public entry points remain on `World`; sequence execution must not introduce
a second query object or expose storage adapters.

The selected API family is:

```csharp
world.ForEach(entities, action);
world.ForEach(entities, in query, action);
```

The fluent facade is planned as:

```csharp
world.Entities(entities).ForEach(action);
world.Entities(entities).Where(in query).ForEach(action);
```

Both spellings terminate in the generated `World.ForEach` callback matrix:
no-context/context, no-entity/entity, zero components (the explicit
entity-only form), and 1..4 typed component arguments. A future struct-functor
family has the same matrix. The facade is non-generic selection state and must
not expose a second query, row, or storage adapter.

`entities` is a `ReadOnlySpan<Entity>`. The first overload executes against every
valid entity in the supplied order. The second treats the sequence as the candidate
input and applies `query` as a filter and access contract; it does not enumerate the
whole world. Stale, destroyed and foreign entities follow the same rejection rules
as existing explicit-sequence structural operations.

Generated delegate and future struct-functor overloads provide component
arities, context/no-context and entity/no-entity forms. Their implementation
should share a non-generic sequence kernel. Generic types exist only at
component registration, the single-item component boundary, and the final
typed component boundary. The current compatibility `World.Query<TContext>`
cursor path is separate and remains available until the planned callback
family is implemented.

Performance constraints:

- no LINQ, reflection, adapter dispatch or per-entity allocation;
- validate query/access declarations once before sequence traversal;
- cache the last entity archetype and its resolved component rows;
- preserve sequence order and duplicate occurrences unless a separately named
  unordered batch API is introduced;
- do not silently convert sequence execution into query-wide archetype traversal.
