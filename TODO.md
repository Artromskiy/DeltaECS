# DeltaECS TODO

This file is the first source of candidate work for DeltaECS. Check it before
proposing, assigning, or starting another task. Keep unfinished items here;
move verified work to the completed section instead of silently deleting it.

## Cross-world versioned subscriptions

- [ ] Add a versioned change-feed layer for one-way projection between
  independent worlds. Each subscription owns its query, watched components,
  interested change kinds, and cursor. This is a coalesced latest-state feed,
  not an ordered event log: the world stores versioned `ChangeFlags` and does
  not retain a ring buffer of every transition. `Added`, `Changed`, and
  `Removed` may be set together when several operations happened between two
  consumptions; ordering and multiplicity are intentionally unspecified.
  Component writes may initially be tracked at chunk granularity.
- [ ] Keep ownership unambiguous. The source world owns authoritative mutable
  state; the target world stores a projection plus domain-local runtime state.
  For example, gameplay owns `Transform` and `Renderable`, while render owns
  `RenderTransform`, `GpuRegistration`, and visibility data. Render-only
  components never enter the gameplay registry.
- [ ] Treat `ComponentId` as registry-local. A sync plan resolves stable
  `SchemaId` values to source and target component IDs once; numeric component
  IDs are never assumed to match between worlds. Entity identity is translated
  through a generation-aware `WorldEntityMap`, never by matching entity indices.
- [ ] Keep synchronization outside the ECS kernel. `World` exposes change
  batches; a domain sync system creates/destroys target proxies and copies
  watched values. The target then uses ordinary dense queries without mapping
  overhead in its hot path.

```csharp
// Flags are compared against each subscription's independent cursor; Consume
// does not clear global state needed by other subscribers.
[Flags]
enum ChangeFlags : byte
{
    None = 0,
    Added = 1,
    Changed = 2,
    Removed = 4
}

var changes = gameplay.Subscribe(
    gameplayQuery,
    watch: [TransformId, RenderableId],
    kinds: ChangeFlags.Added | ChangeFlags.Changed | ChangeFlags.Removed);

var creations = gameplay.Subscribe(
    gameplayQuery,
    watch: [],
    kinds: ChangeFlags.Added);

using var batch = changes.Consume(); // Holds a bounded source read scope.
renderSync.Apply(
    source: gameplay,
    target: render,
    batch,
    entityMap);

// Local hot path after synchronization.
render.Query(renderQuery, ref frame, RenderVisibleMeshes);
```

Initial implementation order: complete write-version marking (including
`SetComponent`), add chunk-level `SubscribeChanged<T>()`, add structural
`Added`/`Removed` flags with version stamps, then build `WorldEntityMap` and the
external gameplay-to-render projection. Removal keeps only enough latest
generation-aware tombstone state to converge the target projection; slow
subscribers are not promised replay of every intermediate generation. Add
entity-level dirty masks and grouped target writes only after measuring
excessive chunk copying.

## API ergonomics

No open API-ergonomics items.

## Completed

- [x] Removed the transitional `QueryAccess` compatibility argument. Query
  access intent now comes exclusively from typed read/write row bindings, and
  mutation tests cover write-bound component access without user context.

- [x] Renamed the stateful query terminology to `TContext`, `context`, and
  `action` across the public delegate and `World.Query` signatures. This accepts
  the source migration for callers that used named arguments.

- [x] Made `WriteRowBinding<T>` register write intent on its cached query.
  Binding-driven `Query` and `QueryChunks` execution now prepares a write tick
  automatically; dirty versions are still updated only when the write-bound row
  is actually requested.

- [x] Removed the transitional public `GetComponentRow<T>(ComponentId/int)`
  methods from `DenseChunkScope` and `DenseChunkAccessor`. Repository tests use
  typed bindings, including both adapters of the dual-version benchmark.

- [x] Added additive `World.AddComponents(in QueryHandle, ComponentId[])`,
  `World.RemoveComponents(in QueryHandle, ComponentId[])`, and
  `World.Destroy(in QueryHandle)` operations. Untagged queries use a snapshot
  of matching archetypes, one cached transition edge per source, reverse chunk
  traversal, contiguous target ranges, block row copies, and direct record
  updates. Query tag predicates intentionally retain the exact snapshot/list
  fallback. The list path now reuses world-owned stamped edge slots, and input
  component masks are built directly without clone/sort preparation.

- [x] Added query-bound `ReadRowBinding<T>`/`WriteRowBinding<T>` handles.
  Binding validates the registered CLR type, world/query ownership, and
  All-mask membership once; `GetRow` returns `ReadOnlySpan<T>` for read
  bindings and `Span<T>` for write bindings. Write bindings mark only their
  component row. Tests cover multiple archetypes, invalid types,
  foreign/mismatched bindings, read-only access, and precise change tracking.

- [x] Migrated all benchmark ordinal row access to setup-time typed bindings and
  `GetRow`, including unified comparative workloads, Delta-only profiles,
  scenario lanes, and the version-suite shared scenarios. The benchmark
  contract guard rejects numeric ordinal access, and both version-suite
  adapters now share the typed binding path.

Move completed items here with their commit hash and verification summary.

- [x] Migrated the complete repository to the `Delta` namespace root,
  including source, tests, benchmarks, project metadata, documentation, CI
  inputs, generated artifacts, and file names. Assembly and project names stay
  `DeltaECS*` for reference compatibility.
