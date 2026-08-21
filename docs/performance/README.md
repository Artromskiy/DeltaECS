# DeltaECS performance ideas

This document records candidate hot-path work. Ideas here are not implemented
or measured unless a section says otherwise. Any change must preserve the
validated public binding API, dense/tagged query semantics, lifetime barriers,
and the benchmark comparison contract.

## Current evidence

- `DenseChunkAccessor.GetRow` and `DenseChunkScope.GetRow` already use the
  cached query-row index and the internal unchecked managed-array cast.
- Dense query execution now prepares a pooled direct `Array[]` row packet once
  per query invocation and fills it once per visited chunk. `GetRow` performs
  one binding-index lookup into that packet; write access still uses the cached
  physical row index for precise dirty tracking.
- The remaining binding path performs `EnsureCurrent`/ownership validation and
  one prepared-row bounds check for every requested row on every chunk.
- Prior JIT inspection showed bounds checks remain in the entity loop when rows
  are accessed as `Span<T>[i]`. No new measurement is implied by this note.

## Implemented: query-bound prepared row access

The query owns the bindings it creates and validates them at the start of
`World.Query`. Dense execution now prepares a compact per-chunk row packet:

```text
QueryHandle/CachedQuery
    binding token -> query row
DenseArchetypePlan
    binding token -> physical component row
Chunk execution
    prepared query row -> direct component array
```

The dense accessor/cursor uses the prepared packet for the row data while the
public checked path remains available for foreign/default binding rejection.
This removes the physical component-row lookup from Movement2, Movement4,
and other multi-row workloads. Lifetime and ownership validation remain per
`GetRow` until a separate trusted execution path is proven safe.

The implementation uses an `ArrayPool<Array>` scratch packet owned by the
query/enumerator, so it does not allocate or return a packet per chunk. Tagged
paths prepare rows only after the overlay mask accepts the chunk.

Remaining proof:

- foreign, default, and deliberately corrupted internal bindings still fail
  before the callback or through the checked compatibility path;
- archetype plan refresh after a new archetype remains correct;
- read/write dirty tracking remains precise;
- JIT output shows the hot path loading the prepared row directly.

## Candidate 2: remove inner-loop bounds checks

In a separately measured internal experiment, take row references once per
chunk with `MemoryMarshal.GetReference` and advance with `Unsafe.Add`. Keep the
reverse traversal semantics and observable checksum unchanged. This is unsafe
internally and must be limited to a trusted dense packet; do not expose raw
pointers or require unsafe code in the public API/user callback. A safe
`Span<T>` reverse loop remains the compatibility path, but the JIT is not
required to eliminate its index check.

Required proof: JIT disassembly must show the bounds checks disappear, and a
small Release microbenchmark must beat the normal span-indexed loop without
changing the result.

## Candidate 3: adjacent component loads and `ldp`

The dense probe currently loads adjacent fields of a component with separate
`ldr` instructions. Test whether a sufficiently visible row/element shape lets
the AArch64 JIT combine those loads into `ldp` without changing the public
binding API or introducing user-facing unsafe code. This is only an assembly
experiment: do not add explicit assembly or assume that `ldp` is faster until
the generated code and a narrow probe confirm it on the target CPU.

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
