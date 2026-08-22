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

The tool counter expansion is in `d554b42`; it adds calls, branches,
comparison, arithmetic, multiply/divide, bounds, load/store, pair load/store,
prefetch, and AdvSIMD categories. Categories intentionally overlap: for
example, `cbnz` contributes to both `compare branch` and aggregate `branch`.

## Merged JIT comparison

Same Release probe, ARM64, first emitted code block:
`IterateMovement4IndependentIterators`.

| Metric | Baseline `54e0b93` | Current `1a89172` | Delta |
|---|---:|---:|---:|
| Code size | 1408 B | **1108 B** | **−300 B / −21.3%** |
| `blr` | 16 | **11** | −5 |
| `bl` | 3 | 3 | 0 |
| `branch` | 39 | **33** | −6 |
| `sbfiz` | 1 | 2 | +1 |
| `ldr` | 96 | **72** | −24 |
| `str` | 21 | **12** | −9 |
| `ldp/stp` | 33 | **28** | −5 |

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

## Candidate accounting

There were 48 completed isolated candidates across L0, L1, L2, and L3.
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

The interrupted direct-`Chunk[]` L3 check was preserved in a local stash and
is not included in the 48-candidate matrix. No incomplete candidate was
merged.

## Verification

- `DeltaECS.slnx` Release build: passed, 0 warnings/errors.
- `DeltaECSTests`: 66 passed, 0 failed.
- Micro contract smoke: passed.
- `git diff --check`: passed.
- Full comparative BenchmarkDotNet suite: not run.
