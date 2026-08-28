# Prepared query-row evidence

The current query plan prepares direct component-array references per active
chunk. `QuerySlots.GetRow(ReadAccess)` and `QuerySlots.GetRow(WriteAccess)` use
that prepared table; the slot loop then reaches `ReadRow.Ref<T>` or
`WriteRow.Ref<T>` without repeating the physical-row lookup.

## Scope

This is an implementation note for the current `QueryScope` path, not a
separate public API. It applies to `Movement2Components`,
`Movement4Components` and any generated callback that enters the same query
plan.

## Correctness boundary

- `Query.AccessRead` and `Query.AccessWrite` validate the component against the
  query's `All` mask before a scope is opened.
- `QueryScope` validates query ownership and owns the active
  structural lease.
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
