# Prepared query-row evidence

> Historical experiment. The `QueryScope`/row-wrapper implementation described
> here was removed; generated dense execution is the current path.

The former query plan prepared direct component-array references per active
chunk. Its row wrappers were removed; the same invariant is now implemented by
`GeneratedQuerySlots` and generated dense callbacks.

## Scope

This is an implementation note for a removed path, not a public API. It applies
to `Movement2Components`,
`Movement4Components` and any generated callback that enters the same query
plan.

## Correctness boundary

- Query access validates the component against the query's `All` mask before
  generated dense execution starts.
- Generated execution validates query ownership and owns the active structural
  lease.
- `ArchetypePlan.RefreshChunks` rebuilds the direct row table when the active
  chunk set changes.
- Write access records the physical row through the active query write session.

The prepared table does not retain a span or pointer across structural
changes. It is refreshed from the current chunk arrays at the chunk boundary.

## Measurement rule

Use the same Release runtime, entity count, component width and checksum when
comparing a different representation. JIT code size and instruction counts are
supporting evidence; throughput comes from a paired BenchmarkDotNet run. The
reproduction procedure is in [benchmarks/README.md](../../benchmarks/README.md).
