# L0–L3 dense Movement4 sweep

This document records the isolated optimization sweep for
`QueryIteratorIterationMicroBenchmarks.Movement4IndependentIterators`.
Each candidate started from `54e0b93`; a candidate was accepted only when its
Release JIT result improved without a public API or tag-semantics change.
JIT size is evidence about generated code, not a cache-miss or throughput
measurement.

## Accepted merged state

The current branch contains the following accepted source changes:

| Change | Source effect | Commit |
|---|---|---|
| Terminal iterator assignments | Do not write exhausted archetype/chunk indexes a second time | `672ed81` |
| Reverse slot decrement | Use pre-decrement in `DenseSlotIterator.MoveNext` | `827b7e7` |
| Direct validated query state | Pass `CachedQuery` through dense iterators and remove `plan → query` indirection | `8b70dc3` |
| Cached component-row table | Cache `Chunk.RawComponentRows` once per slot iterator and resolve through the cached table | `dd43b6b` |

The tool counter expansion is in `d554b42`; it adds calls, branches,
comparison, arithmetic, multiply/divide, bounds, load/store, pair load/store,
prefetch, and AdvSIMD categories. Categories intentionally overlap: for
example, `cbnz` contributes to both `compare branch` and aggregate `branch`.

## Merged JIT comparison

Same Release probe, ARM64, first emitted code block:
`IterateMovement4IndependentIterators`.

| Metric | Baseline `54e0b93` | 48-candidate checkpoint `1a89172` | Final 50 `dd43b6b` |
|---|---:|---:|---:|
| Code size | 1408 B | 1108 B | **1084 B** |
| `blr` | 16 | 11 | **11** |
| `bl` | 3 | 3 | 3 |
| `branch` | 39 | 33 | **33** |
| `sbfiz` | 1 | 2 | **2** |
| `ldr` | 96 | 72 | **66** |
| `str` | 21 | 12 | **12** |
| `ldp/stp` | 33 | 28 | **28** |

Final code size is **−324 B / −23.0%** versus the original baseline and
`ldr` is down by 30 instructions in the first emitted block.

Detailed current report:
`artifacts/jit-disasm/accepted-l0l1l2-movement4.md`.

## Narrow BDN comparison

The existing benchmark uses `InvocationCount=1`, so the short timings are
directional and BDN reports `MinIterationTime` warnings. Allocation stayed at
736 B per operation.

| Amount | Baseline Mean | Current Mean | Change |
|---:|---:|---:|---:|
| 100 | 3.619 us | 3.574 us | −1.2% |
| 1,000 | 30.392 us | 27.898 us | −8.2% |
| 10,000 | 43.838 us | 42.675 us | −2.7% |
| 100,000 | 171.835 us | 173.420 us | +0.9% |

Current raw result:
`artifacts/micro/accepted-l0l1l2-movement4/results/`.

### Three paired runs for candidate 50

These runs used the same short BDN contract on the parent and candidate in
serial order. The table shows the median of three means; allocations were
736 B for the candidate in every run.

| Amount | Parent median | Candidate median | Median delta |
|---:|---:|---:|---:|
| 100 | 3.409 us | 3.380 us | −0.9% |
| 1,000 | 29.954 us | 30.322 us | +1.2% |
| 10,000 | 43.123 us | 43.572 us | +1.0% |
| 100,000 | 164.108 us | 172.844 us | +5.3% |

The paired result does not establish a throughput win at large sizes. The
candidate is retained for its stable JIT reduction and unchanged public API;
a longer, higher-operation-count BDN run is still appropriate before making a
throughput claim.

## Candidate accounting

There were 50 completed isolated candidates across L0, L1, L2, L3, and the
final two-candidate round.
Most were rejected because the JIT was unchanged or larger; a few compiled
variants changed assembly but had no paired throughput advantage.

| Package | Candidates | Best isolated signal | Decision |
|---|---:|---|---|
| L0-A | 6 | 1408 → 1396 B; `ldr` 96 → 94 | reject: no paired advantage |
| L0-B | 6 | 1408 → 1376 B; `blr` 16 → 15 | accepted, then combined with later changes |
| L1-A | 6 | 1408 → 1400 B; `ldr` 96 → 94; `str` 21 → 20 | reject pending paired evidence |
| L1-B | 6 | 1408 → 1160 B; `blr` 16 → 12 | accepted |
| L2-A | 6 | 1408 → 1404 B | accepted: pre-decrement candidate |
| L2-B | 6 | no reliable throughput win | reject |
| L3-A | 6 | 1408 → 1160 B; duplicate of L1-B winner | reject as duplicate |
| L3-B | 6 | 1408 → 1400 B in fixture-only packet variant | reject: no production change |
| Final 49 | 1 | prepared-column packet; current JIT unchanged at 1108 B | reject |
| Final 50 | 1 | current JIT 1108 → **1084 B**; `ldr` 72 → **66** | accepted |

The interrupted direct-`Chunk[]` L3 check was preserved in a local stash and
is not included in the 50-candidate matrix. No incomplete candidate was
merged.

## Verification

- `DeltaECS.slnx` Release build: passed, 0 warnings/errors.
- `DeltaECSTests`: 66 passed, 0 failed.
- Micro contract smoke: passed.
- `git diff --check`: passed.
- Full comparative BenchmarkDotNet suite: not run.
