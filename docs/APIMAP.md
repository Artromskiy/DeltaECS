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
rg -n "public |internal |Query|OpenQuery|Access|MoveNext" src/DeltaECS
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
| `QuerySpec` | All/Any/None component and tag predicates | `src/DeltaECS/QuerySpec.cs` |
| `Query` | World/query identity and typed access-request factory | `src/DeltaECS/EntityTypes.cs` |
| `ReadRequest<T>`, `WriteRequest<T>` | Query-bound typed access intent | `src/DeltaECS/QueryAccess.cs` |
| `QueryScope` | Dense-only validation and structural lease owner | `src/DeltaECS/QueryScope.cs` |
| `QueryArchetypes`, `QueryChunks`, `QuerySlots` | Independent dense traversal levels | `src/DeltaECS/QueryArchetypes.cs`, `QueryChunks.cs`, `QuerySlots.cs` |
| `QueryChunkCursor` | Current chunk, forward slot traversal, value access and tag mask | `src/DeltaECS/QueryAccess.cs` |
| `ReadValues<T>`, `WriteValues<T>` | Safe typed indexers over prepared component access | `src/DeltaECS/QueryAccess.cs` |
| `World.Query` | Callback-based query execution over `QueryChunkCursor` | `src/DeltaECS/World.cs` |

## Query execution path

For independent dense iteration, read only this chain first:

```text
World.OpenQuery(in Query)
  -> QueryScope.Bind(access request)
  -> QueryArchetypes.MoveNext()
  -> QueryChunks.MoveNext()
  -> QuerySlots.Get(access)
  -> QuerySlots.MoveNext()
  -> ReadValues<T>/WriteValues<T>[iterator]
  -> Chunk.GetComponentRow<T>(physicalRow)
```

`QueryPlan` and `DenseArchetypePlan` live in `QueryAccess.cs`. They own query
matching and the query-row to archetype-row plan. Read them when the issue is
archetype matching, new-archetype refresh, or access row resolution; do not
start with tests or structural move code.

The dense three-loop public shape is:

```csharp
using var scope = world.OpenQuery(in query);
var prepared = scope.Bind(access);
var archetypes = scope.Archetypes;
while (archetypes.MoveNext())
{
    var chunks = archetypes.Current.Chunks;
    while (chunks.MoveNext())
    {
        var slots = chunks.Current.Slots;
        var row = slots.Get(prepared);
        while (slots.MoveNext())
        {
            _ = row[slots];
        }
    }
}
```

For tagged queries, use `World.Query` with an action. It selects matching
chunks and exposes `QueryChunkCursor.IsActiveSlot(cursor.CurrentIndex)` for
partial overlay masks. The tag implementation is in `OverlayTagManager.cs`.

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

- Query ownership/type validation: `Query` in `EntityTypes.cs` and
  `QueryChunkCursor.Get` in `QueryAccess.cs`.
- Query plan refresh: `QueryPlan.MatchingPlans` in `QueryAccess.cs`.
- Active lease barrier: `World._activeChunkLeases`, lease helpers in
  `World.cs`, and `QueryScope.Dispose`/`World.Query`.
- Write tracking: `QueryPlan.RegisterWriteAccess`, `World.QueryWriteTick`,
  `QueryChunkCursor.Get(WriteRequest<T>)`, and
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
- `World.Query` is the callback surface for tagged and general queries;
  dense no-tag code should use `World.OpenQuery`.
- `QueryAction<TContext>` is the callback surface. Do not infer that a
  callback benchmark represents the only supported query execution style.
- Do not reintroduce removed ordinal/public unsafe row APIs without an explicit
  API decision and a benchmark contract update.
