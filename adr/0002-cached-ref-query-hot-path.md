# ADR-0002: Cached chunk execution for dense iteration

## Context

Dense query execution must keep the storage and traversal kernel type-erased
while avoiding repeated archetype matching and component-row resolution in the
hot loop. The public API also needs one clear lifetime owner for structural
mutation exclusion.

## Decision

- `World.CreateQuery(in QuerySpec)` returns a world-owned `Query` whose cached
  `QueryPlan` tracks matching archetypes and the component-row mapping for the
  query's `All` mask.
- `QueryPlan` refreshes matching archetypes when the world's archetype version
  changes. Each `ArchetypePlan` refreshes its active chunks and resolves the
  requested `Array[]` rows into `ChunkPlan` values once per chunk.
- `World.OpenQuery(in Query)` creates a `QueryScope` ref struct. The scope owns
  the structural mutation lease; `QueryArchetypes`, `QueryChunks` and
  `QuerySlots` are stack-only traversal views and do not own an independent
  lease.
- `Query.AccessRead` and `Query.AccessWrite` validate the requested component
  against the query and register write intent before execution. `ReadRow` and
  `WriteRow` remain non-generic until the terminal `Ref<T>` call.
- Generated delegate and functor callbacks reuse the same prepared query plan
  and chunk-row resolution. They are a consumer-side convenience layer, not a
  second storage or traversal implementation.
- Keep benchmark-only specialization at the use site for measured row counts.
  Do not add generic component pools, reflection or a scheduler to the ECS
  kernel.

## Consequences

The direct cursor reduced the 10K/1 distinct-type lane from `3.983 us` to
`2.999 us` in the measured BDN run and reports no allocation in that lane. The
full gate is still open: Array remains slower than legacy in several lanes and
reports `1 B` in the 100K/8 group. The remaining measured bottleneck is typed
Array-row access and chunk traversal, not query matching or callback dispatch.

The query scope must be disposed synchronously and cannot outlive the world
mutation lease. Child iterators are valid only within that scope, and structural
changes are rejected while a conflicting row lease is active.

Darwin PMU counters and BDN disassembly diagnostics were unavailable in this
environment. No hardware-counter or assembly-level limit is claimed.
