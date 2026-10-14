# `Has` live-location fast path — 2026-10-14

Baseline: `06e7a6b`. Candidate: `1b0360b`. The public API is unchanged.
`World.Has` now uses the existing `TryResolveAliveLocation` helper. That helper
checks the entity index, generation, non-null chunk and slot bounds; it skips
the additional read back from `Chunk.RawEntities` to compare the whole entity.
The component check still handles registered data components, tags and invalid
component IDs as before.

Correctness passed the runtime suite (`258/258`) and the profile benchmark
smoke. The smoke fixture uses 21 entities so both present and absent cases are
checked with a non-divisible group size. Benchmark setup compares baseline and
candidate checksums for every case.

BenchmarkDotNet 0.15.8 ran on Linux x64, .NET 10.0.13 RyuJIT, Intel Xeon
Platinum 8573C (one CPU assigned), with 10 warmups, 20 measured iterations of
200 ms and 3 launches. Each operation checks 100K entities. The all-present
case uses the existing profile fixture and generic `Has<T>`. The mixed fixture
checks explicit-ID data, explicit-ID tags and generic data, each present on
half the entities; sequential order interleaves four archetype groups and the
shuffled order uses a fixed seed. Values below are mean ± standard deviation;
BDN's `Error` is the 99.9% confidence margin. All cases allocate 0 B.

| Workload | Baseline | Candidate | Change |
| --- | ---: | ---: | ---: |
| All present, generic, sequential | 833.1 ± 22.94 μs | 686.6 ± 28.65 μs | −17.6% |
| All present, generic, shuffled | 2,000.8 ± 51.14 μs | 1,243.5 ± 305.29 μs | −37.9% |
| Mixed data, explicit ID, sequential | 619.8 ± 169.06 μs | 480.5 ± 89.62 μs | −22.5% |
| Mixed data, explicit ID, shuffled | 2,033.0 ± 389.08 μs | 2,081.9 ± 630.49 μs | +2.4% (within error) |
| Mixed data, generic, sequential | 1,137.5 ± 354.43 μs | 900.1 ± 222.71 μs | −20.9% |
| Mixed data, generic, shuffled | 3,314.1 ± 933.36 μs | 2,006.2 ± 467.25 μs | −39.5% |
| Mixed tag, explicit ID, sequential | 482.2 ± 98.65 μs | 376.4 ± 80.75 μs | −21.9% |
| Mixed tag, explicit ID, shuffled | 1,710.3 ± 206.05 μs | 1,383.6 ± 265.22 μs | −19.1% |

The explicit-ID data shuffled case is neutral within measurement noise; the
other cases improve directionally, with non-overlapping confidence intervals
for the all-present pair and both tag cases. The all-present sequential check
directly covers the regression seen in the earlier, more verbose candidate.

FullOpts JIT for `World.Has(Entity, ComponentId)` changed from `487 B / 148
instructions / 7 calls / 26 branches` to `409 B / 121 instructions / 6 calls /
20 branches`. The probe ran with tiered compilation disabled. Raw BDN reports
are under `/workspace/scratch/has-all-present-long-a` and
`/workspace/scratch/has-dispatch-long-a2`; JIT output is
`/workspace/scratch/has-world-jit.txt`.
