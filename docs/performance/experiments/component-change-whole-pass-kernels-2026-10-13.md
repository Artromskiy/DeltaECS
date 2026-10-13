# Component-change whole-pass kernels — 2026-10-13

## Scope and source identity

Baseline: `e44e5bf` plus the existing incremental change-tracking working tree,
with the three pre-optimization files preserved under
`artifacts/change-kernels-baseline-source/`. Candidate: the same working tree
with changes limited to `ComponentChangeOperation`, `ComponentChangeMasks`,
and the observer mask boundary in `GeneratedRuntime`. Neither snapshot is
represented as a clean Git commit. The paired auxiliary baseline lives under
`/tmp/deltaecs-change-kernels-baseline-20261013` and uses the same updated harness.

No public API change, unrolling, global tiering override, volatile read, or
new persistent mask/slot cache. The existing pending/processing and worker-local
ownership remains unchanged.

## Mechanism and JIT evidence

- Data and tag notification loops each span all dirty chunks. Entity loops are
  no longer entered through a separate method for every chunk.
- Tags intersect pending writes with current tag presence once per chunk, then
  use `None`, `All`, or `Mask`. All uses one dense pointer walk; Mask handles
  full words densely and partial words with trailing-zero enumeration.
- A closed struct filter selects ordinary Modified or structural exclusion
  once per invocation. The ordinary optimized kernel contains no membership
  lookup or per-entity journal-count check.
- Input masks are classified before notifying subscribers. Each subscriber
  still owns independent accumulation; an accumulated All stops mask merging.
- Active chunk IDs are reset by length. A mask-write flag distinguishes unused
  mask storage from populated storage later promoted to All, preserving cleanup
  and retry after exceptions.

An intermediate candidate put `stackalloc` in the tag kernel. The JIT emitted
`Tier0-FullOpts` for it, preventing the intended dynamic PGO. This candidate
reduced the old 100%-tag result from 2.164ms to 186.2µs, but remained slower than
its data kernel. It was superseded: stack allocation now belongs to the outer
method and its span is passed to the hot kernel.

The diagnostic probe uses ordinary tiering and a callback with an observable
counter. At 32, 1,024, and 131,072 entities, the initial invocation plus three
subsequent half-world writes return exactly 80, 2,560, and 327,680 notifications
for both data and tags. No hundreds of preparatory invocations are used.
Final JIT output includes Tier1-OSR for both whole-pass kernels:

| Kernel | Tier1-OSR code bytes | Static instructions, excluding align directives | Static bl/blr sites |
| --- | ---: | ---: | ---: |
| Data | 1,244 | 311 | 6 |
| Tag | 1,036 | 259 | 5 |

These are whole-method static counts, not executed-instruction counters or a
claim of fully inlined callbacks. Raw traces are in
`artifacts/change-kernels-jit-probe/{kernels-jit,final-kernels-jit}.txt`.

## Verification and measurement rules

- Runtime tests: 230/230, including tail words, sparse tags, mask promotion,
  independent subscribers, exception retry, swap-back, bulk movement,
  recycled chunk IDs, recycled entity indices, and membership-event folding.
- Generator tests: 81/81; Systems tests: 23/23, through the workspace dev workflow.
- Runtime builds for net10.0 and netstandard2.1 succeed. The latter retains
  three existing XML-documentation warnings.
- Layout gate and diff whitespace check pass.

Apple M4 Pro Arm64, macOS Tahoe 26.5.2, .NET 10.0.12, BDN 0.15.8.
131,072 entities. Every entity has an additional component excluded from the
query, writes, and observers. Default BDN; changed/first-invocation cases retain
InvocationCount=1 so repeated empty calls do not replace change delivery.
Change preparation is outside the measured Invoke. Dense means a prefix of
entities; scattered uses an identical deterministic shuffle for data and tags.
All reported iterations allocate 0 B; setup allocates world/buffer ownership.

The first full candidate run overlapped an active Unity process. Its four-data,
16-subscriber sample shifted from about 89µs to 145µs within the process and
was rejected as performance evidence. The full matrix is repeated after Unity
was stopped. Both logs remain under artifacts. A noisy auxiliary dense-tag
counter run is also retained rather than discarded silently.

## Results

The final primary run covers all 24 cases. Positive percentages mean more time,
negative percentages mean less time. All comparisons below use the same default
BDN settings and the original empty callback. No primary scenario regresses by
more than 3.5%; sub-5% differences in sequential launches should not be treated
as robust small improvements. The important dense tag results are 25.1× faster
at 50% and 33.0× faster at 100%; tag/data costs at 100% are 65.6µs / 67.1µs.

| Method | Before: Mean ±Error; StdDev | After: Mean ±Error; StdDev | Time change | Allocated before/after |
| --- | ---: | ---: | ---: | ---: |
| `ForEachOneComponentNoSubscribers` | 33.2µs ±359.ns; SD 318.ns | 33.9µs ±469.ns; SD 416.ns | +2.2% | 0 B / 0 B |
| `ForEachOneComponentOneSubscriber` | 31.7µs ±617.ns; SD 577.ns | 31.3µs ±99.5ns; SD 93.1ns | -1.3% | 0 B / 0 B |
| `ForEachOneComponentFourSubscribers` | 31.7µs ±159.ns; SD 141.ns | 32.2µs ±438.ns; SD 410.ns | +1.4% | 0 B / 0 B |
| `ForEachFourComponentsNoSubscribers` | 80.5µs ±1.41µs; SD 1.25µs | 81.6µs ±236.ns; SD 209.ns | +1.4% | 0 B / 0 B |
| `ForEachFourComponentsOneSubscriber` | 78.5µs ±338.ns; SD 300.ns | 79.9µs ±349.ns; SD 327.ns | +1.8% | 0 B / 0 B |
| `ForEachFourComponentsFourSubscribers` | 80.4µs ±278.ns; SD 247.ns | 81.6µs ±396.ns; SD 351.ns | +1.5% | 0 B / 0 B |
| `ForEachFourComponentsSixteenSubscribers` | 84.2µs ±160.ns; SD 150.ns | 86.0µs ±345.ns; SD 323.ns | +2.1% | 0 B / 0 B |
| `ForEachOneTagNoSubscribers` | 8.78µs ±175.ns; SD 180.ns | 8.69µs ±35.0ns; SD 31.0ns | -1.0% | 0 B / 0 B |
| `ForEachOneTagOneSubscriber` | 12.4µs ±161.ns; SD 150.ns | 11.9µs ±79.4ns; SD 70.4ns | -4.2% | 0 B / 0 B |
| `ForEachOneTagFourSubscribers` | 15.4µs ±123.ns; SD 103.ns | 12.7µs ±49.4ns; SD 43.8ns | -17.6% | 0 B / 0 B |
| `ForEachFourTagsNoSubscribers` | 11.5µs ±223.ns; SD 219.ns | 11.3µs ±55.3ns; SD 51.7ns | -1.8% | 0 B / 0 B |
| `ForEachFourTagsOneSubscriber` | 14.7µs ±96.9ns; SD 75.7ns | 14.3µs ±36.1ns; SD 32.0ns | -2.5% | 0 B / 0 B |
| `ForEachFourTagsFourSubscribers` | 24.8µs ±291.ns; SD 258.ns | 23.0µs ±140.ns; SD 124.ns | -7.4% | 0 B / 0 B |
| `ForEachFourTagsSixteenSubscribers` | 39.9µs ±742.ns; SD 694.ns | 27.0µs ±242.ns; SD 227.ns | -32.3% | 0 B / 0 B |

| Method | Before: Mean ±Error; StdDev | After: Mean ±Error; StdDev | Time change | Allocated before/after |
| --- | ---: | ---: | ---: | ---: |
| `ForEachChangeComponentsNoChanges` | 0.7382ns ±0.0235ns; SD 0.0220ns | 0.7039ns ±0.0023ns; SD 0.0020ns | -4.6% | 0 B / 0 B |
| `ForEachChangeTagsNoChanges` | 0.6779ns ±0.0061ns; SD 0.0054ns | 0.7016ns ±0.0024ns; SD 0.0022ns | +3.5% | 0 B / 0 B |
| `ForEachChangeComponentFirstInvocation` | 314.µs ±5.34µs; SD 5.72µs | 303.µs ±5.98µs; SD 11.9µs | -3.3% | 0 B / 0 B |
| `ForEachChangeComponentOnePercentModified` | 31.7µs ±709.ns; SD 2.02µs | 21.5µs ±560.ns; SD 1.59µs | -32.1% | 0 B / 0 B |
| `ForEachChangeComponentFiftyPercentModified` | 61.0µs ±1.22µs; SD 2.32µs | 44.7µs ±884.ns; SD 2.22µs | -26.8% | 0 B / 0 B |
| `ForEachChangeComponentOneHundredPercentModified` | 103.µs ±2.03µs; SD 3.11µs | 67.1µs ±1.34µs; SD 3.24µs | -34.6% | 0 B / 0 B |
| `ForEachChangeTagFirstInvocation` | 293.µs ±6.35µs; SD 18.1µs | 275.µs ±5.47µs; SD 8.51µs | -6.2% | 0 B / 0 B |
| `ForEachChangeTagOnePercentModified` | 28.0µs ±726.ns; SD 2.07µs | 21.6µs ±707.ns; SD 2.02µs | -22.9% | 0 B / 0 B |
| `ForEachChangeTagFiftyPercentModified` | 1.09ms ±20.5µs; SD 22.0µs | 43.5µs ±865.ns; SD 2.31µs | -96.0% | 0 B / 0 B |
| `ForEachChangeTagOneHundredPercentModified` | 2.16ms ±42.1µs; SD 51.7µs | 65.6µs ±1.31µs; SD 2.99µs | -97.0% | 0 B / 0 B |

The two no-change measurements are near the benchmark overhead-subtraction
floor. They establish the constant empty path, not a physical sub-nanosecond
execution latency. First/changed cases use one invocation per iteration; BDN
warns that their measured iteration times are below 100ms. This is the retained
fixture policy needed to measure pending changes, rather than subsequent empty
invocations. Error is the half-width of BDN's 99.9% confidence interval.

Raw primary reports:

- Before: `artifacts/change-tracking-dense-default-20261013/results/`.
- After: `artifacts/change-tracking-kernels-quiet-default-20261013/results/`.

### Observable callbacks and layout

The callback increments a retained `long`, and the diagnostic probe verifies
notification counts independently. Dense candidate rows use the quiet dense
run, with the 100% pair rechecked in a separate process after one anomalous
launch; scattered rows use the final quiet scattered run. Baselines use the
same harness in the preserved baseline source tree. Preparation, shuffle, and
initial observation remain outside the measured call.


#### Dense

| Method | Before: Mean ±Error; StdDev | After: Mean ±Error; StdDev | Time change | Allocated before/after |
| --- | ---: | ---: | ---: | ---: |
| `ForEachChangeComponentOnePercentModified` | 31.8µs ±634.ns; SD 1.43µs | 20.5µs ±1.07µs; SD 3.05µs | -35.5% | 0 B / 0 B |
| `ForEachChangeComponentFiftyPercentModified` | 73.0µs ±1.45µs; SD 3.92µs | 57.7µs ±1.46µs; SD 4.22µs | -20.9% | 0 B / 0 B |
| `ForEachChangeComponentOneHundredPercentModified` | 123.µs ±2.55µs; SD 7.36µs | 91.0µs ±1.82µs; SD 4.46µs | -25.9% | 0 B / 0 B |
| `ForEachChangeTagOnePercentModified` | 29.7µs ±1.30µs; SD 3.77µs | 21.2µs ±1.30µs; SD 3.72µs | -28.5% | 0 B / 0 B |
| `ForEachChangeTagFiftyPercentModified` | 1.07ms ±19.5µs; SD 28.6µs | 54.7µs ±1.65µs; SD 4.68µs | -94.9% | 0 B / 0 B |
| `ForEachChangeTagOneHundredPercentModified` | 2.13ms ±41.8µs; SD 42.9µs | 86.1µs ±1.71µs; SD 4.57µs | -96.0% | 0 B / 0 B |

#### Scattered

| Method | Before: Mean ±Error; StdDev | After: Mean ±Error; StdDev | Time change | Allocated before/after |
| --- | ---: | ---: | ---: | ---: |
| `ForEachChangeComponentOnePercentModified` | 61.6µs ±1.34µs; SD 3.73µs | 30.3µs ±1.48µs; SD 4.19µs | -50.7% | 0 B / 0 B |
| `ForEachChangeComponentFiftyPercentModified` | 1.08ms ±20.9µs; SD 27.1µs | 86.0µs ±2.31µs; SD 6.44µs | -92.1% | 0 B / 0 B |
| `ForEachChangeComponentOneHundredPercentModified` | 121.µs ±2.41µs; SD 5.09µs | 99.2µs ±2.76µs; SD 8.06µs | -18.0% | 0 B / 0 B |
| `ForEachChangeTagOnePercentModified` | 69.7µs ±1.36µs; SD 2.11µs | 29.4µs ±1.04µs; SD 3.02µs | -57.9% | 0 B / 0 B |
| `ForEachChangeTagFiftyPercentModified` | 986.µs ±19.6µs; SD 26.9µs | 81.8µs ±2.70µs; SD 7.87µs | -91.7% | 0 B / 0 B |
| `ForEachChangeTagOneHundredPercentModified` | 2.11ms ±41.0µs; SD 42.1µs | 95.3µs ±2.62µs; SD 7.40µs | -95.5% | 0 B / 0 B |

The dense 100%-tag counter run preceding the pair recheck reported
223.7µs mean, 59.6µs StdDev, with modes near 90µs and 250µs. This is retained
under `artifacts/change-tracking-kernels-quiet-count-dense/`, not silently
removed from the evidence. The first candidate counter run was 93.3µs, the
scattered 100% run was 95.3µs, and the explicit dense repeat was 86.1µs
versus 91.0µs for data. The cause of that anomalous launch is not established;
these results do not prove every future process has identical timing. The
repeat still contains a few individual high outliers, visible in the raw log.

Observable-callback results confirm that the tag improvement persists when
the callback cannot be eliminated. With scattered 50% changes, data improves
from 1.08ms to 86.0µs and tags from 986.µs to 81.8µs. There is no demonstrated
steady regression above 5%, and the measured ordinary runs meet the first
2× tag/data threshold. Small launch-to-launch differences remain inconclusive.

### Decision

Retain the requested candidate. The whole-operation kernels and the placement
of stack allocation are both necessary for the large tag improvement under
normal tiered compilation. Cleanup, subscriber independence, structural folding,
and exception retry passed the runtime suite. Performance evidence applies to
CoreCLR Arm64 on the recorded machine; netstandard2.1 was built, but Mono/Unity
runtime performance was not measured in this experiment.


## Reproduction

```sh
dotnet run -c Release --project benchmarks/DeltaECS.MicroBenchmarks -- \
  change-tracking --amount 131072 --change-callback empty --change-layout dense \
  --filter '*' --artifacts artifacts/change-tracking-kernels-quiet-default-20261013
```

The same 24 methods support optional `--change-callback count` and
`--change-layout scattered`. Auxiliary comparisons filter `*PercentModified`
and use the same default BDN job. The diagnostic JIT probe is an ignored
artifact and does not change benchmark runtime settings.
