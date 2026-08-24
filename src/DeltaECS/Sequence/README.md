# Sequence execution API

Status: implemented for ordered entity-only execution and structural batch
terminals.

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

Both spellings use the generated entity delegate contracts, with and without
caller context. Struct functors use `IForEachEntity` or
`IForEachContextEntity<TContext>`. The facade is non-generic selection state
and does not expose a second query, row or storage adapter.

`entities` is a `ReadOnlySpan<Entity>`. The first overload executes against every
valid entity in the supplied order. The second treats the sequence as the candidate
input and applies `query` as a filter and access contract; it does not enumerate the
whole world. Stale, destroyed and foreign entities follow the same rejection rules
as existing explicit-sequence structural operations.

Typed component callback arities belong to dense `World.ForEach(in query, ...)`.
They are not projected onto explicit sequences because doing so would require
a second component-row execution kernel. Sequence component access uses the
single-item `TryGet<T>/Get<T>/Set<T>` boundary when needed.

Structural terminals are `Add`, `Remove` and `Destroy`. Filtered terminals
first retain only matching candidates and then call the existing batch
kernels; the facade does not loop through public atomic operations.

Performance constraints:

- no LINQ, reflection, adapter dispatch or per-entity allocation;
- validate query/access declarations once before sequence traversal;
- preserve sequence order and duplicate occurrences unless a separately named
  unordered batch API is introduced;
- do not silently convert sequence execution into query-wide archetype traversal.
