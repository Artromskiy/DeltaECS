# Prepared column state evidence

This archived probe tested whether keeping the current chunk's component-row
table in `QuerySlots` changed the generated dense-loop code. The public API and
query execution contract were unchanged.

## Result

The narrow Release probe changed the first emitted block from 1408 B to
1400 B, with `ldr` 96→94, `str` 21→20 and `ldp/stp` 33→34. The accompanying
short BenchmarkDotNet sample was directional because the invocation was too
short for a stable throughput claim.

The current implementation has a stronger, explicit chunk plan: it resolves
the direct `Array[]` rows in `ArchetypePlan.RefreshChunks` and passes that table
to `QuerySlots`. Use [performance README](../README.md) for the current path
and [benchmarks/README.md](../../benchmarks/README.md) for reproduction.
