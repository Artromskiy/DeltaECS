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

## Completed

Move completed items here with their commit hash and verification summary.
