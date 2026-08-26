# DeltaECS performance notes

This is the single index for performance work. It describes the current
execution path and records measured ideas; it is not a task list. A candidate
becomes implementation work only after an explicit decision and a reproducible
workload.

Completed and rejected experiments are indexed in the
[optimization experiment ledger](experiments/README.md). Check that ledger
before starting a candidate so an exhausted mechanism is not repeated.

## Current dense execution

The public low-level path is:

```text
QuerySpec -> Query -> QueryScope
  -> QueryArchetypes -> QueryChunks -> QuerySlots
  -> ReadRow/WriteRow.Ref<T>
```

`QueryPlan` caches matching archetypes and maps query component ordinals to
physical component rows. `ArchetypePlan.RefreshChunks` prepares a
`ChunkPlan` for every active chunk. Each `ChunkPlan` stores direct `Array`
references in query order, so `QuerySlots.GetRow` resolves one row per chunk,
not once per entity. Write access marks the physical component row through the
same chunk boundary and the query write session.

`QueryScope` is the owner of the structural lease. `QueryArchetypes`,
`QueryChunks` and `QuerySlots` are borrowed stack-only views. Their values are
valid only while the scope is active; structural mutation cannot invalidate a
row while that lease is held.

The generated delegate and functor surfaces enter the same type-erased plan and
row preparation. They change callback syntax, not storage or matching.

## Accepted internal improvements

- Active archetypes maintain a dense direct `Chunk[]` view for traversal while
  structural index tables remain separate.
- Query plans refresh only when the world's archetype version changes.
- Component row arrays are resolved once at the chunk boundary and reused by
  the slot loop.
- The public row endpoint is `ReadRow.Ref<T>` or `WriteRow.Ref<T>`; no raw
  pointer or ordinal row API is exposed to consumers.

These statements describe the current source. Code size alone is not a
throughput claim; measurements must use the benchmark protocol below.

## Deferred experiments

### Adjacent component loads

Check whether a visible row layout lets the AArch64 JIT form `ldp`/`stp` pairs.
Do not infer a benefit from instruction spelling alone; compare the complete
hot loop and measure throughput.

## Measurement rules

- Keep setup, world creation, query construction, reset and report formatting
  outside the measured method.
- Return a checksum, count or other observable result to prevent dead-code
  elimination.
- Compare the same entity count, component width, runtime, architecture and
  GC mode. Use paired runs for short operations.
- Review the JIT driver separately from the slot loop. Calls, branches and
  loads in setup or prologue are not slot-loop cost.
- Instruction count does not reveal cache misses. Use hardware counters when
  making a cache claim.

The runnable procedure is in [benchmarks/README.md](../../benchmarks/README.md).
The comparative suite is manual evidence; the microbenchmark project is the
focused source for dense iteration and structural kernels.
