# `TryGetRegisteredCore` lookup experiment — 2026-10-14

## Question

Can typed component reads avoid the tag-layout lookup and resolve the component
column directly from the chunk's owning archetype without slowing tag reads?
The public API and entity validation remain unchanged in every candidate.

## Method

Compared the baseline at `3b3dc15` with three internal candidates. BenchmarkDotNet
0.15.8 on Debian 13, .NET 10.0.13 / SDK 10.0.402, x64 RyuJIT, Xeon Platinum
8573C (one CPU assigned). Each paired run used 100K entities, 10 warmups,
30 × 200 ms iterations and 3 launches. Component reads use sequential and
shuffled entity order. The tag guardrail reads a tag present on half the
entities, also sequentially and shuffled. Setup checksums matched, and all
cases allocated 0 B per operation.

## Candidates and results

### Direct owner-archetype reference

Added an internal `Chunk.Archetype` accessor and replaced the indexed
`World.Archetypes[chunk.ArchetypeId]` lookup, preserving the original tag-first
branch order. The observed direction favored the candidate, but the distributions
were too wide to establish a result: `RatioSD` ranged from `0.24` to `0.45`,
and the confidence intervals overlapped for all four `Get`/`TryGet` workloads.
This candidate alone is inconclusive.

### Data-first lookup with tag fallback

Tried the chunk's archetype component-index lookup before checking the tag
layout. This produced a clear data-read improvement in its paired run:

| Workload | Baseline | Candidate | Change |
| --- | ---: | ---: | ---: |
| `Get<T>` sequential | 809.1 ± 13.37 μs | 668.0 ± 12.01 μs | −17.4% |
| `Get<T>` shuffled | 1,545.3 ± 14.21 μs | 1,378.9 ± 18.74 μs | −10.8% |
| `TryGet<T>` sequential | 823.6 ± 19.79 μs | 690.5 ± 16.12 μs | −16.2% |
| `TryGet<T>` shuffled | 1,578.6 ± 12.15 μs | 1,376.7 ± 14.52 μs | −12.8% |

However, the tag guardrail was inconclusive and suggested a possible regression:
tag `TryGet<T>` medians changed from `636.0` to `666.7 μs` sequential (+4.8%)
and `1,555.7` to `1,664.0 μs` shuffled (+7.0%). The paired distributions
were noisy (`RatioSD 0.32` and `0.28`) with overlapping confidence intervals.
Because the tag workload does not establish absence of a regression, this
candidate was not retained.

### Cached generic tag classification

Added a per-closed-generic-type cached tag flag to retain the tag-first route for
tag types and use the data-first route for data types. Tag reads were neutral in
the confirmation run: sequential `641.5 ± 10.12` versus `623.6 ± 6.72 μs`,
shuffled `1,511.9 ± 9.92` versus `1,537.9 ± 32.39 μs`. The data-access results
were too noisy to confirm a gain: `RatioSD` ranged from `0.19` to `0.79`, and
all baseline/candidate confidence intervals overlapped. For example,
sequential `Get<T>` medians were `798.4` versus `880.7 μs`, while shuffled
`Get<T>` medians were `2,497.7` versus `1,923.3 μs`. The spread is too large to
accept this as a stable result.

## Decision

No runtime change is retained on the benchmark/evidence branch. The first
data-first candidate had a strong data-read signal, but its tag guardrail did
not rule out a slowdown. The type-classified variant avoided that concern but
the longer confirmation run was too noisy to prove the required benefit. The
direct owner reference alone was also inconclusive. The benchmark scenarios
remain in the harness so a future run on a quieter machine can revisit the
question without changing the workload. To keep the tested implementations
available without treating them as accepted runtime changes, each is preserved
in its own branch:

- `perf/tryget-core-owner-ref` (`5766f0d`): direct chunk-to-archetype reference.
- `perf/tryget-core-data-first` (`2183a72`): data column lookup followed by a
  tag-layout fallback.
- `perf/tryget-core-typed-route` (`aafbe93`): cached generic tag classification
  selects the tag or data lookup route.

All raw reports are under the ignored `/workspace/scratch/try-get-registered-core-*`
directories. The final confirmation reports are:

- `try-get-registered-core-typed-route-v1/results/DeltaECS.ProfileBenchmarks.ComponentAccessBenchmarks-report-github.md`
- `try-get-registered-core-typed-route-v1/results/DeltaECS.ProfileBenchmarks.HasDispatchBenchmarks-report-github.md`

The other run directories are `try-get-registered-core-owner-ref-v1`,
`try-get-registered-core-data-first-v2` and
`try-get-registered-core-tag-guard-v1`.
