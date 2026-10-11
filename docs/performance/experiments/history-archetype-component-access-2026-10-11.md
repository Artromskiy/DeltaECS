# Archetype history restore with typed component access

## Change

Baseline commit: `f5000dc`. Candidate: uncommitted working-tree changes.

The candidate adds `IArchetypeForEachComponent<TContext,TComponent>` and a
`Process` overload that binds the component's row index for each matching
archetype. Typed processors run directly over that component row. The loop no
longer resolves the component through `EntityRef.GetRef(ComponentId)` or
dispatches the case interface once per entity. It increments each written
component's archetype stamp once before processing its matching row.

## Workload and method

The history restore copy registers 400 `ComponentId`s over four CLR component
types, creates 4,096 deterministic random archetype compositions and assigns
5–20 components to each entity. It compares 400 individual cached
`ForEachEntity` operations with one reusable `ForEachArchetype` operation
using typed component processors. Setup validates the restored checksum before
BenchmarkDotNet starts; the entity count is propagated to child processes with
`DELTA_ECS_HISTORY_ENTITY_COUNT`.

Apple M4 Pro, macOS Tahoe 26.5.2, .NET 10.0.12 Arm64 RyuJIT, BenchmarkDotNet
0.15.8, 10 warmups, 30 measured iterations of 200 ms, one launch. Ratios are
candidate divided by baseline. Every case allocated 0 B.

| Entities | Per-component mean ± error (StdDev) | Typed-row mean ± error (StdDev) | Ratio |
| ---: | ---: | ---: | ---: |
| 32 | 15.073 ± 0.0843 us (0.1182 us) | 2.761 ± 0.0049 us (0.0068 us) | 0.18 |
| 1,024 | 215.7 ± 3.30 us (4.63 us) | 166.8 ± 4.33 us (6.35 us) | 0.77 |
| 131,072 | 8.064 ± 0.2307 ms (0.3453 ms) | 9.735 ± 0.1538 ms (0.2255 ms) | 1.21 |
| 1,048,576 | 37.47 ± 0.616 ms (0.922 ms) | 34.46 ± 0.485 ms (0.711 ms) | 0.92 |

The candidate is substantially faster for small worlds and 8% faster at
1,048,576 entities, but it is 21% slower at 131,072. Keep investigating the
medium-size archetype-plan cost before claiming an all-size win.

The 1,048,576-entity hot loop was also compared as a `for` loop with separate
ref cursors against a non-unrolled `while` loop that advances the existing
`firstEntity` and `firstComponent` refs directly. The earlier paired run
measured 11.30 ms for `ForEachArchetype`; the direct-ref `while` version
measured 12.52 ± 0.219 ms (0.321 ms StdDev) in a repeat with the same 10
warmups and 30 × 200 ms iterations. The shape matches the generated loop and
avoids duplicate ref locals, but this measurement does not show a speedup.
Raw reports are under `artifacts/history-archetype-component-access-while*`.

An alternative that cached matching archetypes separately per processor and
then executed cases one at a time was rejected. At 131,072 entities it measured
13.524 ± 0.3130 ms (0.4388 ms StdDev) versus the same run's 7.064 ± 0.1403 ms
(0.2012 ms StdDev) per-component baseline, a 1.92 ratio. It also changes the
archetype-major callback order. Raw output is
`artifacts/history-archetype-component-access/case-major-131072-rejected.log`.

## JIT evidence

The shared `run-jit-disasm.sh` runner successfully captured the benchmark
entrypoint at 48 bytes, but that wrapper only calls the retained operation and
does not contain its hot loop. The generic `ArchetypeForEachComponentCase`
executor's code size and instruction summary have not yet been captured. Do
not treat the wrapper listing as evidence for the hot method; capture that
method before merging the optimization.

## Artifacts and validation

- Raw BDN logs: `artifacts/history-archetype-component-access/`
- Benchmark checksum guard passed for each child-process entity count.
- `ArchetypeForEachOperationTests`: 5 passed.
- Runtime consumer proof builds successfully.
- `DeltaECS` builds for `netstandard2.1` successfully.

## Archetype-plan checks for dense component processors — 2026-10-11

The operation now skips archetypes with no active chunks before marking stamps
or running cases. `EnsurePlans` caches whether each bound query has tag filters.
For a bound component case without tag filters, a non-empty matching archetype
already proves that the query selects entities, so `MarkWrites` does not call
`HasMatchingEntities` per component binding. Tag-filtered cases retain that
check because a matching archetype can still have no selected slots. The
component ID/type and `WhereAll` requirements are still validated when the
operation is configured and its plan is bound; the public API and other
iteration implementations are unchanged.

On the same 1,048,576-entity, 4,096-archetype workload, the immediate baseline
was `16.888 ± 0.180 ms` (0.264 ms StdDev); the optimized operation measured
`15.40 ± 0.230 ms` (0.329 ms StdDev), a `−8.81%` change. Both runs used
BenchmarkDotNet 0.15.8, Apple M4 Pro, .NET 10.0.12 Arm64 RyuJIT, 10 warmups,
30 measured iterations of 200 ms and 16 invocations per iteration. Both
allocated 0 B, and the workload checksum guard passed. It processed 13,043,321
entity-component pairs (12.439 per entity). The direct raw-row estimate from
the same experiment is 3.421 ms; the archetype operation remains `4.50×` that
data-only estimate.

An additional attempt to delay loading the case object until tag filtering was
known to be necessary measured `15.37 ± 0.140 ms`; its interval overlaps the
retained `15.40 ± 0.230 ms` result, so that variant was reverted as
inconclusive. Reports are under
`artifacts/history-row-theoretical-1048576/archetype-{trusted-cached,trusted-conditional-case}/`.

## Stack-view entity address and stamp lookup — 2026-10-11

The stack-owned `GeneratedEntityRefView` now caches the native entity-row base
when a chunk is bound. `EntityRef.Index`, generation reads and the implicit
`Entity` conversion therefore resolve the current slot from that cached address
instead of following `Chunk.RawEntities` on each read. The archetype operation
also fetches the archetype stamp array once before its write-binding loop. No
public signature or other iteration loop changed.

On the same 1,048,576-entity workload, the fresh A/B baseline measured
`16.175 ± 0.182 ms` (0.255 ms StdDev), and the candidate measured `14.00 ±
0.165 ms` (0.242 ms StdDev): `−13.4%`, with 0 B allocated and the checksum
guard passing. Their 99.9% confidence intervals do not overlap. An earlier
candidate run measured `13.536 ± 0.471 ms`, so throughput varies between runs,
but both candidate runs are faster than the baseline. The no-stamp diagnostic
upper bound measured `13.016 ± 0.125 ms`, about 7.0% faster than the final
candidate, while omitting required component-stamp updates. It does not justify
a stamp-free public/default path.

Rejected variants on this workload:

- Updating a direct entity pointer in the view for every callback measured
  `16.397 ± 0.307 ms`; the extra pointer store/dispatch path regressed, so it
  was removed.
- Caching each archetype's stamp-array reference inside the plan measured
  `15.013 ± 0.202 ms`; the larger plan did not pay for the lookup saved, so it
  was removed.

All runs used BenchmarkDotNet 0.15.8, Apple M4 Pro, .NET 10.0.12 Arm64 RyuJIT,
10 warmups and 30 measured iterations of 200 ms with 16 invocations per
iteration. Raw reports are under
`artifacts/history-row-theoretical-1048576/archetype-{cached-address-stamps-repeat,no-stamps-upper-bound,direct-entity-cursor,cached-stamps-plan}/`.

## Thinner-loop experiments — 2026-10-11

The non-unrolled reference-view loop measured `14.15 ± 0.106 ms` on the same
1,048,576-entity workload. Four-way unrolling measured `13.228 ± 0.073 ms`,
while eight-way unrolling measured `13.276 ± 0.095 ms`; the intervals overlap.
The four-way version was not retained: it duplicates the callback body and is
the wrong direction for keeping this loop small and easy to simplify. The
tag-branch flattening measured `13.252 ± 0.121 ms` together with four-way
unrolling, so that run does not isolate a performance effect for flattening.

Several attempts to make the callback path thinner did not improve throughput:

| Variant | Mean | Change from 14.15 ms reference | Result |
| --- | ---: | ---: | --- |
| Store the stack-view address in `EntityRef` as one `nint` | 14.689 ± 0.154 ms | +3.8% | Rejected; pointer dereference cost more than the smaller value copy saved. |
| Cache the case object in each archetype binding instead of its index | 14.47 ± 0.131 ms | +2.3% | Rejected; larger bindings did not pay for the removed list lookup. |
| Force-inline the consumer processor methods | 14.344 ± 0.149 ms | +1.4% | Rejected; the constrained processor call is already sufficiently thin. |

The `EntityRef` address-access attribute probe measured `14.26 ± 0.335 ms` and
was inconclusive. These results point away from adding another callback wrapper
or changing `EntityRef` representation. The retained simplification combines
the component case's nested tag checks into one conditional; it keeps the
existing dense loop and its `Unsafe.Add` progression unchanged.

To remove repeated setup from the case boundary, one experiment moved creation
of `GeneratedEntityRefView` and `EntityRef` from each component-case execution
to the start of the whole operation invocation, then reused the pair across
cases. A fresh baseline measured `13.167 ± 0.090 ms` (0.131 ms StdDev); the
shared-view candidate measured `14.469 ± 0.190 ms` (0.279 ms StdDev), a `9.9%`
regression, with 0 B allocated in both runs. The stack-local view/ref and
per-case setup were restored. The optimized ARM64 disassembly also shows the
generic processor `Invoke` body inlined into the entity loop; the remaining
type-erased case call is outside that loop, once per matching case/archetype.
Thus another callback wrapper or moving the same ref across cases does not
make the per-entity call thinner. Raw reports are under
`artifacts/history-row-theoretical-1048576/archetype-{shared-entity-ref,current-thin-call-baseline}/`.
