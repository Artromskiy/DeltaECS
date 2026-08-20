# DeltaECS

Standalone archetype ECS kernel focused on dense iteration, immediate structural
changes, batch operations, cheap tags and predictable memory use. Public
namespace is `Delta.ECS`; project/assembly names remain `DeltaECS*`.

Before choosing work, read [TODO.md](TODO.md). Do not infer tasks from old
benchmark reports or completed roadmap text.

## Storage model

- `Entity` is index + generation; `EntityRecord` resolves its current location.
- `ComponentId` is dense runtime identity; schema IDs are stable tooling identity.
- `Type` is cold registration metadata, never component identity.
- Archetypes use an opaque 256-bit mask; public API does not expose its words so
  the implementation can widen later.
- Chunks store one typed CLR array per dense component in `Array[]` (SoA).
- Different component IDs may use the same CLR type and still own separate rows.
- Value, reference and structs-with-reference components use the same row model.

The production kernel has one storage implementation. Legacy byte storage is a
benchmark reference, not an alternative backend.

## Structural changes and temporary state

Create, destroy, add and remove are immediate. The base world has no command
buffer or mandatory playback barrier. Batch APIs group entities by archetype and
chunk and must not loop through the public atomic API.

Overlay tags use:

```text
TagId -> active ChunkId -> entity-slot bit mask
```

Tag changes do not move entities. Data-bearing overlays and event streams are
future storage classes and should not complicate the dense kernel prematurely.

Structural mutation remains invalid while a conflicting row lease is active;
this is a local lifetime rule, not a global barrier.

## Queries and hot path

Reusable `QueryHandle` instances cache matching archetypes and per-archetype row
plans. Public typed bindings validate world/query/type ownership once. Read rows
return `ReadOnlySpan<T>`; write rows return `Span<T>` and update coarse row
versions once per yielded chunk.

```csharp
var query = world.CreateQuery(description);
var positions = query.Bind<Position>(positionId, RowAccess.Write);
var velocities = query.Bind<Velocity>(velocityId, RowAccess.Read);

world.Query(in query, ref state,
    static (ref State state, ref DenseChunkAccessor chunk) =>
    {
        var p = chunk.GetRow(state.Positions);
        var v = chunk.GetRow(state.Velocities);
        for (var i = chunk.SlotCount - 1; i >= 0; i--)
            p[i].Value += v[i].Value;
    });
```

Raw ordinal row access stays internal. Bindings and cached plans must remain
correct when new archetypes appear. Dense queries use a branch-free path;
overlay queries select full/partial/empty masks once per chunk.

Current optimization work removes repeated
`QueryComponentIndex -> rowIndices[index]` lookup from `GetRow` by caching direct
per-archetype binding rows. Keep validation outside entity loops and preserve
the public binding API.

## Change tracking

Read access never marks data. Requesting a write row records the component row
version even if the caller performs no write. Consumers such as renderer glue
can layer finer entity-slot dirty sets above this coarse mechanism; one consumer
must not consume another consumer's changes. Scheduler synchronization is a
future runner concern, not part of the base world.

## Build and test

```bash
dotnet build DeltaECS.slnx -c Release --no-restore \
  --disable-build-servers -m:1 /p:UseSharedCompilation=false -v:minimal
dotnet test tests/DeltaECSTests/DeltaECSTests.csproj \
  -c Release --no-build --no-restore --disable-build-servers -m:1
```

Correctness coverage includes entity generations, structural transitions,
managed rows, overlay holes, cached-query invalidation, bindings, change
tracking and benchmark scenario contracts.

## Benchmarks

The unified manual comparison covers DeltaECS, Arch, Friflo.Engine.ECS,
DefaultEcs and LeoECS Lite across iteration, atomic structural operations,
list-batch operations and query-batch operations. DeltaECS is the baseline.

```bash
dotnet build benchmarks/DeltaECS.Benchmarks/DeltaECS.Benchmarks.csproj \
  -c Release --no-restore
dotnet benchmarks/DeltaECS.Benchmarks/bin/Release/net8.0/DeltaECS.Benchmarks.dll \
  full-comparison --filter '*' --warmupCount 3 --iterationCount 5 \
  --launchCount 1 --exporters json csv markdown github \
  --artifacts artifacts/full-comparison \
  --combined-report artifacts/full-comparison
```

Do not run this suite during ordinary review. A `--job dry` run verifies only
compilation/lifecycle and is not a performance result. Version comparison is a
separate manual GitHub Actions suite for API-compatible DeltaECS revisions.

Performance claims must name runtime, hardware, entity count, layout and memory
cost. Smoke tests and shared-runner cross-run differences are not evidence of a
regression or win.
