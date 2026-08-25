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

The generated implicit callback currently prepares an access route like this:

```csharp
var access = GeneratedForEachRuntime.AccessWrite(
    world,
    in query,
    typeof(Position));
```

This is setup work before the archetype/chunk/slot loop. It resolves
`Type -> ComponentId`, validates the component layout and returns the query
ordinal. It must not be confused with per-entity reflection or row access.

`ComponentId` is world-local. A generic static field containing only one ID is
therefore unsafe: another world can register the same component in a different
order, and a CLR type can have explicit secondary registrations.

### 1. Query-local ComponentId-to-ordinal table

Build a fixed table while creating `QueryPlan`:

```csharp
private readonly int[] _queryOrdinalByComponentId = CreateOrdinalTable();

private int ResolveOrdinal(ComponentId component)
{
    int ordinal = _queryOrdinalByComponentId[component.Value];
    return ordinal;
}
```

The table replaces repeated `AllMask.Contains` plus `AllMask.Rank` for explicit
ID accesses. Capacity is already bounded by `ComponentMask.Capacity`, so this
does not require a dictionary or a public API change.

Expected effect: small setup/cold-query improvement; no direct benefit inside
the slot loop. Risk: low.

### 2. One combined prepare-and-validate route

Merge `ResolvePrimaryComponent` and `ValidateComponent` into one internal
operation:

```csharp
internal int PrepareAccess(
    World world,
    ComponentId component,
    Type expectedType,
    bool write)
{
    if (!ReferenceEquals(world, Owner))
        QueryThrowHelper.ThrowAccessMismatch();

    ComponentLayout layout = world.Layouts.Get(component);
    if (layout.RuntimeType != expectedType)
        QueryThrowHelper.ThrowAccessTypeMismatch();

    int ordinal = ResolveOrdinal(component);
    if (ordinal < 0)
        QueryThrowHelper.ThrowAccessMismatch();

    if (write)
        _hasWriteAccess = true;

    return ordinal;
}
```

This removes duplicate world/query checks, duplicate layout lookup and a
second mask/rank operation. The returned ordinal remains the only value needed
by the generated invoker.

Expected effect: low/medium for repeated small `ForEach` calls; no hot-loop
change. Risk: low if all validation remains on this boundary.

### 3. Remove the duplicate QueryPlan type dictionary

`ComponentLayoutRegistry` already owns the world-local primary map:

```text
ComponentLayoutRegistry: Type -> primary ComponentId
```

The query should either use that map directly:

```csharp
ComponentId component = world.Layouts.GetPrimary(runtimeType);
return query.Cached.PrepareAccess(world, component, runtimeType, write);
```

or cache the complete validated route rather than maintaining a second
`QueryPlan: Type -> ComponentId` dictionary. Do not replace it with a global
`static ComponentId` per generic type: IDs are not process-global.

Expected effect: lower cold-query allocation and one fewer dictionary layer;
throughput effect is expected to be small. Risk: low, but update tests that
inspect `PrimaryRouteResolutionCount`.

### 4. Query-local prepared route cache

If the same query is executed repeatedly, cache the result after the first
validated resolution:

```csharp
internal readonly struct PreparedAccessRoute
{
    public PreparedAccessRoute(ComponentId component, int ordinal, Type type)
    {
        Component = component;
        Ordinal = ordinal;
        RuntimeType = type;
    }

    public ComponentId Component { get; }
    public int Ordinal { get; }
    public Type RuntimeType { get; }
}
```

For explicit IDs, `ComponentId` can be the cache key. For implicit routes, the
first call still needs `Type -> primary ComponentId`; subsequent calls can use
the prepared route. A small bounded array may be preferable to a dictionary
because a callback normally has only a few component accesses.

Expected effect: useful for repeated cold/small query calls, negligible for a
single large iteration. Risk: medium; cache entries must include the expected
type and component ID so secondary registrations cannot bypass validation.

### 5. Separate explicit-ID and implicit-type paths

The explicit generated form already has the information needed to avoid the
primary lookup:

```csharp
GeneratedForEachRuntime.AccessWrite(
    world,
    in query,
    positionId,
    typeof(Position));
```

It still performs one setup-time check that `positionId` is registered as
`Position`. After that check, only the query ordinal is retained. The implicit
form remains safe and convenient, but necessarily performs a world-local
primary resolution on its first preparation.

Expected effect: explicit-ID path gets the best small-query setup cost. Risk:
low; never remove the one-time type check from the safe generated boundary.

### 6. Prepared active-chunk view

For generated and two-loop execution, maintain a flat active-chunk view in
parallel with the archetype plans:

```csharp
internal readonly struct PreparedChunkPlan
{
    public readonly Chunk Chunk;
    public readonly Array[] Rows;
    public readonly int[] ComponentRows;
}
```

The view is updated only when an archetype or chunk becomes active/inactive.
The three-loop archetype API can continue using `ArchetypePlan`; only the
chunk-oriented path consumes the flat view.

Expected effect: removes plan/chunk traversal indirection from the generated
driver and enables an immediate empty-query return. Risk: medium; structural
updates must keep the swap-back table synchronized.

### 7. Cache execution flags and empty state

Maintain query-plan metadata updated with plan/chunk activation:

```csharp
internal bool HasActiveChunks { get; }
internal int ActiveChunkCount { get; }
internal bool HasWriteAccess { get; }
```

The execution entry point can return before opening a lease or reserving a
write stamp when `HasActiveChunks` is false. This is especially relevant to
cold queries over empty or sparse worlds.

Expected effect: high relative improvement for empty/small queries, zero for a
large dense query. Risk: low/medium; activation and deactivation notifications
must update the counters exactly once.

### 8. Generic static cache: rejected as the primary design

This shape is not safe in the current world-local registry model:

```csharp
// Do not use as a global ECS cache.
private static ComponentId PositionId;
```

A per-`T` cache keyed by registry could be made correct with a
`ConditionalWeakTable`, but it adds allocation and synchronization to a path
that is already setup-only. Prefer the registry's existing type map and a
query-local validated route cache. Revisit a generic static cache only if a
global immutable component catalog is introduced and measured as beneficial.

### 9. Batch write reservation for generated query execution

The generated driver currently scans all matching plans before reserving a
write tick:

```csharp
if (hasWrites)
{
    for (int planIndex = 0; planIndex < plans.Length; planIndex++)
    {
        if (plans[planIndex].Chunks.Length == 0)
        {
            continue;
        }

        writeTick = ReserveQueryWrite(out writeStamp);
        break;
    }
}
```

The direct internal batch shape could be:

```csharp
private void PrepareQueryWrite(
    bool hasWrites,
    bool hasActiveChunks,
    out uint writeTick,
    out Stamp writeStamp)
{
    writeTick = 0;
    writeStamp = default;
    if (hasWrites && hasActiveChunks)
    {
        writeTick = ReserveQueryWrite(out writeStamp);
    }
}
```

The preferred source for `hasActiveChunks` is an incrementally maintained
`QueryPlan.ActiveChunkCount`, updated by `OnChunkActivated` and
`OnChunkDeactivated`:

```csharp
internal bool HasActiveChunks => _activeChunkCount != 0;
```

This changes the pre-execution scan from `O(matching plans)` to `O(1)` and
also enables an immediate empty-query return. It does not move any write
operation into the slot loop and preserves the existing one stamp per query
execution semantics.

Expected effect: high relative improvement for empty/sparse queries, small
absolute improvement for dense queries with many matching archetypes. Risk:
medium; every chunk activation/deactivation path must update the counter
exactly once, including swap-back deactivation.

An internal method that accepts `ReadOnlySpan<ArchetypePlan>` and scans it once
is simpler but is only a cleanup of the current code. It should be preferred
only if maintaining the counter is not worth the structural bookkeeping.

## ComponentId-only generated access

The implicit generated form currently starts with a CLR type:

```csharp
GeneratedForEachRuntime.AccessWrite(
    world,
    in query,
    typeof(Position));
```

The desired execution route is:

```text
prepared ComponentId
    -> query ordinal
    -> archetype physical row
    -> chunk Array
```

There is a hard constraint: `ComponentId` is world-local and does not encode
`Position`. The same CLR type can also have a primary and a secondary
registration. Therefore this is unsafe as a replacement:

```csharp
// ComponentId alone cannot prove that the callback type is Position.
GeneratedForEachRuntime.AccessWrite(world, in query, positionId);
```

The `ComponentId`/CLR-type check must happen once at a trusted setup boundary,
or the API must carry a type-bound registration handle.

### Recommended no-breaking scheme: prepare once, execute by ID

Keep the existing callback spelling, but make generated setup produce a
prepared route. After preparation, execution uses only IDs and ordinals:

```csharp
internal readonly struct PreparedAccess
{
    public PreparedAccess(ComponentId component, int queryOrdinal)
    {
        Component = component;
        QueryOrdinal = queryOrdinal;
    }

    public ComponentId Component { get; }
    public int QueryOrdinal { get; }
}

internal PreparedAccess PrepareAccess(
    World world,
    ComponentId component,
    Type expectedType,
    bool write);
```

The first implicit call still does:

```text
typeof(T) -> registry primary ComponentId -> validate layout -> cache route
```

The generated invoker then retains only `PreparedAccess.QueryOrdinal`; no
`Type`, type dictionary or registry lookup enters the archetype/chunk/slot
execution path. Explicit-ID calls can skip the primary lookup while retaining
the one-time type check.

### Remove the duplicate query type dictionary

The registry already owns the world-local primary map. The query-level cache
duplicates part of that work:

```csharp
private Dictionary<Type, ComponentId>? _primaryComponentIdsByType;
```

Possible replacement:

```csharp
ComponentId component = world.Layouts.GetPrimary(runtimeType);
return query.Cached.PrepareAccess(world, component, runtimeType, write);
```

This leaves one registry lookup per generated setup call and moves the useful
cache boundary to the fully validated route. It is likely better for cold
queries because it avoids allocating a second dictionary. Repeated executions
should still be measured before removing the query-level cache.

### ComponentId-indexed query ordinal table

The ID-only portion can be dictionary-free because mask capacity is bounded:

```csharp
private readonly int[] _queryOrdinalByComponentId;

internal int QueryOrdinal(ComponentId component)
    => _queryOrdinalByComponentId[component.Value];
```

Populate this table when `QueryPlan` is created. Then `Contains` and `Rank` are
not repeated during access preparation. This is the preferred internal route
for both explicit and already-resolved implicit IDs.

### Additive typed registration handle

If a future API extension is allowed, registration can return a type-bound
handle:

```csharp
var position = layouts.Register<Position>(PositionSchema);

world.ForEach(in query, position, velocity,
    static (ref Position p, in Velocity v) => p.X += v.X);
```

The handle can contain a `ComponentId` and internal type identity. The type is
checked when the handle is created or bound to a world; execution receives
only the ID. This is an API extension and should not be mixed into the
no-breaking experiment.

### Global generic static ID: rejected for the current model

This would be fast but incorrect in the current world-local registry model:

```csharp
// Do not use as a global ECS cache.
static class ComponentIdOf<T>
{
    public static ComponentId Value;
}
```

Different worlds can register components in different orders, and secondary
registrations make one static ID per CLR type insufficient. It becomes valid
only after introducing a global immutable component catalog with stable IDs,
which is a storage/registration architecture change.

### Decision boundary

For the current API, the practical target is not “no `Type` anywhere”; it is:

```text
Type -> ComponentId: setup only
ComponentId -> query ordinal: prepared table
query ordinal -> row: execution only
```

This preserves safety and the implicit callback API while removing runtime-type
and dictionary work from the actual query execution path.

## Token-based component validation

The current generated setup validates the same facts separately:

```csharp
world + query owner
component ID + registered CLR type
component present in query AllMask
component ID -> query ordinal
```

A prepared token can collapse these checks after the first trusted validation:

```csharp
internal readonly struct ComponentAccessToken
{
    public ComponentAccessToken(
        QueryPlan query,
        ComponentId component,
        int queryOrdinal,
        RuntimeTypeHandle runtimeType)
    {
        Query = query;
        Component = component;
        QueryOrdinal = queryOrdinal;
        RuntimeType = runtimeType;
    }

    public QueryPlan Query { get; }
    public ComponentId Component { get; }
    public int QueryOrdinal { get; }
    public RuntimeTypeHandle RuntimeType { get; }
}
```

The first preparation keeps the complete safe validation:

```text
Query owner check
    -> ComponentId exists in registry
    -> registered RuntimeType matches expected type
    -> ComponentId is in query AllMask
    -> query ordinal is computed
    -> ComponentAccessToken is created
```

Later setup calls can use a narrow fast validation:

```csharp
internal int ValidatePrepared(
    in ComponentAccessToken token,
    QueryPlan query,
    ComponentId component,
    RuntimeTypeHandle runtimeType)
{
    if (ReferenceEquals(token.Query, query)
        && token.Component == component
        && token.RuntimeType.Equals(runtimeType))
    {
        return token.QueryOrdinal;
    }

    QueryThrowHelper.ThrowAccessMismatch();
    return -1;
}
```

The important property is that the hot execution route consumes only
`QueryOrdinal` and does not inspect `Type`, `Layout` or `ComponentMask`.

### Hash-only validation is not safe

This is not sufficient as a correctness check:

```csharp
int token = runtimeType.GetHashCode();
```

Hashes can collide, and a collision at an unsafe row boundary can become type
confusion. A hash may be used as a quick reject before an exact comparison, but
the final check must use an exact identity such as:

```csharp
runtimeType.TypeHandle.Equals(layout.RuntimeType.TypeHandle)
```

or a trusted registration token owned by the same `ComponentLayoutRegistry`.

### Registry-owned token

An alternative is to assign a private type token at registration:

```csharp
internal readonly struct ComponentTypeToken
{
    public ComponentTypeToken(RuntimeTypeHandle runtimeType, int registryRevision)
    {
        RuntimeType = runtimeType;
        RegistryRevision = registryRevision;
    }

    public RuntimeTypeHandle RuntimeType { get; }
    public int RegistryRevision { get; }
}
```

The token must include registry/world identity or revision. A bare integer is
not enough because `ComponentId` values are local and can be reused by another
world. This token is suitable for an internal prepared route, not as a direct
replacement for the public `ComponentId` contract.

Expected effect: low/medium setup improvement, especially for repeated small
queries; no change to the slot loop after the route is prepared. Risk: medium,
because cache lifetime, world ownership and stale-token behavior must be
specified before implementation.
