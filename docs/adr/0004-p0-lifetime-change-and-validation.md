# ADR-0004: P0 lifetime, stamp, and validation contracts

## Context

The first P0 review found three correctness hazards around structural movement
and query access: an edge cache could reuse one transition's edge for another
transition from the same source archetype, write access could miss its mutation
stamp, and query access could be validated too late.

## Decision

- Structural add/remove is immediate: single-entity and batch calls normalize
  the change set, reuse the existing transition edge cache, and complete
  `MoveEntity` before returning. Batch calls do not invoke the public
  single-entity API in a loop and have no deferred world-owned command state.
- Mutation tracking uses the opaque `Stamp` contract. ECS exposes exact
  entity/component stamps through `TryGetComponentStamp` and `StampRow`; it does
  not expose a separate chunk-tick change query. Consumers that need change
  detection store and compare the returned stamps themselves.
- `QuerySpec` stores normalized `All`, `Any` and `None` component masks.
  `Query` validates ownership, component registration and `All`-mask
  membership when an access token is created; `QuerySlots.GetRow` and
  `QuerySlots.GetObject` validate that the token belongs to the active scope
  before resolving storage.
- `QueryScope` owns the structural lease and its archetype, chunk and slot
  iterators are borrowed views. A disposed scope or a row access from another
  query cannot be used to read or write storage.

## Consequences

The hot query loop keeps its cached row indices and has no per-slot version
branch. Stamp comparison is intentionally a consumer-owned cold-path operation.
Structural
movement remains separate from semantic component change tracking: copying a
row during an archetype transition does not claim that a consumer wrote it.
The implementation remains single-threaded and does not introduce a scheduler,
events, dirty-slot bitsets, or typed facade.
