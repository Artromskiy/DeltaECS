# Per-entity change markers, 2026-10-13

## Change

`ComponentChangeOperation<T>` used an `int[]` indexed by entity index and stored
the entity generation for every observed write. The candidate stores a single
byte marker instead. The existing entity sets still compare complete `Entity`
values, so added/removed classification remains generation-aware; the marker
only answers whether the current entity index was written since the previous
invocation. Recycled-index behavior is covered by
`ForEachChangeDoesNotTreatReusedEntityIndexAsModifiedOldEntity`.

This keeps exact per-entity notifications. An archetype/chunk marker would
also report unchanged entities in a partially written range, while a packed
bitset would need atomic bit updates when parallel writers mark different
entities in the same word. The byte marker reduces marker storage and clearing
traffic by four times compared with the previous `int[]`; notification and
entity traversal remain unchanged.

## Workload and method

- The unchanged benchmark harness was added in commit `46ac30c` (`Add
  component change tracking benchmarks`).
- Baseline source: `c1ba030`; candidate: working tree with the byte-marker
  change.
- 131,072 entities in one dense archetype. Write-observer cases cover one or
  four written components and 0, 1, 4 or 16 subscribers. `ForEachChange` cases
  cover first invocation, no changes, 1% modified and 50% modified entities.
- BenchmarkDotNet 0.15.8, .NET 10.0.12 Arm64 RyuJIT, Apple M4 Pro,
  macOS Tahoe 26.5.2; SDK 11.0.100-rc.1. The observer suite used five
  warmups and 20 × 200 ms measured iterations, one launch. The `ForEachChange`
  suite used the same job; its mutation cases intentionally invoke once per
  iteration.
- Baseline and candidate ran sequentially, not interleaved. Treat changes
  under a few percent as inconclusive. All steady observer cases allocate
  `0 B`; the first `ForEachChange` invocation allocates `6,037,560 B` in both
  builds, and its steady cases allocate `0 B`.

## Results

Observer rows show mean ± BenchmarkDotNet error in microseconds, with
candidate/baseline ratio. A ratio below `1.0` means the candidate was faster.

| Case | Baseline | Candidate | Ratio | Change |
| --- | ---: | ---: | ---: | ---: |
| 1 component, 0 subscribers | 24.94 ±0.280 µs | 23.98 ±0.227 µs | 0.962 | −3.8% |
| 1 component, 1 subscriber | 256.84 ±1.358 µs | 248.66 ±1.081 µs | 0.968 | −3.2% |
| 1 component, 4 subscribers | 867.39 ±8.730 µs | 841.96 ±5.694 µs | 0.971 | −2.9% |
| 4 components, 0 subscribers | 79.54 ±0.401 µs | 81.22 ±0.392 µs | 1.021 | +2.1% |
| 4 components, 1 subscriber | 315.08 ±1.205 µs | 312.11 ±1.466 µs | 0.991 | −0.9% |
| 4 components, 4 subscribers | 1,299.89 ±2.350 µs | 1,221.88 ±15.715 µs | 0.940 | −6.0% |
| 4 components, 16 subscribers | 5,934.01 ±28.516 µs | 4,929.02 ±57.535 µs | 0.831 | −16.9% |

`ForEachChange` results:

| Case | Baseline | Candidate | Ratio | Change |
| --- | ---: | ---: | ---: | ---: |
| First invocation | 3.079 ±0.136 ms | 2.850 ±0.065 ms | 0.926 | −7.4% |
| 1% modified | 7.184 ±0.239 ms | 6.672 ±0.094 ms | 0.929 | −7.1% |
| 50% modified | 6.985 ±0.260 ms | 6.694 ±0.072 ms | 0.958 | −4.2% |
| No changes | 4.958 ±0.103 ms | 4.867 ±0.215 ms | 0.982 | −1.8% |

The largest repeatable-looking signal in this single sequential comparison is
the 16-subscriber observer case. The smaller observer differences and the
unchanged `ForEachChange` case are too small to distinguish from process or
thermal drift. The results support the lower-footprint representation, but a
second interleaved A/B run is needed before making a strong throughput claim.

## Correctness and evidence

- `ArchetypeForEachOperationTests`: 13/13 passed, including recycled entity
  index and generation behavior.
- `git diff --check`: clean.
- A standalone unsafe-array probe checked both managed indexing and a pinned
  pointer to the last byte after the write.
- Raw reports are under ignored `artifacts/change-marker-baseline/`,
  `artifacts/change-marker-candidate/`, and
  `artifacts/change-marker-for-each-change-baseline/`.
