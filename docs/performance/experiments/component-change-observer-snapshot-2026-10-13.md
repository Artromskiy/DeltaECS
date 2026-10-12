# Component change observer snapshots, 2026-10-13

## Change

Before, generated query writes and `ForEachArchetype` called
`World.RecordComponentChange` for each matched entity. Each call looked up the
component's observer route and resolved its weak references again. The
candidate resolves live observers once per written `ComponentId`, stores
strong references in a pooled array, and calls them directly from the
notification loop. The array is cleared and returned when that pass finishes.
If the route has no live observers, it returns without renting an array.

The same pattern now covers generated entity-list writes and archetype
operations. Entity-list writes resolve each route once before traversing the
provided list; `ForEachArchetype` prepares writable observers once per
operation invocation and releases them after execution. Single direct writes
such as `World.GetRef` still record one entity through the ordinary world
route.

This does not remove notification traversals or change the public API.

## Workload and method

- 131,072 entities in one dense archetype.
- `ForEach` writes either one or four data components.
- Each case has zero, one, four, or sixteen live `ForEachChange` subscribers
  distributed across the written components.
- Subscriber callbacks are empty; the benchmark measures change recording,
  including its observer storage.
- BenchmarkDotNet 0.15.8, .NET 10.0.12 Arm64 RyuJIT, Apple M4 Pro, macOS Tahoe
  26.5.2; SDK 11.0.100-rc.1.
- `Default` job: 5 warmups, 20 measured iterations of 200 ms, one launch.
- Baseline was measured at commit `46ac30c`; the final candidate was measured
  from the working tree after extending the snapshot to entity-list and
  archetype loops. Runs were sequential, not interleaved, so small differences
  should be treated as noise.
- Both runs report 0 B allocated per operation.

## Results

Each cell is `Mean ± Error (StdDev)`. Ratio is candidate / baseline; below 1.0
means the candidate is faster.

| Case | Baseline | Candidate | Ratio | Change |
| --- | ---: | ---: | ---: | ---: |
| 1 component, 0 subscribers | 23.88 ±0.116 (0.129) µs | 24.80 ±0.214 (0.238) µs | 1.039 | +3.9% |
| 1 component, 1 subscriber | 350.16 ±1.402 (1.614) µs | 256.85 ±2.256 (2.414) µs | 0.734 | −26.7% |
| 1 component, 4 subscribers | 1,149.86 ±11.296 (12.555) µs | 849.76 ±4.687 (5.398) µs | 0.739 | −26.1% |
| 4 components, 0 subscribers | 79.72 ±0.266 (0.296) µs | 81.70 ±0.648 (0.746) µs | 1.025 | +2.5% |
| 4 components, 1 subscriber | 415.25 ±1.855 (2.062) µs | 314.85 ±0.943 (1.009) µs | 0.758 | −24.2% |
| 4 components, 4 subscribers | 1,707.99 ±8.643 (9.953) µs | 1,302.84 ±5.738 (5.636) µs | 0.763 | −23.7% |
| 4 components, 16 subscribers | 5,747.09 ±64.848 (74.679) µs | 5,920.04 ±48.070 (47.211) µs | 1.030 | +3.0% |

With one or four subscribers, the measured query cases are 24–27% faster.
The zero-subscriber rows are 2–4% slower in this run. The sixteen-subscriber
case is inconclusive: candidate runs ranged from `4.933 ms` to `6.291 ms`,
while the final run was `5.920 ms`. Since baseline and candidate were not
interleaved, treat both no-subscriber and high-fanout differences cautiously.
The BDN comparison measures dense query-based `ForEach`; entity-list and
`ForEachArchetype` paths are covered by focused runtime tests but do not have
separate timing data.

Observer calls remain in the entity/subscriber loop; the change removes
repeated route lookup and weak-reference resolution from that loop.

## Evidence

- Baseline BDN report: `artifacts/change-tracking-131k-final/results/Delta.ECS.MicroBenchmarks.ForEachWriteObserverMicroBenchmarks-report-github.md`
- Final candidate BDN report: `artifacts/change-tracking-131k-observer-cursor-final/results/Delta.ECS.MicroBenchmarks.ForEachWriteObserverMicroBenchmarks-report-github.md`
- Candidate 16-subscriber repeat: `artifacts/change-tracking-131k-cached-observers-16-repeat/results/Delta.ECS.MicroBenchmarks.ForEachWriteObserverMicroBenchmarks-report-github.md`
- Earlier candidate runs: `artifacts/change-tracking-131k-cached-observers-v2/results/Delta.ECS.MicroBenchmarks.ForEachWriteObserverMicroBenchmarks-report-github.md`, `artifacts/change-tracking-131k-cached-observers-unsafe-final/results/Delta.ECS.MicroBenchmarks.ForEachWriteObserverMicroBenchmarks-report-github.md`
- Release microbenchmark build succeeded; `ForEachChange` tests passed 3/3.
