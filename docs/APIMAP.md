# DeltaECS API and source map

This file is a contributor navigation map. It is intentionally smaller than
the public contract: stable behavior belongs in `docs/README.md`, and
folder-specific API details are kept in the corresponding `docs/src/` tree.

The canonical generated API grammar is [API-GRAMMAR.md](API-GRAMMAR.md).
Consult it before changing overload shapes or generator templates.

## Fast read order

1. Read the repository and project `AGENTS.md` files.
2. Read [API-GRAMMAR.md](API-GRAMMAR.md) for overload shapes, then `docs/README.md`
   for stable behavior and `WORKFLOW.md` before commands.
3. Use the folder map below to choose the smallest source slice.
4. Read the nearest focused test only after locating the implementation.

Useful searches:

```bash
rg -n "public |internal |Query|BeginScope|Access|MoveNext" src/DeltaECS
rg -n "<relevant API or invariant>" tests/DeltaECSTests
```

## Folder map

| Folder | Responsibility | Documentation |
|---|---|---|
| `Core` | World, identity and storage-facing structural operations | [Core API](src/DeltaECS/Core/README.md) |
| `Generic` | CLR-type registration and single-component convenience operations | [Generic API](src/DeltaECS/Generic/README.md) |
| `Delegate` | Delegate callback contracts and zero-component compiler anchors | [Delegate API](src/DeltaECS/Delegate/README.md) |
| `Functor` | Marker contracts for generated struct-functor callbacks | [Functor API](src/DeltaECS/Functor/README.md) |
| `Parallel` | Chunk-disjoint multi-threaded query execution | [Parallel API](src/DeltaECS/Parallel/README.md) |
| `API` | Neutral integration contract implemented by `World` | [Integration API](src/DeltaECS/API/README.md) |
| `Stamps` | Catalog and entity/component revision values | [Stamp contract](src/DeltaECS/Stamps/README.md) |
| `Properties` | Assembly metadata; no consumer API | — |

The consumer source generator is documented in
[DeltaECS.Generators/README.md](src/DeltaECS.Generators/README.md).

## Public API map

| Type or entry point | Role | Source |
|---|---|---|
| `World` | World ownership, entity lifecycle, structural operations and query entry points | `src/DeltaECS/Core/World.cs` and partials |
| `Entity` | Index/generation entity handle | `src/DeltaECS/Core/EntityTypes.cs` |
| `ComponentId` | World-local component identity | `src/DeltaECS/Core/ComponentTypes.cs` |
| `SchemaId` | Stable schema identity used during component registration | `src/DeltaECS/Core/ComponentTypes.cs` |
| `ComponentLayoutRegistry` | Component registration and primary CLR-type lookup | `src/DeltaECS/Core/ComponentLayoutRegistry.cs` |
| `QuerySpec` | Opaque `All`/`Any`/`None` selection description created by the fluent query factories | `src/DeltaECS/Core/QuerySpec.cs` |
| `Query` | World-owned cached query and generated API input | `src/DeltaECS/Core/EntityTypes.cs` |
| `ReadAccess`, `WriteAccess` | Compiler-support access tokens used by generated callbacks | `src/DeltaECS/Core/QueryAccess.cs` |
| `World.Create<T>`, `Add<T>`, `Remove<T>`, `TryGet<T>`, `Has<T>`, `Get<T>`, `Set<T>` and typed stamps | Single-component typed conveniences over core operations | `src/DeltaECS/Generic/World.Generic.cs` |
| Generated `World.Create<T1,...>`, `Add<T1,...>`, `Remove<T1,...>`, multi-value `Set<T1,...>` | On-demand primary-component structural operations and archetype-validated value writes | `src/DeltaECS.Generators/GeneratedStructuralGenerator.cs` |
| `World/Query.WhereAll`, `WhereAny`, `WhereNone` | Runtime `ComponentId` factories; generated typed variants compose through the existing query cache | `src/DeltaECS/Core/World.cs`, `src/DeltaECS/Core/EntityTypes.cs`, `src/DeltaECS.Generators/GeneratedQueryGenerator.cs`, `src/DeltaECS/Core/QuerySpec.cs` |
| `World.ForEach`, `ForEachEntity` | Delegate callback entry points and throwing zero-component compiler anchors | `src/DeltaECS/Delegate/ForEachZeroArity.cs` |
| `IForEach*` | Stable functor marker contracts | `src/DeltaECS/Functor/ForEachFunctorContracts.cs` |
| `World.ForEachParallel` | Generated typed parallel query callback entry point | `src/DeltaECS/Parallel/World.Parallel.cs` |
| `IEcsWorld` | Neutral lifecycle, structural and object-value integration contract | `src/DeltaECS/API/IntegrationContracts.cs` |
| `Stamp` | 64-bit equality token for exact component revisions | `src/DeltaECS/Stamps/Stamp.cs` |

## Query execution boundary

Consumers use the generated `ForEach`, `ForEachEntity`, `ForEachParallel` and
`ForEachEntityParallel` methods listed in [API-GRAMMAR.md](API-GRAMMAR.md).
They validate the query, acquire the structural lease, prepare typed component
routes and execute the dense chunk loop. The former `BeginScope`/iterator/row
chain was removed; generated callbacks are the supported consumer shape.

## Generated callback path

The analyzer runs in the consumer assembly and emits only callback shapes
observed by that consumer. It supports:

- no context or one caller-provided context;
- callbacks with or without `Entity`;
- zero-component callback forms (which throw) and component-bearing generated
  forms;
- `ref readonly T`, `in T`, and by-value `T` reads, plus `ref T` writes;
- primary component lookup or explicit `ComponentId` selection;
- delegate and struct-functor forms.

The generated callback is the supported consumer surface over the type-erased
query plan and compiler-support access declarations. CLR component types appear
at registration and at the callback/ref boundary; they are not carried by
query or plan storage. See the generator README for lambda inference and
diagnostics.

For maximum performance on delegate-shaped hot loops, the consumer should
enable the project-local Roslyn interceptor opt-in:

```xml
<InterceptorsNamespaces>Delta.ECS.Generated</InterceptorsNamespaces>
<CompilerVisibleProperty Include="InterceptorsNamespaces" />
```

Eligible static non-capturing `World.ForEach` calls keep the same public source
API but lower to the generated trusted struct-functor path. Unsupported or
capturing callbacks retain ordinary delegate semantics; the analyzer remains
build-time only and is not part of a NativeAOT deployment.

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
  marked through the operation-specific entity, chunk or archetype stamp route.
- `Entity` and `ComponentId` are compact world-local core values. `SchemaId`
  is the stable cross-world identity used by integration consumers.
- `Stamp` is a 64-bit equality token (`ulong Value`). It is not wall-clock
  time; consumers compare exact values and must not infer ordering from them.
- Effective component stamps combine entity/component, chunk/component and
  archetype/component overrides. The latter two layers are centrally owned by
  `World`; they do not enlarge `Chunk` or `Archetype`. There is no aggregate
  world mutation stamp; consumers use exact entity/component stamps.
- `IEcsWorld` (`Delta.ECS.Integration`) is a neutral local .NET boundary. It uses the core `Entity` and
  `ComponentId` types and exposes object snapshots only for integration work.

See the [integration README](src/DeltaECS/API/README.md) and
[stamp README](src/DeltaECS/Stamps/README.md) for their complete contracts.

## Focused test map

| Concern | Test file |
|---|---|
| Lifecycle, queries, rows and leases | `tests/DeltaECSTests/DeltaECSTests.cs` |
| Row defaults, managed references and stale entities | `tests/DeltaECSTests/ComponentRowOperationTests.cs` |
| Query structural operations | `tests/DeltaECSTests/QueryStructuralOperationsTests.cs` |
| Active chunk reuse | `tests/DeltaECSTests/ActiveChunkTests.cs` |
| Structural transitions and records | `tests/DeltaECSTests/StructuralAlgorithmTests.cs` |
| Generic single-item boundary | `tests/DeltaECSTests/GenericSingleItemApiTests.cs` |
| Parallel chunk execution | `tests/DeltaECSTests/ParallelIterationTests.cs` |
| Consumer source generation | `tests/DeltaECS.Generators.Tests/DemandDrivenForEachGeneratorTests.cs`, `tests/DeltaECS.Generators.Consumer/` |

The `docs/README.md` is the stable contract; `TODO.md` selects work and
`IDEAS.md` records proposals that have not been selected.
