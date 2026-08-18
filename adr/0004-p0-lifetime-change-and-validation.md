# ADR-0004: P0 lifetime, change-version, and validation contracts

## Context

The first P0 review found three correctness hazards around deferred structural
work and query access: a playback edge cache could reuse one command's edge for
another command from the same source archetype, write access was accepted but
did not update component versions, and invalid tags could be stored while query
normalization silently discarded them.

## Decision

- Transition playback uses a monotonically increasing cache token per queued
  command. The archetype-indexed edge array remains reusable; on token rollover
  the version slots are cleared before tokens restart at one.
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
