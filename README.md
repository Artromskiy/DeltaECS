# DeltaECS

Standalone archetype ECS kernel focused on dense iteration, immediate
structural changes, batch operations, cheap tags and predictable memory use.
Public namespace is `Delta.ECS`; project/assembly names remain `DeltaECS*`.

## Storage

- `Entity` is index + generation; `EntityRecord` resolves its location.
- `ComponentId` is dense runtime identity; schema IDs are stable tooling
  identity; `Type` is cold registration metadata.
- Archetypes use an opaque 256-bit mask whose public API can be widened later.
- Chunks store one typed CLR array per component in `Array[]` SoA rows.
- Different component IDs may use the same CLR type and retain separate rows.
- Value, reference and structs-with-reference components share one row model.

Create, destroy, add and remove are immediate; the world has no mandatory
command buffer/playback barrier. Batch APIs group by archetype/chunk rather
than loop through public atomic operations. Overlay tags use per-chunk slot
masks and do not move entities.

## Queries and changes

Reusable `QueryHandle` values cache matching archetypes and row plans. Typed
bindings validate world/query/type ownership outside the entity loop. Read rows
return `ReadOnlySpan<T>`; write rows return `Span<T>` and mark coarse row
versions once per yielded chunk. Raw ordinal access remains internal.

For explicit low-level traversal, `world.Iterate(in query)` exposes three
nested loops: `MoveNextArchetype()`, `MoveNextChunk()`, and the returned
`DenseChunkCursor.MoveNext()`. The cursor API remains responsible for typed row
resolution; tagged queries must still check `IsActiveSlot` for partial chunks.

Structural mutation is invalid while a conflicting row lease is active. This
is a local lifetime rule, not a global barrier. External consumers keep their
own cursors/caches; one consumer never clears another's change state.

See [TODO.md](TODO.md) before selecting work, [IDEAS.md](IDEAS.md) for deferred
designs, [WORKFLOW.md](WORKFLOW.md) for correctness checks and
[benchmarks/README.md](benchmarks/README.md) for bounded assembly-guided work.
Full comparisons and version benchmarks are manual only.
