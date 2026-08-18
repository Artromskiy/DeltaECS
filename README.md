# DeltaECS

DeltaECS is a standalone, type-erased ECS kernel for DeltaEngine. Its primary
design goals are predictable data locality, very fast batch operations, cheap
temporary state, and generated typed ergonomics without making the storage
kernel depend on CLR component types.

The public namespace is `DVG.ECS`; test and benchmark code uses the nested
namespaces `DVG.ECS.Tests` and `DVG.ECS.Benchmarks`. Project and assembly names
remain `DeltaECS*` for reference compatibility, so consumers only need a
source-level namespace update.

Performance is a release criterion, not a slogan. A claim that DeltaECS is
faster than Arch, Unity Entities, Flecs, EnTT-like designs, or another ECS must
name the workload, hardware, data layout, compiler/runtime, entity count, and
memory cost. Regressions are rejected by benchmarks checked into this project.

## Ownership and boundaries

This directory is owned by the ECS workstream. It must remain buildable and
benchmarkable without DeltaEngine or DeltaRender.

- The kernel knows `ComponentId`, `ComponentLayout`, byte offsets, alignment,
  archetypes, chunks, entities, queries, and access modes.
- The initial kernel does not use generic component storage and does not use
  `System.Type` as component identity.
- A later Roslyn generator may expose typed C# systems and component accessors,
  but generated code lowers to the same ID/layout API.
- KibiHex.Maths can be used by typed fixtures later; the kernel does not depend
  on it.
- Arch is a migration baseline and benchmark competitor, not a dependency.

## Core model

### Identity and layouts

Use two identities:

- a dense runtime `ComponentId` for hot paths;
- a stable schema identifier for serialization, tooling, and hot reload.

`ComponentLayout` describes size, alignment, stride, storage class, field
metadata when available, move/copy/drop policy, and serialization hooks.
Production uses direct `ArrayRows` only: registration carries a `RuntimeType` as
cold metadata for `Array.CreateInstance`. A row may contain values, structs with
reference fields, or direct object references; all use the same `Array[]`
storage path. The old byte implementation is benchmark-only reference code and
is not a second production backend. `Type` is never component identity and is
not consulted by cached query iteration. Entity handles contain an index and
generation; a location table resolves them to archetype, chunk, and entity slot
index.

### ArrayRows and the legacy baseline

Every dense component row is stored directly as an element array in `Array[]`;
there are no row wrapper classes, `IColumn` objects, or virtual row dispatch.
Two different `ComponentId` values may use the same CLR element type, but each
gets its own array and its own mask bit. Value rows store elements inline;
reference-type rows store contiguous CLR references whose target objects remain
GC-managed. `ComponentLayout(Type)` deliberately does not invent a byte size for
managed layouts: `Size` and `Stride` are zero for Type-backed ArrayRows layouts.
The benchmark-only byte reference uses explicit size/alignment metadata;
production rejects layouts without a runtime element type.

Registering an already-used `SchemaId` returns the original `ComponentId` only
when the complete layout, including runtime type, is equal. A different layout
with the same schema is an error. Virtual components therefore use distinct
schema IDs.

### Three storage classes

1. `Dense`: stable, hot data stored as SoA component rows inside archetype chunks.
2. `Overlay`: frequently toggled tags and short-lived components which do not
   participate in the archetype signature.
3. `Stream`: frame-lifetime events and commands allocated from a resettable
   arena rather than represented as persistent components.

Do not add automatic promotion between these classes in the first versions.
Storage is an explicit part of the layout so performance remains predictable.

### Archetypes and chunks

An archetype is keyed by a 256-bit `ComponentMask`; a component row index is
the mask rank of its `ComponentId`. Chunks own entity slots and SoA component
rows. A logical entity column is the set of components at one entity slot and
need not be physically contiguous. Chunk capacity is configurable; the legacy
byte implementation is isolated to benchmark comparison code.

Structural changes are deferred through command buffers and played back in
batches. Add/remove transitions are cached as archetype graph edges. Batch
create, destroy, add, remove, move, clone, and set operations are first-class;
the implementation must not loop through the public single-entity API.

### Overlay tags and temporary components

Tags use a two-level sparse index:

```text
TagId -> active ChunkId -> bit mask of entity slots
```

Adding or removing a tag changes a bit and never moves the entity. Queries
combine masks with word operations and iterate set bits. Full and empty masks
have dedicated fast paths.

Data-bearing overlays use a lazily allocated chunk-local sidecar component row plus a
presence mask. They must not be implemented as one allocation per entity.
Very sparse payloads may get a separate storage policy only after benchmarks
show that sidecar pages waste significant memory.

### Queries and batch access

Queries contain dense `All/Any/None`, overlay `All/Any/None`, and optional
change predicates. Reused/system queries cache matching archetypes and active
chunks; ad-hoc queries may remain uncached.

The hot API yields chunks and component rows, not one entity at a time. The kernel
needs explicit read/write leases so the scheduler and change tracker know the
access set. Raw references and spans must not escape a lease or survive a
structural playback/sort point.

### Change tracking

The default mechanism is a monotonically increasing world tick and a version
per `(ChunkId, ComponentId)`. A write lease marks the component row when that chunk is
actually yielded, not when the query object is constructed. Each consumer keeps
its own last-seen tick; there is no globally cleared dirty flag.

Entity-slot tracking is opt-in:

- full write span: mark the whole component row;
- contiguous batch: record a dirty range;
- sparse GPU-mirrored writes: use a slot bitset;
- multi-threaded writers: collect job-local slot masks and merge at the barrier.

Keep semantic `ChangeVersion` separate from `OrderVersion` and topology changes.
Sorting entity slots changes storage order without pretending every component value was
modified.

### Ordering and locality

Default dense iteration is already sequential. Optional locality features are:

- chunk partition/group keys such as world cell, material, LOD, or simulation
  island;
- periodic physical repacking using a sort key such as Morton order;
- consistent permutation of entity slots, every component row, overlay masks,
  dirty slot masks, and the entity location table.

Sorting is a maintenance job driven by churn/utilization thresholds, not a
per-frame default.

### Events and scheduling

Event streams are append-only per producer, merged deterministically at stage
boundaries, double-buffered where readers need the previous frame, and reset in
bulk. No allocation is allowed per event.

The scheduler consumes read/write `ComponentId` sets generated for systems,
builds a dependency graph, and schedules non-conflicting chunk jobs. Structural
and overlay-presence changes are applied at explicit barriers. Deterministic
single-threaded execution remains available for tests and debugging.

## Roslyn code generation

The kernel ships first. The generator then maps authoring types and system
signatures to stable schema IDs, dense runtime IDs, layouts, query descriptors,
and direct component-row loops. It must generate code rather than use reflection or
runtime generic dispatch in hot paths.

Generated access semantics should distinguish `in`, `ref`, write-only, optional,
overlay, event reader/writer, and structural command access. Diagnostics must
reject escaping references, conflicting aliases, managed fields in unmanaged
layouts, unsupported alignment, and ambiguous schema IDs.

## Performance gates

Benchmarks must report time, throughput, allocations, resident memory, chunk
utilization, and relevant hardware counters when available. Include at least:

- dense iteration over 1, 2, 4, and 8 component rows;
- cached and ad-hoc queries across few and many archetypes;
- batch create/destroy for 1K, 100K, and 1M entities;
- batch add/remove transitions;
- random and coherent overlay-tag churn;
- temporary overlay payload creation/removal;
- event production and fan-out;
- changed-chunk and sparse dirty-slot consumption;
- single-thread and scheduler scaling;
- compaction and sorting under controlled churn.

Compare against the current Arch integration first. Add separate reproducible
harnesses for Unity Entities/Burst and native competitors rather than presenting
in-process .NET numbers as an apples-to-apples Unity result.

Hot paths must allocate zero managed memory after warm-up. Benchmarks include
correctness checks so dead-code elimination or skipped work cannot produce false
wins.

## Implementation order

1. Standalone solution, entity allocator, layout registry, unmanaged slabs.
2. Dense archetypes/chunks, location table, batch create/destroy.
3. Cached dense queries and chunk/component-row leases.
4. Batched structural command playback and transition cache.
5. Overlay tag masks and filtered chunk iteration.
6. Change versions, order versions, dirty ranges, opt-in dirty-slot bitsets.
7. Event streams.
8. Sidecar overlay payloads.
9. Scheduler and deterministic barriers.
10. Roslyn generator/analyzers and DeltaEngine adapter.
11. Partitioning, compaction, sorting, and GPU-oriented export.

The first delivery ends after step 5 with tests and BenchmarkDotNet baselines.
Do not start the scheduler, GPU ECS, or typed generator until the type-erased
storage and query invariants are covered by randomized tests.

## Definition of done for the first delivery

- Standalone build and tests pass on macOS, Windows, and Linux.
- `Type` is not component identity and is not required in the hot loop; it is
  permitted only as cold ArrayRows creation metadata.
- Entity generations catch stale handles.
- Batch create/destroy and archetype transitions preserve all component bytes.
- Cached queries remain correct after new archetypes appear.
- Overlay tags do not cause archetype transitions.
- Cached dense hot iteration allocates zero managed memory after warm-up;
  structural batch APIs are correct but their current allocations are measured
  and listed as remaining work below.
- Benchmarks compare the same operations with the current Arch implementation.
- Public invariants and benchmark reproduction commands are documented.

## Performance-pass status (2026-08-18)

The cached dense hot path now has an allocation-free API:
`World.CreateQuery` creates a reusable `QueryHandle`; the ref-state query
overload invokes a `DenseChunkLeaseView` ref struct and uses cached
component-index mappings. The structural lease guard covers the synchronous
query and is released in `finally`; the view cannot escape the callback.
The original `Action<DenseChunkLease>` API remains available for compatibility.

The primary dense benchmark now uses `World.QueryChunks`, a stack-only cached
chunk enumerator. It holds the mutation lease for the whole synchronous pass,
returns the cached row-index plan, and avoids callback dispatch plus per-chunk
lease disposal. The callback query API remains for general correctness and
filtered/tagged access; it is not the primary dense performance lane.

The benchmark/use-site dispatch selects 1/2/4/8 component-row loops once per
benchmark operation. Component-row count and active-slot state are chunk
invariants, so the inner slot loop has no per-slot component-count branches.
This preserves the type-erased kernel: specialization exists only at the
use-site, not in storage or component pools.

The staged `HotPathProfile` measured dispatch at `363.0 ns`, cached two-component-row
lookup at `715.1 ns`, and the generic two-component-row slot loop at `59.0 us`, all at
`0 B/op` in the BDN child process. This separates sub-microsecond query/component-row
overhead from the slot work before comparing the full lanes.

The reproducible distinct-type dense comparison command is:

```text
dotnet build benchmarks/DeltaECS.Benchmarks/DeltaECS.Benchmarks.csproj -c Release --no-restore
dotnet benchmarks/DeltaECS.Benchmarks/bin/Release/net8.0/DeltaECS.Benchmarks.dll distinct --filter '*' --warmupCount 3 --iterationCount 5 --launchCount 1
```

The harness uses BenchmarkDotNet `MemoryDiagnoser`, Amount `10_000` and
`100_000`, distinct CLR types, and 1, 2, 4, and 8 component rows. Results below
are Apple M4 Pro, macOS 26.5.2, .NET 8.0.29 / Arm64 RyuJIT,
`WarmupCount=3`, `IterationCount=5`. Means are microseconds; ratios are relative
to Arch, lower is better. `-` is BDN's zero-allocation display.

| Amount | Rows | Array mean / ratio / alloc | Legacy mean / ratio / alloc | Arch mean / alloc | Friflo mean / ratio / alloc |
|---:|---:|---:|---:|---:|---:|
| 10K | 1 | 2.999 / 0.80 / - | 2.784 / 0.74 / - | 3.744 / - | 5.298 / 1.42 / - |
| 10K | 2 | 5.741 / 0.96 / - | 5.753 / 0.96 / - | 6.001 / - | 6.448 / 1.07 / - |
| 10K | 4 | 11.117 / 1.10 / - | 11.613 / 1.16 / - | 10.081 / - | 10.766 / 1.07 / - |
| 10K | 8 | 30.145 / 0.76 / - | 22.422 / 0.56 / - | 39.710 / - | 38.582 / 0.97 / - |
| 100K | 1 | 29.936 / 0.83 / - | 28.134 / 0.78 / - | 36.242 / - | 51.721 / 1.43 / - |
| 100K | 2 | 57.046 / 0.98 / - | 55.960 / 0.96 / - | 58.375 / - | 65.073 / 1.11 / - |
| 100K | 4 | 111.418 / 1.15 / - | 111.584 / 1.15 / - | 96.901 / - | 105.325 / 1.09 / - |
| 100K | 8 | 280.907 / 0.69 / 1 B | 222.304 / 0.54 / - | 408.087 / 1 B | 384.362 / 0.94 / 1 B |

The dense gate is not passed: Array is slower than legacy in several lanes and
slower than Arch at 10K/4 and 100K/4. BDN reports zero allocations in most
lanes; the 100K/8 group reports `1 B` for Array, Arch, and Friflo and `0 B` for
the byte reference, so this is not claimed as a zero-allocation gate. The direct
cursor removed measured callback overhead, but typed Array-row access across
many chunks remains the main bottleneck. This is an Arch/Friflo baseline, not a
Unity or Burst claim.

The capacity sweep command is:

```text
dotnet benchmarks/DeltaECS.Benchmarks/bin/Release/net8.0/DeltaECS.Benchmarks.dll capacity --filter '*' --warmupCount 2 --iterationCount 3 --launchCount 1
```

The 100K/8 sweep measured Array `431.3/386.0/360.5/347.3 us` and legacy
`258.2/227.2/222.1/218.3 us` at capacities `1024/2048/4096/8192`. Larger
chunks reduce traversal overhead but do not close the gap; no default change is
made without a retained-memory measurement.

Structural benchmark commands are:

```text
dotnet run --no-restore --project benchmarks/DeltaECS.Benchmarks/DeltaECS.Benchmarks.csproj -c Release -- --filter '*Batch*' --warmupCount 3 --iterationCount 5
dotnet run --no-restore --project benchmarks/DeltaECS.Benchmarks/DeltaECS.Benchmarks.csproj -c Release -- --filter '*HotPathProfile*' --warmupCount 3 --iterationCount 5
```

Structural comparison against legacy/Arch/Friflo and retained-live-memory
measurement remain pending. Existing structural numbers were collected before
the ArrayRows-only redesign and must not be presented as current Array backend
measurements.

The correctness suite has 23 passing tests, including randomized transitions,
`DestroyBatch`, managed reference rows, virtual IDs with independent arrays,
schema dedup/conflict, source/target row mapping, reference clearing, stale
handles, lease ownership, immutable query inputs, chunk reuse, TagId validation,
and write-version tracking. `perf` PMU counters and BenchmarkDotNet
disassembly diagnostics are unavailable on this Darwin host, so no
hardware-counter or assembly-level claim is made. Scheduler, events, typed
facade/sourcegen, GPU export, dirty-slot tracking, and engine integration remain
future work.

The P0 correctness fixes are covered by the Release command
`dotnet test tests/DeltaECSTests/DeltaECSTests.csproj -c Release --no-restore`.
Write access advances `WorldTick` and marks only the component rows in the
query's `AllComponents` set when a matching chunk is actually yielded;
`World.HasChangedSince(chunkId, componentId, sinceTick)` is a cold-path query
for that chunk-level version. Read access does not mark rows. Tag APIs and query
normalization reject negative `TagId` values consistently.

The benchmark harness now forces its Arch `ProjectReference` to use
`Configuration=Release`; the serialized build confirms Release outputs for
`Arch.SourceGen`, `Arch`, and the benchmark executable. The validator remains
enabled. The smoke command is:

```text
dotnet benchmarks/DeltaECS.Benchmarks/bin/Release/net8.0/DeltaECS.Benchmarks.dll --filter '*AlgorithmicMovementBenchmarks*' --warmupCount 0 --iterationCount 1 --launchCount 1
```

It completed 24/24 movement cases on Apple M4 Pro, macOS 26.5.2, .NET 8.0.29,
Arm64 RyuJIT. The one-iteration smoke means are not performance claims. It
reported zero managed bytes in all but one lane (`DeltaECS_Movement`, 100K,
`PayloadRows=2`: `1 B`); the sandbox also reports a non-fatal high-priority
permission warning. The existing 3-warmup/5-iteration dense baseline remains
the authoritative performance comparison.
