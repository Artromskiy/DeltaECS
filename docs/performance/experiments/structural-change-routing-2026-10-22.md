# Structural transitions and observer bookkeeping — 2026-10-22

Baseline: `8e7a5c1`. Candidate: working-tree changes in `World`, `Chunk`, `Archetype`, `ComponentChangeOperation`, `ComponentRowArrayPool`, and `QueryAccess`. The exact same fixture is compiled against the two runtimes through the existing aliased profile adapters.

## Workload and measurement

- Apple M4 Pro, macOS Arm64, .NET 10.0.12. `DOTNET_TieredCompilation=0`, `DOTNET_ReadyToRun=0`.
- 20, 1,024, and 131,072 entities, twelve separately registered 24-byte components sharing one CLR type, one tag, and 64 retained queries.
- Each round trip removes four registrations separately, then adds them back; sparse data/tag writes are marked before the transitions and subscribers are invoked afterward. Full coverage selects all entities; partial coverage selects every second entity.
- Either zero subscribers or 64 subscribers (54 data and 10 tag subscriptions). Setup, initial snapshots and world construction are outside measurement.
- Seven alternating baseline/candidate windows per scenario, each at least 250ms, after warm-up. Times below are means with sample standard deviation, not BDN confidence intervals. This is a bounded paired probe, not a full BenchmarkDotNet run.
- Unity remained active during measurement; small differences and the noisier samples must not be treated as established improvements/regressions.
- Cold bytes measure the first structural round trip after setup. Warm bytes are allocated bytes per measured round trip, not total retained heap size. Public return counts and preserved entity values/composition are checked.

## Execution time

| Entities | Selection | Subscribers | Baseline mean ± SD | Candidate mean ± SD | Change |
|---:|---|---:|---:|---:|---:|
| 20 | All | 0 | 39.7us ± 0.482us | 25.7us ± 0.707us | -35.13% |
| 20 | All | 64 | 196us ± 1.92us | 33.2us ± 0.186us | -83.03% |
| 20 | Half | 0 | 29.4us ± 0.31us | 29.4us ± 0.545us | -0.12% |
| 20 | Half | 64 | 113us ± 1.19us | 49.1us ± 0.239us | -56.38% |
| 1024 | All | 0 | 849us ± 12.6us | 44.1us ± 4.56us | -94.81% |
| 1024 | All | 64 | 11.1ms ± 951us | 257us ± 12.7us | -97.68% |
| 1024 | Half | 0 | 466us ± 53us | 190us ± 105us | -59.09% |
| 1024 | Half | 64 | 5.52ms ± 138us | 1.01ms ± 32us | -81.72% |
| 131072 | All | 0 | 127ms ± 9.01ms | 131ms ± 22ms | 3.02% |
| 131072 | All | 64 | 1.46s ± 11.1ms | 917ms ± 10.8ms | -37.07% |
| 131072 | Half | 0 | 62.1ms ± 643us | 60.2ms ± 505us | -3.07% |
| 131072 | Half | 64 | 749ms ± 4.08ms | 467ms ± 2.69ms | -37.57% |

The 131K/all/zero-subscriber difference (+3.02%) overlaps broad variation and is inconclusive. The 1K/half/zero-subscriber point is also noisy. The substantial observer-heavy improvements are the primary CPU evidence; these measurements do not predict exact Unity frame times.

## Allocations

| Entities | Selection | Subscribers | Cold baseline / candidate, B | Warm baseline / candidate, B |
|---:|---|---:|---:|---:|
| 20 | All | 0 | 1048904/136144 | 0/0 |
| 20 | All | 64 | 1084168/146488 | 0/0 |
| 20 | Half | 0 | 1048904/407144 | 0/0 |
| 20 | Half | 64 | 1078456/420272 | 0/0 |
| 1024 | All | 0 | 1048944/135984 | 0/0 |
| 1024 | All | 64 | 1528792/368144 | 0/0 |
| 1024 | Half | 0 | 1048944/407184 | 0/0 |
| 1024 | Half | 64 | 1371304/600072 | 0/0 |
| 131072 | All | 0 | 124435120/124178104 | 3641344/0 |
| 131072 | All | 64 | 184611504/184365208 | 3641344/0 |
| 131072 | Half | 0 | 62476160/62587256 | 1580544/0 |
| 131072 | Half | 64 | 101652520/101767672 | 1580544/0 |

The single-chunk path lowers first-use row allocation; the inactive-binding cache removes the repeated 1.58–3.64MB metadata allocations in the large round trips. The new cache has a memory tradeoff: each query/archetype retains bindings indexed by its historical chunk-slot capacity rather than retaining just one inactive binding. Reuse validates chunk identity and row-array identity, so recycled IDs and changed layouts cannot silently reuse a stale binding. This probe measures allocation traffic; it is not a retained-memory snapshot.

## Implementation and rejected intermediate behavior

1. Structural membership notifications use existing transition row mappings and per-ComponentId subscriber routes. They no longer scan unrelated data/tag subscribers and compare source/target masks for every entity. Routes are read from the current world table, including subscriptions registered after an edge was cached.
2. A single observer traversal transfers the incoming slot dirty bit and repairs source swap-back bits. It preserves unchanged components and tags that were already dirty, and prevents incoming clean entities from inheriting the target's All marker. Generic callbacks remain unchanged.
3. Batch plan refresh now synchronizes flattened chunk arrays once per dirty query plan. Immediate updates retain their previous path.
4. Complete entity-list coverage of the sole active source chunk can reuse the existing block-adoption path when the destination has no active chunks and the mutation changes data rows. Arbitrary order is supported; duplicates/stale handles fall back to normal processing. Batches larger than one chunk or without complete single-chunk coverage retain the existing entity path.
5. Row-reference arrays are pooled separately by exact length; unchanged data arrays are transferred during adoption. Chunk ownership metadata and mixed data/tag filter invalidation are updated on adoption.
6. Cached row bindings survive multi-chunk deactivation/reactivation and are validated before reuse.

An intermediate version also adopted a complete source into a populated target. The probe exposed 14,656 B of recurring metadata allocation and a small no-subscriber regression in the 20/half workload. That case was returned to the existing per-entity path. This differs from the previously rejected shared observer-write experiments: the accepted mechanism targets structural membership routing, fused slot relocation, and existing chunk adoption, not ordinary ForEach write dispatch.

## Correctness and scope

Added coverage for dirty All/Mask transfers in both data and tag observers, late observer registration on cached edges, distinct registrations of the same CLR type, duplicate entity handles, row preservation during full-chunk movement, mixed data/tag filters after adoption, and multiple inactive chunk bindings. Existing randomized structural/change-tracking tests are retained. Validation: scoped Furnace dev mode passed (288 runtime tests, 110 generator tests and 23 Systems tests, plus consumer builds and both runtime targets); shared metrics completed with zero errors and existing warnings. Formatter apply completed; check still reports the nine pre-existing source-order findings outside this change. `git diff --check` passed. Logs are under `artifacts/structural-20261022/`.

No public API, source-generator shape or SkyPirates implementation is changed. No JIT code-size or hardware instruction counts were collected; the instruction-level evidence is limited to the removed algorithmic work (global membership scans, three-to-one observer relocation traversals, repeated flat-plan synchronization). A normal Unity capture is still needed for application-level before/after evidence.

## Reproduction

```bash
mkdir -p artifacts/structural-20261022/baseline
git archive 8e7a5c1 | tar -x -C artifacts/structural-20261022/baseline
dotnet build benchmarks/DeltaECS.ProfileBenchmarks/DeltaECS.ProfileBenchmarks.csproj -c Release \
  -p:BaselineRoot="$PWD/artifacts/structural-20261022/baseline"
DOTNET_TieredCompilation=0 DOTNET_ReadyToRun=0 dotnet \
  benchmarks/DeltaECS.ProfileBenchmarks/bin/Release/net10.0/DeltaECS.ProfileBenchmarks.dll \
  structural-probe --amount 131072
```
