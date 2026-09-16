# Typed dense signature binding — 2026-09-16

## Scope and retained implementation

Experimental branch `perf/typed-binding`, base `6dfaf2b`. Both the baseline and
candidate use 512-slot chunks and source project references. The separate
`perf/scalar-iteration` experiment is not used as the baseline. No entity-loop
unrolling or SIMD was introduced. The implementation is retained regardless of
benchmark outcome, as requested; it is not merged or released.

Sequential query-wide `ForEach` and `ForEachEntity` with primary component
selectors use a query-owned binding for the complete component signature.
Delegates, intercepted static callbacks and explicit functors share the new
path, including their supported context modes. Explicit ComponentId selectors,
entity-list targets, parallel and stamp iteration retain the existing path.
Those forms remain supported, but are not optimized by this experiment.

The generator emits a typed descriptor and binding inside the existing generated
extension class. The descriptor contains a chunk handle and typed component
arrays. The binding resolves primary routes once, caches unique write routes,
and projects matching chunks into a contiguous descriptor array.

The query keeps a last-binding slot plus a dictionary for alternation between
signature types. Bindings are world/query-local; no static cache retains worlds.
On a topology-version change, the descriptor array is rebuilt from current
query plans. Counts are always read from the live chunk, so appending or removing
entities without changing topology cannot leave a cached count stale. World
disposal clears the descriptor arrays and removes bindings from the query cache.

Write stamps are marked once per distinct component and non-empty archetype
before the first callback, preserving exception behavior. The structural lease
covers all callbacks and is released when a callback throws. Read-only bindings
have no write routes. The entity loop itself is unchanged.

## Measurement protocol fixed before the run

- Apple M4 Pro / Arm64, .NET 10.0.9, SDK 10.0.301, BDN 0.13.12.
- Same four scalar workloads at 100 and 100000 entities, padding 0.
- A1 -> B1 -> B2 -> A2; each phase runs both population sizes.
- Fresh BDN child processes: one launch per case per phase, five warmups,
  twenty requested measurements at 200 ms. BDN's ordinary outlier policy stays
  unchanged and actual retained measurement counts can differ.
- Identical source benchmark harness and commands. Only candidate core/generator
  sources differ. Both versions were built and smoke-tested before starting.
- No tests, builds or other benchmarks were launched alongside the measurement
  sequence by this task. BDN builds its own harness before timed execution.
  Unrelated workstation activity is not controlled; ABBA exposes some drift.
- `DOTNET_JitDisasm=*:DeltaECS` is set consistently for all phases. Tiered JIT and
  dynamic PGO retain defaults. Assembly output is recorded in phase logs.
- A speedup is confirmed only if **both** candidate intervals are below **both**
  baseline intervals: `max(B Mean + Error) < min(A Mean - Error)`.
  Error is BDN's default mean confidence-interval half-width. Overlap means
  inconclusive, not a win; measurement is not extended until significance.
- No competitor claim is made from this A/B experiment.

The reproducible driver and raw logs are under `artifacts/binding/`. The exact
runner command for a phase is:

```sh
dotnet benchmarks/Ecs.CSharp.Benchmark/bin/Release/net10.0/Ecs.CSharp.Benchmark.dll \
  --entity-count 100000 --filter '*SystemWith*.DeltaECS' \
  --warmupCount 5 --iterationCount 20 --iterationTime 200 --launchCount 1 \
  --artifacts <phase-output>
```

## Results

### 100 entities

All times below are ns. Each launch cell is Mean ± Error (StdDev in parentheses).

| Workload | A1 | B1 | B2 | A2 |
| --- | --- | --- | --- | --- |
| 1 component | 31.600 ± 0.261 (0.301) | 32.260 ± 0.185 (0.206) | 32.210 ± 0.166 (0.178) | 32.900 ± 0.110 (0.122) |
| 2 components | 34.980 ± 0.222 (0.246) | 32.890 ± 0.147 (0.170) | 32.320 ± 0.299 (0.345) | 35.990 ± 0.495 (0.530) |
| 3 components | 39.660 ± 0.292 (0.312) | 35.240 ± 0.162 (0.180) | 35.910 ± 0.620 (0.714) | 39.570 ± 0.114 (0.131) |
| 2 components, 4 archetypes | 39.630 ± 0.188 (0.217) | 36.690 ± 0.761 (0.876) | 35.890 ± 0.213 (0.246) | 39.830 ± 0.265 (0.305) |

### 100000 entities

All times below are µs. Each launch cell is Mean ± Error (StdDev in parentheses).

| Workload | A1 | B1 | B2 | A2 |
| --- | --- | --- | --- | --- |
| 1 component | 27.470 ± 0.452 (0.503) | 27.330 ± 0.118 (0.136) | 27.480 ± 0.079 (0.091) | 27.690 ± 0.101 (0.116) |
| 2 components | 30.900 ± 0.200 (0.230) | 29.890 ± 0.147 (0.170) | 30.120 ± 0.197 (0.227) | 30.400 ± 0.191 (0.212) |
| 3 components | 39.090 ± 0.126 (0.140) | 38.100 ± 0.127 (0.141) | 38.080 ± 0.093 (0.103) | 39.460 ± 0.221 (0.255) |
| 2 components, 4 archetypes | 31.270 ± 0.190 (0.211) | 30.410 ± 0.214 (0.247) | 30.580 ± 0.228 (0.263) | 31.460 ± 0.156 (0.180) |

### Conservative interval envelope across processes

Each interval covers both per-process Mean ± Error intervals. It is a conservative decision envelope, not a newly estimated pooled confidence interval. Percentage change uses the average of the two process means; it is not claimed as a speedup for overlapping envelopes.

| Entities | Workload | A envelope | B envelope | Mean change | Result |
| --- | --- | --- | --- | --- | --- |
| 100 | 1 component | 31.339–33.010 ns | 32.044–32.445 ns | -0.05% | Inconclusive |
| 100 | 2 components | 34.758–36.485 ns | 32.021–33.037 ns | -8.12% | Confirmed |
| 100 | 3 components | 39.368–39.952 ns | 35.078–36.530 ns | -10.20% | Confirmed |
| 100 | 2 components, 4 archetypes | 39.442–40.095 ns | 35.677–37.451 ns | -8.66% | Confirmed |
| 100000 | 1 component | 27.018–27.922 µs | 27.212–27.559 µs | -0.63% | Inconclusive |
| 100000 | 2 components | 30.209–31.100 µs | 29.743–30.317 µs | -2.10% | Inconclusive |
| 100000 | 3 components | 38.964–39.681 µs | 37.973–38.227 µs | -3.02% | Confirmed |
| 100000 | 2 components, 4 archetypes | 31.080–31.616 µs | 30.196–30.808 µs | -2.77% | Confirmed |

All 32 measured cases report 0 B managed allocation per iteration. Five scenarios meet the predeclared non-overlap rule. One-component iteration at both populations and two-component iteration at 100K remain inconclusive. In particular, the first one-component 100-entity run alone would have suggested a regression; the later baseline moved across the candidate, demonstrating why one launch is insufficient.

### JIT evidence

For the 100-entity A1/B1 runs, Tier1 benchmark-body size changes from 1448/2244/3008/2240 bytes to 780/796/824/816 bytes (1/2/3 components, then multiple archetypes). These are caller-body sizes, not a sum of all transitive methods. The captured hot loop remains scalar; no delegate call or entity-loop unrolling was introduced. All per-phase assembly listings are in the corresponding `.log` files.


## Costs and limits

The cache adds retained managed memory and cold binding work. On Arm64, each
descriptor is approximately `8 * (component arity + 1)` bytes: one chunk object
reference plus the component-array references. For 196 chunks, the payload is
3136/4704/6272 bytes for arities 1/2/3, excluding array headers, binding objects,
route arrays and dictionary entries. Geometric growth can retain spare capacity.
These are layout calculations, not a measured process-memory claim.

A topology change rebuilds all descriptors for each binding when it is next
used. Frequent structural changes can therefore move cost from steady iteration
to preparation. This experiment does not establish a structural-throughput or
cold-start improvement. Different generated invocation shapes can own distinct
binding types even when their component types overlap.

The generated runtime support remains hidden with EditorBrowsable.Never; public
user syntax and ordinary component-write contracts are unchanged. New public
compiler-support types are needed because generated code lives in a consumer
assembly.

## Validation and retained files

- Generator tests: 64/64 passed, including generated-source compilation checks.
- Runtime tests: 132/133 passed. All four new binding tests passed: live counts
  and chunk adoption, world ownership, write stamps and exception cleanup,
  read-only stamps and disposal after alternating bindings.
- The remaining failure is `ExplicitThrowKeywords_AreOnlyInThrowHelpers`:
  unchanged `SystemAccess.cs` and `SystemScheduler.cs` contain explicit throws.
  Neither Systems file differs from the baseline commit.
- Runtime `netstandard2.1` build: zero errors; existing warnings remain.
- Both benchmark variants built and passed their contract smoke before the
  measurement sequence. Repository layout and `git diff --check` pass.

Implementation is retained, uncommitted, in
`/Users/rum/GitProjects/TheFurnace/DeltaECS` on `perf/typed-binding`.
After measurement, the branch, changes and raw results were moved to this main
workspace at the user's request; the original `typed-binding` worktree was
removed after verifying file hashes. The separate baseline worktree is retained.
There is no merge, publication, or claim that this experiment beats Friflo/Myriad.

Key sources are `src/DeltaECS/Generator/GeneratedDenseBinding.cs`,
`src/DeltaECS/Core/QueryAccess.cs` and
`src/DeltaECS.Generators/DenseBindingTemplates.cs`. Raw measurements, the ABBA
driver, parsed comparison and validation logs stay in `artifacts/binding/`.
