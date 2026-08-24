# DeltaECS TODO

## User-owned API redesign

- The user is redesigning the API. Colleagues and coordinating agents must not
  implement, optimize or stabilize the current surface unless explicitly
  assigned a bounded task.
- Previous candidates such as terminal `Ref<T>` validation, QuerySpec factories
  and mask widening are context, not selected work for the replacement API.

## Selected stabilization work

- [x] Expand mutation-stamp correctness coverage across single, sequence and
  query structural operations, exact component writes, no-ops, stale entities,
  swap-back, block transitions, managed rows and deterministic randomized
  state-machine tests.
- [ ] Measure and improve create, destroy, add and remove kernels for atomic,
  sequence and query workloads. Benchmark setup must stay outside measurement;
  change-width fixtures must represent their declared width exactly. Create and
  ordered/list destroy now have block kernels; add/remove still lack a general
  measured improvement beyond the existing archetype-transition paths.
- [x] Standardize structural APIs without expanding `IEcsWorld`: preserve the
  stable non-generic iteration path, complete the generic single-entity
  boundary and keep generic types out of storage and query plans.
- [x] Generate the `World.ForEach` delegate and struct-functor matrices for
  component arities, with and without context, with and without `Entity`, plus
  the entity-only form. Generated variants must share the same execution
  kernels and deterministic source-generation tests.
- [x] Define and validate the `Sequence` surface for ordered entity spans,
  including non-generic, generic, delegate and functor terminals where each
  form is justified. Fluent builders remain allocation-free facades over the
  direct sequence/query kernels, not a separate execution layer.

## Historical completed work

- [x] Remove the legacy generic query-access API and document the non-generic
  access/cursor path.
- [x] Add an independent dense query scope with separate archetype, chunk and
  reverse slot iterators. Validation and lease ownership stay at scope setup;
  dense `MoveNext` methods contain no world, scratch or disposal branch.
  The bounded Movement4 JIT probe reduced code size from 1956 B to 1552 B.

Deferred subscriptions and performance hypotheses live in
[IDEAS.md](IDEAS.md), not here.
