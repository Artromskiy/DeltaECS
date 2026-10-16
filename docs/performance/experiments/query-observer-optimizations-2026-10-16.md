# Query, observer and tag-membership optimizations — 2026-10-16

## Scope

Evaluated the five runtime changes from `DeltaECS_optimized_5files (1).zip`, retained the measured improvements, and added focused BenchmarkDotNet coverage. The evaluated changes:

- Stream `OrderedQuery.First()` as a minimum search instead of collecting and sorting every matching entity.
- Test returning already ordered inputs directly and swapping merge buffers instead of copying the final merge result. This sort change measured within error and was not retained.
- Add a direct one-route observer-write path; bulk-record full-chunk writes for `DirtyKind.All`; resolve entity location and query/tag membership once for all write routes in multi-entity operations.
- Use `Archetype.TryGetComponentIndex` instead of calculating the component row with `Mask.Rank` in structural value-copy paths.
- Add a focused `Add/Remove` tag-membership benchmark and an `OrderedQuery.First` baseline/candidate profile benchmark.

The source changes preserve existing public APIs. The candidate uses no per-iteration allocations in the measured scenarios.

## Measurement setup

- Baseline: clean checkout at `a59913c`; candidate: the combined optimization working tree. The retained code is isolated in `1e3aead` (streaming `OrderedQuery.First`), `74dbc4b` (structural row lookup), and `52807ca` (observer write routes). Candidate-only stable-sort and tag-mask variants are listed separately below and were not retained.
- Apple M4 Pro, macOS Tahoe 26.5.2, .NET SDK `11.0.100-rc.1.26425.128`, .NET `10.0.12` Arm64 RyuJIT, BenchmarkDotNet `0.15.8`.
- ECS iteration workload: 131,072 entities. BenchmarkDotNet default job; stateful `ForEachChange` cases use `InvocationCount=1`, `UnrollFactor=1` so each measurement invokes the stateful operation exactly once.
- The profile harness compares baseline and candidate in one BDN process and checks setup checksums before timing.
- Values below are `Mean ± Error`, with relative change computed from exact means. `−` means faster; `+` means slower. All measured cases allocated `0 B`.
- ForEachChange's no-change means are below 1 ns and therefore at the measurement floor. The 1% component/tag scenarios were reported as multimodal; interpret their relative changes cautiously. Error intervals overlap for several smaller differences. BenchmarkDotNet also warned that it could not raise process priority; the runs completed normally.

## ForEach with change observers

| Scenario | Baseline | Candidate | Change |
| --- | ---: | ---: | ---: |
| 1 component, 0 subscribers | 34.217 ± 0.201 μs | 33.141 ± 0.181 μs | −3.1% |
| 1 component, 1 subscriber | 32.103 ± 0.213 μs | 31.324 ± 0.205 μs | −2.4% |
| 1 component, 4 subscribers | 32.708 ± 0.132 μs | 31.378 ± 0.122 μs | −4.1% |
| 4 components, 0 subscribers | 80.649 ± 0.521 μs | 80.633 ± 0.252 μs | 0.0% |
| 4 components, 1 subscriber | 79.326 ± 0.399 μs | 79.912 ± 0.299 μs | +0.7% |
| 4 components, 4 subscribers | 81.578 ± 0.334 μs | 81.154 ± 0.517 μs | −0.5% |
| 4 components, 16 subscribers | 85.707 ± 0.728 μs | 85.203 ± 0.697 μs | −0.6% |
| 1 tag, 0 subscribers | 8.808 ± 0.102 μs | 8.479 ± 0.045 μs | −3.7% |
| 1 tag, 1 subscriber | 11.791 ± 0.049 μs | 11.471 ± 0.121 μs | −2.7% |
| 1 tag, 4 subscribers | 12.617 ± 0.093 μs | 12.234 ± 0.068 μs | −3.0% |
| 4 tags, 0 subscribers | 11.365 ± 0.097 μs | 11.154 ± 0.076 μs | −1.9% |
| 4 tags, 1 subscriber | 14.093 ± 0.065 μs | 14.026 ± 0.069 μs | −0.5% |
| 4 tags, 4 subscribers | 22.151 ± 0.167 μs | 21.811 ± 0.128 μs | −1.5% |
| 4 tags, 16 subscribers | 26.252 ± 0.109 μs | 26.018 ± 0.097 μs | −0.9% |

The one-component path is consistently a little faster. Multi-component paths are effectively neutral within this run's uncertainty, with no allocation change.

## ForEachChange

| Scenario | Baseline | Candidate | Change |
| --- | ---: | ---: | ---: |
| Components, no changes | 0.7347 ± 0.0085 ns | 0.7264 ± 0.0162 ns | −1.1%* |
| Tags, no changes | 0.7433 ± 0.0155 ns | 0.7892 ± 0.0088 ns | +6.2%* |
| Components, first invocation | 345.706 ± 7.534 μs | 320.244 ± 6.280 μs | −7.4% |
| Components, 1% modified | 27.374 ± 1.831 μs | 20.689 ± 0.958 μs | −24.4%† |
| Components, 50% modified | 48.182 ± 1.887 μs | 48.669 ± 1.831 μs | +1.0% |
| Components, 100% modified | 74.001 ± 1.599 μs | 68.768 ± 1.371 μs | −7.1% |
| Tags, first invocation | 325.215 ± 7.754 μs | 298.984 ± 5.980 μs | −8.1% |
| Tags, 1% modified | 23.859 ± 1.375 μs | 22.160 ± 0.876 μs | −7.1%† |
| Tags, 50% modified | 45.727 ± 1.050 μs | 44.167 ± 0.931 μs | −3.4% |
| Tags, 100% modified | 70.946 ± 1.660 μs | 68.231 ± 1.557 μs | −3.8% |

`*` These sub-nanosecond no-change timings are at the measurement floor and do not establish a meaningful speed change. `†` BDN reported a multimodal distribution for both 1% scenarios; the component interval is separated in this run, while the tag interval overlaps. Treat these as promising signals, not settled results.

The first invocation and dense 100% cases improved for both component and tag observers. The component 50% case is neutral within uncertainty. No case allocated memory.

## Ordered queries and structural operations

| Scenario | Baseline | Candidate | Change |
| --- | ---: | ---: | ---: |
| `OrderedQuery.First` | 2.022 ± 0.0085 ms | 1.848 ± 0.0097 ms | −8.6% |
| Stable two-key sort | 36.12 ± 0.714 ms | 35.65 ± 0.168 ms | −1.3% |
| Structural add/remove toggle | 86.98 ± 0.432 ns | 84.24 ± 0.473 ns | −3.1% |

`First` has a clear improvement in this profile workload and avoids collecting/sorting the whole result. Stable sorting's uncertainty intervals overlap, so its small mean reduction is inconclusive. The add/remove change is small and should be treated as neutral without a repeat.

### Re-test: swap the merge result buffer without sorted-input early exit

Baseline is `9ffa766`. The candidate changed only the final merge handling in
`World.SortOrderedQueryEntities`: it kept the normal merge passes and removed
the final `Array.Copy` by swapping the world's two buffer fields when the
sorted result was in the merge buffer. It did not add an already-sorted input
check. `StableSortBenchmarks` setup compared the baseline and candidate
checksums before timing at each size.

| Entities | Baseline Mean ± Error (StdDev) | Candidate Mean ± Error (StdDev) | Candidate change | Allocated |
| ---: | ---: | ---: | ---: | ---: |
| 131,072 | 34.819 ± 0.383 ms (0.651 ms) | 35.115 ± 0.243 ms (0.406 ms) | +0.9% | 0 B / 0 B |
| 1,048,576 | 575.015 ± 28.718 ms (49.537 ms) | 638.935 ± 41.552 ms (72.776 ms) | +11.1% | 0 B / 0 B |

Both 99.9% confidence intervals overlap. The 1M candidate distribution was
multimodal, and macOS Storage Management and WindowServer processes were using
substantial CPU during the run, so the apparent slowdown is not conclusive.
There is no measured improvement to retain. The 131K JIT smoke measured
`SortOrderedQueryEntities` at 772 B / 192 instructions / 4 calls / 33 branches
for baseline and 756 B / 189 instructions / 5 calls / 33 branches for the
candidate. The candidate shrinks code slightly but adds a reference-store call;
throughput does not improve in these measurements.

Protocol: Apple M4 Pro, macOS Tahoe 26.5.2, .NET 10.0.12 Arm64 RyuJIT,
BenchmarkDotNet 0.15.8, 8 warmups, 20 measured iterations, 200 ms target per
iteration, 2 launches. Raw reports: `artifacts/ordered-sort-swap-131k` and
`artifacts/ordered-sort-swap-1048k`.

## Rejected tag-mask scan

The proposed optimization scanned selected tag slots by extracting set bits from a chunk mask. It was slower than checking tag membership while walking slots, so it was not retained. The stable-sort candidate was also removed after its BDN interval overlapped the baseline and the JIT method grew.

| Variant | Mean ± Error | Change vs baseline |
| --- | ---: | ---: |
| Baseline per-slot membership | 3.849 ± 0.015 ms | — |
| Set-bit mask scan | 4.165 ± 0.033 ms | +8.2% |
| Dense-branch mask scan | 4.206 ± 0.030 ms | +9.3% |
| Final reverted per-slot path | 3.877 ± 0.017 ms | +0.7% |

The final and baseline intervals overlap. All variants allocated `0 B`. This result does not support retaining the mask-scan implementation.

## JIT code size and instruction counts

ARM64 JIT disassembly comparison for the observer-write and stable-sort methods:

| Method | Baseline | Candidate | Change |
| --- | ---: | ---: | ---: |
| `RecordComponentWriteChunk` code size | 532 B | 676 B | +144 B |
| `RecordComponentWriteChunk` instructions | 133 | 169 | +36 |
| `RecordComponentWriteChunk` branches / calls | 19 / 8 | 27 / 10 | +8 / +2 |
| `SortOrderedQueryEntities` code size | 908 B | 1,016 B | +108 B |
| `SortOrderedQueryEntities` instructions | 235 | 261 | +26 |
| `SortOrderedQueryEntities` branches / calls | 27 / 4 | 32 / 7 | +5 / +3 |

Both methods grow because they now contain the added fast paths and buffer-ownership logic. The observer method's larger code did not produce a broad multi-component win; retain that path based on the modest single-route/tag gains and the overall observer table, not on code-size reduction. The sort implementation reduces copying but its BDN result is inconclusive. A separate standalone `OrderedQuery.First` assembly listing was not captured.

## Correctness and verification

- Furnace `dev` workflow for `DeltaECS` completed successfully.
- DeltaECS runtime tests: 259/259 passed.
- DeltaECS.Generators tests: 86/86 passed.
- DeltaECS.Systems tests: 23/23 passed.
- Generator/runtime consumers and grammar C# 9 registration builds passed; grammar AOT test project built successfully.
- `OrderedQuery.First` profile setup checks compare baseline and candidate results before timing.
- All reported benchmark cases allocated `0 B` per operation.

Raw BenchmarkDotNet reports and JIT disassembly are retained locally under ignored `BenchmarkDotNet.Artifacts/` and `artifacts/jit-disasm/`. They are not committed as source artifacts; the tables here are the durable results.
