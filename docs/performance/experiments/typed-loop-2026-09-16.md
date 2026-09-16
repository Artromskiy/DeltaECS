# Typed binding loop spelling — 2026-09-16

## Compared implementations

Baseline: `7f561f87d08d3105b4d335b1089ab3b5b82732c3`, which already contains
prepared typed binding and the merged execution gate. Candidate: the same
commit with a bounded generator-template change requested by the user:

- cache `execution.Rows.Length` before the chunk loop;
- take its first reference with `MemoryMarshal.GetReference`, then access each
  descriptor through `Unsafe.Add` while retaining a `ref readonly` local;
- render the intercepted entity traversal as `for`, moving the index increment
  into the loop header and keeping component-reference advancement unchanged.

Both ordinary and intercepted typed-binding paths use the new chunk traversal.
No chunk size, component layout, arithmetic, callback shape, or stamp contract
is changed. The chunk capacity remains 512. A zero-length descriptor span is
safe: obtaining its first reference does not dereference it, and the cached
length prevents entry into the loop.

The ordinary execution template already used an entity `for`. The shared
sequential intercepted template now also emits `for` for the component-only
path; entity-bearing and stamp paths already used `for`.

## Protocol

- Same source revision, source project references, workload and runtime in
  isolated baseline and candidate worktrees.
- Apple M4 Pro / ARM64, .NET 10.0.9, SDK 10.0.301, BDN 0.13.12.
- Four scalar `SystemWith*.DeltaECS` workloads, at 100 and 100000 entities;
  padding 0; no manual unrolling or SIMD introduced.
- A1 -> B1 -> B2 -> A2, running both populations in each phase.
- One fresh BDN launch per case and phase, five warmups, twenty requested
  measurement iterations at 200 ms. Default outlier handling is retained.
- `DOTNET_JitDisasm=*:DeltaECS` is identical in every phase; normal tiering and
  dynamic PGO remain enabled. Logs retain the generated assembly listings.
- A speedup is confirmed only if both candidate Mean +/- Error intervals are
  below both baseline intervals. A regression uses the inverse criterion.
  Overlap is inconclusive; the protocol is not extended until significance.
- No builds, tests or other benchmark jobs are started by this task during the
  timed sequence. External workstation activity is not controlled.

This measures the three requested edits together. It does not attribute an
individual effect to each edit or compare ECS competitors.

## Validation

Generator tests: 64/64. Runtime tests: 133/133. Both fork benchmark builds and
contract smokes pass. Layout and `git diff --check` pass. Raw validation logs,
protocol, exact candidate patch and ABBA driver are retained under
`artifacts/typed-loop/`.

## Results

### 100 entities

Each launch is Mean +/- Error (StdDev in parentheses).

| Workload | A1 | B1 | B2 | A2 |
| --- | --- | --- | --- | --- |
| 1 component | 31.290 +/- 0.247 (0.284) ns | 31.910 +/- 0.187 (0.215) ns | 32.040 +/- 0.199 (0.229) ns | 32.720 +/- 0.132 (0.141) ns |
| 2 components | 32.360 +/- 0.187 (0.215) ns | 32.200 +/- 0.146 (0.168) ns | 32.720 +/- 0.259 (0.299) ns | 32.950 +/- 0.194 (0.224) ns |
| 3 components | 35.020 +/- 0.188 (0.201) ns | 34.710 +/- 0.250 (0.288) ns | 34.960 +/- 0.174 (0.193) ns | 35.830 +/- 0.669 (0.770) ns |
| 2 components, 4 archetypes | 35.180 +/- 0.154 (0.164) ns | 36.100 +/- 0.169 (0.188) ns | 35.840 +/- 0.197 (0.227) ns | 35.700 +/- 0.185 (0.212) ns |

### 100000 entities

Each launch is Mean +/- Error (StdDev in parentheses).

| Workload | A1 | B1 | B2 | A2 |
| --- | --- | --- | --- | --- |
| 1 component | 26.750 +/- 0.112 (0.129) μs | 27.150 +/- 0.094 (0.092) μs | 27.240 +/- 0.096 (0.111) μs | 27.310 +/- 0.131 (0.151) μs |
| 2 components | 29.740 +/- 0.255 (0.262) μs | 29.410 +/- 0.223 (0.257) μs | 29.510 +/- 0.216 (0.249) μs | 29.900 +/- 0.115 (0.128) μs |
| 3 components | 38.440 +/- 0.160 (0.184) μs | 37.940 +/- 0.167 (0.193) μs | 37.960 +/- 0.203 (0.234) μs | 38.470 +/- 0.145 (0.167) μs |
| 2 components, 4 archetypes | 29.890 +/- 0.376 (0.433) μs | 30.320 +/- 0.254 (0.282) μs | 30.290 +/- 0.240 (0.277) μs | 30.450 +/- 0.185 (0.213) μs |

### Conservative envelopes across processes

The envelope covers both launches' Mean +/- Error intervals; it is not a pooled
confidence interval. Mean change compares the average of the two process means.
It is not treated as a speedup or slowdown when the envelopes overlap.

| Entities | Workload | Before envelope | After envelope | Mean change | Result |
| --- | --- | --- | --- | --- | --- |
| 100 | 1 component | 31.043–32.852 ns | 31.723–32.239 ns | -0.09% | Inconclusive |
| 100 | 2 components | 32.173–33.144 ns | 32.054–32.979 ns | -0.60% | Inconclusive |
| 100 | 3 components | 34.832–36.499 ns | 34.460–35.134 ns | -1.67% | Inconclusive |
| 100 | 2 components, 4 archetypes | 35.026–35.885 ns | 35.643–36.269 ns | +1.50% | Inconclusive |
| 100000 | 1 component | 26.638–27.441 μs | 27.056–27.336 μs | +0.61% | Inconclusive |
| 100000 | 2 components | 29.485–30.015 μs | 29.187–29.726 μs | -1.21% | Inconclusive |
| 100000 | 3 components | 38.280–38.615 μs | 37.757–38.163 μs | -1.31% | Faster |
| 100000 | 2 components, 4 archetypes | 29.514–30.635 μs | 30.050–30.574 μs | +0.45% | Inconclusive |

All 32 measurements report **0 B** steady-state allocation. The three-component
100000-entity case is faster by **1.31%** and meets the fixed non-overlap rule.
The remaining seven cases are inconclusive; no regression meets that rule.
The baseline drift between A1 and A2 is retained in these envelopes.

## JIT evidence

Tier1 caller-body sizes from A1/B1 at 100 entities:

| Workload | Before | After |
| --- | --- | --- |
| 1 component | 872 B | 852 B |
| 2 components | 876 B | 880 B |
| 3 components | 884 B | 888 B |
| 2 components, 4 archetypes | 876 B | 880 B |

The scalar three-component entity-loop instructions are identical in these
A1/B1 listings (apart from labels). The changed code is in the chunk-loop
setup/control flow; replacing `while` with `for` did not simplify the entity
kernel. The old generated `Rows.Length` access was already hoisted in these
JIT listings. These are caller-body sizes, not transitive code-size totals.

## Retained result

The requested edits are retained. They yield a small confirmed improvement in
one measured scenario, with no evidence of a general speedup across workloads.
The production patch is limited to `DemandDrivenForEachTemplates.cs`; two
existing generator assertions were updated. No runtime API was added.

The patch and this report were accepted for merging from `perf/typed-binding`
into `main`. The isolated baseline/candidate worktrees and raw artifacts are
also retained. This run makes no comparison against Friflo or Myriad.
