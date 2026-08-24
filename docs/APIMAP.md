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
| `World` | World ownership, entity lifecycle, structural changes, query entry points | `src/DeltaECS/Core/World.cs` |
| `Entity` | Entity index/generation value and stale-handle identity | `src/DeltaECS/Core/EntityTypes.cs` |
| `ArchetypeHandle` | World-owned archetype identity used by create paths | `src/DeltaECS/Core/EntityTypes.cs` |
| `ComponentId`, `ComponentMask`, `ComponentLayout` | Dense component identity, matching mask, registered layout metadata | `src/DeltaECS/Core/ComponentTypes.cs` |
| `ComponentLayoutRegistry` | CLR type/storage registration and validation | `src/DeltaECS/Core/ComponentLayoutRegistry.cs` |
| `QuerySpec` | All/Any/None component predicates | `src/DeltaECS/Core/QuerySpec.cs` |
| `Query` | World/query identity and non-generic read/write access factory | `src/DeltaECS/Core/EntityTypes.cs` |
| `ReadAccess`, `WriteAccess` | Query-bound type-erased access intent | `src/DeltaECS/Core/QueryAccess.cs` |
| `QueryScope` | Dense-only validation and structural lease owner | `src/DeltaECS/Core/QueryScope.cs` |
| `QueryArchetypes`, `QueryChunks`, `QuerySlots` | Independent dense traversal levels | `src/DeltaECS/Core/QueryArchetypes.cs`, `QueryChunks.cs`, `QuerySlots.cs` |
| `QueryChunkCursor` | Current chunk, forward slot traversal and value access | `src/DeltaECS/Core/QueryAccess.cs` |
| `ReadValues`, `WriteValues` | Non-generic prepared values; final `Ref<T>` must match the registered component type. Controlled pre-loop mismatch validation is selected correctness work. | `src/DeltaECS/Core/Values.cs`, `src/DeltaECS/Generic/Values.cs` |
| `World.Query` | Callback-based query execution over `QueryChunkCursor` | `src/DeltaECS/Core/World.cs` |
| `World.Create<T>/Add<T>/Remove<T>/TryGet<T>/Get<T>/Set<T>` | Thin typed single-item boundary over existing structural/component operations | `src/DeltaECS/Generic/World.Generic.cs` |
| generated `World.ForEach` | Delegate and struct-functor dense/sequence matrix, arities 0..4 with explicit read/write patterns | `src/DeltaECS.Generators/ForEachGenerator.cs`, `src/DeltaECS/Delegate/ForEachRuntime.cs`, `src/DeltaECS/Sequence/SequenceComponentRuntime.cs` |
| `World.Entities(ReadOnlySpan<Entity>)` | Ordered non-owning sequence facade | `src/DeltaECS/Sequence/EntitySequence.cs` |
| `World.ForEach(ReadOnlySpan<Entity>, ...)` | Ordered entity-only or typed sequence execution, optionally filtered by `Query` | `src/DeltaECS/Sequence/World.Sequence.cs`, `src/DeltaECS/Sequence/SequenceComponentRuntime.cs` |

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

`QueryPlan` and `ArchetypePlan` live in `QueryAccess.cs`. They own query
matching and the query-row to archetype-row plan. Read them when the issue is
archetype matching, new-archetype refresh, or access row resolution; do not
start with tests or structural move code.

Generated dense callbacks enter through this chain:

```text
World.ForEach<...>(Query, ...)
  -> ForEachRuntime validates Query/ComponentId/T once
  -> World.ExecuteForEach type-erased traversal
  -> generated invoker resolves values once per chunk
  -> delegate or constrained struct-functor call per slot
```

The generator project owns only API repetition. It does not generate storage,
archetype matching or structural kernels.

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
| Generic single-item boundary | `tests/DeltaECSTests/GenericSingleItemApiTests.cs` |
| Ordered sequence facade and terminals | `tests/DeltaECSTests/SequenceExecutionTests.cs` |
| Generated matrix determinism/compilation | `tests/DeltaECS.Generators.Tests/ForEachGeneratorTests.cs` |

For a source change, first locate the relevant method with `rg`, then read the
nearest test method and its setup helpers. Benchmark source is not a substitute
for a production correctness test.

## Compatibility boundaries

- `Chunk.GetComponentRow<T>(int)` is an internal storage primitive, not a
  public user API. Do not remove it while migrating public cursor access.
- `World.Query` is the callback surface for dense component queries;
  `World.OpenQuery` exposes the explicit three-loop form.
- Generated `World.ForEach` is the convenience callback/functor surface;
  `World.Query<TContext>` remains the lower-level cursor callback.
- Do not reintroduce removed ordinal/public unsafe row APIs without an explicit
  API decision and a benchmark contract update.

## Integration and tooling contract

This is the neutral world boundary for engine/editor integration, implemented
by `World`. It is separate from the dense query API. Runtime, structural and
object-based tooling operations belong to one `IEcsWorld`. Callers must not
combine world-local IDs obtained from different worlds.

The IDs below are the canonical core types, not integration-specific
duplicates.
`Stamp` is defined by the separate revision contract with a 64-bit payload. It
is opaque and only supports equality comparison. It is not a timestamp,
sequence number or value that callers may order or add.

```csharp
namespace Delta.ECS;

public readonly struct Entity
{
    public int Index { get; }
    public int Generation { get; }
}

public readonly struct Stamp
{
    public ulong Value { get; }
}

public readonly struct ComponentId
{
    public int Value { get; }
}

public readonly struct SchemaId
{
    public ulong Value { get; }
}
```

The integration interfaces use those core values directly:

```csharp
using System;
using Delta.ECS;

namespace Delta.ECS.Integration;

[Flags]
public enum ComponentCapabilities
{
    None = 0,
    Read = 1,
    Write = 2
}

public readonly record struct ComponentDescriptor(
    ComponentId Id,
    SchemaId Schema,
    string Name,
    Type ValueType,
    ComponentCapabilities Capabilities,
    bool AllowsNull);

public readonly record struct ComponentSnapshot(
    object? Value,
    Stamp Stamp);

public readonly record struct ComponentCatalog(
    ReadOnlyMemory<ComponentDescriptor> Components,
    Stamp Stamp);

public enum EcsReadErrorCode
{
    None,
    EntityNotAlive,
    ComponentUnknown,
    ComponentMissing,
    Unsupported
}

public readonly record struct EcsReadError(
    EcsReadErrorCode Code);

public enum EcsWriteErrorCode
{
    None,
    EntityNotAlive,
    ComponentUnknown,
    ComponentMissing,
    StaleStamp,
    InvalidValue,
    Unsupported
}

public readonly record struct EcsWriteError(
    EcsWriteErrorCode Code);

public interface IEcsWorld
{
    Stamp Stamp { get; }

    ComponentCatalog Catalog { get; }

    void Initialize();
    void Update(float deltaSeconds);
    void Shutdown();

    bool IsAlive(Entity entity);

    Entity Create(
        ReadOnlySpan<ComponentId> components);

    bool Destroy(
        Entity entity);

    bool Add(
        Entity entity,
        ReadOnlySpan<ComponentId> components);

    bool Remove(
        Entity entity,
        ReadOnlySpan<ComponentId> components);

    bool TryGetComponents(
        Entity entity,
        Span<ComponentId> destination,
        out int totalCount);

    bool TryRead(
        Entity entity,
        ComponentId component,
        out ComponentSnapshot snapshot,
        out EcsReadError error);

    bool TryWrite(
        Entity entity,
        ComponentId component,
        object? value,
        Stamp expectedStamp,
        out Stamp writtenStamp,
        out EcsWriteError error);
}
```

### Identity and catalog rules

- `Entity`, `ComponentId`, `SchemaId` and `Stamp` are core `Delta.ECS`
  values used directly by the integration contract. The world API does not
  define parallel ID wrappers or convert their numeric payloads.
- `Entity` remains world-local and `ComponentId` remains registry-local.
  Passing either value to a different `IEcsWorld` is a caller contract
  violation. Because the compact IDs do not carry world/registry identity, a
  coincident numeric value cannot be diagnosed as foreign by the receiver.
- `SchemaId` is the stable identity used to map a component across world
  instances or persisted tooling state. Numeric `ComponentId` values must not
  be persisted.
- No default/invalid ID sentinel is part of this contract. Entity validity is
  checked with `IsAlive`; component validity is membership in
  `Catalog.Components`.
- `Catalog` is an immutable snapshot whose `Components` are sorted by
  `ComponentId`. `Catalog.Stamp` changes whenever the snapshot changes. A
  consumer caches the snapshot and reacquires it when that stamp differs;
  memory from an old catalog stamp must not be used after observing a new
  stamp.
- Registration is append-only at world safe points. Existing component IDs,
  schema IDs and descriptors are never reassigned. `Name` is display metadata,
  not identity; `Schema` must be unique.
- `IEcsWorld.Stamp` and `IEcsWorld.Catalog.Stamp` are independent equality
  domains and must not be compared with each other.
- `ValueType` is the exact tooling-facing CLR type returned by `TryRead` and
  accepted by `TryWrite`. It may be the storage component type or a DTO
  selected by a registered value converter. `AllowsNull` describes null
  acceptance independently of `Type.IsValueType`. `Capabilities` tells tools
  whether object snapshots and writes are supported before they attempt them.

### Lifecycle and threading

`Initialize`, zero or more `Update` calls, and `Shutdown` form one lifecycle.
Lifecycle misuse is a programming error and may throw
`InvalidOperationException`; state-dependent entity/component failures use the
documented boolean/error results instead. `deltaSeconds` must be finite and
non-negative.

The world is a safe-point, single-owner-thread API. Runtime update,
structural operations and tooling access must not execute concurrently. The
optimistic stamp check and successful write are one indivisible world
operation; this contract does not promise general multi-threaded access.

### Structural semantics

- `Create` accepts zero or more known components. An empty span creates a
  zero-component entity. IDs absent from the target registry are argument
  errors. Duplicate IDs are canonicalized before one entity is created.
- `Add` and `Remove` validate the complete input before mutation and are
  all-or-nothing. They return `true` only when the entity's component set
  changed. Empty input and a complete no-op return `false` and do not advance
  stamps.
- `Destroy` returns `true` only when the supplied generation was alive and was
  destroyed. Stale or unknown entities return `false`.
- Added components receive their registered default value. Removed reference
  values are released according to the core storage contract.
- `TryGetComponents` returns `false`, sets `totalCount` to zero and does not
  modify `destination` for a non-alive entity. For a live entity it returns
  `true`, writes the ascending-`ComponentId` prefix that fits, and reports the
  total required count. A live zero-component entity therefore returns
  `true` with `totalCount == 0`.

Every successful atomic structural call creates at most one mutation stamp.
Archetype moves preserve the exact stamps of surviving components; newly added
component slots receive the structural mutation stamp. Swap-back moves values
and their exact stamps together and does not create an extra stamp.

### Tooling read/write semantics

`TryRead` returns the exact stamp for the selected entity/component pair. On
success it sets `error.Code` to `None`. On failure it returns a default
snapshot, performs no mutation and reports the symmetric read error:

| Code | Meaning |
|---|---|
| `EntityNotAlive` | Index/generation is not alive in this world. |
| `ComponentUnknown` | ID is absent from this world's catalog. |
| `ComponentMissing` | ID is known but absent from the entity. |
| `Unsupported` | The descriptor does not advertise `Read`. |

`ComponentSnapshot.Value` must have the descriptor's `ValueType`. Value-type
components are boxed snapshots. Reference-type components may preserve the
stored object identity; neither `object?` nor a `ref readonly` access path makes
the referenced object immutable. Mutating such an object directly bypasses
stamps and write validation and is the caller's responsibility. `TryWrite` is
the tracked tooling-write path. A backend may still register a per-component
converter that returns a detached DTO or serialized snapshot when isolation is
required, but mutable reference components are not `Unsupported` merely for
being mutable references.

`TryWrite` uses `expectedStamp` only as the exact entity/component stamp from a
previous `TryRead` or successful `TryWrite`; `IEcsWorld.Stamp` is not a
substitute. On success the world writes one value, advances its mutation
stamp, returns that exact value through `writtenStamp`, sets `error.Code` to
`None`, and returns `true`. On failure `writtenStamp` is default, no mutation
occurs, and the error has this meaning:

| Code | Meaning |
|---|---|
| `EntityNotAlive` | Index/generation is not alive in this world. |
| `ComponentUnknown` | ID is absent from this world's catalog. |
| `ComponentMissing` | ID is known but absent from the entity. |
| `StaleStamp` | The exact component stamp differs from `expectedStamp`. |
| `InvalidValue` | Nullability or exact CLR type does not match the descriptor. |
| `Unsupported` | The descriptor does not advertise `Write`. |

For non-null values, runtime type equality with the tooling-facing `ValueType`
is required; the world does not perform implicit numeric, enum or inheritance
conversions. A registered value converter may explicitly map that value to a
different storage CLR type. Without such a converter, a reference value is
stored using normal component assignment semantics and may retain
caller-visible identity.

Stamp comparison is deliberately pull-based and consumer-local. A renderer,
editor or other tool stores its own last observed stamps; reading changes for
one consumer never consumes them for another.

## API layers and generated callback API

The implemented structural API is non-generic: entity lifecycle and component
set operations use `Entity`, `ComponentId` and spans. The dense query chain is
also non-generic from `QuerySpec` through `Query`, access tokens, scope,
archetype/chunk/slot iterators and row containers. A CLR component type appears
only at registration, the single-item `World.SetComponent<T>`/
`World.TryGetComponent<T>` boundary, or the terminal `ReadValues.Ref<T>` /
`WriteValues.Ref<T>` call. The existing `World.Query<TContext>` callback is a
compatibility boundary for caller state; `TContext` does not leak into query,
access, storage or cursor types.

The implemented `World.ForEach` callback/functor matrix is source-generated and
covers every combination of:

- no context or one caller-provided `TContext`;
- no entity or current `Entity` argument;
- zero components (the explicit entity-only form), or 1, 2, 3 and 4 typed
  component arguments;
- every read/write bitmask for component arities 1..4, with `in T` read
  arguments and `ref T` write arguments.

The delegate and struct-functor families share one type-erased query
validation, access preparation and dense execution kernel. `World.ForEach`
owns the temporary lease internally. Generated access tags such as
`ForEachAccessTag_RW` make non-all-write lambda/functor contracts explicit
without introducing generic query/access/value objects. `World.OpenQuery`
remains the advanced path for explicit three-loop execution and reusable
prepared accesses.

## Explicit-sequence execution

Sequence execution uses the same `World.ForEach` family as dense
query execution rather than introducing another public selection type:

```csharp
world.ForEach(entities, action);
world.ForEach(entities, in query, action);
```

The unfiltered overload visits every valid occurrence in the supplied
`ReadOnlySpan<Entity>`. The filtered overload treats that span as the candidate set
and applies `query` to each resolved entity; it never broadens execution to every
entity matching the query in the world. Input order and duplicate occurrences are
preserved. Invalid, stale and foreign handles follow the explicit-sequence policy used
by structural APIs.

The delegate/functor arity matrix is source-generated. Both surfaces feed one
type-erased sequence kernel that validates access once, resolves entity locations, and
caches the most recently used archetype row plan. It does not call public
single-item component APIs per entity. A future explicitly named
unordered batch API may group candidates by archetype; `ForEach` must not reorder
silently.

The fluent spelling is
`world.Entities(entities).Where(in query).ForEach(action)`. It is an ordered
facade over the same `ReadOnlySpan<Entity>` candidate set and callback matrix,
not a generic query/storage object.

Implementation files for this family belong in `src/DeltaECS/Sequence`. Public entry
points stay on `World`, and the folder must not grow a parallel query model.
