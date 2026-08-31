# DeltaECS TODO

## User-owned API redesign

- The user is redesigning the API. Colleagues and coordinating agents must not
  implement, optimize or stabilize the current surface unless explicitly
  assigned a bounded task.
- Previous candidates such as terminal `Ref<T>` validation and legacy
  `QuerySpec` factories are context, not selected work for the replacement API.
  Dynamic native component-mask storage is part of the current implementation;
  its performance evidence is recorded in the experiment ledger.

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
- [x] Replace the producer-owned callback matrix with a demand-driven
  consumer-assembly source generator. Preserve the outer `world.ForEach(...)`
  spelling through generated extension methods; the analyzer emits only the
  callback shapes actually used by the consumer.
  Support the entity-only form, with/without context, with/without `Entity`,
  delegate and struct-functor callbacks, and arbitrary requested read/write
  patterns up to the generator's 256-component callback-arity limit. This
  limit is independent of the dynamic component mask.
- [x] Complete the no-ID component form: resolve every generic component type
  independently to its registry primary `ComponentId`, even when the query
  contains additional required components. Keep explicit-ID overloads for
  secondary registrations of the same CLR type.
- [x] Keep the runtime/storage/query path type-erased; generic types may appear
  only in registration and at the generated callback/ref boundary. Prove
  consumer-assembly analyzer execution with a separate fixture and deterministic
  source-generation coverage, then run the normal Release gates.
- [x] Define and validate the `Sequence` surface for ordered entity spans,
  including non-generic, generic, delegate and functor terminals where each
  form is justified. Fluent builders remain allocation-free facades over the
  direct sequence/query kernels, not a separate execution layer.

## Completed stabilization work

- [x] Keep query access type-erased and document the non-generic access path.
- [x] Add an independent dense query scope with separate archetype, chunk and
  forward slot iterators. Validation and lease ownership stay at scope setup;
  child `MoveNext` methods only enforce the active session and advance their
  local index.
  The bounded Movement4 JIT probe reduced code size from 1956 B to 1552 B.

Deferred subscriptions and performance hypotheses live in
[IDEAS.md](IDEAS.md), not here.

## Cross-project UI performance consultation (advisory)

This item is a bounded consultation requested by the UI/render owners. It does
not authorize changes to the ECS kernel, public API, storage layout or ECS
dependencies. Validate the following patterns against existing ECS principles
and return measured-risk guidance to DeltaXAML/DeltaRender:

- [ ] persistent dense render-instance storage with stable local handles;
- [ ] dirty-range uploads and contiguous range coalescing;
- [ ] clip/material slot caches without ECS-owned subscriptions;
- [ ] order-preserving adjacent batching for `A-B-A` draw sequences;
- [ ] zero-allocation warm frames and world-space projection as a consumer
  transform rather than a second UI runtime.
