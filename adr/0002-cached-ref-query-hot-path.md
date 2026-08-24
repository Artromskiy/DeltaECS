# ADR-0002: Prepared chunk rows for dense queries

## Context

Dense query execution must keep storage and traversal type-erased while
avoiding repeated archetype matching and physical row lookup in the slot loop.
The public API also needs one clear owner for the structural mutation lease.

## Decision

- `World.CreateQuery(in QuerySpec)` returns a world-owned `Query` whose cached
  `QueryPlan` tracks matching archetypes and query-to-component row positions.
- The plan refreshes matching archetypes when the world's archetype version
  changes. Each `ArchetypePlan` refreshes active chunks and resolves its
  requested `Array[]` rows into `ChunkPlan` values once per chunk.
- `World.OpenQuery(in Query)` creates a stack-only `QueryScope`. The scope owns
  the structural lease; `QueryArchetypes`, `QueryChunks` and `QuerySlots` are
  borrowed traversal views.
- `Query.AccessRead` and `Query.AccessWrite` validate the requested component
  against the query and register write intent before traversal. `ReadRow` and
  `WriteRow` remain non-generic until the terminal `Ref<T>` call.
- Generated delegate and functor callbacks use the same query plan, access
  declarations and chunk-row preparation. They are a convenience surface, not
  a second storage or traversal model.

## Consequences

Physical row resolution is performed at the chunk boundary rather than for
each slot. The scope lifetime prevents structural changes from invalidating
borrowed row values. The callback generator may specialize callback shapes,
but query plans, storage and traversal remain shared and type-erased.

The implementation does not claim a throughput result from code size alone.
Assembly observations and BenchmarkDotNet measurements belong in the separate
performance documentation and must use the same workload and runtime.
