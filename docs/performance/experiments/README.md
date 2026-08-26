# Optimization experiment ledger

This is the authoritative ledger for DeltaECS performance experiments. Record
every measured candidate here whether it is accepted, rejected or inconclusive.
An entry is not a performance claim unless it names a workload and evidence.

Do not repeat a rejected or inconclusive mechanism unless the new experiment
states what changed: runtime, architecture, workload, implementation mechanism
or measurement quality. Raw BenchmarkDotNet and JIT output stays under ignored
`artifacts/`; durable conclusions and reproduction details belong here or in a
linked focused report.

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
| Generated rows | Trusted generated query slots/row preparation | `0ce7a95`, `022a1f5` | Retained after Movement4 comparison |
| Query access setup | Prebuild `ComponentId -> ordinal/type` read routes and promote the validated route to write | `e9ccffe`, `fb6c8d0` | Movement4 delegate: about 15% faster at 100 entities, about 2% at 1k/10k, neutral at large sizes; affected helper JIT 1584 B to 888 B |
| Structural create | Batched entity creation kernel | `7540054` | Retained measured structural improvement |
| Structural destroy | Ordered/list destroy kernels with whole-chunk path | `c63b492`, `addc9b4`; [destroy evidence](../destroy-kernels-v1.md) | 49% to 91% faster for measured batches of 8 to 4096 |
| Dense sweep | Combined L0-L3 internal cleanup | `9b0f42e`, `c9fac9a`, `92ac844`; [sweep summary](../l0-l3-sweep.md) | ARM64 JIT block 1408 B to 1084 B; throughput mostly neutral, retained for simpler generated code |

## Rejected

| Candidate | Evidence/result | Reason not retained |
| --- | --- | --- |
| Local slot-count/index caches and direct iterator field access | [slot matrix](../l0-b-slot-iterator-experiments.md) | No JIT change |
| `readonly`/`in` binding helper variants | [slot matrix](../l0-b-slot-iterator-experiments.md) | Invalid span/ref escape semantics |
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

## Inconclusive or superseded evidence

| Candidate | Evidence | Interpretation |
| --- | --- | --- |
| Matching-plan V1 pre-sized managed arrays | `6d3ecdf`; [V1 JIT](../matching-archetypes-v1-jit.md) | Same 1004 B/251-instruction method; allocation shape improved but throughput was not measured independently |
| Dense 48/50-candidate assembly sweep | `c9fac9a`, `92ac844` | Code size improved substantially, but short 100k throughput was near noise; only merged source is authoritative |
| Generic cached array versus `Span<T>` | Historical comparison | JIT was effectively identical; direct span form was kept for simplicity |
| Large native ECS buffer storage | `bc62b9f`, `d03fa85` | Some benchmark signals improved while generated code grew; retained source later evolved, so old isolated ratios are not current claims |
| Split generated read/write drivers | `4a8db12`; [evidence](perf-split-generated-read-write-drivers.md) | Write guardrail removed one branch/compare and 8 B; Functor improved 1.57% at 100, all other tested write cases were neutral. Direct component-bearing read-only evidence is still missing |
| Metalama layer-major chunking | `8040b2e`; [chunked experiment](../../../tools/DeltaECS.LayeredPipeline/README.md) | Promising cache signal, but the measurement used separate flat and chunked runs and the tile changes execution order across entities; not an ECS runtime decision yet |
| Prepared generated access routes | `c6b819a`; [profile evidence](prepared-generated-access.md) | The generated tree no longer contains the old runtime-type resolver descendants, but calibration R² was `0.6545` and no paired BDN result exists; keep as an experiment until throughput is measured |

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
