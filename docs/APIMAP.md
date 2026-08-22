# DeltaECS API and source map

This is a navigation map for contributors and agents. It is not a second
public contract or a work queue. Stable behavior belongs in `README.md`,
selected work belongs in `TODO.md`, and deferred ideas belong in `IDEAS.md`.

## Fast read order

1. Read the root and project `AGENTS.md` files.
2. Read `TODO.md` only when selecting or continuing implementation work.
3. Read `README.md` for the public contract and `WORKFLOW.md` before build,
   test, or benchmark commands.
4. Use this map to select the smallest source slice.
5. Read tests only after locating the source path, and only the test class
   covering that path. Do not scan the whole test project first.

Useful navigation commands:

```bash
rg -n "public |internal |QueryCursor|QueryIterator|CursorBind|MoveNext" src/DeltaECS
rg -n "<relevant API or invariant>" tests/DeltaECSTests
```

## Public API map

| API | Purpose | First source file |
|---|---|---|
| `World` | World ownership, entity lifecycle, structural changes, query entry points | `src/DeltaECS/World.cs` |
| `Entity` | Entity index/generation value and stale-handle identity | `src/DeltaECS/EntityTypes.cs` |
| `ArchetypeHandle` | World-owned archetype identity used by create paths | `src/DeltaECS/EntityTypes.cs` |
| `ComponentId`, `ComponentMask`, `ComponentLayout` | Dense component identity, matching mask, registered layout metadata | `src/DeltaECS/ComponentTypes.cs` |
| `ComponentLayoutRegistry` | CLR type/storage registration and validation | `src/DeltaECS/ComponentLayoutRegistry.cs` |
| `QueryDescription` | All/Any/None component and tag predicates | `src/DeltaECS/QueryDescription.cs` |
| `QueryHandle` | World/query identity and typed cursor-binding factory | `src/DeltaECS/EntityTypes.cs` |
| `CursorReadBinding<T>`, `CursorWriteBinding<T>` | Query-bound typed row intent | `src/DeltaECS/WorldQuery.cs` |
| `QueryIterator` | Explicit `archetype -> chunk -> slot` traversal | `src/DeltaECS/QueryIterator.cs` |
| `DenseChunkCursor` | Current chunk, reverse slot traversal, row resolution and tag mask | `src/DeltaECS/WorldQuery.cs` |
| `ResolvedReadRow<T>`, `ResolvedWriteRow<T>` | Safe typed cursor indexers over a resolved row | `src/DeltaECS/WorldQuery.cs` |
| `World.QueryCursor` | Callback-based query execution over `DenseChunkCursor` | `src/DeltaECS/World.cs` |
| `World.QueryCursorChunks` | Compatibility chunk enumeration over the cursor API | `src/DeltaECS/World.cs` |

## Query execution path

For dense iteration, read only this chain first:

```text
World.Iterate(in QueryHandle)
  -> QueryIterator.MoveNextArchetype()
  -> QueryIterator.MoveNextChunk()
  -> DenseChunkCursor.MoveNext()
  -> DenseChunkCursor.Resolve(binding)
  -> ResolvedReadRow<T>/ResolvedWriteRow<T>[cursor]
  -> Chunk.GetComponentRow<T>(physicalRow)
```

`CachedQuery` and `DenseArchetypePlan` live in `WorldQuery.cs`. They own query
matching and the query-row to archetype-row plan. Read them when the issue is
archetype matching, new-archetype refresh, or binding row resolution; do not
start with tests or structural move code.

The three-loop public shape is:

```csharp
using var iterator = world.Iterate(in query);
while (iterator.MoveNextArchetype())
{
    while (iterator.MoveNextChunk())
    {
        var cursor = iterator.Current;
        var row = cursor.Resolve(binding);
        while (cursor.MoveNext())
        {
            _ = row[cursor];
        }
    }
}
```

For tagged queries, `MoveNextChunk()` selects chunks and
`DenseChunkCursor.IsActiveSlot(cursor.CurrentIndex)` selects slots in a
partial mask. The tag implementation is in `OverlayTagManager.cs`.

## Structural and storage paths

Use these slices only for structural or storage work:

- Entity record resolve, create, destroy, add/remove, query structural changes:
  `World.cs`.
- Archetype ownership, active chunk list, chunk reuse and active chunk swap-back:
  `Archetype.cs`.
- Entity rows, component rows, row versions, swap-back and reference clearing:
  `Chunk.cs` and `ComponentRowOperations.cs`.
- Overlay tag transfer and masks:
  `OverlayTagManager.cs`.
- Component transition/cache types are near the bottom of `World.cs`; inspect
  only when the task explicitly concerns transitions.

Do not read `Archetype.cs` or `Chunk.cs` for a pure query API naming task unless
the query path proves to depend on their storage contract.

## Lifetime and validation map

- Query ownership/type validation: `QueryHandle` in `EntityTypes.cs` and
  `DenseChunkCursor.Resolve` in `WorldQuery.cs`.
- Query plan refresh: `CachedQuery.MatchingPlans` in `WorldQuery.cs`.
- Active lease barrier: `World._activeChunkLeases`, lease helpers in
  `World.cs`, and `QueryIterator.Dispose`/`CursorChunkEnumerator.Dispose`.
- Write tracking: `CachedQuery.RegisterWriteBinding`, `World.QueryWriteTick`,
  `DenseChunkCursor.Resolve(CursorWriteBinding<T>)`, and
  `Chunk.MarkComponentWritten`.
- Stale entity generation/location: `EntityRecord` and resolve helpers in
  `World.cs`.

When investigating an exception, follow this order: ownership → query plan →
component layout/type → chunk/row lifetime → structural storage. This avoids
opening unrelated test fixtures first.

## Targeted test map

Tests are evidence for a source path, not the starting point for navigation.
Open only the relevant class:

| Concern | Test file |
|---|---|
| Public lifecycle, queries, cursor rows, tags and leases | `tests/DeltaECSTests/DeltaECSTests.cs` |
| Component row defaults, references and stale entities | `tests/DeltaECSTests/ComponentRowOperationTests.cs` |
| Query Add/Remove/Destroy structural semantics | `tests/DeltaECSTests/QueryStructuralOperationsTests.cs` |
| Active chunk reuse and direct active traversal | `tests/DeltaECSTests/ActiveChunkTests.cs` |
| Block transition correctness and records | `tests/DeltaECSTests/StructuralAlgorithmTests.cs` |
| Comparative benchmark catalog/report contracts only | `tests/DeltaECSTests/ComparativeBenchmarkContractTests.cs` |

For a source change, first locate the relevant method with `rg`, then read the
nearest test method and its setup helpers. Benchmark source is not a substitute
for a production correctness test.

## Compatibility boundaries

- `Chunk.GetComponentRow<T>(int)` is an internal storage primitive, not a
  public user API. Do not remove it while migrating public cursor access.
- `World.QueryCursorChunks` remains a compatibility surface; new outer-loop
  code should use `World.Iterate` and `QueryIterator`.
- `QueryCursorAction<TContext>` is the callback surface. Do not infer that a
  callback benchmark represents the only supported query execution style.
- Do not reintroduce removed ordinal/public unsafe row APIs without an explicit
  API decision and a benchmark contract update.
