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
| `QuerySpec` | All/Any/None component predicates | `src/DeltaECS/Core/QuerySpec.cs` |
| `Query` | World/query identity and non-generic read/write access factory | `src/DeltaECS/EntityTypes.cs` |
| `ReadAccess`, `WriteAccess` | Query-bound type-erased access intent | `src/DeltaECS/QueryAccess.cs` |
| `QueryScope` | Dense-only validation and structural lease owner | `src/DeltaECS/QueryScope.cs` |
| `QueryArchetypes`, `QueryChunks`, `QuerySlots` | Independent dense traversal levels | `src/DeltaECS/QueryArchetypes.cs`, `QueryChunks.cs`, `QuerySlots.cs` |
| `QueryChunkCursor` | Current chunk, forward slot traversal and value access | `src/DeltaECS/QueryAccess.cs` |
| `ReadValues`, `WriteValues` | Non-generic prepared values; final `Ref<T>` must match the registered component type. Controlled pre-loop mismatch validation is selected correctness work. | `src/DeltaECS/QueryAccess.cs` |
| `World.Query` | Callback-based query execution over `QueryChunkCursor` | `src/DeltaECS/World.cs` |
| `World.ForEach` (planned) | Main high-level dense entry point; owns scope, validation, preparation and disposal | `src/DeltaECS/World.cs` |
| `World.ForEach(ReadOnlySpan<Entity>, ...)` (planned) | Explicit ordered entity-list execution, optionally filtered by a prepared `Query` | `src/DeltaECS/List/README.md` |

## Query execution path

For independent dense iteration, read only this chain first:

```text
World.OpenQuery(in Query)
  -> QueryScope.Bind(access)
  -> QueryArchetypes.MoveNext()
  -> QueryChunks.MoveNext()
  -> QuerySlots.Get(access)
  -> QuerySlots.MoveNext()
  -> ReadValues/WriteValues.Ref<T>(iterator)
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
            _ = row.Ref<Component>(slots);
        }
    }
}
```

## Structural and storage paths

Use these slices only for structural or storage work:

- Entity record resolve, create, destroy, add/remove, query structural changes:
  `World.cs`.
- Archetype ownership, active chunk list, chunk reuse and active chunk swap-back:
  `Archetype.cs`.
- Entity rows, component rows, row versions, swap-back and reference clearing:
  `Chunk.cs` and `ComponentRowOperations.cs`.
- Component transition/cache types are near the bottom of `World.cs`; inspect
  only when the task explicitly concerns transitions.

Do not read `Archetype.cs` or `Chunk.cs` for a pure query API naming task unless
the query path proves to depend on their storage contract.

## Lifetime and validation map

- Query ownership/mask validation: `Query` in `EntityTypes.cs` and
  `QueryChunkCursor.GetRead/GetWrite` in `QueryAccess.cs`.
- Query plan refresh: `QueryPlan.MatchingPlans` in `QueryAccess.cs`.
- Active lease barrier: `World._activeChunkLeases`, lease helpers in
  `World.cs`, and `QueryScope.Dispose`/`World.Query`.
- Write tracking: `QueryPlan.RegisterWriteAccess`, `World.QueryWriteTick`,
  `QueryChunkCursor.GetWrite(WriteAccess)`, and
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
| Public lifecycle, queries, cursor rows and leases | `tests/DeltaECSTests/DeltaECSTests.cs` |
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
- `World.Query` is the callback surface for dense component queries;
  `World.OpenQuery` exposes the explicit three-loop form.
- `QueryAction<TContext>` is the callback surface. Do not infer that a
  callback benchmark represents the only supported query execution style.
- Do not reintroduce removed ordinal/public unsafe row APIs without an explicit
  API decision and a benchmark contract update.

## Planned generated callback API

The planned callback surface covers every combination of:

- no context or `TState` context;
- no entity or current `Entity` argument;
- zero or more explicitly typed component arguments.

For each supported component arity, source generation will emit the overloads
instead of maintaining a handwritten variadic matrix. The zero-component case
is an explicit entity-only path. Generic types are used at the component and
final `ref T` boundaries; query, access, iteration and storage state remain
type-erased. A separate future struct-functor family may provide the same
matrix through `where TFunctor : struct, IQueryFunctor` for static dispatch and
inlining. Neither family is implemented yet.

`World.ForEach` is the planned default user-facing entry point. It owns the
temporary query scope and validation internally. `World.OpenQuery` remains the
advanced path for reusing prepared accesses across multiple passes or combining
callback execution with explicit archetype/chunk/slot traversal. Both paths
must call the same dense execution kernel.

## Planned explicit-list execution

List execution uses the same `World.ForEach` family as dense query execution rather
than introducing another public selection type:

```csharp
world.ForEach(entities, action);
world.ForEach(entities, in query, action);
```

The unfiltered overload visits every valid occurrence in the supplied
`ReadOnlySpan<Entity>`. The filtered overload treats that span as the candidate set
and applies `query` to each resolved entity; it never broadens execution to every
entity matching the query in the world. Input order and duplicate occurrences are
preserved. Invalid, stale and foreign handles follow the explicit-list policy used
by structural APIs.

The delegate/functor arity matrix remains source-generated. Both surfaces feed one
type-erased list kernel that validates access once, resolves entity locations, and
caches the most recently used archetype row plan. A future explicitly named
unordered batch API may group candidates by archetype; `ForEach` must not reorder
silently.

Implementation files for this family belong in `src/DeltaECS/List`. Public entry
points stay on `World`, and the folder must not grow a parallel query model.
