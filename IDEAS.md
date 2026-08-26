# DeltaECS ideas

These are not active tasks. Require an explicit decision and measured workload.

## Cross-world versioned subscriptions

An external projection layer could give each consumer its own query, watched
component set and version cursor. It would coalesce latest `Added`, `Changed`
and `Removed` flags rather than retain an ordered event log.

Keep it outside the storage kernel:

- source and target worlds retain independent component registries;
- stable schema IDs build a cached mapping to local `ComponentId` values;
- a generation-aware entity map translates source entities to target proxies;
- consumers never clear global change state needed by another consumer;
- begin with chunk/row versions and add entity dirty masks only after measuring
  excessive copying.

Possible order if promoted: complete all write-version marking, add changed-row
cursors, add structural version/tombstone state, then build the external world
projection. Do not add per-entity subscriptions to the base world.

## Performance candidates

Evidence and candidate order live in
[docs/performance/README.md](docs/performance/README.md). Promote one candidate
at a time with accumulator parity, JIT capture and an unchanged public API.

## Prepared query access routes

Generated implicit callbacks currently resolve `Type -> ComponentId`, validate
the layout and calculate the query ordinal before entering the iteration loops.
The remaining ideas below target that setup path. `ComponentId` is world-local,
so no cache may assume one process-global ID per CLR type.

### ComponentId-to-ordinal table

Build a fixed lookup table when `QueryPlan` is created:

```csharp
private readonly int[] _queryOrdinalByComponentId = CreateOrdinalTable();

internal int QueryOrdinal(ComponentId component)
    => _queryOrdinalByComponentId[component.Value];
```

This replaces repeated `AllMask.Contains` plus `AllMask.Rank` during access
preparation. Capacity is bounded by `ComponentMask.Capacity`, so no dictionary
or public API change is required.

Expected effect: small setup/cold-query improvement; no slot-loop change.
Risk: low.

### Complete validated route cache

Combine primary resolution, layout validation and ordinal lookup at one trusted
boundary. Cache the complete result rather than a second `Type -> ComponentId`
dictionary:

```csharp
internal readonly struct PreparedAccessRoute
{
    internal PreparedAccessRoute(
        ComponentId component,
        int queryOrdinal,
        RuntimeTypeHandle runtimeType)
    {
        Component = component;
        QueryOrdinal = queryOrdinal;
        RuntimeType = runtimeType;
    }

    internal ComponentId Component { get; }
    internal int QueryOrdinal { get; }
    internal RuntimeTypeHandle RuntimeType { get; }
}
```

The first implicit call still performs:

```text
Type -> registry primary ComponentId -> layout validation -> query ordinal
```

Repeated execution should consume the validated route. Exact query/world,
component and runtime-type identity must remain part of the safe boundary;
hash-only validation is insufficient because hashes can collide.

Expected effect: low/medium for repeated small `ForEach` calls; no slot-loop
change. Risk: medium because world ownership, secondary registrations and cache
lifetime must remain exact.

### Prepared active-chunk view and execution state

Maintain a flat active-chunk view in parallel with archetype plans for generated
and two-loop execution:

```csharp
internal readonly struct PreparedChunkPlan
{
    internal readonly Chunk Chunk;
    internal readonly Array[] Rows;
    internal readonly int[] ComponentRows;
}
```

Also maintain `ActiveChunkCount` on `QueryPlan`. The execution entry point can
return before opening a lease and reserve a write stamp in O(1), instead of
scanning every matching plan to find the first active chunk. The three-loop API
can continue consuming `ArchetypePlan`.

Expected effect: high relative improvement for empty/small queries and less
driver indirection; little change for one large dense query. Risk: medium,
because every chunk activation, deactivation and swap-back must update both
views exactly once.

## Read-to-write access promotion

Allow a query to prepare component access as read-only and promote selected
routes to write access immediately before execution. The prepared read route
already contains the reusable data:

```text
ComponentId -> query ordinal -> archetype physical row
```

Promotion should reuse that route instead of repeating registry lookup,
runtime-type validation, mask membership checks or ordinal calculation:

```csharp
var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
query.PrepareRead(positionId);
query.PrepareRead(velocityId);

using var scope = world.OpenQuery(in query);
scope.PromoteWrite(positionId);
```

The query owns immutable prepared routes. Each execution scope owns a small
read/write overlay, so promotion does not mutate the shared plan. Before a
writable ref is exposed, the scope must still verify query/world ownership,
reserve a write tick when active chunks exist and mark promoted rows as written.

Expected effect: lower setup overhead when repeated queries are usually
read-only but selected executions write. No slot-loop change. Risk: medium due
to write stamps, row-version marking, empty-query behavior and concurrency.
