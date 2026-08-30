# Operation-specific stamp writers

## Hypothesis

The hierarchical stamp storage is world-owned, but one universal write state
should not be carried through every execution path. A point write needs only an
entity/component stamp writer. A complete row traversal needs a
chunk/component writer. A generated dense query spans the complete active rows
of each matching chunk and can use one archetype/component writer per component
before the entity loop. Read-only
execution should carry none of that write state.

This experiment changes only internal lowering and writer routes. It does not
change the public API or the meaning of `Stamp` equality.

## Implemented routes

| Runtime path | Route | Reason |
| --- | --- | --- |
| `World.Set` and integration `TryWrite` | `EntityComponentStampWriter.MarkPoint` | Updates the exact entity/component term without introducing a chunk write intent |
| Generated sequence write | `EntityComponentStampWriter.Mark` | Preserves selected-entity component stamp semantics |
| `QuerySlots.GetRow(WriteAccess)` / object write row | `ChunkComponentStampWriter.Mark` | The borrowed write row represents the complete component row in the current chunk |
| Generated dense write | `GeneratedQuerySlots.MarkGeneratedWrite` | Marks the archetype/component term once before the slot loop |
| Archetype/world internal endpoints | `ArchetypeComponentStampWriter` / `WorldComponentStampWriter` | Keep broad mutation routes available without putting their storage on `Chunk` or `Archetype` |
| Generated read-only and zero-arity execution | `GeneratedReadDenseExecution` + `GeneratedReadQuerySlots` | Carries no tick, stamp, native stamp buffer, or writer state |

## Correctness gates

- `DeltaECS.Generators.Tests`: **21/21**.
- `DeltaECSTests`: **136/136**.
- Generated write coverage includes a multi-chunk archetype and verifies the
  resulting component stamps.
- Generator coverage verifies that a read-only callback selects
  `OpenReadDense` and emits no `MarkGeneratedWrite` call.
- Release benchmark project build: 0 errors.
- Allocation result in the matched benchmark: **0 B** for both variants.

## Matched large Movement4 benchmark

Baseline and candidate were run separately on the same Apple M4 Pro machine:
.NET `10.0.9`, Arm64 RyuJIT AdvSIMD, tiered compilation disabled,
ReadyToRun disabled, one launch, 20 warmups, 20 measured iterations,
`IterationTime=1000 ms`. BenchmarkDotNet could not acquire high priority
without sudo; both runs completed normally. The benchmark is the existing
`ComparativeMovement4ComponentsBenchmarks.DeltaECS_Movement4Components`
scenario with 3 writes and 1 read over one dense archetype.

| Entities | Baseline mean | Baseline error | Baseline stddev | Candidate mean | Candidate error | Candidate stddev | Candidate / baseline | Delta |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 136.9 ns | ±0.71 ns | 0.76 ns | 135.2 ns | ±0.69 ns | 0.74 ns | 0.988x | −1.24% |
| 1,000 | 1,108.2 ns | ±4.17 ns | 4.64 ns | 1,108.5 ns | ±5.81 ns | 6.46 ns | 1.000x | +0.03% |
| 10,000 | 10,993.1 ns | ±94.11 ns | 104.60 ns | 11,037.0 ns | ±69.51 ns | 77.26 ns | 1.004x | +0.40% |
| 100,000 | 112,263.3 ns | ±1,312.71 ns | 1,511.72 ns | 111,903.0 ns | ±515.55 ns | 573.03 ns | 0.997x | −0.32% |

This is not a proven end-to-end throughput win: the large-size differences
are inside measurement error, while the 100-entity point is setup-sensitive.
The useful result is architectural: read paths no longer carry write stamp
state, and entity/row/dense operations no longer share an unnecessarily broad
writer representation. The specialized writers are therefore retained as a
state/layout specialization, not as a stable multi-percent speedup claim.

Raw reports:

- [baseline BDN report](../../../artifacts/stamp-writers-large-before/results/Delta.ECS.Benchmarks.ComparativeMovement4ComponentsBenchmarks-report.csv)
- [candidate BDN report](../../../artifacts/stamp-writers-large-after/results/Delta.ECS.Benchmarks.ComparativeMovement4ComponentsBenchmarks-report.csv)

## Matched read-only dense benchmark

The same protocol was run for the read-only dense path. This isolates the
`GeneratedReadDenseExecution` and `GeneratedReadQuerySlots` state split: the
candidate does not reserve a write tick and does not carry stamp storage or a
writer through the generated read loop. Both variants allocated **0 B**.

| Entities | Baseline mean | Baseline error | Candidate mean | Candidate error | Candidate / baseline | Delta |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 34.42 ns | ±0.126 ns | 36.04 ns | ±1.333 ns | 1.047x | +4.707% |
| 1,000 | 302.04 ns | ±1.108 ns | 304.30 ns | ±1.686 ns | 1.007x | +0.748% |
| 10,000 | 2,871.58 ns | ±24.794 ns | 2,869.55 ns | ±19.962 ns | 0.999x | −0.071% |
| 100,000 | 29,386.84 ns | ±159.491 ns | 29,330.85 ns | ±67.477 ns | 0.998x | −0.191% |

The 100-entity candidate result is affected by a wider/multimodal sample
distribution; the 1k–100k intervals overlap. The read-state reduction is
therefore a valid layout and safety improvement, but this run does not prove a
stable read-throughput gain.

Raw reports:

- [baseline dense-read BDN report](../../../artifacts/stamp-writers-dense-before/results/Delta.ECS.Benchmarks.ComparativeDenseIterationBenchmarks-report.csv)
- [candidate dense-read BDN report](../../../artifacts/stamp-writers-dense-after/results/Delta.ECS.Benchmarks.ComparativeDenseIterationBenchmarks-report.csv)

## Reproduction

```bash
env NuGetAudit=false RestoreIgnoreFailedSources=true \
  DOTNET_TieredCompilation=0 DOTNET_ReadyToRun=0 \
  dotnet benchmarks/DeltaECS.Benchmarks/bin/Release/net10.0/DeltaECS.Benchmarks.dll iteration \
  --filter '*ComparativeMovement4ComponentsBenchmarks.DeltaECS_Movement4Components*' \
  --warmupCount 20 --minIterationCount 20 --maxIterationCount 21 \
  --iterationTime 1000 --launchCount 1 \
  --exporters csv markdown json \
  --artifacts artifacts/stamp-writers-large-after
```
