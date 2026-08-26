# Generated slot-loop unrolling experiment

## Decision

Rejected. Partial unroll×4 is useful for a standalone scalar array loop, but it
does not transfer to the generated callback path. The generated method became
larger and the larger dense workloads became slower, so the production
generated loop remains scalar and compact.

## Scope

- Baseline: current generated `World.ForEach` closed dense method.
- Candidate: generated slot loop with four callback bodies per loop iteration
  and a scalar remainder loop.
- Workload: `ComparativeMovement4ComponentsBenchmarks.DeltaECS_Movement4Components`.
- Data: 4 components, write/write/write/read access, 100/1,000/10,000/100,000
  entities, default chunk capacity 512.
- Runtime: .NET 10.0.9, Arm64 RyuJIT AdvSIMD, Apple M4 Pro, workstation GC.
- BDN job: 5 warmups, 20 measurement iterations, 1 launch, invocation count 1.

## Throughput

| Entities | Baseline | Unroll×4 | Change |
|---:|---:|---:|---:|
| 100 | 3.044 ± 0.704 μs | 2.877 ± 0.616 μs | −5.5% |
| 1,000 | 24.585 ± 3.681 μs | 16.544 ± 2.542 μs | −32.7% |
| 10,000 | 40.762 ± 5.253 μs | 75.226 ± 4.681 μs | **+84.5%** |
| 100,000 | 174.449 ± 14.094 μs | 213.266 ± 22.278 μs | **+22.2%** |

The short-run 100/1,000 results are not sufficient to justify the change: the
confidence intervals are wide and the direction reverses at larger workloads.
The 10k/100k regressions are the relevant signal for the dense path.

## JIT

| Method | Code size |
|---|---:|
| Baseline `ExecuteClosed_01A3663E` | 744 B |
| Unroll×4 `ExecuteClosed_01A3663E` | 1,016 B |

The candidate adds four copies of the component-reference setup and callback
site. It reduces loop-control frequency, but it also increases code footprint,
entity loads and register pressure. The synthetic benchmark did not contain
the generated delegate callback and therefore overstated the likely gain.

## Reproduction

```bash
cd /Users/rum/GitProjects/TheFurnace/DeltaECS/benchmarks/DeltaECS.Benchmarks
env NuGetAudit=false RestoreIgnoreFailedSources=true \
dotnet bin/Release/net10.0/DeltaECS.Benchmarks.dll \
  --filter '*ComparativeMovement4ComponentsBenchmarks.DeltaECS_Movement4Components*' \
  --warmupCount 5 --iterationCount 20 --launchCount 1 \
  --artifacts ../../artifacts/unroll-real-baseline
```

The candidate was captured with the same command after changing only the
generated loop. Raw BDN and JIT outputs remain under the ignored `artifacts/`
directory:

- `artifacts/unroll-real-baseline/results/`
- `artifacts/unroll-real-candidate/results/`
- `artifacts/unroll-real-baseline-jit.txt`
- `artifacts/unroll-real-candidate-jit.txt`
