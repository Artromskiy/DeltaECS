# ADR-0002: Cached chunk execution for dense iteration

## Context

The class lease API allocated a lease object per non-empty chunk. The ref-state
callback removed that allocation, but callback dispatch and per-chunk lease
cleanup remained in the dense benchmark hot path.

## Decision

- Keep the class lease and ref-state callback APIs for general and filtered query
  access.
- Add `World.QueryChunks`, a synchronous ref-struct enumerator that holds one
  mutation lease for its lifetime and exposes cached component-row indices.
- Build query masks and source-archetype row mappings once when a `QueryHandle`
  is populated. Cached row-index access uses Debug-only assertions in Release
  hot loops; public ComponentId lookup remains checked.
- Keep storage type-erased: the cursor only carries ComponentId-derived row
  indices and `Array[]` rows. Typed casts happen once per requested row per
  chunk at the benchmark/use site.
- Keep benchmark-only specialization at the use site for 1/2/4/8 rows. Do not
  add generic component pools, reflection, scheduler, or source generation.

## Consequences

The direct cursor reduced the 10K/1 distinct-type lane from `3.983 us` to
`2.999 us` in the measured BDN run and reports no allocation in that lane. The
full gate is still open: Array remains slower than legacy in several lanes and
reports `1 B` in the 100K/8 group. The remaining measured bottleneck is typed
Array-row access and chunk traversal, not query matching or callback dispatch.

The enumerator must be disposed synchronously and cannot outlive the world
mutation lease. `MoveNext` disposes only the current pooled overlay mask; the
enumerator owns the outer mutation lease. Tag-filtered queries retain the same
overlay correctness rules.

Darwin PMU counters and BDN disassembly diagnostics were unavailable in this
environment. No hardware-counter or assembly-level limit is claimed.
