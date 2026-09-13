# ADR-0002: Prepared chunk rows for dense queries

> Superseded: the scope/iterator/row API described below was removed as a
> breaking change. Current traversal uses the generated dense execution path.

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
- The former `World.BeginScope`/iterator/row chain owned the structural lease
  and resolved rows through borrowed views. That implementation was removed.
- Generated callbacks now validate access and register write intent at the
  dense execution boundary, using prepared query-plan routes.
- Generated delegate and functor callbacks use the same query plan, access
  declarations and chunk-row preparation. They are a convenience surface, not
  a second storage or traversal model.

## Consequences

Physical row resolution is performed at the chunk boundary rather than for
each slot. Generated execution owns the traversal lifetime and prevents
structural changes from invalidating prepared routes. The callback generator
may specialize callback shapes, but query plans and storage remain shared and
type-erased.

The implementation does not claim a throughput result from code size alone.
Assembly observations and BenchmarkDotNet measurements belong in the separate
performance documentation and must use the same workload and runtime.
