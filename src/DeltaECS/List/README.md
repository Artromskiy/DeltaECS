# List execution API

This folder is reserved for the type-erased implementation of explicit entity-list
execution. Public entry points remain on `World`; list execution must not introduce
a second query object or expose storage adapters.

The selected API family is:

```csharp
world.ForEach(entities, action);
world.ForEach(entities, in query, action);
```

`entities` is a `ReadOnlySpan<Entity>`. The first overload executes against every
valid entity in the supplied order. The second treats the list as the candidate
set and applies `query` as a filter and access contract; it does not enumerate the
whole world. Stale, destroyed and foreign entities follow the same rejection rules
as existing explicit-list structural operations.

Generated delegate and future struct-functor overloads provide component arities,
context/no-context and entity/no-entity forms. Their implementation should share a
non-generic list kernel. Generic types exist only at component registration and the
final typed component boundary.

Performance constraints:

- no LINQ, reflection, adapter dispatch or per-entity allocation;
- validate query/access declarations once before list traversal;
- cache the last entity archetype and its resolved component rows;
- preserve list order and duplicate occurrences unless a separately named unordered
  batch API is introduced;
- do not silently convert list execution into query-wide archetype traversal.
