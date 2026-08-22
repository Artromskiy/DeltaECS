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

## Current-branch merge audit

The remaining candidates were evaluated by merging each branch into temporary
worktrees based on current commit `05b8b43`, then running the same Release JIT
probe. The baseline at that point was 1412 B with `blr=16`, `bl=3`,
`branch=40`, `ldr=96`, `str=21` and `ldp/stp=33`; absent `bhs` and `umull`
are zero in this release block.

| Candidate | Merged result | Decision |
|---|---|---|
| v03–v15 | 1412 B; no counter change | rejected: no effect |
| v16 | already present in current | retained as existing change |
| v17 | 1408 B; `branch=39`, all other counters unchanged | accepted and merged |
| v18 | 1412 B; no counter change | rejected: no effect |
| v19 | 1412 B; no counter change after applying its unique `ref readonly` change | rejected: no effect |
| v20 | 1412 B; no counter change | rejected: no effect |

The no-effect ledger for future iterations is: ref-based resolved-row access
(v04–v06), extra iterator inlining/state reshaping (v07–v14), alternate slot
decrement (v15), the v03 array-access combination when layered on the current
branch, the unused optimization helper (v18), `ref readonly` plan storage
(v19), and the repeated compact slot state variant (v20). These changes should
not be reintroduced without a materially different surrounding JIT shape.

The accepted v17 change is the pre-increment form of
`DenseArchetypeIterator.MoveNext`; it is merged in commit `9b975d6`. No
extreme code-size/counter trade-off requiring a candidate branch to remain was
observed.
