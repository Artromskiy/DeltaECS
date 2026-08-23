# Cached resolved query rows experiment

Branch: `perf/cached-resolved-query-rows`  
Baseline: `54a2967`  
Implementation: `9189545`

## Hypothesis

Cache query-ordered managed `Array` references for every active chunk so
`QuerySlots.Get` no longer traverses `Chunk.RawComponentRows[physicalRow]`.
The cache is refreshed before a query scope starts when the active chunk set
changes. It does not retain spans, byrefs, pointers, or native memory across
structural changes.

## Release JIT

Probe: `MicroBenchmarkKernels.IterateMovement4Dense`, macOS ARM64, FullOpts,
tiered compilation and ReadyToRun disabled.

| Metric | Baseline | Candidate | Delta |
|:---|---:|---:|---:|
| Code size | 1276 B | 1212 B | -64 B (-5.0%) |
| `blr` | 14 | 13 | -1 |
| `bl` | 3 | 3 | 0 |
| Compare branch | 10 | 9 | -1 |
| Branch | 27 | 25 | -2 |
| Compare | 13 | 12 | -1 |
| Add/sub | 35 | 35 | 0 |
| `sbfiz` | 2 | 2 | 0 |
| Shift/bitfield | 1 | 1 | 0 |
| `ldr` | 88 | 82 | -6 |
| `str` | 26 | 24 | -2 |
| `ldp/stp` | 29 | 31 | +2 |

## Narrow BenchmarkDotNet result

Both revisions were run consecutively on the same Apple M4 Pro with .NET
8.0.29 and BenchmarkDotNet's default adaptive job. The benchmark's existing
correctness requirement keeps `InvocationCount=1` and `UnrollFactor=1` because
Movement4 mutates component rows and resets them once per iteration.

| Amount | Baseline mean | Candidate mean | Candidate / baseline | Allocated |
|---:|---:|---:|---:|---:|
| 100 | 3.509 us | 3.661 us | 1.043 | 736 B |
| 1,000 | 28.280 us | 29.550 us | 1.045 | 736 B |
| 10,000 | 41.572 us | 41.465 us | 0.997 | 736 B |
| 100,000 | 149.572 us | 150.609 us | 1.007 | 736 B |
| 1,000,000 | 1,239.612 us | 1,207.902 us | 0.974 | 736 B |

The short workloads are noisy and BenchmarkDotNet reports very short
iteration-time warnings. The candidate's only positive throughput signal is
at one million entities, while 100 and 1,000 entities regress by about 4.5%.

## Correctness

- Release microbenchmark build passed.
- Micro contract smoke passed Movement2, Movement4, Add, Remove, Create and
  Destroy.
- Unit tests passed: 58/58.
- `ActiveChunkTests` now verifies that a query created before chunk
  deactivation/reuse reads values from the reactivated chunk.

## Recommendation

Do not merge this representation as a general improvement yet. It reduces the
Movement4 driver by 5% and removes loads, but moves work and managed storage to
query-scope preparation and regresses small workloads. Retain the branch as
evidence; reconsider only if a repeated large-N comparison confirms the 1M
gain and the additional per-query/chunk arrays are acceptable.
