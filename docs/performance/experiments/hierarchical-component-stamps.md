# Historical four-term component stamp experiment

> Superseded by the current three-term contract in
> [`src/DeltaECS/Stamps/README.md`](../../src/DeltaECS/Stamps/README.md).
> The measurements below are retained as historical evidence for the removed
> world/component term and must not be read as the current formula.

## Scope

This experiment adds four mutation terms without changing the public API:

```text
effective(Entity, Component) =
    entity/component
  + chunk/component
  + archetype/component
  + world/component
```

The sum is an unchecked `ulong` equality token. It is not an ordered clock.
The existing per-slot `ComponentStampStorage` remains the entity/component
term. The chunk, archetype and world terms in this historical candidate were
stored in buffers owned by `World`, indexed by archetype id, chunk id and
component ordinal/id. `Chunk` and `Archetype` therefore did not acquire
additional hierarchy fields or references.

At the time of this experiment the mutation mapping was deliberately
conservative:

- point `Set` and sequence writes update the entity/component term;
- a validated dense query write updates the chunk/component term once its
  trusted row endpoint is entered;
- internal trusted endpoints could update the archetype/component or
  world/component terms for future broad operations;
- clearing a physical chunk clears its chunk-level terms before reuse, while
  archetype- and world-level terms retain their own scope.

The public `World.TryGetComponentStamp` and `StampRow` return the combined
value. No public type or call shape changed.

## Correctness evidence

The focused test suite passed **136/136**. It covers the four-term sum,
chunk-term persistence across point writes, term clearing on physical chunk
reuse, entity movement, generated whole-chunk writes, stale handles and the
existing read/write stamp invariants. The generator test suite passed
**20/20**.

Reference-type component mutation through a returned reference remains the
component owner's responsibility; it does not reserve a stamp unless it goes
through an ECS write endpoint.

## Paired Movement4 comparison

Baseline and candidate were both derived from commit `6c56cb2`. The baseline
was run in a clean detached worktree before the World-owned hierarchy was
applied; the candidate was the temporary working tree derived from the same
commit. Both runs used .NET `10.0.9`, Arm64 RyuJIT AdvSIMD, Apple M4
Pro, concurrent workstation GC, tiering and ReadyToRun disabled, one launch,
10 warmups, 10--20 measured iterations and `IterationTime=200 ms`. High
priority was unavailable without sudo (`Permission denied`), and both runs
completed normally with `0 B` allocation.

| Entities | Baseline mean | Baseline error | Baseline stddev | Candidate mean | Candidate error | Candidate stddev | Candidate / baseline | Delta |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 133.4 ns | ±1.26 ns | 0.83 ns | 136.2 ns | ±0.44 ns | 0.29 ns | 1.021x | +2.1% |
| 100,000 | 108.3 μs | ±1.78 μs | 1.58 μs | 109.6 μs | ±0.42 μs | 0.22 μs | 1.012x | +1.2% |

The candidate's variance was lower, but its mean was higher at both measured
sizes. This is a storage-layout/API-semantics experiment, not a throughput
win. In the measured write-heavy path, the extra World-owned chunk-term
write adds overhead; no performance claim is made for the currently unused
archetype/world trusted endpoints.

Raw reports:

- [baseline, 100 entities](../../../artifacts/hierarchical-stamps-before-100/results/Delta.ECS.Benchmarks.ComparativeMovement4ComponentsBenchmarks-report.csv)
- [candidate, 100 entities](../../../artifacts/hierarchical-stamps-after-100/results/Delta.ECS.Benchmarks.ComparativeMovement4ComponentsBenchmarks-report.csv)
- [baseline, 100,000 entities](../../../artifacts/hierarchical-stamps-before-100000/results/Delta.ECS.Benchmarks.ComparativeMovement4ComponentsBenchmarks-report.csv)
- [candidate, 100,000 entities](../../../artifacts/hierarchical-stamps-after-100000/results/Delta.ECS.Benchmarks.ComparativeMovement4ComponentsBenchmarks-report.csv)

The attempted `DOTNET_JitDisasm` probe did not emit a matching listing from
the BenchmarkDotNet child process, so this experiment does not claim a JIT
code-size delta. A future JIT comparison must capture the actual generated
closed method from an isolated probe rather than infer it from the BDN report.

## Reproduction

From the repository root, build first and run each amount separately:

```bash
env NuGetAudit=false RestoreIgnoreFailedSources=true \
  DOTNET_TieredCompilation=0 DOTNET_ReadyToRun=0 \
  dotnet benchmarks/DeltaECS.Benchmarks/bin/Release/net10.0/DeltaECS.Benchmarks.dll iteration \
  --filter '*DeltaECS_Movement4Components(Amount: 100)' \
  --warmupCount 10 --minIterationCount 10 --maxIterationCount 20 \
  --iterationTime 200 --launchCount 1 \
  --exporters csv markdown json \
  --artifacts artifacts/hierarchical-stamps-after-100
```

Replace `100` with `100000` and update the artifact directory for the second
run. The baseline reports were produced by the same command from the clean
`6c56cb2` worktree.
