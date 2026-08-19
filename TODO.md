# DeltaECS TODO

This file is the first source of candidate work for DeltaECS. Check it before
proposing, assigning, or starting another task. Keep unfinished items here;
move verified work to the completed section instead of silently deleting it.

## API ergonomics

- [ ] Replace the error-prone positional row API
  `DenseChunkAccessor.GetComponentRow<T>(int queryComponentIndex)` with
  query-bound typed row handles, while keeping the cached query-to-archetype
  row mapping internal:

  ```csharp
  var position = query.Bind<Position>(PositionId);
  Span<Position> positions = chunk.GetRow(position);
  ```

  Keep the storage and query core type-erased. Generic row access may exist as
  a thin checked boundary over `Array[]`, but it must not turn the core into a
  generic `Query<T1, T2>` implementation. A binding validates the registered
  component type once, outside chunk and entity loops. This change will
  probably break existing benchmark and test source API, so first inventory
  positional-index callers and provide a temporary compatibility overload or
  migrate all consumers in one commit. Preserve the allocation-free cached row
  plan and verify that performance does not regress.

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

## Completed

Move completed items here with their commit hash and verification summary.

- [x] Migrated the complete repository to the `Delta` namespace root,
  including source, tests, benchmarks, project metadata, documentation, CI
  inputs, generated artifacts, and file names. Assembly and project names stay
  `DeltaECS*` for reference compatibility.
