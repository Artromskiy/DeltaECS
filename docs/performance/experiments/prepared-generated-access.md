# Prepared generated access evidence

## Candidate

`perf/prepared-generated-access` moves dense generated callback access setup
after the validated `OpenDense` boundary and returns read/write access objects
from query-plan caches. The public generated callback shape is unchanged.

The generated dense path now has this shape:

```text
OpenDense
GetPreparedReadAccess / GetPreparedWriteAccess
MoveNext(out slots)
generated row access and callback
```

The prepared access objects retain the existing query-plan ownership and route
checks. This is not a new public trusted escape hatch.

## Profile evidence

Command, run from the candidate worktree:

```bash
tools/profile-hotpath.sh \
  --movement4 \
  --root World.ForEach \
  --depth 32 \
  --warmups 10 \
  --correction optional \
  --sample-capacity 4000000 \
  --sections all \
  --format text \
  --sort adjusted \
  --destination file \
  --output artifacts/profiling/prepared-generated-access-movement4-foreach-hot-cache-depth32.txt
```

| Field | Value |
| --- | ---: |
| Runtime | .NET 10, Release |
| Architecture | host architecture |
| Probe | Movement4 |
| Root | `World.ForEach` |
| Entities | 100 |
| Depth | 32 |
| Warmups | 10 |
| Launches | 156,521 |
| Samples | 3,599,983 |
| Dropped samples | 0 |
| Probe overhead | 27.64 ns/sample |
| Adjusted root time | 57.970 ms |
| Calibration R² | 0.6545 |

The calibration R² is below the requested 0.8000 threshold, so this profile is
structural evidence only; it is not a reliable throughput claim.

### Adjusted call profile

| Method | Calls | Adjusted | Root |
| --- | ---: | ---: | ---: |
| `DemandForEachExtensions_90EB81AA.ForEach` | 156,521 | 57.970 ms | 100.0% |
| `GeneratedForEachRuntime.OpenDense(...)` | 156,521 | 10.365 ms | 17.9% |
| `Chunk.MarkComponentWrittenTrusted(...)` | 469,563 | 9.553 ms | 16.5% |
| `GeneratedForEachRuntime.GetPreparedWriteAccess(...)` | 469,563 | 9.249 ms | 16.0% |
| `GeneratedForEachRuntime.GetPreparedReadAccess(...)` | 156,521 | 3.465 ms | 6.0% |
| `World.EndQueryLease()` | 156,521 | 1.929 ms | 3.3% |

The `GetPrepared*` counts are one read access and three write accesses per
generated execution. They are no longer resolving a runtime type through the
old `Create*Access` path, but they are still setup calls at the execution
boundary. The slot loop itself is not represented by a separately named
profiled method in this report.

## Baseline comparison

The previous main hot-cache profile was:

`artifacts/profiling/movement4-main-hot-cache-depth32.txt`

Its generated setup tree contained `CreateReadAccess`/
`CreateWriteAccess` with `ResolvePrimaryReadRoute`, `ValidateQuery` and
`UpgradeReadRouteToWrite` descendants. The candidate tree contains the
prepared access calls without those old resolver descendants. The two runs
have different launch counts and weak calibration (`0.6018` baseline versus
`0.6545` candidate), so adjusted milliseconds must not be treated as a valid
speedup comparison.

## Decision

**Inconclusive pending BDN.** The candidate removes repeated resolver work from
the generated access path and preserves the validation boundary, but the
profile does not establish a throughput improvement. A paired Release BDN run
with the same workload and checksum is required before merging this branch.
