# DeltaECS TODO

## User-owned API redesign

- The user is redesigning the API. Colleagues and coordinating agents must not
  implement, optimize or stabilize the current surface unless explicitly
  assigned a bounded task.
- Previous candidates such as terminal `Ref<T>` validation, QuerySpec factories
  and mask widening are context, not selected work for the replacement API.

## Historical completed work

- [x] Remove the legacy generic query-access API and document the non-generic
  access/cursor path.
- [x] Add an independent dense query scope with separate archetype, chunk and
  reverse slot iterators. Validation and lease ownership stay at scope setup;
  dense `MoveNext` methods contain no world, scratch or disposal branch.
  The bounded Movement4 JIT probe reduced code size from 1956 B to 1552 B.

Deferred subscriptions and performance hypotheses live in
[IDEAS.md](IDEAS.md), not here.
