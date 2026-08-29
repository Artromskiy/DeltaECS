# Optimization experiment ledger

This is the authoritative ledger for DeltaECS performance experiments. Record
every measured candidate here whether it is accepted, rejected or inconclusive.
An entry is not a performance claim unless it names a workload and evidence.

Do not repeat a rejected or inconclusive mechanism unless the new experiment
states what changed: runtime, architecture, workload, implementation mechanism
or measurement quality. Raw BenchmarkDotNet and JIT output stays under ignored
`artifacts/`; durable conclusions and reproduction details belong here or in a
linked focused report.

## Measurement corrections

| Correction | Evidence | Result |
| --- | --- | --- |
| Make the shared benchmark terminal helpers `internal` so the already-enabled Roslyn interceptor can legally copy static lambda bodies into generated code | Candidate source diff in `benchmarks/DeltaECS.Benchmarks/UnifiedIterationBenchmarks.cs`; full matrix `artifacts/perf-round-20260829/interceptor-all`; Movement4 JIT `artifacts/perf-round-20260829/candidate-interceptor-harness-jit/movement4-jit.txt` | Corrects the intended interceptor-enabled benchmark mode; it does not change ECS runtime or public API. The generated Movement4 method is `872 B / 218 instructions` versus the pre-correction delegate method `876 B / 219 instructions`; `blr` remains `6` (including one callback-path indirect call). Delta improves versus the old harness by `22.8/11.1/10.8/10.0%` Dense, `5.9/30.0/31.4/31.6%` Movement2, `22.0/23.1/14.5/12.8%` Movement4 and `18.4/12.8/39.0/12.7%` Wide for `100/1K/10K/100K` entities. It is a measurement correction, not evidence that Delta wins every iteration category. |

## Accepted

| Area | Change | Evidence | Result |
| --- | --- | --- | --- |
| Query matching | Replace temporary `List`/`ToArray` matching-plan construction with reusable compact storage | `4994f78`, `6d3ecdf`, `eeec3ea`; [V3 JIT](../query-plan-matching-v3-jit.md) | Large setup-driver JIT reduction; retained storage shape |
| Query refresh | Incrementally maintain plans and refresh only when archetype state changes | `933dc13` | Avoids rebuilding stable plans during execution |
| Chunk traversal | Dense active-chunk arrays and cached iterator counts | `aaf905c`, `ecfa99a`, `cf75cee` | Retained after JIT/benchmark comparison |
| Row resolution | Resolve physical component rows once per active chunk and cache direct managed arrays | `9189545`, `a9732db`, `11a1258`; [row evidence](cached-resolved-query-rows.md) | Removes repeated row lookup from the slot loop |
| Row endpoint | Trusted internal array indexing and bounds-check removal after validation | `dc4dd9f`, `b77065d` | Retained internal fast endpoint; public API remains safe |
| Iterator state | Carry validated plan directly and remove unused owner/terminal state | `1a89172`, `54a2967`; [plan-state evidence](../l1b-dense-plan-state.md) | ARM64 driver code and load/store count reduced |
| Slot traversal | Remove terminal assignments from outer advancement | `a8fefe7`; [slot evidence](../l0-b-slot-iterator-experiments.md) | JIT block reduced; retained as code-generation improvement |
| Slot order | Forward slot traversal | `8b27472` | Won the dedicated forward/reverse Movement4 comparison |
| Query access | Type-erased access objects and non-generic storage/query chain | `0213ae0`, `6da5550`, `7a2f999` | Equivalent or better JIT while preserving generic only at `Ref<T>` boundary |
| Generated callbacks | Inline generated invokers and reserve one write tick per execution | `53f3122`, `f29ed8a` | Removed repeated driver work; retained |
| Delegate interception | Convert supported static `World.ForEach` lambdas and method groups into generated struct functors without changing source spelling | `3c7edbb`, `6946781`, merge `612d26a`; [evidence](delegate-interception.md) | Accepted opt-in path. Movement4 measured 0.56x-0.61x of the pre-created delegate fallback with 0 B allocation; unsupported callbacks retain delegate semantics |
| Interceptor discard-name hygiene | Give repeated lambda discard parameters unique generated names | `368b207` -> `17aac7c`; generator tests 21/21 | Accepted correctness fix; avoids duplicate generated parameter declarations, with no throughput claim |
| Generated rows | Trusted generated query slots/row preparation | `0ce7a95`, `022a1f5` | Retained after Movement4 comparison |
| Query access setup | Prebuild `ComponentId -> ordinal/type` read routes and promote the validated route to write | `e9ccffe`, `fb6c8d0` | Movement4 delegate: about 15% faster at 100 entities, about 2% at 1k/10k, neutral at large sizes; affected helper JIT 1584 B to 888 B |
| Prepared generated access and trusted advance | Prepare generated read/write routes at the validated execution boundary and use the trusted advance in generated callbacks | `c6b819a`, `138cbd9`; [evidence](prepared-generated-access.md) | Merged into `main`; 100-entity adaptive Movement4 was 111.5 ±0.60 ns on main versus 112.8 ±0.43 ns on the candidate (+1.17%). The benchmark exercises the shared three-loop path, so it is not a direct `MoveNextTrusted` throughput claim |
| Structural create | Batched entity creation kernel | `7540054` | Retained measured structural improvement |
| Structural destroy | Ordered/list destroy kernels with whole-chunk path | `c63b492`, `addc9b4`; [destroy evidence](../destroy-kernels-v1.md) | 49% to 91% faster for measured batches of 8 to 4096 |
| Dense sweep | Combined L0-L3 internal cleanup | `9b0f42e`, `c9fac9a`, `92ac844`; [sweep summary](../l0-l3-sweep.md) | ARM64 JIT block 1408 B to 1084 B; throughput mostly neutral, retained for simpler generated code |

## Rejected

| Candidate | Evidence/result | Reason not retained |
| --- | --- | --- |
| Local slot-count/index caches and direct iterator field access | [slot matrix](../l0-b-slot-iterator-experiments.md) | No JIT change |
| `readonly`/`in` binding helper variants | [slot matrix](../l0-b-slot-iterator-experiments.md) | Invalid span/ref escape semantics |
| Per-archetype generated route carried by the execution driver | `502230a` (reverted by `9d352c1`); JIT `artifacts/perf-round-20260829/candidate-in-ctor-baseline-jit.txt` vs `candidate-archetype-route-cache-jit.txt`; BDN `artifacts/perf-round-20260829/candidate-archetype-route-cache-bdn` vs `/tmp/deltaecs-perf-baseline-20260829/artifacts` | JIT shrank `888→884 B` (`sbfiz 4→3`, `add/sub 33→32`, `str 20→19`), but Movement4 throughput regressed `+2.5%/+4.7%/−3.0%/+7.7%/+2.5%` at 100/1k/10k/100k/1M entities; no stable win |
| Passing generated plan/chunk state by `in` | `a32f1f6` (reverted by `f005b4e`); JIT `artifacts/perf-round-20260829/candidate-in-plan-byref-jit.txt`; BDN `artifacts/perf-round-20260829/candidate-in-plan-byref-bdn` vs `/tmp/deltaecs-perf-baseline-20260829/artifacts` | JIT grew `888→908 B`, `ldr 68→71`, and `ldp/stp 31→30`; Movement4 changed `+17.8%/+2.4%/−13.1%/−4.5%/+2.8%` at 100/1k/10k/100k/1M entities, with no all-size win |
| Duplicate query-route reference in every `ChunkPlan` | `2e8ea3e` (reverted by `82bb25d`); JIT `artifacts/perf-round-20260829/candidate-chunkplan-routes-jit.txt`; BDN `artifacts/perf-round-20260829/candidate-chunkplan-routes-bdn` vs `/tmp/deltaecs-perf-baseline-20260829/artifacts` | JIT shrank `888→884 B` (`sbfiz 4→2`, `add/sub 33→32`, `str 20→19`, `ldp/stp 31→30`), but the extra 8-byte reference per chunk produced mixed Movement4 results: `+0.7%/+2.0%/−3.3%/+0.5%/−1.3%` at 100/1k/10k/100k/1M; errors overlap and there is no stable win |
| Inductive typed refs in generated functor loops | `6f73b63` (reverted by `465a51a`); JIT `artifacts/perf-round-20260829/candidate-functor-inductive-refs-jit.txt`; BDN `artifacts/perf-round-20260829/candidate-functor-inductive-refs-bdn` vs `/tmp/deltaecs-perf-baseline-20260829/artifacts` | JIT shrank `888→872 B` (`sbfiz 4→3`, `cmp 5→4`), but Movement4 changed `+11.9%/+3.0%/−2.7%/−5.1%/+1.6%` at 100/1k/10k/100k/1M; allocation stayed `0 B` and no all-size throughput win |
| Extra prepared column state | `dd43b6b`; [column-state evidence](../l1a/candidate-2-column-state.md) | Small JIT movement, no stable throughput win; superseded by direct chunk row cache |
| Advancing `ref byte` rows in `MoveNext` | Historical Movement4 comparison | Removed `sbfiz` and reduced code about 2%, but repeated long runs did not improve throughput |
| Per-row static/common/write-only helpers | Historical L0-L3 follow-up | Smaller code in some variants, inconsistent or negative throughput |
| Shared ref-struct scope context for all three iterators | Historical isolated experiments | Added indirection/copies and did not improve the measured loop |
| Single/zero matching-archetype special cases | Historical cold-query experiment | Removed after unstable or negative small-query results |
| `ReadOnlyArray<T>` cached-length wrapper | Historical array/span comparison | Slower than direct `ReadOnlySpan<T>` with trusted ref access |
| `Memory<T>`/`MemoryManager<T>` ownership wrapper for ECS storage | `443635b` and follow-ups | More indirection and code than the retained owner-controlled storage |
| Native query-plan/row-index storage | `8472f0a` and follow-ups | Larger JIT and ownership complexity without a reliable end-to-end win |
| Native AoS `NativePlanEntry` buffer | Historical query-plan experiment | Benchmark difference overlapped error/deviation; experiment cancelled |
| Generated slot-loop unroll×4 | [chunk-loop-unrolling evidence](chunk-loop-unrolling.md) | Isolated scalar loop improved 15.8%, but generated Movement4 JIT grew 36.6% (`744 B` to `1016 B`) and the 10k/100k real `ForEach` cases regressed |
| Function pointer in place of entity callback | Historical callback experiment | Indirect call remained and JIT could not inline the callback body |
| Delegate wrapped in a generic struct adapter | Historical callback JIT experiment | Delegate invocation still produced an indirect call; wrapper did not create a direct inlineable lambda body |
| Singleton destroy through chunk kernel | [destroy evidence](../destroy-kernels-v1.md) | Slower/noisy than direct atomic destroy |
| Query-specific destroy helper/active-chunk variants | [destroy evidence](../destroy-kernels-v1.md) | Neutral to slower across tested sizes |
| Aggressive inlining of structural helpers | [destroy evidence](../destroy-kernels-v1.md) | No repeatable benefit |
| Compact linear primary-route cache | `877fa90`; [evidence](compact-primary-route-cache.md) | Rejected; experiment branch `perf/compact-primary-route-cache` deleted. Functor was up to 14.7% faster, but Delegate regressed 7.6% to 22.2%; lookup JIT grew 572 B to 620 B |
| Unconditional flat active-chunk view | `6cc65a1`; [evidence](flat-active-chunk-view.md) | TwoWhile improved 26% to 32% for 1k to 10M, but regressed 92.7% at 100/single chunk; Functor was neutral |
| Non-inlined generated row-preparation helper | `ee92c97`; [evidence](arity-trusted-chunk-kernel.md) | Rejected; experiment branch `perf/arity-trusted-chunk-kernel` deleted. Added one `blr` per chunk and grew total emitted code; Functor regressed 49% to 62%, Delegate 6% to 9% |
| Global `AggressiveInlining` on every generated closed core | `artifacts/aggressive-inline-closed-movement4`, `artifacts/aggressive-inline-closed-micro-api` | Rejected as a global policy. It improved comparative write cases, but Micro `Intercepted` regressed at 1,000 entities from `1,077.3 ± 4.02 ns` to `1,103.8 ± 15.45 ns`; narrowed to read-only shapes in `17aac7c` |
| Read-only component value copies | `artifacts/final-read-only-copy-dense`, `artifacts/read-only-copy-wide` | Rejected despite Dense/Wide speedups: replacing `ref readonly` with a mutable local can change the source lambda's `in` readonly semantics |
| One prepared generated access bundle | `artifacts/prepared-bundle-movement4` | Rejected: mixed result against the interception-only control, including a small-size regression; no all-size no-regression proof |
| Direct generated plan/chunk loop | `artifacts/direct-loop-movement4` | Rejected: slower than the interception-only control across the measured Movement4 sizes |
| Typed arrays acquired once per chunk | `artifacts/typed-array-movement4` | Rejected: slower at the measured small and large Movement4 endpoints |
| `AggressiveOptimization` on generated closed core | `artifacts/aggressive-optimization-movement4` | Rejected: mixed small result and large-size regressions against the prepared-bundle control |
| Direct typed row references | `artifacts/typed-ref-movement4` | Rejected: slightly slower at every measured Movement4 size than the helper control |
| Span row endpoint | `artifacts/span-row-movement4`, `artifacts/read-only-span-wide` | Rejected: mixed Movement4 result and worse than the accepted read-only inlining result on Wide |
| Read-only array endpoint | `artifacts/read-only-array-dense` | Rejected: mixed against the read-only reference; 1,000-entity Dense regressed |
| Discard-only Sparse access elimination | `artifacts/no-discard-sparse`, `artifacts/discard-only-sparse` | Rejected: improved 100 entities but regressed 1K/10K/100K by roughly 7%/10%/13%; the later read-only inlining experiment is a materially different JIT mechanism |
| Conditional generated read-only closed-core inlining | `17aac7c`; clean main-baseline recheck `artifacts/recheck-clean2-{dense,movement2,movement4,wide,sparse}` versus `artifacts/recheck-main2-{dense,movement2,movement4,wide,sparse}`; JIT `artifacts/recheck-clean2-jit-comparative-dense.log` versus `artifacts/recheck-main2-jit-comparative-dense.log` | Rejected as a broad candidate. With benchmark helper inlining hints removed, Wide alone improved 23.9%–31.8%; Dense regressed 1.4%–3.2%, Movement4 regressed 2.4%–6.9%, Movement2 was mixed within 1.5%, and Sparse was mixed within 5.6%. The generated caller grew from 284 B / 3 single-block inlinees on main to 720 B / 33 single-block inlinees, without a no-regression result |
| Benchmark-only interceptor activation | `17aac7c` -> `005085f`; current matrix `artifacts/recheck-current-{dense,movement2,movement4,wide,sparse}` versus `artifacts/recheck-main-current-{dense,movement2,movement4,wide,sparse}`; JIT `artifacts/recheck-current-jit-interceptor-dense.log` versus `artifacts/recheck-main-current-jit-interceptor-dense.log` | Rejected as benchmark configuration. The current NoInlining generated caller was identical to main at 284 B / 3 single-block inlinees / 2 non-PGO inlinees, and the BDN matrix was mixed: Dense −3.6% to +1.6%, Movement2 −4.1% to +1.2%, Movement4 −1.2% to +12.0%, Wide −3.3% to −1.1%, Sparse −5.4% to +1.2%. The general interceptor runtime feature remains covered by the separate accepted method-group evidence |
| Compact `Query` handle plus strong last-plan cache | `artifacts/query-handle-compact-last-cache`, `artifacts/query-hash-dry` | Rejected: the only stable screening point was 51.77 ns for Delta query construction at 100 entities versus the 52.62 ns reference; the combination is far from the 30% target and was reverted |
| Cheap `ComponentMask` hash | `artifacts/query-hash-dry` | Rejected: the all-size probe used `Dry` measurements only and provided no reliable throughput evidence; the hash implementation was reverted |
| Advance typed row refs inside the generated slot loop | Baseline `d9ad3e3`; candidate artifacts `artifacts/perf-round-20260829/candidate-typed-ref-advance` and `artifacts/perf-round-20260829/candidate-typed-ref-advance-jit` | Rejected. Replacing `Unsafe.Add(ref row, index)` with four advancing managed `ref` locals removed the hot-loop `sbfiz` and one address add, but forced four ref spills/reloads around the delegate call. The generated method grew `876 -> 888 B` and `221 -> 224` instructions; `blr` stayed `6`. Movement4 Delta regressed `11.7% / 24.1% / 37.9% / 37.9%` at `100 / 1K / 10K / 100K` entities. Allocations stayed `0 B`; the candidate run used .NET 10.0.9, Arm64 RyuJIT, tiering/R2R disabled, `IterationTime=100 ms`, `WarmupCount=10`, `MinIterationCount=10`, `MaxIterationCount=20`, `LaunchCount=1`. |
| Reuse the existing primary route dictionary instead of prepared access dictionaries | Baseline `d9ad3e3`; candidate artifacts `artifacts/perf-round-20260829/candidate-prepared-route-single-map` and `artifacts/perf-round-20260829/candidate-prepared-route-single-map-jit` | Rejected. The change preserved all type/route validation and removed two `Dictionary<Type, Access>` stores, but JIT was identical (`876 B / 221 instructions / 6 blr / 4 sbfiz / 54 ldr / 23 str`). Delta Movement4 was mixed at `−0.75% / +0.18% / −4.13% / −1.18%` for `100 / 1K / 10K / 100K`; no stable throughput or code-size benefit. Allocations remained `0 B`; benchmark used .NET 10.0.9, Arm64 RyuJIT, tiering/R2R disabled, `IterationTime=100 ms`, `WarmupCount=10`, `MinIterationCount=10`, `MaxIterationCount=20`, `LaunchCount=1`. |
| Compact linear primary route cache (`Type[]` + `Access[]`) | Candidate `artifacts/perf-round-20260829/candidate-linear-primary-routes/dense` versus corrected interceptor control `artifacts/perf-round-20260829/interceptor-all/results/Delta.ECS.Benchmarks.ComparativeDenseIterationBenchmarks-report.csv` | Rejected. Replacing the small primary-access dictionaries with linear scans regressed Dense by `+18.8% / +12.9% / +1.0% / +0.8%` at `100 / 1K / 10K / 100K` entities. The candidate preserved type and query validation; the runtime's dictionary lookup is faster for this path. The candidate was reverted. |
| Separate generated read-only execution/slot structs | Candidate `artifacts/perf-round-20260829/candidate-readonly-execution` versus corrected interceptor control `artifacts/perf-round-20260829/interceptor-all/results/Delta.ECS.Benchmarks.ComparativeDenseIterationBenchmarks-report.csv`; JIT probe `artifacts/perf-round-20260829/candidate-readonly-execution-jit2-dense-jit.txt` | Rejected. Removing write tick/stamp fields from the read-only generated state did not reduce the observed JIT method enough to offset the separate execution path; Dense regressed by `+1.7% / +30.6% / +1.5% / +2.2%` at `100 / 1K / 10K / 100K` entities. The candidate preserved validation and was reverted. |
| Per-generic static cache for prepared primary access | Candidate `artifacts/perf-round-20260829/candidate-generic-primary-cache/dense` versus corrected interceptor control `artifacts/perf-round-20260829/interceptor-all/results/Delta.ECS.Benchmarks.ComparativeDenseIterationBenchmarks-report.csv` | Rejected. Caching `QueryPlan -> ReadAccess/WriteAccess` in a generic static slot removed the repeated `Dictionary<Type, Access>` lookup after the first query, but added a plan-identity branch and static cache state. Dense changed by `+4.8% / −0.8% / +0.2% / +1.0%` at `100 / 1K / 10K / 100K` entities and remained at `0 B` allocation. The small regression and lack of an all-size no-regression result outweigh the isolated 1K signal; the generated overloads and cache were reverted. |
| Four-entry prepared primary-access cache with a single-access generator path | Candidates `artifacts/perf-round-20260829/candidate-primary-access-cache/dense`, `.../candidate-primary-access-cache-v2/dense` and `.../candidate-primary-access-cache-v3/dense` versus corrected interceptor control `artifacts/perf-round-20260829/interceptor-all/results/Delta.ECS.Benchmarks.ComparativeDenseIterationBenchmarks-report.csv` | Rejected. A direct-mapped `Type` cache plus single-component fast path preserved query/type validation and kept Delta at `0 B` allocation, but results stayed mixed: v1 was `+7.3% / +9.3% / −2.3% / −4.4%`; v2 was `+1.2% / +3.6% / −2.5% / −2.6%`; v3 was `+2.8% / +3.0% / +0.7% / −0.8%` at `100 / 1K / 10K / 100K` entities. The small-query regressions and lack of an all-size no-regression result outweigh the large-query signal. The cache and generated endpoints were reverted. |

## Inconclusive or superseded evidence

| Candidate | Evidence | Interpretation |
| --- | --- | --- |
| Matching-plan V1 pre-sized managed arrays | `6d3ecdf`; [V1 JIT](../matching-archetypes-v1-jit.md) | Same 1004 B/251-instruction method; allocation shape improved but throughput was not measured independently |
| Dense 48/50-candidate assembly sweep | `c9fac9a`, `92ac844` | Code size improved substantially, but short 100k throughput was near noise; only merged source is authoritative |
| Generic cached array versus `Span<T>` | Historical comparison | JIT was effectively identical; direct span form was kept for simplicity |
| Large native ECS buffer storage | `bc62b9f`, `d03fa85` | Some benchmark signals improved while generated code grew; retained source later evolved, so old isolated ratios are not current claims |
| Split generated read/write drivers | `4a8db12`; [evidence](perf-split-generated-read-write-drivers.md) | Write guardrail removed one branch/compare and 8 B; Functor improved 1.57% at 100, all other tested write cases were neutral. Direct component-bearing read-only evidence is still missing |
| Metalama layer-major chunking | `8040b2e` (historical experiment) | Promising cache signal, but the measurement used separate flat and chunked runs and the tile changes execution order across entities; not an ECS runtime decision yet |
| Full Sparse campaign with generated-core inlining | `368b207` plus temporary worktree patch; `artifacts/aggressive-inline-closed-sparse` | Inconclusive: the 40-case run was interrupted after the first Delta construction case because BDN estimated about 1 h 35 min and produced no report |
| Focused SparseWorldQueryPlan run | `17aac7c`; `artifacts/aggressive-inline-closed-sparse-world-plan` | Valid 20-case matrix with default adaptive sampling; construction-heavy query-plan measurements are intentionally not inferred from it |

## Adaptive benchmark campaign (2026-08-27)

The campaign baseline is `368b207`; the historical candidate was `17aac7c` in
the separate `codex/adaptive-benchmarks` worktree. Measurements used .NET
10.0.9 / SDK 10.0.301 on macOS 26.5.2, Apple M4 Pro, Arm64 RyuJIT AdvSIMD,
Concurrent Workstation GC, `DOTNET_TieredCompilation=0`,
`DOTNET_ReadyToRun=0`, and BenchmarkDotNet `Default` with adaptive
`IterationTime=100 ms`. The Dense and Wide confirmation runs used
`IterationCount=20`; their CSVs include the full Mean/Error/StdDev/Allocated
rows. Comparative benchmark methods validate checksums on every operation;
all completed runs and both contract smokes passed without a mismatch.
Rejected variants were deliberately left uncommitted; each artifact directory
is the unique candidate identifier for a temporary snapshot based on
`368b207`, and no rejected patch was merged or pushed.

This historical table is superseded by the clean recheck below. Its candidate
also changed comparative benchmark helper methods to `AggressiveInlining`;
those are harness-only changes, were removed in `06fe7f6`, and must not be
attributed to ECS runtime performance. The table is retained only to explain
why the earlier apparent 30% result was not accepted.

| Workload | Delta Mean ± Error (StdDev), entities `100 / 1K / 10K / 100K` | Fastest external Mean ± Error (StdDev) | Delta / fastest |
| --- | --- | --- | --- |
| Dense, 20-iteration confirmation | `35.17 ±0.190 (0.219) / 289.57 ±6.930 (7.980) / 2,796.39 ±31.231 (35.965) / 28,542.88 ±265.112 (305.303) ns` | Leo `81.38 ±0.484 (0.557) / 787.09 ±3.876 (4.463) / 8,319.08 ±16.917 (18.101) / 80,580.47 ±466.035 (536.687) ns` | `0.432 / 0.368 / 0.336 / 0.354` |
| Wide archetype, 20-iteration confirmation | `54.25 ±0.548 (0.631) / 515.51 ±2.650 (3.052) / 5,157.10 ±24.292 (27.001) / 52,065.04 ±351.282 (404.537) ns` | Leo/Arch `130.50 ±0.549 (0.610) / 1,062.21 ±8.186 (9.099) / 10,336.38 ±53.786 (57.551) / 104,665.83 ±872.203 (933.247) ns` | `0.416 / 0.485 / 0.499 / 0.497` |
| Movement2, adaptive control | `105.4 ±0.39 (0.37) / 869.0 ±2.90 (2.71) / 9,186.3 ±35.32 (29.49) / 91,967.9 ±265.28 (248.14) ns` | Arch/Leo `161.5 ±3.20 (3.55) / 1,350.0 ±4.06 (3.39) / 14,150.9 ±30.92 (25.82) / 142,750.8 ±807.16 (755.02) ns` | `0.653 / 0.644 / 0.649 / 0.644` |
| Movement4, adaptive control | `134.8 ±0.20 (0.17) / 1,113.5 ±15.18 (13.46) / 10,940.0 ±101.85 (95.27) / 107,502.4 ±625.37 (554.37) ns` | Arch `220.5 ±1.74 (1.45) / 1,642.0 ±6.69 (6.26) / 17,082.4 ±338.63 (527.21) / 165,591.1 ±2,131.75 (1,889.74) ns` | `0.611 / 0.678 / 0.640 / 0.649` |
| Sparse world plan, 20-case focused run | `24.406 ±0.3394 (0.3175) / 91.008 ±1.5925 (1.4896) / 698.990 ±13.2155 (11.7152) / 6,700.595 ±106.8803 (99.9759) ns` | Default `7.079 ±0.1087 (0.1017) / 71.229 ±1.4410 (2.2856) / 623.318 ±12.4279 (12.2059) / 6,227.608 ±123.7598 (147.3272) ns` | `3.448 / 1.278 / 1.121 / 1.076` |

The exact adaptive control CSVs for Movement2 and Movement4 are
`artifacts/final-movement2` and `artifacts/final-movement4`; their complete
rows retain BDN Error/StdDev/Allocated values even where the compact table
above omits repeated uncertainty fields. The read-only campaign evidence is
`artifacts/final-conditional-20-dense`,
`artifacts/final-conditional-20-wide`, and
`artifacts/aggressive-inline-closed-sparse-world-plan`.

The API-shape matrix also retained the interception win against the
pre-created delegate at `100 / 1K / 10K / 100K / 1M / 10M` entities: the
Intercepted/Delegate ratios were `0.615 / 0.583 / 0.578 / 0.558 / 0.589 /
0.568`, all at `0 B` allocation. It did not beat the already optimized
Functor path at every size.

The strict user target was not reached. These historical numbers are not a
current runtime claim, and the branch must not be described as winning all
adaptive benchmarks.

## Reverted invalid candidate (2026-08-27)

Commit `da92547` is rejected and reverted by `1d6a879`. Its `countOnly`
lowering recognized a narrow `ref int` increment shape and replaced the
per-entity `ForEach` callback with `CountMatching`. That changes observable
semantics: callbacks are not invoked, declared component accesses are not
acquired, and callback side effects and per-row validation are skipped. Its
Sparse and query-construction measurements are therefore not performance
evidence. The associated `MatchingEntityCount`, cached archetype entity
counter, last-query cache, and custom hash experiments are not retained.

## Clean main-baseline recheck (2026-08-27)

The conditional generated-core candidate was rechecked against the current
main baseline after removing all benchmark-helper `AggressiveInlining` hints.
The source snapshot under test was `17aac7c`; the helper cleanup was
`06fe7f6`. Both sides used .NET 10.0.9 / Arm64 RyuJIT AdvSIMD with tiering and
ReadyToRun disabled, BenchmarkDotNet `Default`, adaptive `IterationTime=100
ms`, and 10–20 measured iterations. Every Delta result allocated `0 B`.

| Workload | Candidate mean, entities `100 / 1K / 10K / 100K` | Main mean, entities `100 / 1K / 10K / 100K` | Candidate / main |
| --- | --- | --- | --- |
| Dense | `139.2 / 1,282.7 / 12,898.6 / 130,226.4 ns` | `137.1 / 1,265.6 / 12,631.4 / 126,227.3 ns` | `1.02 / 1.01 / 1.02 / 1.03` |
| Movement2 | `201.6 / 2,152.6 / 22,400.5 / 230,829.5 ns` | `200.2 / 2,185.4 / 22,595.3 / 229,008.4 ns` | `1.01 / 0.98 / 0.99 / 1.01` |
| Movement4 | `275.7 / 2,155.9 / 21,200.9 / 210,767.5 ns` | `262.9 / 2,104.4 / 19,826.1 / 200,822.9 ns` | `1.05 / 1.02 / 1.07 / 1.05` |
| Wide | `140.6 / 1,267.5 / 12,635.4 / 126,348.5 ns` | `184.8 / 1,846.9 / 18,169.0 / 185,175.2 ns` | `0.76 / 0.69 / 0.70 / 0.68` |
| Sparse | `54.65 / 354.62 / 3,359.36 / 33,220.43 ns` | `57.89 / 356.31 / 3,410.19 / 33,317.85 ns` | `0.94 / 1.00 / 0.99 / 1.00` |

This is a rejected candidate: the Wide improvement is not universal, while
Dense and Movement4 regress. The JIT confirms the mechanism but not its
acceptance: the candidate Dense caller is 720 B with 33 single-block inlinees
and 5 non-PGO inlinees; main is 284 B with 3 single-block inlinees and 2
non-PGO inlinees. The conditional inlining source was subsequently removed by
`f51f3c1`, so the current branch does not retain this rejected runtime policy.
The raw BDN and JIT files are the `recheck-clean2-*` and `recheck-main2-*`
artifacts named in the rejected table above.

Correctness gates for the cleaned branch passed: generator tests `21/21`, ECS
tests `135/135`, comparative iteration contract smoke for `5` classes, and
the micro contract smoke. `PipelineApiTests` additionally checks one callback
per matching entity, unchanged read-component stamps, changed write-component
stamps, unchanged entity count, and component-validation failure before the
first callback.

## Accepted evidence: Roslyn delegate interception

| Field | Evidence |
| --- | --- |
| Baseline / candidate | `44cfe13` / `3c7edbb`; hardened by `6946781` and merged into `main` by `612d26a` |
| Operation | `Movement4ApiComparisonMicroBenchmarks.Delegate` (pre-created delegate fallback) versus `Intercepted` (static method group interception) |
| Runtime/host | .NET 10.0.9, SDK 10.0.301, Roslyn package 4.13.0, macOS 26.5.2, Apple M4 Pro, Arm64 RyuJIT AdvSIMD, Concurrent Workstation GC |
| Configuration | Project-local `InterceptorsNamespaces=Delta.ECS.Generated`; no global preview switch and no `InterceptorsPreviewNamespaces` |
| Correctness | Generator tests 20/20 and ECS tests 133/133; consumer fixture rebuild and micro contract smoke succeed; alias isolation, fallback, `ref`/`in`/write checksum, static method-group context and single invocation are checked |

The current SDK supports the generated interceptors. Roslyn's interceptable
location API is consumed by the generator and the generated bridge keeps the
delegate-compatible parameter list, while the bridge ignores the delegate and
enters the existing closed struct-functor execution path. Capturing lambdas,
instance or ambiguous method groups, pre-created delegates,
async/generic/unsupported forms and sequence receivers remain on the ordinary
delegate path; unambiguous static method groups now use the same intercepted
struct-functor path as static lambdas.

ShortRun BDN (three actual iterations, tiering and ReadyToRun disabled) was
run separately for the exact baseline and candidate methods:

```bash
env DOTNET_TieredCompilation=0 DOTNET_ReadyToRun=0 \
  dotnet benchmarks/DeltaECS.MicroBenchmarks/bin/Release/net10.0/DeltaECS.MicroBenchmarks.dll \
  --filter '*Movement4ApiComparisonMicroBenchmarks.Delegate' --job Short \
  --exporters csv --artifacts artifacts/interceptor-bdn-methodgroup-inline-delegate

env DOTNET_TieredCompilation=0 DOTNET_ReadyToRun=0 \
  dotnet benchmarks/DeltaECS.MicroBenchmarks/bin/Release/net10.0/DeltaECS.MicroBenchmarks.dll \
  --filter '*Movement4ApiComparisonMicroBenchmarks.Intercepted' --job Short \
  --exporters csv --artifacts artifacts/interceptor-bdn-methodgroup-inline-intercepted
```

| Entities | Delegate Mean ± Error (StdDev) | Intercepted Mean ± Error (StdDev) | Candidate/Baseline | Allocated |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 229.8 ± 14.17 ns (0.78 ns) | 140.9 ± 5.03 ns (0.28 ns) | 0.61x | 0 B / 0 B |
| 1,000 | 1,857.9 ± 141.27 ns (7.74 ns) | 1,128.4 ± 35.39 ns (1.94 ns) | 0.61x | 0 B / 0 B |
| 100,000 | 199,204.7 ± 7,905.19 ns (433.31 ns) | 111,834.7 ± 2,487.47 ns (136.35 ns) | 0.56x | 0 B / 0 B |

The required cold-start Dry cross-check also completed for all 42 catalog
cases (`artifacts/interceptor-bdn-final`); it is retained as a probe rather
than a throughput claim.

JIT disassembly was captured with:

```bash
env DOTNET_TieredCompilation=0 DOTNET_ReadyToRun=0 \
  DOTNET_JitDisasm='Delta.ECS.DemandForEachExtensions_1D7130AA:ExecuteClosed_1D7130AA' \
  dotnet benchmarks/DeltaECS.MicroBenchmarks/bin/Release/net10.0/DeltaECS.MicroBenchmarks.dll \
  --filter '*Movement4ApiComparisonMicroBenchmarks.Delegate*' --job Dry \
  > artifacts/interceptor-jit-methodgroup/delegate-inlinehint.log 2>&1

env DOTNET_TieredCompilation=0 DOTNET_ReadyToRun=0 \
  DOTNET_JitDisasm='Delta.ECS.DemandForEachExtensions_1D7130AA:ExecuteInterceptedClosed_F5758854' \
  dotnet benchmarks/DeltaECS.MicroBenchmarks/bin/Release/net10.0/DeltaECS.MicroBenchmarks.dll \
  --filter '*Movement4ApiComparisonMicroBenchmarks.Intercepted*' --job Dry \
  > artifacts/interceptor-jit-methodgroup/intercepted-inlinehint2.log 2>&1
```

The first `ExecuteClosed` listing is 860 B / 215 instructions; the
intercepted `ExecuteInterceptedClosed` listing is 880 B / 220 instructions
(+20 B, +5 instructions). Both have 10 branch instructions and zero direct
`bl` instructions. The delegate driver has six `blr` instructions, 80 loads
and 36 stores; the intercepted driver has five `blr` instructions, 82 loads
and 33 stores. The delegate listing has one `blr` in the entity loop that
loads the callback from the delegate object. The intercepted listing has no
`blr` in that loop: the generated functor `Invoke` and the benchmark's static
target are marked for aggressive inlining. The remaining five `blr`
instructions are runtime/access setup calls before iteration. Without an
inline hint on an arbitrary user method, the generated method-group forwarding
call remains direct-to-static but may still be visible as one entity-loop
`blr`; the delegate Invoke indirection is removed in either case.

The design and fallback details are in
[the focused experiment report](delegate-interception.md), with the
[Roslyn interceptor specification](https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md)
as the required compiler reference.

## Validated candidates awaiting a decision

`perf/split-generated-read-write-drivers` is the only candidate from the
`fb6c8d0` round that did not regress its write guardrail. Before merging it,
add a direct generated read-only workload: the change primarily targets that
path, while the current BDN evidence measures write-heavy Movement4.

## Required entry fields

Every future experiment records:

- baseline and candidate commits;
- exact operation, public API path, entity count and data layout;
- runtime, architecture, GC mode and BenchmarkDotNet job;
- correctness/checksum evidence;
- Mean, Error, StdDev, Allocated and candidate/baseline ratio;
- JIT code size and relevant complete instruction summary;
- decision: accepted, rejected or inconclusive, with the reason.
