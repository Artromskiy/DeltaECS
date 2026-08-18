# ADR-0004: P0 lifetime, change-version, and validation contracts

## Context

The first P0 review found three correctness hazards around structural movement
and query access: an edge cache could reuse one transition's edge for another
transition from the same source archetype, write access was accepted but did
not update component versions, and invalid tags could be stored while query
normalization silently discarded them.

## Decision

- Structural add/remove is immediate: single-entity and batch calls normalize
  the change set, reuse the existing transition edge cache, and complete
  `MoveEntity` before returning. Batch calls do not invoke the public
  single-entity API in a loop and have no deferred world-owned command state.
- `WorldTick` is advanced once for each write query invocation. A component row
  is marked only when its matching chunk is actually yielded, and only for the
  query's requested `AllComponents` rows. Read queries do not mark versions.
  `HasChangedSince(chunkId, componentId, sinceTick)` exposes the chunk-level
  semantic version without adding checks to the dense iteration loop.
- `QueryDescription` owns normalized copies of its inputs and exposes read-only
  spans. `DenseChunkLeaseView` owns its pooled overlay mask through the world
  view token; a stale copy cannot read or return the mask after the enumerator
  advances.
- Negative `TagId` values are invalid at every public tag mutation/read API and
  in query construction. Invalid input throws `ArgumentOutOfRangeException`.

## Consequences

The hot query loop keeps its cached row indices and has no per-slot version
branch. Version inspection is intentionally a cold-path operation. Structural
movement remains separate from semantic component change tracking: copying a
row during an archetype transition does not claim that a consumer wrote it.
The implementation remains single-threaded and does not introduce a scheduler,
events, dirty-slot bitsets, or typed facade.
