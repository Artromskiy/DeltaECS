# Component-access follow-up from the published-package profile — 2026-10-14

The supplied profile was captured from the latest stable NuGet package,
DeltaECS `0.0.58` (published at 2026-10-14 10:06 UTC). The existing
`World.Has` live-location optimization was committed later that day, so this
profile predates that change. The paired experiments below use `22bc506` as
the baseline; it already contains the accepted `Has` change.

## Profile priority

The largest DeltaECS inclusive totals are `World.TryGetRegisteredCore`
(`1,132.493 ms`, 1,677,200 calls, `121.049 ms` self), `World.Get`
(`841.915 ms`, 838,600 calls, `40.489 ms` self), `World.Has`
(`621.414 ms`, 609,572 calls, `48.589 ms` self), `World.TryResolve`
(`237.560 ms`, 412,832 calls, `58.883 ms` self), and
`Archetype.TryGetComponentIndex` (`199.770 ms`, 855,916 calls,
`68.780 ms` self). In the Unity profile, `EntityContext.Get` is
`926.926 ms` inclusive and `EntityContext.Has` is `369.458 ms`.

These are deeply instrumented inclusive times; nested rows overlap, and the
Unity wrappers call into DeltaECS. They identify hot paths and priorities but
are not uninstrumented throughput claims. `World.Has` already uses the
validated live-location path documented in
[the accepted Has report](has-fast-live-resolution-2026-10-14.md).

## Paired follow-up experiments

All runs used BenchmarkDotNet 0.15.8, .NET 10.0.13 x64 RyuJIT on Debian 13,
Intel Xeon Platinum 8573C with one CPU assigned. The shared profile fixture
uses 100,000 entities, 256 registered components and 8 archetypes. Baseline
and candidate checksums must match during setup. BDN `Error` is its 99.9%
confidence margin; `StdDev` describes spread across measured iterations.

| Candidate | Workload and protocol | Baseline → candidate | Decision |
| --- | --- | --- | --- |
| Bypass `TryGetRegisteredCore` in primary `Get<T>(Entity)` and directly resolve/read the row | Sequential and shuffled primary `Get`; 10 warmups, 30 × 200 ms, 3 launches | Sequential `778.2 ± 4.24 μs` → `779.8 ± 6.22 μs` (`+0.2%`); shuffled `1,552.0 ± 28.30 μs` → `1,498.7 ± 26.23 μs` (`−3.4%`). Both comparisons have overlapping confidence intervals; allocation is `0 B`. | Rejected: no stable gain, and neither result reaches the 5–10% threshold. |
| Skip the second `EnsureExecutionAccess` reached by generic `Has<T>` | Sequential and shuffled all-present primary `Has`; 10 warmups, 20 iterations, 3 launches, 256 invocations per iteration | Sequential `800.2 ± 64.09 μs` → `802.7 ± 70.73 μs` (`+0.3%`); shuffled `1,286.1 ± 137.55 μs` → `1,159.2 ± 133.30 μs` (`−9.9%`). Confidence intervals overlap; ratio standard deviation is `0.22` sequential and `0.30` shuffled. Allocation is `0 B`. | Inconclusive/no stable improvement. The shuffled mean reaches the target, but spread and overlapping intervals do not support retaining the change. |
| Remove `RawEntities[slot] == entity` at the end of `TryResolve`, after index, generation, chunk and slot checks | Random destruction of 1,000,000 handles; 8 warmups, 20 iterations, 3 launches, one batch per invocation | Full list `197.1 ± 10.97 ms` → `185.2 ± 5.39 ms` (`−6.0%`); random half `104.7 ± 3.37 ms` → `102.4 ± 1.33 ms` (`−2.2%`). Both confidence intervals overlap; ratio SD is `0.11` and `0.07`. Allocation remains `64 B`. | Rejected: neither workload demonstrates a stable no-regression gain. The full-list mean is promising but uncertain, and the half-list result is below threshold. |

The raw reports are under `/workspace/scratch/hotpath-get-primary-v1c`,
`/workspace/scratch/hotpath-has-core-v2` and
`/workspace/scratch/hotpath-tryresolve-destroy-v1`. The generic primary `Get`
workload is now included in `ComponentAccessBenchmarks` so later candidates
will cover the public overload used by the profile instead of only
explicit-component-ID access.
