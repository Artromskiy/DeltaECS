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
  APIs. They return a writable `Span<T>` even under `QueryAccess.Read`, so they
  can bypass precise dirty tracking; migrate callers to typed row bindings first.

## Completed

- [x] Added query-bound `ReadRowBinding<T>`/`WriteRowBinding<T>` handles.
  Binding validates the registered CLR type, world/query ownership, and
  All-mask membership once; `GetRow` returns `ReadOnlySpan<T>` for read
  bindings and `Span<T>` for write bindings. Write bindings mark only their
  component row, while positional `GetComponentRow<T>(int)` remains
  compatible. Tests cover multiple archetypes, invalid types, foreign/mismatched
  bindings, read-only access, precise change tracking, and the legacy API.

Move completed items here with their commit hash and verification summary.

- [x] Migrated the complete repository to the `Delta` namespace root,
  including source, tests, benchmarks, project metadata, documentation, CI
  inputs, generated artifacts, and file names. Assembly and project names stay
  `DeltaECS*` for reference compatibility.
