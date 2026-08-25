# DeltaECS API and source map

This file is a contributor navigation map. It is intentionally smaller than
the public contract: stable behavior belongs in the root `README.md`, and
folder-specific API details belong in the README beside the source.

## Fast read order

1. Read the repository and project `AGENTS.md` files.
2. Read `README.md` for stable behavior and `WORKFLOW.md` before commands.
3. Use the folder map below to choose the smallest source slice.
4. Read the nearest focused test only after locating the implementation.

Useful searches:

```bash
rg -n "public |internal |Query|OpenQuery|Access|MoveNext" src/DeltaECS
rg -n "<relevant API or invariant>" tests/DeltaECSTests
```

## Folder map

| Folder | Responsibility | Documentation |
|---|---|---|
| `Core` | World, identity, storage-facing structural operations and explicit query traversal | [Core API](../src/DeltaECS/Core/README.md) |
| `Generic` | CLR-type registration and single-component convenience operations | [Generic API](../src/DeltaECS/Generic/README.md) |
| `Delegate` | Delegate callback contracts and zero-component callback entry points | [Delegate API](../src/DeltaECS/Delegate/README.md) |
| `Functor` | Marker contracts and generated functor runtime bridge | [Functor API](../src/DeltaECS/Functor/README.md) |
| `Sequence` | Ordered execution over an explicit entity span | [Sequence API](../src/DeltaECS/Sequence/README.md) |
| `API` | Neutral integration contract implemented by `World` | [Integration API](../src/DeltaECS/API/README.md) |
| `Stamps` | World and component revision values | [Stamp contract](../src/DeltaECS/Stamps/README.md) |
| `Properties` | Assembly metadata; no consumer API | — |

The consumer source generator is documented in
[DeltaECS.Generators/README.md](../src/DeltaECS.Generators/README.md).

## Public API map

| Type or entry point | Role | Source |
|---|---|---|
| `World` | World ownership, entity lifecycle, structural operations and query entry points | `src/DeltaECS/Core/World.cs` and partials |
| `Entity` | Index/generation entity handle | `src/DeltaECS/Core/EntityTypes.cs` |
| `ComponentId`, `ComponentMask` | World-local identity and query matching | `src/DeltaECS/Core/ComponentTypes.cs` |
| `ComponentLayout`, `SchemaId` | Registered CLR layout and stable schema identity | `src/DeltaECS/Core/ComponentTypes.cs` |
| `ComponentLayoutRegistry` | Non-generic layout registration and lookup | `src/DeltaECS/Core/ComponentLayoutRegistry.cs` |
| `QuerySpec` | `All`/`Any`/`None` selection masks | `src/DeltaECS/Core/QuerySpec.cs` |
| `Query` | World-owned cached query and access factory | `src/DeltaECS/Core/EntityTypes.cs` |
| `ReadAccess`, `WriteAccess` | Non-generic query access declarations | `src/DeltaECS/Core/QueryAccess.cs` |
| `QueryScope` | One validated query execution and its structural lease | `src/DeltaECS/Core/QueryScope.cs` |
| `QueryArchetypes`, `QueryChunks`, `QuerySlots` | Independent traversal levels | `src/DeltaECS/Core/QueryArchetypes.cs`, `QueryChunks.cs`, `QuerySlots.cs` |
| `ReadRow`, `WriteRow` | Non-generic row values; terminal `Ref<T>` is the typed boundary | `src/DeltaECS/Core/Rows.cs`, `src/DeltaECS/Generic/Rows.cs` |
| `World.Create<T>`, `Add<T>`, `Remove<T>`, `TryGet<T>`, `Get<T>`, `Set<T>` | Single-component typed conveniences over core operations | `src/DeltaECS/Generic/World.Generic.cs` |
| `World.ForEach`, `ForEachEntity` | Delegate callback entry points, including handwritten zero-component forms | `src/DeltaECS/Delegate/ForEachZeroArity.cs` |
| `IForEach*` | Stable functor marker contracts | `src/DeltaECS/Functor/ForEachFunctorContracts.cs` |
| `World.From` and `ForEachEntity` | Ordered entity-sequence entry points and terminals | `src/DeltaECS/Sequence/World.Sequence.cs`, `EntitySequence.cs` |
| `IEcsWorld` | Neutral lifecycle, structural and object-value integration contract | `src/DeltaECS/API/IntegrationContracts.cs` |
| `Stamp` | Opaque 64-bit revision value | `src/DeltaECS/Stamps/Stamp.cs` |

## Explicit query traversal

Read this chain for the three-loop API:

```text
World.OpenQuery(in Query)
  -> QueryScope.Archetypes
  -> QueryArchetypes.MoveNext()
  -> QueryArchetypes.Current.Chunks
  -> QueryChunks.MoveNext()
  -> QueryChunks.Current.Slots
  -> QuerySlots.GetRow(access)
  -> QuerySlots.MoveNext()
  -> ReadRow/WriteRow.Ref<T>(slots)
```

The public shape is:

```csharp
using var scope = world.OpenQuery(in query);
var positionAccess = query.AccessWrite(positionId);
var position = positionAccess;
var archetypes = scope.Archetypes;

while (archetypes.MoveNext())
{
    var chunks = archetypes.Current.Chunks;
    while (chunks.MoveNext())
    {
        var slots = chunks.Current.Slots;
        var positions = slots.GetRow(position);
        while (slots.MoveNext())
        {
            ref Position value = ref positions.Ref<Position>(slots);
            value.X++;
        }
    }
}
```

`QueryScope` validates query ownership and owns the active structural lease.
The child iterators are borrowed stack-only views. `Bind` performs scope-level
ownership validation; row resolution then uses the access declaration and the
current chunk's prepared row table. `ReadRow`, `WriteRow` and the iterators
must not escape the scope.

## Generated callback path

The analyzer runs in the consumer assembly and emits only callback shapes
observed by that consumer. It supports:

- no context or one caller-provided context;
- callbacks with or without `Entity`;
- zero-component callbacks and component arities up to the 256-bit mask limit;
- `in T` reads and `ref T` writes;
- primary component lookup or explicit `ComponentId` selection;
- delegate and struct-functor forms.

The generated callback is a convenience surface over the same type-erased
query plan, access declarations and chunk traversal used by `OpenQuery`. CLR
component types appear at registration and at the callback/ref boundary; they
are not carried by query, access, plan or iterator storage. See the generator
README for lambda inference and diagnostics.

## Ordered sequence path

`World.From(ReadOnlySpan<Entity>)` creates a non-owning ordered facade.
`Where(in Query)` narrows that candidate span; it does not enumerate all
entities matching the query. Callback terminals preserve input order and
duplicates while skipping stale or foreign handles. `Add`, `Remove` and
`Destroy` forward to the existing structural batch kernels.

## Structural and storage navigation

- Entity resolution and structural transitions: `src/DeltaECS/Core/World.cs`.
- Archetype ownership and active chunks: `src/DeltaECS/Core/Archetype.cs`.
- Component rows, swap-back and reference clearing: `src/DeltaECS/Core/Chunk.cs`.
- Layout registration: `src/DeltaECS/Core/ComponentLayoutRegistry.cs`.
- Query matching and physical row plans: `src/DeltaECS/Core/QueryAccess.cs`.

Do not start a pure API/documentation task in structural tests. For storage
work, inspect the focused test class after locating the source method.

## Lifetime, revision and integration rules

- Structural mutation is rejected while a conflicting query scope is active.
- Read and write access intent is declared before row traversal; write rows are
  marked according to the current query write session.
- `Entity` and `ComponentId` are compact world-local core values. `SchemaId`
  is the stable cross-world identity used by integration consumers.
- `Stamp` values are opaque equality tokens, not wall-clock timestamps or
  arithmetic counters exposed to consumers.
- `IEcsWorld` is a neutral local .NET boundary. It uses the core `Entity` and
  `ComponentId` types and exposes object snapshots only for integration work.

See the [integration README](../src/DeltaECS/API/README.md) and
[stamp README](../src/DeltaECS/Stamps/README.md) for their complete contracts.

## Focused test map

| Concern | Test file |
|---|---|
| Lifecycle, queries, rows and leases | `tests/DeltaECSTests/DeltaECSTests.cs` |
| Row defaults, managed references and stale entities | `tests/DeltaECSTests/ComponentRowOperationTests.cs` |
| Query structural operations | `tests/DeltaECSTests/QueryStructuralOperationsTests.cs` |
| Active chunk reuse | `tests/DeltaECSTests/ActiveChunkTests.cs` |
| Structural transitions and records | `tests/DeltaECSTests/StructuralAlgorithmTests.cs` |
| Generic single-item boundary | `tests/DeltaECSTests/GenericSingleItemApiTests.cs` |
| Ordered sequence facade | `tests/DeltaECSTests/SequenceExecutionTests.cs` |
| Consumer source generation | `tests/DeltaECS.Generators.Tests/DemandDrivenForEachGeneratorTests.cs`, `tests/DeltaECS.Generators.Consumer/` |

The root `README.md` is the stable contract; `TODO.md` selects work and
`IDEAS.md` records proposals that have not been selected.
