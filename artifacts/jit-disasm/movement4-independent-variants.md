# Movement4 independent iterator JIT variants

## Scope

Target method:

`MicroBenchmarkKernels.IterateMovement4IndependentIterators`

Baseline: current `perf/query-cursor-version1` before the selected variant.
Runtime: .NET 8.0.29 Checked JIT on macOS ARM64. Every variant used the same
debug JIT probe and the same BenchmarkDotNet filter:

```text
--method '*IterateMovement4IndependentIterators*'
--filter '*QueryIteratorIterationMicroBenchmarks.Movement4IndependentIterators*'
--mode debug --no-build
```

The counts below are static JIT instruction counts for the first emitted code
block. They are not throughput measurements. `score` is a coarse comparison
of `blr + bhs + ldr + str + ldp/stp`; it is only used to rank candidates for
further validation.

## Results

| Variant | Change | Code | Score | blr | bl | bhs | branch | sbfiz | umull | ldr | str | ldp/stp |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| base | baseline | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |
| v01 | unchecked archetype-plan access | 1448 | 174 | 16 | 4 | 2 | 39 | 1 | 0 | 99 | 23 | 34 |
| v02 | unchecked chunk-array access | 1448 | 174 | 16 | 4 | 2 | 39 | 1 | 1 | 100 | 22 | 34 |
| v03 | v01 + v02 | 1432 | 172 | 16 | 3 | 1 | 39 | 1 | 0 | 99 | 22 | 34 |
| v04 | ref-based read/write resolved rows | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |
| v05 | ref-based read resolved row | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |
| v06 | ref-based write resolved row | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |
| v07 | aggressive optimization on slot MoveNext | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |
| v08 | compact slot iterator state | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |
| v09 | compact state + ref rows | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |
| v10 | aggressive optimization on chunk MoveNext | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |
| v11 | aggressive optimization on archetype MoveNext | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |
| v12 | inline scope Archetypes getter | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |
| v13 | inline chunk Slots getter | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |
| v14 | inline archetype Chunks getter | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |
| v15 | alternate slot decrement form | 1456 | 175 | 16 | 4 | 3 | 39 | 1 | 1 | 98 | 23 | 35 |
| v16 | pre-increment chunk MoveNext | 1440 | 170 | 16 | 4 | 2 | 40 | 1 | 1 | 97 | 22 | 33 |
| v17 | pre-increment archetype MoveNext | 1456 | 176 | 16 | 4 | 3 | 38 | 1 | 1 | 100 | 23 | 34 |
| v18 | unused aggressive-optimization helper | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |
| v19 | ref-based DenseQueryArchetype plan | 1448 | 174 | 16 | 4 | 2 | 39 | 1 | 0 | 99 | 23 | 34 |
| v20 | compact slot iterator state (repeat) | 1460 | 176 | 16 | 4 | 3 | 39 | 1 | 1 | 100 | 23 | 34 |

## Decision

`v16` was selected for the current branch because it produced the lowest
coarse score and reduced code size by 20 bytes. The change is committed as
`72c154b` and only rewrites `DenseChunkIterator.MoveNext` to pre-increment its
index.

`v03` produced the smallest code block, but `v16` had fewer `ldr`, `str` and
`ldp/stp` instructions. Neither candidate changed the `blr` count. The slot
loop itself contains no `blr` or bounds branch in the mapped output; the
remaining calls and checks are in setup/error paths.

Throughput and allocation measurements were not run in this sweep. A selected
candidate must still pass the normal Release tests and a non-dry benchmark
before being treated as a runtime speedup.
