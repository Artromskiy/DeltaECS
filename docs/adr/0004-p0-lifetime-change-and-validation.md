# ADR-0004: P0 lifetime, stamp, and validation contracts

> Superseded in part: the legacy scope/iterator/row traversal was removed. The
> mutation-stamp contract described below was later removed; only the lifetime
> and query-validation decisions remain current.

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
- The historical mutation-stamp contract described in this ADR is removed.
  Consumers that need change notifications use the separate `ForEachChange`
  observer API.
- `QuerySpec` stores normalized `All`, `Any` and `None` component masks.
  `Query` validates ownership, component registration and `All`-mask
  membership when generated access routes are prepared. Query execution owns
  the structural lease for the duration of the generated dense pass.

## Consequences

The hot query loop keeps its cached row indices.
Structural
movement remains separate from semantic component change tracking: copying a
row during an archetype transition does not claim that a consumer wrote it.
The implementation remains single-threaded and does not introduce a scheduler,
events, dirty-slot bitsets, or typed facade.
