# Destroy kernels v1

Baseline: `8a2c9f2`
Candidate code: `f4571ce` (`perf/destroy-kernels-v1`)
Scope: `Destroy(Entity)`, batch `Destroy(ReadOnlySpan<Entity>)`, and query
`Destroy(in Query)`.

No public API or semantic contract was changed.

## Accepted implementation

Batch `Destroy(ReadOnlySpan<Entity>)` now has three internal routes in
`src/DeltaECS/Core/World.cs`:

1. One valid entry skips sorting and the second lifetime/record validation.
2. Already ascending entries are consumed from the end, grouped by archetype/chunk, and use the existing whole-chunk destroy kernel when the group is complete.
3. Unsorted input keeps the old sort fallback, but complete groups use the whole-chunk kernel; partial groups retain per-entry validation and swap-back behavior.

The fallback is intentionally retained for arbitrary order, duplicates, stale handles, and partial chunks. `Destroy(Entity)` and query `Destroy` were not changed after their alternatives failed to show a repeatable gain.

## Controlled microbenchmark

The structural matrix uses `Amount = 1, 8, 256, 4096`. Fixture creation, scratch priming, query destroy priming, and reset are in `IterationSetup`; the benchmark method only executes the operation and verifies the returned count. Both sides used the same process class and:

```text
InvocationCount=1, WarmupCount=5, IterationCount=30, LaunchCount=3, UnrollFactor=1
.NET 8.0.29, Arm64 RyuJIT AdvSIMD, macOS arm64, workstation GC
```

Positive delta means the candidate is slower; negative means faster. Allocations are managed bytes per operation.

| Path | Amount | Baseline mean | Candidate mean | Delta | Baseline alloc | Candidate alloc |
|---|---:|---:|---:|---:|---:|---:|
| `Destroy(Entity)` | 1 | 439.3 ns | 480.7 ns | +9.4% | 736 B | 736 B |
| `Destroy(ReadOnlySpan<Entity>)` | 1 | 413.3 ns | 352.8 ns | −14.6% | 736 B | 736 B |
| `Destroy(ReadOnlySpan<Entity>)` | 8 | 3,035.2 ns | 1,538.2 ns | −49.3% | 800 B | 736 B |
| `Destroy(ReadOnlySpan<Entity>)` | 256 | 78,683.3 ns | 27,272.2 ns | −65.3% | 800 B | 736 B |
| `Destroy(ReadOnlySpan<Entity>)` | 4,096 | 1,261,696.7 ns | 107,160.0 ns | −91.5% | 800 B | 736 B |
| Query `Destroy` | 1 | 596.0 ns | 497.6 ns | −16.5% | 736 B | 736 B |
| Query `Destroy` | 8 | 493.8 ns | 524.0 ns | +6.1% | 736 B | 736 B |
| Query `Destroy` | 256 | 1,664.6 ns | 1,713.0 ns | +2.9% | 736 B | 736 B |
| Query `Destroy` | 4,096 | 17,483.0 ns | 17,159.1 ns | −1.9% | 736 B | 736 B |

The atomic and query measurements are short single-invocation operations and remain noisy. The batch improvement is repeatable across the non-trivial sizes and comes from avoiding `Span.Sort` plus repeated per-entry row work, not from changing allocation ownership.

Raw BDN CSV/JSON reports are retained outside the repository for this run:

- Baseline: `/private/tmp/deltaecs-destroy-baseline-final-results/results/`
- Candidate: `/private/tmp/deltaecs-destroy-candidate-final-results/results/`
- Atomic/list-1 confirmation: `/private/tmp/deltaecs-destroy-baseline-final2-results/results/` and `/private/tmp/deltaecs-destroy-candidate-final2-results/results/`

## Correctness evidence

Focused tests passed: **43/43**. The added test `StampInvariantTests.DestroyBatchHandlesDuplicatesStaleEntriesWholeChunksAndFallbackOrder` covers duplicate and stale handles, a complete chunk, unsorted partial fallback, survivor liveness, generations, and one stamp per successful batch. Existing tests cover managed-reference clearing, swap-back records, query destroy, free-record reuse, stamp exhaustion/atomicity, and active-lease rejection.

## Rejected approaches

- Singleton `Destroy(Entity)` routed through `DestroyChunk`: slower/noisy rather than repeatably faster (`661.2 ns` candidate vs `435.5 ns` baseline in the controlled trial); removed.
- Aggressive-inlining attributes on `TryResolve`, `Archetype.RemoveEntity`, and `Chunk.RemoveSwapBack`: no repeatable benefit; removed.
- Query-specific helper/active-chunk alternatives: the controlled query repeat was neutral to slower (`+1.0%` to `+21.1%` across 1/8/256/4096); no production query change kept.

## Verification

- Release solution rebuild with analyzer metrics: passed; baseline and candidate both 1,006 warning lines / 157 SARIF diagnostics, category delta `0`.
- Effective `Nullable`: `enable`.
- `FORMAT_CHECK=1 ../eng/format.sh "$PWD"`: passed.
- Focused Release tests: 43/43 passed.
- `git diff --check`: passed.
- No full comparative BenchmarkDotNet suite and no push.
