# Metalama layered pipeline experiment

This isolated project tests compile-time replacement of a two-layer `ForEach`-style method.
The aspects resolve the named layer methods at compile time and emit the execution loop.

The first variant, `LayeredPipelineAttribute`, uses one batch of two stack-resident buffers:

```text
source array -> ping -> Layer1 -> pong -> Layer2 -> ping -> source array
```

The layer calls are generated direct calls through Metalama invokers; no reflection or runtime
delegate is used to select the layers. The probe intentionally uses a `TransformData160` value
to make cache traffic visible. `BatchSize` is 32, so both ping-pong buffers occupy 10 KiB in
total.

The second variant, `InPlaceLayeredPipelineAttribute`, avoids record copies entirely:

```text
source[index] -> ref state -> Layer1(ref state) -> Layer2(ref state)
```

It keeps only a managed `ref` local for the current element. No temporary array, `stackalloc`,
or whole-record value copy is used in this path. This is the relevant direction for an ECS
component row where the storage already owns the component memory.

## Instruction-cache stress

The earlier `ManyLayerPipeline` contained 32 separate layer methods. The current
`MathStressPipeline` extends this to 160 aggressive-inline stages: 144 bounded arithmetic
stages, 8 floating-point trigonometric stages, and 8 fixed-point stages using `Delta.Maths.fix`.
The generated method emits all 160 stage calls inside the element loop, while the hand-written
baseline contains the same calls in the same order. The resulting stage body is deliberately
larger than a typical L1 instruction cache, so this is an instruction-cache working-set stress
test; it does not claim to force literal hardware cache invalidation. The two driver methods
remain `NoInlining` so the driver boundary is visible in JIT output.

Run a correctness smoke check:

```bash
cd /Users/rum/GitProjects/TheFurnace/.worktrees/metalama-layered-ping-pong
env NuGetAudit=false RestoreIgnoreFailedSources=true \
  dotnet run --project tools/DeltaECS.LayeredPipeline/DeltaECS.LayeredPipeline.csproj \
  -c Release --no-build -- --smoke
```

Run the comparison:

```bash
env NuGetAudit=false RestoreIgnoreFailedSources=true \
  dotnet run --project tools/DeltaECS.LayeredPipeline/DeltaECS.LayeredPipeline.csproj \
  -c Release --no-build -- --filter '*LayeredPipelineBenchmarks*'
```

The heavy 32-layer comparison requests 1-second measurement targets, 10 warmups, 10 measured
iterations, and one launch. BenchmarkDotNet may stop early or add measurements while applying
its adaptive confidence rules:

```bash
env NuGetAudit=false RestoreIgnoreFailedSources=true \
  dotnet run --project tools/DeltaECS.LayeredPipeline/DeltaECS.LayeredPipeline.csproj \
  -c Release --no-build -- --filter '*MathInPlace*' \
  --iterationTime 1000 --warmupCount 10 --iterationCount 10 \
  --launchCount 1 --invocationCount 1
```

The generated source can be inspected after a diagnostic build at:

```text
tools/DeltaECS.LayeredPipeline/obj/Release/net10.0/metalama/LayeredPipelineAspect.cs
```

The measured result on Apple M4 Pro / .NET 10.0 was:

| Elements | Ordinary value | Ping-pong value | In-place ordinary | In-place layered |
| ---: | ---: | ---: | ---: | ---: |
| 32 | 221.46 ns | 778.41 ns | 18.98 ns | 19.12 ns |
| 128 | 891.02 ns | 2.616 us | 77.82 ns | 78.43 ns |
| 1,024 | 7.418 us | 20.193 us | 1.551 us | 1.553 us |
| 10,000 | 71.095 us | 195.117 us | 15.021 us | 14.919 us |

The relative results are:

| Elements | Ping-pong / value ordinary | In-place layered / in-place ordinary |
| ---: | ---: | ---: |
| 32 | 3.52x | 1.007x |
| 128 | 2.94x | 1.008x |
| 1,024 | 2.72x | 1.001x |
| 10,000 | 2.74x | 0.993x |

The result is negative for this two-layer test. The generated form adds three full-record
copies per batch and therefore is not a candidate for the ECS runtime as-is. It is only a
useful proof that Metalama can generate the layered execution shape; a viable ECS version needs
multiple expensive layers or a representation that avoids copying the whole component record.
The in-place result removes that copy penalty: it is statistically equivalent to the hand-written
in-place loop in this small probe, with a 0.7% difference at 32 elements, 0.8% at 128, 0.1% at
1,024, and 0.7% faster at 10,000. The benchmark changes only four `Vector4` fields of the
160-byte record, so it validates the dispatch and copy shape rather than full-record memory
bandwidth.

The earlier 32-layer in-place stress result was:

| Elements | Ordinary 32 layers | Generated 32 layers | Generated / ordinary |
| ---: | ---: | ---: | ---: |
| 1,000,000 | 29.00 ms ± 0.526 ms | 28.93 ms ± 0.566 ms | 0.98x |
| 10,000,000 | 287.93 ms ± 5.757 ms | 289.58 ms ± 4.520 ms | 1.00x |

Both variants allocated 0 B per operation. The difference is within the measured confidence
intervals, so this run shows no material throughput penalty from the generated 32-call chain.

The current 160-stage math/trigonometry/fixed-point stress result was:

| Elements | Ordinary 160 stages | Generated 160 stages | Generated / ordinary |
| ---: | ---: | ---: | ---: |
| 1,000,000 | 1.217 s ± 0.015 s | 1.214 s ± 0.006 s | 0.998x |
| 10,000,000 | 12.413 s ± 0.245 s | 12.169 s ± 0.243 s | 0.980x |

The 10M mean is 1.97% lower for the generated method, but the 99.9% intervals overlap
([12.168 s, 12.658 s] versus [11.927 s, 12.412 s]), so the run does not prove a stable
throughput improvement. No managed allocation or GC activity was observed. BenchmarkDotNet
could not raise process priority in the local environment (`Permission denied`).

The JIT inventory for the same build was:

| Scope | Code size | Instructions | `blr` | `ldr` |
| --- | ---: | ---: | ---: | ---: |
| Ordinary driver | 3,968 B | 991 | 162 | 163 |
| Generated driver | 4,004 B | 1,000 | 162 | 163 |
| Ordinary/generated hot loop | 3,848 B | 962 | 160 | 160 |
| All 160 stages | 103,740 B | 24,695 | — | — |

The generated driver is 36 B and 9 instructions larger, while the hot loop is identical:
160 indirect stage calls, each reached through one target load. The difference between the two
execution methods is therefore not a reduced callback count; this probe measures whether the
Metalama-generated call chain changes instruction-cache behavior under a deliberately large
working set.

The stage-size breakdown is 77,584 B / 18,244 instructions for the 144 arithmetic stages,
2,748 B / 663 instructions for the 8 floating-point trigonometric stages, and 23,408 B /
5,788 instructions for the 8 fixed-point stages.

## Chunked layer-major variant

`ChunkedLayeredPipelineAttribute` is the next experiment. It processes a 128-element tile and
runs each of the 160 stages across that tile before advancing to the next stage:

```text
stream -> [128 elements]
          Layer001(all 128) -> Layer002(all 128) -> ... -> Layer160(all 128)
          next tile
```

`ProcessOrdinaryMathChunked` is the hand-written equivalent. The transformation is valid for
this probe because every stage only reads and writes one element; it is not a general replacement
for a pipeline whose stages communicate across elements. The chunked methods are separate from
the flat methods, so the original comparison remains available.

Use the corrected BDN launcher and measure only the chunked pair:

```bash
env NuGetAudit=false RestoreIgnoreFailedSources=true \
  dotnet run --project tools/DeltaECS.LayeredPipeline/DeltaECS.LayeredPipeline.csproj \
  -c Release --no-build -- --filter '*Chunked*' \
  --iterationTime 250 --warmupCount 3 --iterationCount 5 \
  --launchCount 1 --invocationCount 1 --unrollFactor 1
```

The `BenchmarkSwitcher` entry point is intentional: `BenchmarkRunner.Run<T>()` did not forward
the custom command-line arguments in this executable, which made filters and timing settings
silently ineffective.

The chunked run on Apple M4 Pro / .NET 10.0 used 5 measured iterations, one invocation, one
launch, three warmups, and a 250 ms target:

| Elements | Chunked ordinary | Chunked layered | Layered / ordinary |
| ---: | ---: | ---: | ---: |
| 1,000,000 | 1.045 s ± 0.038 s | 1.049 s ± 0.029 s | 1.004x |
| 10,000,000 | 10.661 s ± 0.589 s | 10.750 s ± 0.290 s | 1.008x |

For a same-settings reference, the flat pair measured in a separate run at 1.178 s ± 0.019 s
and 1.222 s ± 0.279 s for 1M, and 12.023 s ± 0.253 s and 12.225 s ± 0.366 s for 10M. The
chunked means were lower by 11.3% / 14.2% at 1M and 11.3% / 12.1% at 10M, respectively. The
flat layered 1M result had an outlier removed, so these deltas are a strong experiment signal,
not a final claim until repeated on the same machine with longer runs.

The chunked JIT driver inventory was:

| Scope | Code size | Instructions | `blr` | `bhs` | `ldr` | `str` |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Chunked ordinary driver | 29,372 B | 7,343 | 295 | 160 | 483 | 36 |
| Chunked layered driver | 29,420 B | 7,355 | 296 | 160 | 484 | 36 |
| Flat ordinary driver | 14,416 B | 3,604 | 144 | 0 | 304 | 84 |
| Flat layered driver | 14,476 B | 3,619 | 147 | 0 | 309 | 86 |

Chunking therefore improves this stress probe while roughly doubling driver code size and adding
one loop/bounds path per stage. It is not yet a candidate for automatic use: the main question is
whether the gain survives repeated 1-second runs and whether a smaller tile gives a better code/
cache trade-off.
