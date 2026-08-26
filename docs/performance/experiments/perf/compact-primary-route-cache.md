# Compact primary route cache experiment

## Verdict

**Rejected.** Replacing `QueryPlan`'s `Dictionary<Type, int>` with a compact
per-plan `PrimaryReadRoute[]` did not produce a reliable cross-surface win.
The serialized default-job comparison improved Functor but regressed Delegate
at every measured amount. The 1,000,000-entity results also moved by about 2%
for Functor and 20% for Delegate even though the changed lookup is fixed setup
work outside the entity loop. That split is not causally credible as a safe
setup-only improvement, and the Delegate regressions fail the guardrail.

The source commit is retained as experiment history; it should not be merged.

## Hypothesis and boundary

- Branch: `perf/compact-primary-route-cache`
- Baseline: `fb6c8d0b4b87511ac121eff74c84956da96c94d2`
- Candidate source: `877fa909fa738dcd8e67c413fafe5e9e244bb11c`
- Hypothesis: typical generated queries resolve only 1-4 primary CLR types, so
  a compact linear entry array may beat `Dictionary<Type, int>` during each
  generated Delegate/Functor call.
- Change: one per-query `PrimaryReadRoute[]`, prebuilt with the existing
  registry-primary check and searched with exact `Type` reference identity.
- Unchanged: public API, generic/type-erasure boundaries, query/world
  ownership, scope lease lifetime, write-route upgrades and stamps, empty
  query behavior, explicit secondary registrations, and process-global state.
- Correctness addition: a secondary-only registration has no inferred primary
  route; a query containing primary and secondary IDs resolves the primary ID
  without aliasing the secondary route.

## Environment and protocol

- Apple M4 Pro, 14 physical/logical cores, 24 GiB RAM.
- macOS 26.5.2 (25F84), Darwin 25.5.0, native ARM64.
- .NET SDK 10.0.301; .NET runtime 10.0.9; Arm64 RyuJIT AdvSIMD.
- Concurrent Workstation GC; BenchmarkDotNet 0.13.12 `DefaultJob`, one launch.
- Baseline and candidate were run serially in an exclusive benchmark slot.
  A process-list check found no other BDN host before either run.
- High-priority elevation was unavailable for both runs; both used the same
  normal process priority.
- Fresh clean artifacts: `artifacts/compact-primary-route/serialized-baseline`
  and `artifacts/compact-primary-route/serialized-candidate`. Earlier
  overlapping measurements are excluded from every table and conclusion.
- Setup/world creation/query creation stay in `[GlobalSetup]`. The measured
  methods are the existing `Movement4ApiComparisonMicroBenchmarks.Functor` and
  `.Delegate`; each returns an observable checksum.
- `contract-smoke` passed in both pinned worktrees. At `Amount=8`, ThreeWhile,
  TwoWhile, Functor, Delegate, DelegateContext and FunctorContext all returned
  the same checksum, 160 (`20 * Amount`). Thus the requested benchmark amounts
  have the same expected checksum formula on baseline and candidate.
- Local BDN restore used `NuGetAudit=false RestoreIgnoreFailedSources=true`.
- The existing parameter matrix also measured 10,000, 100,000 and 10,000,000;
  these supporting rows are retained rather than discarded.

Commands:

```bash
env NuGetAudit=false RestoreIgnoreFailedSources=true \
dotnet benchmarks/DeltaECS.MicroBenchmarks/bin/Release/net10.0/DeltaECS.MicroBenchmarks.dll \
  --filter '*Movement4ApiComparisonMicroBenchmarks.Delegate' \
           '*Movement4ApiComparisonMicroBenchmarks.Functor' \
  --artifacts <fresh-baseline-or-candidate-directory>
```

## Serialized BenchmarkDotNet results

Ratio is candidate mean divided by baseline mean; lower is better. `Allocated`
is managed allocation per operation.

| Method | Amount | Baseline Mean | Error | StdDev | Allocated | Candidate Mean | Error | StdDev | Allocated | Candidate/main |
|:---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Functor | 100 | 125.9 ns | 0.95 ns | 0.79 ns | 0 B | 107.4 ns | 2.15 ns | 2.48 ns | 0 B | 0.8531 |
| Delegate | 100 | 192.2 ns | 0.55 ns | 0.46 ns | 0 B | 206.8 ns | 3.22 ns | 2.51 ns | 0 B | 1.0760 |
| Functor | 1,000 | 1,096.0 ns | 3.23 ns | 3.03 ns | 0 B | 1,074.3 ns | 20.91 ns | 27.19 ns | 0 B | 0.9802 |
| Delegate | 1,000 | 1,734.9 ns | 6.00 ns | 5.61 ns | 0 B | 2,048.1 ns | 40.32 ns | 68.46 ns | 0 B | 1.1805 |
| Functor | 10,000 | 11,185.9 ns | 41.67 ns | 38.98 ns | 0 B | 10,870.6 ns | 193.35 ns | 180.86 ns | 0 B | 0.9718 |
| Delegate | 10,000 | 17,787.1 ns | 80.80 ns | 71.63 ns | 0 B | 21,539.7 ns | 428.45 ns | 804.74 ns | 0 B | 1.2110 |
| Functor | 100,000 | 110,687.3 ns | 372.48 ns | 330.19 ns | 0 B | 105,789.5 ns | 360.55 ns | 281.49 ns | 0 B | 0.9558 |
| Delegate | 100,000 | 173,149.8 ns | 1,410.42 ns | 1,177.76 ns | 0 B | 208,858.0 ns | 699.98 ns | 546.50 ns | 0 B | 1.2062 |
| Functor | 1,000,000 | 1,072,824.0 ns | 12,768.74 ns | 11,319.16 ns | 0 B | 1,046,740.5 ns | 2,465.70 ns | 2,058.97 ns | 0 B | 0.9757 |
| Delegate | 1,000,000 | 1,720,250.2 ns | 5,850.35 ns | 4,885.31 ns | 0 B | 2,064,189.9 ns | 3,873.47 ns | 3,433.73 ns | 0 B | 1.1999 |
| Functor | 10,000,000 | 10,473,687.1 ns | 37,244.76 ns | 29,078.26 ns | 0 B | 10,482,205.5 ns | 44,500.81 ns | 37,160.19 ns | 0 B | 1.0008 |
| Delegate | 10,000,000 | 17,200,891.8 ns | 52,228.56 ns | 40,776.62 ns | 0 B | 21,019,061.0 ns | 287,395.75 ns | 268,830.16 ns | 0 B | 1.2220 |

The required cold/small rows therefore range from a 14.7% Functor improvement
to an 18.1% Delegate regression. The 1,000,000 guardrail is 2.4% faster for
Functor and 20.0% slower for Delegate. This is neither a uniform improvement
nor a credible setup-only signature.

## ARM64 Release JIT

The same already-built Release DLL in each pinned worktree was captured with
`DOTNET_TieredCompilation=0`, `DOTNET_ReadyToRun=0` and diffable JIT output.
`jit-report.py` invokes `run-jit-disasm.sh`; the job is `dry`, so its timings
are not performance evidence.

```bash
python3 benchmarks/jit-report.py \
  --method '*ResolvePrimaryReadRoute*' \
  --filter '*Movement4ApiComparisonMicroBenchmarks.Functor' \
  --mode release --no-build \
  --output artifacts/compact-primary-route/jit/<revision>-resolve.txt \
  --report artifacts/compact-primary-route/jit/<revision>-resolve.md
```

Equivalent captures used `*ExecuteGeneratedForEach*`, `Functor`, and
`Delegate` for the generated driver and Movement4 benchmark entry methods.

### Code size

| ARM64 method | Baseline | Candidate | Delta |
|:---|---:|---:|---:|
| `QueryPlan.ResolvePrimaryReadRoute(Type)` | 572 B | 620 B | +48 B (+8.4%) |
| `World.ExecuteGeneratedForEach<DemandForEachInvoker_76791710>` | 888 B | 888 B | 0 B |
| `Movement4ApiComparison...Functor()` | 448 B | 448 B | 0 B |
| `Movement4ApiComparison...Delegate()` | 624 B | 624 B | 0 B |

### Full compact instruction-count table

Counts are for the first emitted ARM64 block. Cells are baseline/candidate.
Categories intentionally overlap as defined by `jit-report.py`; zero rows are
included to make the table complete.

| Operation | Resolve route | Generated driver | Movement4 Functor | Movement4 Delegate |
|:---|---:|---:|---:|---:|
| `blr` | 9/8 | 5/5 | 9/9 | 10/10 |
| `bl` | 2/3 | 0/0 | 0/0 | 4/4 |
| `ret` | 1/1 | 2/2 | 1/1 | 1/1 |
| `bounds branch` | 0/1 | 0/0 | 0/0 | 0/0 |
| `conditional branch` | 0/0 | 0/0 | 0/0 | 0/0 |
| `compare branch` | 1/0 | 7/7 | 0/0 | 2/2 |
| `test-bit branch` | 0/0 | 0/0 | 0/0 | 1/1 |
| `branch` | 5/6 | 17/17 | 0/0 | 4/4 |
| `compare` | 4/8 | 12/12 | 0/0 | 0/0 |
| `add/sub` | 12/15 | 32/32 | 2/2 | 3/3 |
| `multiply` | 0/0 | 0/0 | 0/0 | 0/0 |
| `divide` | 0/0 | 0/0 | 0/0 | 0/0 |
| `sbfiz` | 0/0 | 4/4 | 0/0 | 0/0 |
| `shift/bitfield` | 2/3 | 1/1 | 0/0 | 0/0 |
| `ldr` | 30/31 | 63/63 | 17/17 | 20/20 |
| `str` | 7/7 | 17/17 | 4/4 | 7/7 |
| `ldp/stp` | 8/8 | 18/18 | 11/11 | 13/13 |
| `prefetch` | 0/0 | 0/0 | 0/0 | 0/0 |
| `AdvSIMD load/store` | 0/0 | 0/0 | 0/0 | 0/0 |
| `AdvSIMD arithmetic` | 0/0 | 0/0 | 0/0 | 0/0 |

The lookup removes one indirect call but adds one direct call, one bounds
branch, four compares, three add/sub operations, one shift/bitfield operation,
one load, and 48 bytes of code. The generated driver and both Movement4 entry
methods are unchanged. Assembly does not explain or validate the opposing BDN
movements, reinforcing the rejection.

A plausible tradeoff is that the linear scan removes dictionary indirection
for the small Functor setup path but exposes repeated comparisons, bounds work,
and larger lookup code that interacts less favorably with the Delegate call
shape. The captured downstream methods are identical, so this is a codegen
hypothesis rather than a proven explanation; it does not make the mixed result
safe to accept.

## Correctness and source checks before evidence

- Release solution build: passed, 0 errors (690 existing analyzer warnings).
- `DeltaECSTests`: 132 passed, 0 failed, 0 skipped.
- Microbenchmark Release build: passed.
- Baseline and candidate `contract-smoke`: passed with matching checksums.
- `git diff --check`: passed before the source commit.
