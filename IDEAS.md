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
