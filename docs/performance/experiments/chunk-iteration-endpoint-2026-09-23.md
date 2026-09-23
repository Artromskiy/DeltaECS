# Chunk iteration end-pointer experiment — 2026-09-23

The candidate changes only chunk metadata, not the inner scalar loop:

- Baseline descriptor: `DataPtr`, `Length`, `Next`; each chunk computes
  `EndPtr = DataPtr + Length` before iterating.
- Candidate descriptor: `DataPtr`, precomputed `EndPtr`, `Next`; descriptors
  are stored in one contiguous unmanaged block and linked in order.
- Both traverse exactly the active entity bytes and return the same checksum.

The benchmark uses a 1,024-byte chunk capacity and two workloads: one partial
128-element chunk and 4,194,304 elements across 4,096 full chunks. It is a
synthetic chunk-loop probe, not a timing claim for `World.ForEach`. All loops
are scalar; there is no SIMD, manual unrolling, or multithreading.

## Results

BenchmarkDotNet, .NET 10.0.12 ARM64 RyuJIT on Apple M4 Pro, 5 warmups,
30 × 100 ms, one launch:

| Active elements | Length baseline | End-pointer candidate | Candidate / baseline |
| ---: | ---: | ---: | ---: |
| 128 | 38.43 ±0.303 ns | 38.81 ±0.237 ns | 1.010 |
| 4,194,304 | 1.169 ±0.0139 ms | 1.173 ±0.0143 ms | 1.003 |

The intervals overlap in both cases, so the independent-process BDN result is
inconclusive for throughput. Three same-process ABBA repeats gave mean
candidate/baseline ratios of `0.992` (128 values) and `0.994` (4,194,304
values); those differences are also too small to call a speedup. There is no
statistically supported slowdown.

The original inspected ARM64 JIT body was 72 bytes for the candidate and 80
bytes for the baseline. A subsequent JIT-only experiment moved the `Next`
descriptor load before the row loop. That uses part of the JIT's loop-alignment
padding and reduces the candidate body to 68 bytes, while the baseline remains
80 bytes. The new body retains the same scalar row loop and passed the setup
checksum check in a BenchmarkDotNet Dry run. Throughput was not remeasured after
this final instruction-order change; the table above is evidence for the prior
72-byte form only. The 68-byte form is a confirmed code-size reduction, but its
throughput still needs a paired check before claiming it is speed-neutral.

A follow-up JIT-only probe changed the inner condition from pointer-range
comparison (`currentPointer < endPointer`) to endpoint equality. On the same
.NET 10.0.12 ARM64 RyuJIT build it still emitted 68 bytes: `cmp` remained, and
the branch changed from `blo` to `bne`. The source was restored to the range
comparison because the rewrite saved no code and assumes exact pointer arrival.
The Dry run passed the setup checksum; no throughput comparison was made. Raw
output is `artifacts/chunk-iteration-user-20260923/equality-jit/jit.log`.

A second JIT-only probe reordered the local initialization to encourage keeping
the checksum in the return register and removing the final `mov w0, w1`. RyuJIT
still assigned the accumulator to `w1`, emitted the same return move, and kept
the method at 68 bytes. The original source order is retained. Raw output is
`artifacts/chunk-iteration-user-20260923/return-register-jit/jit.log`.

Reproduce from `DeltaECS`:

```bash
COMPlus_TieredCompilation=0 COMPlus_ReadyToRun=0 \
  dotnet benchmarks/DeltaECS.ArrayRefBenchmarks/bin/Release/net10.0/DeltaECS.ArrayRefBenchmarks.dll \
  --amount 128 --chunk-capacity 1024 \
  --filter '*ChunkIterationBenchmarks*' \
  --warmupCount 5 --iterationCount 30 --iterationTime 100 --launchCount 1

COMPlus_TieredCompilation=0 COMPlus_ReadyToRun=0 \
  dotnet benchmarks/DeltaECS.ArrayRefBenchmarks/bin/Release/net10.0/DeltaECS.ArrayRefBenchmarks.dll \
  --amount 4194304 --chunk-capacity 1024 \
  --filter '*ChunkIterationBenchmarks*' \
  --warmupCount 5 --iterationCount 30 --iterationTime 100 --launchCount 1
```

Raw reports and local JIT output are in ignored `artifacts/` under
`chunk-iteration-user-20260923`.
