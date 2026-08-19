# DeltaECS TODO

This file is the first source of candidate work for DeltaECS. Check it before
proposing, assigning, or starting another task. Keep unfinished items here;
move verified work to the completed section instead of silently deleting it.

## API ergonomics

- [ ] Add a simple `Query` overload without user context:

  ```csharp
  world.Query(in handle, access,
      static (ref DenseChunkAccessor chunk) => { });
  ```

  Keep the existing `Query<TState>(..., ref TState state,
  ChunkAction<TState> body)` overload unchanged for allocation-free
  accumulators and existing callers. Implement the simple overload through the
  same cached query hot path, preserve benchmark and test API compatibility,
  and add a correctness test covering component mutation.

- [ ] Clarify the stateful query terminology:

  ```text
  TState → TContext
  state  → context
  body   → action
  ```

  The generic parameter rename is binary-safe. Renaming public method
  parameters can break source callers that use named arguments, so audit the
  repository and decide whether to accept that migration before changing
  `Query<TState>` or `ChunkAction<TState>`.

- [ ] Make binding-driven execution own the write intent instead of duplicating
  it between `WriteRowBinding<T>` and `QueryAccess.Write`. Keep `QueryAccess`
  for compatibility; defer any lazy/shared write-tick design.

- [ ] Retire the transitional legacy `GetComponentRow<T>(ComponentId/int)`
  APIs. The `ComponentId` overload still returns a writable `Span<T>` even under
  `QueryAccess.Read`, so it can bypass precise dirty tracking. The ordinal `int`
  overload is now internal; migrate remaining compatibility callers to typed
  row bindings before removing the transitional APIs completely.

## Completed

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
  component row. The positional `GetComponentRow<T>(int)` compatibility path is
  internal so it cannot remain part of the public hot-path contract. Tests cover
  multiple archetypes, invalid types, foreign/mismatched bindings, read-only
  access, precise change tracking, and the legacy API.

- [x] Migrated all benchmark ordinal row access to setup-time typed bindings and
  `GetRow`, including unified comparative workloads, Delta-only profiles,
  scenario lanes, and the version-suite shared scenarios. The benchmark
  contract guard rejects numeric ordinal access, while the version suite keeps
  its shared baseline/candidate adapter compatibility through the existing
  `ComponentId` overload.

Move completed items here with their commit hash and verification summary.

- [x] Migrated the complete repository to the `Delta` namespace root,
  including source, tests, benchmarks, project metadata, documentation, CI
  inputs, generated artifacts, and file names. Assembly and project names stay
  `DeltaECS*` for reference compatibility.
