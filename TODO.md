# DeltaECS TODO

This file is the first source of candidate work for DeltaECS. Check it before
proposing, assigning, or starting another task. Keep unfinished items here;
move verified work to the completed section instead of silently deleting it.

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
