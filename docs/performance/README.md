# DeltaECS performance ideas

This document records candidate hot-path work. Ideas here are not implemented
or measured unless a section says otherwise. Any change must preserve the
validated public binding API, dense/tagged query semantics, lifetime barriers,
and the benchmark comparison contract.

## Current evidence

- `DenseChunkAccessor.GetRow` and `DenseChunkScope.GetRow` already use the
  cached query-row index and the internal unchecked managed-array cast.
- The remaining binding path performs `EnsureCurrent`, query ownership
  validation, and a `QueryComponentIndex -> ComponentRows` lookup for every
  requested row on every chunk.
- Prior JIT inspection showed bounds checks remain in the entity loop when rows
  are accessed as `Span<T>[i]`. No new measurement is implied by this note.

## Candidate 1: query-bound prepared row access

The query should own the bindings it creates and validate them once at the
start of `World.Query`. The current query-owned registration/validation is the
first step; the next step is to prepare a compact per-archetype row packet:

```text
QueryHandle/CachedQuery
    binding token -> query row
DenseArchetypePlan
    binding token -> physical component row
Chunk execution
    prepared physical row -> component array
```

The dense accessor/cursor would then use a trusted internal path for bindings
that were validated by the current query invocation. The public checked path can
remain available for foreign/default binding rejection. This removes repeated
`EnsureCurrent`, ownership checks, and query-row-index lookup from Movement2,
Movement4, and other multi-row workloads.

Required proof:

- foreign, default, and deliberately corrupted internal bindings still fail
  before the callback or through the checked compatibility path;
- archetype plan refresh after a new archetype remains correct;
- read/write dirty tracking remains precise;
- JIT output shows the hot path loading the prepared row directly.

## Candidate 2: remove inner-loop bounds checks

In a separately measured internal experiment, take row references once per
chunk with `MemoryMarshal.GetReference` and advance with `Unsafe.Add`. Keep the
reverse traversal semantics and observable checksum unchanged. This is unsafe
and must be limited to a trusted dense packet; do not expose raw pointers in
the public API.

Required proof: JIT disassembly must show the bounds checks disappear, and a
small Release microbenchmark must beat the normal span-indexed loop without
changing the result.

## Implemented: direct active chunk references

The active-chunk path currently performs:

```text
active index -> chunk index -> List<Chunk> -> Chunk
```

The active list now keeps a parallel dense `Chunk[]` alongside the reverse
position/index arrays. Dense and tagged query traversal reads the chunk directly
instead of resolving `active index -> chunk index -> List<Chunk> -> Chunk`.
The physical `_chunks` list and index-based structural paths remain unchanged.

The remaining question is measured impact in sparse and full active-list cases;
the extra reference array and maintenance may outweigh the gain when every
chunk is active.

## Candidate 4: trusted dense execution path

The handle query path still creates/invalidates an accessor and invokes a
delegate for each chunk. A future dense cursor or internal execution packet
could validate lifetime once per query and avoid per-chunk accessor-id and
dispose work. Tagged queries and the public stale-accessor contract must keep
their checked path.

## Benchmark fairness note

Comparative callbacks should accumulate checksums in a local variable and write
the result to state once per callback/chunk. Repeated writes such as
`current.Sum += ...` inside Delta callbacks must not be compared with local
accumulators in competitor callbacks. This is benchmark infrastructure, not an
ECS kernel optimization, and should be corrected before interpreting movement
ratios.

## Order of work

1. Fix benchmark accumulator parity.
2. Measure the prepared row packet against the current binding path.
3. Test `MemoryMarshal.GetReference` + `Unsafe.Add` using JIT disassembly.
4. Measure direct active chunk references and trusted dense execution only if
   the previous changes leave chunk-boundary overhead dominant.
