# L7 type-erased dense Movement4 comparison

Baseline: `main` at `6fc0e9c`. Candidate: `perf/query-type-erased-l7` at the
L7 code commit recorded below plus the benchmark-only changes in the evidence
commit. The workload is the
existing `DenseIterationMicroBenchmarks.Movement4Components` fixture: four
`int` component values, reverse dense traversal, setup/reset outside the
measured method, and an observable checksum. BDN used its default strategy;
the runtime selected `InvocationCount=1` for this relatively long operation.

## Throughput

| Amount | Main Mean | Main Error | Main StdDev | L7 Mean | L7 Error | L7 StdDev | L7/Main | Allocated |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | 3.345 us | 0.1197 us | 0.3436 us | 5.411 us | 0.1734 us | 0.4948 us | 1.618x | 736 B / 736 B |
| 1,000 | 26.468 us | 1.0579 us | 3.0859 us | 43.159 us | 1.5656 us | 4.5171 us | 1.631x | 736 B / 736 B |
| 10,000 | 39.910 us | 1.5731 us | 4.5639 us | 61.473 us | 2.0331 us | 5.8984 us | 1.540x | 736 B / 736 B |
| 100,000 | 163.565 us | 5.0671 us | 14.7809 us | 227.890 us | 10.4182 us | 30.3905 us | 1.393x | 736 B / 736 B |

The L7 result is slower in this first implementation. The non-generic
`Ref<T>` boundary currently performs `Unsafe.As<T[]>` and span construction at
the final access boundary for every slot; the assembly evidence below is
consistent with that cost. This is a measurement result, not a claim that the
type-erased API is intrinsically slower after further tuning.

Raw BDN reports:

- [L7 CSV](../micro/l7-movement4/results/Delta.ECS.MicroBenchmarks.DenseIterationMicroBenchmarks-report.csv)
- [L7 Markdown](../micro/l7-movement4/results/Delta.ECS.MicroBenchmarks.DenseIterationMicroBenchmarks-report-github.md)
- [main CSV](../micro/main-movement4/results/Delta.ECS.MicroBenchmarks.DenseIterationMicroBenchmarks-report.csv)
- [main Markdown](../micro/main-movement4/results/Delta.ECS.MicroBenchmarks.DenseIterationMicroBenchmarks-report-github.md)

## First emitted Release JIT block

| Variant | Representation | Bytes/entity | Code size | `blr` | `bl` | Branches | `bhs` | `ldr` | `str` | `ldp/stp` | `sbfiz` |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Main `6fc0e9c` | generic typed requests + typed spans | 16 | 1084 B | 11 | 3 | 33 | 0 | 66 | 12 | 28 | 2 |
| L7 `3083370` | non-generic requests/access/values + `Ref<T>` boundary | 16 | 1016 B | 10 | 3 | 25 | 0 | 61 | 12 | 28 | 2 |

Code size is 68 B smaller (6.3%) and the first block has fewer indirect calls
and loads, but throughput is worse. The report does not infer cache misses
from code size or instruction counts. Branch counts include driver/chunk
transition code and are not equivalent to slot-loop branch counts.

The comparative/version benchmark sources were intentionally left at the
baseline legacy compatibility API. Only the dedicated microbenchmark fixture
uses the new type-erased chain.

Code commit: `bf93d88` (`Implement L7 type-erased access variant`).

Raw JIT and generated reports:

- [L7 raw JIT](l7-movement4-release.txt)
- [L7 JIT report](l7-movement4-release.md)
- [main raw JIT](main-movement4-release.txt)
- [main JIT report](main-movement4-release.md)

## Reproduction

```bash
cd /private/tmp/deltaecs-l7
env NuGetAudit=false RestoreIgnoreFailedSources=true \
  dotnet build benchmarks/DeltaECS.MicroBenchmarks/DeltaECS.MicroBenchmarks.csproj \
  -c Release --no-restore --disable-build-servers -m:1 \
  /p:UseSharedCompilation=false /p:NuGetAudit=false
env NuGetAudit=false RestoreIgnoreFailedSources=true \
  dotnet benchmarks/DeltaECS.MicroBenchmarks/bin/Release/net8.0/DeltaECS.MicroBenchmarks.dll \
  --filter '*DenseIterationMicroBenchmarks.Movement4Components*' \
  --artifacts artifacts/micro/l7-movement4
python3 -B benchmarks/jit-report.py \
  --method '*IterateMovement4Dense*' \
  --filter '*DenseIterationMicroBenchmarks.Movement4Components*' \
  --mode release --no-build \
  --output artifacts/jit-disasm/l7-movement4-release.txt \
  --report artifacts/jit-disasm/l7-movement4-release.md
```
