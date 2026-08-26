# DeltaECS ideas

These are not active tasks. Require an explicit decision and measured workload.

## Cross-world versioned subscriptions

An external projection layer could give each consumer its own query, watched
component set and version cursor. It would coalesce latest `Added`, `Changed`
and `Removed` flags rather than retain an ordered event log.

Keep it outside the storage kernel:

- source and target worlds retain independent component registries;
- stable schema IDs build a cached mapping to local `ComponentId` values;
- a generation-aware entity map translates source entities to target proxies;
- consumers never clear global change state needed by another consumer;
- begin with chunk/row versions and add entity dirty masks only after measuring
  excessive copying.

Possible order if promoted: complete all write-version marking, add changed-row
cursors, add structural version/tombstone state, then build the external world
projection. Do not add per-entity subscriptions to the base world.

## Performance candidates

Evidence and candidate order live in
[docs/performance/README.md](docs/performance/README.md). Promote one candidate
at a time with accumulator parity, JIT capture and an unchanged public API.

### Prepared generated access routes

Generated dense callbacks can fetch read/write access objects from routes
prepared when the query plan is created, after `OpenDense` has established the
query/lifetime boundary. This removes the old runtime-type resolver chain from
the generated access path without exposing an unchecked public operation.

The first implementation is recorded in
[prepared-generated-access evidence](docs/performance/experiments/prepared-generated-access.md).
The profiler confirms the changed call tree, but its calibration is weak and
no paired BDN result exists yet; do not treat this as an accepted speedup.

## One-time chunk traversal selection

A flat active-chunk view was substantially faster for the public two-loop path
once a query covered multiple chunks, but almost twice as slow for its
single-chunk lane. A follow-up may choose the representation once when opening
the scope: retain the direct one-plan/one-chunk path and use a maintained flat
view only after the query becomes multi-chunk.

The selection must not add a mode branch to every `MoveNext`, duplicate
validation or change the three-loop API. Activation, deactivation and
swap-back must update reverse indices exactly once. This is a new hypothesis;
the unconditional flat view is already rejected in the experiment ledger.
