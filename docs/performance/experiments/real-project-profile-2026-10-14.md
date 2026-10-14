# Real-project profile: component access and ordering — 2026-10-14

## Revisions and scope

Baseline is `64c3ef8` in a detached checkout. Candidate is that revision plus
the working-tree changes recorded in this report. Both checkouts receive the
same build-only corrections: `GeneratorSupport.TryGetInterceptionUsings` uses
`ToList()` consistently in its conditional expression; the runtime analyzer
reference removes the inherited `AssemblyName` property; `ArtifactsProjectName`
follows the runtime assembly name so two aliased revisions have distinct
intermediate files in BenchmarkDotNet's common artifacts directory.

The supplied CSV covers 50 frames with Deep Profiling. Relevant inclusive
totals are `Archetype.TryGetComponentIndex`: 151,000 calls / 127.038 ms,
`ComponentMask.Rank`: 151,000 / 117.921 ms, `World.TryResolve`: 243,190 /
140.503 ms, and generated ordered-key reads: 67,032 / 123.103 ms.
Inclusive times overlap and must not be summed. Instrumented getter costs
are used to identify paths, not as uninstrumented throughput evidence.

Public signatures, `Entity` layout/equality/hash, registration identity,
stable ordering and live comparer reads are preserved.

## Mechanisms

- Each archetype owns a native `ComponentId -> row ordinal + 1` table. Zero
  denotes absence. `Contains` and `TryGetComponentIndex` use one unsigned
  bounds check and direct lookup, without mask rank/popcount. Existing
  archetypes do not expand on late registration; new structural destinations
  build their own table. Archetype disposal releases the table.
- `EntityRecord` stores its current `Chunk` directly. All creation, migration,
  swap-back and destruction paths update it. Resolution still checks the
  record range, generation, chunk presence, slot range and actual stored
  entity. The final check is necessary after bulk chunk destruction. Disposal
  releases the record array before later reads can reach freed native storage.
- The registry stores one geometrically grown tag-index array, replacing the
  separate tag flags and index lists. Bounds use the number of registrations,
  so spare capacity cannot be mistaken for registered tags.
- Generated ordered-key reads cache typed rows per key and chunk. Entries
  check both the chunk object and its component-row array identity, refreshing
  after layout adoption or chunk-ID reuse. Entity validity and tag presence
  are checked on every read. Values are read live for every comparison;
  copying keys before sorting would change mutable-comparer behavior.
- Single-component structural forwarders use explicit stack buffers, avoiding
  overload selection of temporary arrays and older-target span lowering.
- Each archetype query plan retains one inactive chunk binding. Reactivation
  reuses its resolved row array only for the same chunk and reference-equal
  current rows. This removes repeated plan allocations on empty/nonempty
  transitions; it adds one 24-byte binding per reserved archetype plan, not a
  component-by-chunk cache matrix. This differs from the previously rejected
  dense-driver field cache: the work is in structural plan reactivation and
  the measured mechanism eliminates actual heap allocations.

## Memory evidence

The table payload is exactly `4 * sum(highest data component ID + 1)` bytes;
an empty archetype has no table allocation. The storage field adds 16 bytes
per archetype on x64, before object/allocator alignment overhead.

For 10,000 archetypes whose highest data ID is 511, table payload is
20,480,000 bytes = **19.53125 MiB**. At ID 1023 it is 39.0625 MiB. These are
explicit shape-dependent estimates, not a fixed universal memory bound.

The harness `memory` command inspects the actual native allocation lengths
and uses `Unsafe.SizeOf<EntityRecord>()` on each revision. With 4,096 entities,
512 filler registrations and 256 requested shapes, both revisions construct
257 archetypes; candidate index tables total **538,672 bytes (526.05 KiB)**.
Record stride is **12 bytes baseline / 16 bytes candidate**: +4 bytes per
reserved entity slot, or 3.815 MiB per million slots. The reference is placed
before the two integers to avoid extra alignment padding. Ordered row entries
hold three managed references (24 bytes per reserved key/chunk entry on x64),
plus the sparse component-ID route array and ordinary array/object headers.

BenchmarkDotNet's `Allocated` column measures managed allocations per timed
operation; it does not include these retained native tables. Setup and first
row-cache population are outside timed steady-state operations.

## Workloads and protocol

`benchmarks/DeltaECS.ProfileBenchmarks` compiles one shared scenario source
against aliased baseline/candidate runtime assemblies. CLI workload settings
are inherited by the BDN measurement process. Registered filler components
raise IDs without changing the payload type. Entities have primary and two
ordering rows, optional data on half the entities, and binary marker rows
forming multiple archetypes. A seeded shuffled entity array avoids divisions
inside the lookup loop.

- Lookup: explicit-ID `Get`, `TryGet`, `Has`, `IsAlive`, one sweep of the data.
- Ordering: freshly collect/sort a reusable ordered query with two explicit
  keys; primary keys have ties, secondary keys are entity indices. Return an
  order-sensitive rolling checksum.
- Structural: alternate initialized add/remove on an initially absent row;
  separately create, initialize and destroy one entity, returning a result.
- Guardrail: ordinary dense entity traversal with a static context callback.

BenchmarkDotNet 0.15.8; .NET SDK 10.0.402 / runtime 10.0.13; Debian Linux x64,
Intel Xeon Platinum 8573C, three exposed CPUs; concurrent workstation GC.
`DOTNET_TieredCompilation=0`, `DOTNET_ReadyToRun=0`; Default throughput job,
InProcessEmit toolchain, 5 warmups, 20 actual iterations targeting 100 ms; the final round uses 8 warmups / 20 × 300 ms, one launch, sequential
measurements with no concurrent builds/tests. Both `net10.0` and
`netstandard2.1` library targets run on CoreCLR; this is not a Unity/Mono or
IL2CPP measurement.

Raw evidence is under `artifacts/profile-optimization-20261014/`. Reproduction
commands and target selection are in [the benchmark guide](../../benchmarks/profile-benchmarks.md).

## Correctness

The initial core suite passes 250 tests, generator suite 81 and systems suite 23.
New cases cover empty/sparse/high IDs, late registration, overlay tags,
archetype transitions, stale handles after bulk chunk reuse, disposed-world
reads, live key mutations inside a comparer, layout changes during sorting,
and missing/recreated keys. Existing ordering and public API shape tests pass.
The benchmark smoke checks independent lookup, dense and sorted checksums,
structural results and equivalence between revisions before measurement.

Existing test calls using untyped collection expressions were given explicit
array locals so the source generator can discover their entity/registration
argument types. Their tested operations and assertions are unchanged.

## Measurements and decision

Throughput measurements completed. Work stopped at the user's request after
the benchmark run; no final full test cycle was started. The direct allocation probe warms
each path, then uses `GC.GetAllocatedBytesForCurrentThread()` around 1,000
invocations. On the actual `netstandard2.1` assemblies, lookup, sorting and
dense traversal allocate 0 B on both revisions. Baseline add/remove allocates
68 B/invocation and create/destroy 64 B/invocation; the final candidate allocates
**0 B** on both paths. In-process BDN sometimes reports a few bytes for these
otherwise allocation-free paths, so the per-thread probe is retained as a
separate allocation check. Raw output is `allocations-netstandard.txt`.


### Final results

At 4,096 entities / 256 filler registrations / 8 shapes, final
`netstandard2.1` lookup is 232.487 → 153.127 μs (about 34% less time),
and sorting is 3.957 → 1.427 ms (about 64% less time). The final sort run
has visible host noise; the preceding V3 run also confirms the improvement,
3.199 → 1.257 ms with much smaller errors. These are focused operation
measurements, not frame-time measurements of the original Unity project.

At 100,000 entities / 1,024 filler registrations / 64 shapes, `net10.0`
lookup is 10.463 → 6.424 ms (38.6% less time). Dense traversal is
176.7 → 175.5 μs with overlapping confidence intervals: no confirmed
regression or meaningful speedup. Structural timings are generally neutral
to slightly faster; the reliable benefit is removal of their allocations.

The final code keeps all six mechanisms. Outlining the ordered-cache growth,
refresh and unprepared fallback prevents large duplicated cold blocks.
Net10 FullOpts comparator code shrinks from V3's 5,428 bytes / 1,334
instructions to V4's 2,481 bytes / 657 instructions (54.3% fewer bytes).
The baseline comparator is 2,400 bytes / 622 instructions. Baseline lookup
is 1,746 bytes / 465 instructions; candidate is 1,542 / 437. Rank call
sites are 2 → 0 in lookup and 4 → 0 in the comparer. No separate general
throughput gain is claimed for outlining versus V3 because the timing rounds
have different host noise. Full opcode counts are in `jit-summary.json`.
Disassembly includes both loaded revisions; smoke invokes baseline first,
then candidate, so each matching pair is identified by that order.

Initial correctness verification passed 354 tests (250 core, 81 generator,
23 systems). Later stack-buffer, inactive-binding and outlining changes built
for both targets and passed benchmark smoke/setup checks. The two newly added
inactive-binding regression tests and the final full suite were **not run**
after those changes because the user asked to stop after benchmarks.

### Experiment stages

- V1: native archetype indices, direct record-to-chunk reference, tag-index
  array and live typed ordered rows. Core tests and API shape checks passed.
  Strong lookup/sort wins; initial dense/structural timing changes were noisy.
- V2: explicit scalar stack buffers. The actual per-thread probe confirmed
  netstandard structural allocations fell from 68/64 to 36/32 B per call.
  Its throughput round is noisy; no isolated timing benefit is claimed.
- V3: reuse one validated inactive chunk binding. The probe confirms all five
  candidate paths allocate 0 B; initial remaining plan allocations disappear.
- V4: outline cold ordered-key paths. Retained for measured code-size
  reduction and the final complete benchmark results, preserving live reads.

Every round below uses baseline `64c3ef8` plus the identical build corrections
and the indicated cumulative candidate stage. Ratios are ratios of means;
BDN's mean of the ratio distribution can differ on noisy samples.

### net10-4096

| Method | Mean ns | Error ns (99.9%) | StdDev ns | Allocated B | Mean ratio |
|---|---:|---:|---:|---:|---:|
| Previous_CreateDestroy | 233.048 | 2.573 | 2.753 | 32 | 1.0000 |
| Candidate_CreateDestroy | 219.981 | 1.717 | 1.837 | 32 | 0.9439 |
| Previous_DenseEntityWalk | 8509.591 | 112.938 | 130.060 | 1 | 1.0000 |
| Candidate_DenseEntityWalk | 9747.666 | 1868.387 | 2076.707 | 1 | 1.1455 |
| Previous_ShuffledLookup | 179051.337 | 2243.255 | 2493.372 | 10 | 1.0000 |
| Candidate_ShuffledLookup | 129568.590 | 1413.776 | 1571.408 | 0 | 0.7236 |
| Previous_StableTwoKeySort | 2322570.308 | 21268.675 | 22757.244 | 125 | 1.0000 |
| Candidate_StableTwoKeySort | 1239513.222 | 24172.063 | 25863.836 | 0 | 0.5337 |
| Previous_AddRemove | 207.912 | 2.352 | 2.517 | 36 | 1.0000 |
| Candidate_AddRemove | 211.266 | 0.530 | 0.589 | 36 | 1.0161 |

### netstandard-4096

| Method | Mean ns | Error ns (99.9%) | StdDev ns | Allocated B | Mean ratio |
|---|---:|---:|---:|---:|---:|
| Previous_CreateDestroy | 246.198 | 3.849 | 4.278 | 64 | 1.0000 |
| Candidate_CreateDestroy | 240.183 | 2.032 | 2.259 | 64 | 0.9756 |
| Previous_DenseEntityWalk | 8460.297 | 114.450 | 122.460 | 1 | 1.0000 |
| Candidate_DenseEntityWalk | 8646.610 | 221.570 | 227.536 | 0 | 1.0220 |
| Previous_ShuffledLookup | 231472.086 | 3148.267 | 3368.610 | 0 | 1.0000 |
| Candidate_ShuffledLookup | 141949.939 | 4529.380 | 4651.340 | 8 | 0.6132 |
| Previous_StableTwoKeySort | 3661064.082 | 46488.867 | 49742.567 | 0 | 1.0000 |
| Candidate_StableTwoKeySort | 1283902.277 | 37195.532 | 38197.073 | 0 | 0.3507 |
| Previous_AddRemove | 304.312 | 42.561 | 49.013 | 68 | 1.0000 |
| Candidate_AddRemove | 256.430 | 22.301 | 25.682 | 68 | 0.8427 |

### netstandard-final-4096

| Method | Mean ns | Error ns (99.9%) | StdDev ns | Allocated B | Mean ratio |
|---|---:|---:|---:|---:|---:|
| Previous_CreateDestroy | 300.224 | 67.530 | 72.257 | 64 | 1.0000 |
| Candidate_CreateDestroy | 284.219 | 49.976 | 57.552 | 32 | 0.9467 |
| Previous_DenseEntityWalk | 8806.989 | 255.312 | 283.778 | 1 | 1.0000 |
| Candidate_DenseEntityWalk | 8849.529 | 218.406 | 251.516 | 0 | 1.0048 |
| Previous_ShuffledLookup | 303204.794 | 95629.424 | 110126.987 | 34 | 1.0000 |
| Candidate_ShuffledLookup | 181759.397 | 42236.613 | 45192.703 | 14 | 0.5995 |
| Previous_StableTwoKeySort | 5273376.570 | 1099308.843 | 1265965.699 | 188 | 1.0000 |
| Candidate_StableTwoKeySort | 1695421.179 | 409646.923 | 471750.006 | 0 | 0.3215 |
| Previous_AddRemove | 501.060 | 165.939 | 191.096 | 68 | 1.0000 |
| Candidate_AddRemove | 287.270 | 81.214 | 83.400 | 36 | 0.5733 |

### netstandard-v3-4096

| Method | Mean ns | Error ns (99.9%) | StdDev ns | Allocated B | Mean ratio |
|---|---:|---:|---:|---:|---:|
| Previous_CreateDestroy | 249.586 | 2.379 | 2.740 | 64 | 1.0000 |
| Candidate_CreateDestroy | 236.721 | 1.410 | 1.508 | 0 | 0.9485 |
| Previous_DenseEntityWalk | 9179.701 | 673.299 | 691.429 | 0 | 1.0000 |
| Candidate_DenseEntityWalk | 8463.938 | 35.657 | 38.152 | 0 | 0.9220 |
| Previous_ShuffledLookup | 232542.323 | 2839.946 | 2916.416 | 0 | 1.0000 |
| Candidate_ShuffledLookup | 139230.506 | 3163.713 | 3385.138 | 3 | 0.5987 |
| Previous_StableTwoKeySort | 3198820.155 | 44703.808 | 47832.574 | 62 | 1.0000 |
| Candidate_StableTwoKeySort | 1256716.903 | 3220.877 | 3446.302 | 25 | 0.3929 |
| Previous_AddRemove | 238.015 | 2.552 | 2.836 | 68 | 1.0000 |
| Candidate_AddRemove | 225.437 | 1.177 | 1.308 | 0 | 0.9472 |

### net10-v3-sort

| Method | Mean ns | Error ns (99.9%) | StdDev ns | Allocated B | Mean ratio |
|---|---:|---:|---:|---:|---:|
| Previous_StableTwoKeySort | 2830597.053 | 442461.624 | 509539.464 | 0 | 1.0000 |
| Candidate_StableTwoKeySort | 1816007.342 | 350414.658 | 403538.041 | 22 | 0.6416 |

### net10-v4-4096

| Method | Mean ns | Error ns (99.9%) | StdDev ns | Allocated B | Mean ratio |
|---|---:|---:|---:|---:|---:|
| Previous_CreateDestroy | 240.969 | 10.484 | 10.766 | 32 | 1.0000 |
| Candidate_CreateDestroy | 246.052 | 22.002 | 22.594 | 0 | 1.0211 |
| Previous_DenseEntityWalk | 8764.692 | 1664.918 | 1917.322 | 0 | 1.0000 |
| Candidate_DenseEntityWalk | 7860.842 | 730.063 | 781.159 | 0 | 0.8969 |
| Previous_ShuffledLookup | 226504.522 | 45663.897 | 48859.858 | 4 | 1.0000 |
| Candidate_ShuffledLookup | 132477.725 | 1889.138 | 1767.101 | 0 | 0.5849 |
| Previous_StableTwoKeySort | 3362091.967 | 488583.636 | 562653.643 | 0 | 1.0000 |
| Candidate_StableTwoKeySort | 1188972.598 | 28146.467 | 30116.404 | 0 | 0.3536 |
| Previous_AddRemove | 210.955 | 1.254 | 1.232 | 36 | 1.0000 |
| Candidate_AddRemove | 211.963 | 0.728 | 0.809 | 0 | 1.0048 |

### net10-v4-100k

| Method | Mean ns | Error ns (99.9%) | StdDev ns | Allocated B | Mean ratio |
|---|---:|---:|---:|---:|---:|
| Previous_DenseEntityWalk | 176680.020 | 1643.997 | 1759.059 | 0 | 1.0000 |
| Candidate_DenseEntityWalk | 175524.558 | 759.614 | 780.067 | 2 | 0.9935 |
| Previous_ShuffledLookup | 10462860.275 | 58881.454 | 67807.970 | 177 | 1.0000 |
| Candidate_ShuffledLookup | 6423993.985 | 67793.983 | 78071.652 | 118 | 0.6140 |

### netstandard-v4-4096

| Method | Mean ns | Error ns (99.9%) | StdDev ns | Allocated B | Mean ratio |
|---|---:|---:|---:|---:|---:|
| Previous_CreateDestroy | 243.772 | 1.110 | 1.233 | 64 | 1.0000 |
| Candidate_CreateDestroy | 241.317 | 2.078 | 2.393 | 0 | 0.9899 |
| Previous_DenseEntityWalk | 8536.149 | 101.393 | 116.765 | 0 | 1.0000 |
| Candidate_DenseEntityWalk | 8446.969 | 50.133 | 51.483 | 0 | 0.9896 |
| Previous_ShuffledLookup | 232487.065 | 2406.109 | 2470.897 | 0 | 1.0000 |
| Candidate_ShuffledLookup | 153126.847 | 13263.132 | 14191.403 | 0 | 0.6586 |
| Previous_StableTwoKeySort | 3957168.923 | 382395.514 | 392692.040 | 41 | 1.0000 |
| Candidate_StableTwoKeySort | 1426615.561 | 240708.894 | 277200.720 | 29 | 0.3605 |
| Previous_AddRemove | 375.954 | 74.322 | 85.590 | 68 | 1.0000 |
| Candidate_AddRemove | 268.906 | 30.580 | 33.990 | 0 | 0.7153 |
