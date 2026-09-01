# DeltaECS

Standalone archetype ECS kernel focused on fast component iteration, immediate
structural changes, batch operations and predictable memory use.
Public namespace is `Delta.ECS`; project/assembly names remain `DeltaECS*`.

This is the repository's substantive documentation entry point. The repository
root intentionally contains only agent and workflow controls (`AGENTS.md`,
`TODO.md`, `WORKFLOW.md` and `IDEAS.md`); API, architecture, benchmark and
decision documentation lives below `docs/`.

## API organization

The implementation is split by API role while sharing one archetype/chunk
storage model:

| Area | Public role | Details |
|---|---|---|
| Core | entities, component IDs, structural operations, queries and explicit traversal | [Core API](src/DeltaECS/Core/README.md) |
| Generic | typed registration, single-item helpers and terminal row refs | [Generic API](src/DeltaECS/Generic/README.md) |
| Delegate | Delegate `ForEach` callbacks | [Delegate API](src/DeltaECS/Delegate/README.md) |
| Functor | struct-based `ForEach` callbacks | [Functor API](src/DeltaECS/Functor/README.md) |
| Sequence | ordered execution over explicit entity candidates | [Sequence API](src/DeltaECS/Sequence/README.md) |
| Parallel | chunk-disjoint multi-threaded query execution | [Parallel API](src/DeltaECS/Parallel/README.md) |
| Integration | neutral runtime/editor `IEcsWorld` boundary | [Integration API](src/DeltaECS/API/README.md) |
| Stamps | catalog and entity/component mutation revisions | [Stamp contract](src/DeltaECS/Stamps/README.md) |

The [source API index](src/DeltaECS/README.md) maps folders, and the
[consumer generator README](src/DeltaECS.Generators/README.md) explains
demand-driven component callback generation.

## Storage

- `Entity` is index + generation; `EntityRecord` resolves its location.
- `ComponentId` is compact runtime identity; schema IDs are stable tooling
  identity; `Type` is cold registration metadata.
- Archetypes use an immutable component mask backed by a dynamically sized
  native array of `uint` words. Non-negative `ComponentId` values are not
  limited to 256 bits; `ComponentMask.Capacity` remains only as the legacy
  four-word source-compatibility constant. Practical limits are available
  native memory and the `int` range of `ComponentId.Value`.
- Chunks store one typed CLR array per component in `Array[]` SoA rows.
- The default chunk capacity is 512 entities; callers may provide another
  positive capacity to `World` when the workload requires it.
- Different component IDs may use the same CLR type and retain separate rows.
- Value, reference and structs-with-reference components share one row model.

Create, destroy, add and remove are immediate; the world has no mandatory
command buffer/playback barrier. Batch APIs group by archetype/chunk rather
than loop through public atomic operations.

`World` explicitly implements the neutral `Delta.ECS.Integration.IEcsWorld`
lifecycle and tooling boundary. Its parameterless `Update` method validates
the integration lifecycle and performs no scheduling because this ECS kernel
has no system scheduler or time source. Runtime hosts remain responsible for
invoking their systems. Integration structural and tooling operations are
valid only between `Initialize` and `Shutdown`.

## Queries and changes

Reusable `Query` values cache matching archetypes and row plans. Non-generic
access requests validate world/query ownership outside the entity loop. The
component type is supplied only at registration and at the terminal
`ReadRow.Ref<T>`/`WriteRow.Ref<T>` call. Raw ordinal access remains
internal.

For explicit traversal, `world.BeginScope(in query)` exposes a primary
two-level chunk/slot path. The lower-level archetype/chunk/slot path remains
available when callers need archetype boundaries. Generated `ForEach` delegate
and functor overloads provide the callback form.

The query API has three deliberately separate stages: `QuerySpec` describes
selection, `World.CreateQuery` returns the world-owned `Query`, and
`query.AccessRead(id)` or `query.AccessWrite(id)` declares component access and
returns the corresponding non-generic `ReadAccess` or `WriteAccess` token. Inside an
scope started by `BeginScope`, `slots.GetRow(access)` validates that declaration against
the active scope and exposes a non-generic component row whose
terminal `Ref<T>` call provides the component reference.
`T` must match the component type registered for the access token. Controlled
pre-loop mismatch validation is selected correctness work; callers must not
use a different `T` to reinterpret row storage.

The primary explicit path flattens matching archetypes into the chunk iterator:

```csharp
using var scope = world.BeginScope(in query);
var positionAccess = query.AccessRead(positionId);
var position = positionAccess;
var chunks = scope.Chunks;
while (chunks.MoveNext())
{
    var slots = chunks.Current.Slots;
    var row = slots.GetRow(position);
    while (slots.MoveNext())
    {
        _ = row.Ref<Position>(slots);
    }
}
```

Use the independent three-level path when archetype metadata or boundaries are
part of the algorithm:

```csharp
using var scope = world.BeginScope(in query);
var positionAccess = query.AccessRead(positionId);
var position = positionAccess;
var archetypes = scope.Archetypes;
while (archetypes.MoveNext())
{
    var chunks = archetypes.Current.Chunks;
    while (chunks.MoveNext())
    {
        var slots = chunks.Current.Slots;
        var row = slots.GetRow(position);
        while (slots.MoveNext())
        {
            _ = row.Ref<Position>(slots);
        }
    }
}
```

### API layers and demand-driven callback generation

The structural kernels remain type-erased: component-set operations use
`Entity`, `ComponentId` and spans, while query selection, access tokens and
archetype/chunk/slot traversal remain non-generic. Thin single-item helpers
`Create<T>`, `Add<T>`, `Remove<T>`, `TryGet<T>`, `Get<T>` and `Set<T>` validate
the component type and delegate to those existing kernels; they do not create
a typed storage or query layer.

`World.ForEach` is the high-level component-query entry point. Its delegate and
struct-functor overloads are generated on demand in the consumer assembly by
the DeltaECS analyzer. The generator inspects the calls that the consumer
actually makes and emits only the requested callback shapes:

| Callback axis | Implemented forms |
|---|---|
| Context | no context, or one caller-provided `TContext` |
| Entity argument | no entity, or current `Entity` |
| Component arity | handwritten zero-component delegates, plus generated component-bearing callbacks with 1–256 parameters |
| Component access | any read/write pattern for the requested arity |
| Component ID form | no-ID primary registration, or explicit IDs for secondary registrations of the same CLR type |
| Selection | prepared `Query`; the generator emits extension methods in the consumer assembly |

Component callback parameters support four modes: `ref readonly T` (`R`),
`ref T` (`W`), `in T` (`I`), and by-value `T` (`V`). Only `ref T` is a write;
the other modes read the component. The generated callback shape uses these
letters in its internal delegate name.
The component type is validated against the registered `ComponentId` before
execution. Dense generated paths resolve each row once per chunk and advance
direct typed references inside the generated entity loop; sequence paths use
the same trusted reference endpoint for the current entity chunk.
Functors implement one of four stable marker interfaces: `IForEach`,
`IForEachEntity`, `IForEachContext<TContext>`, or
`IForEachContextEntity<TContext>`. The generator derives component types and
read/write intent from the concrete `Invoke` signature; it does not generate
pattern-specific functor interfaces.
No-ID calls resolve each component type independently through its registry
primary `ComponentId`; they do not infer IDs from the query's `All` mask, so a
query may contain additional required components. If one CLR type has multiple
registrations, the secondary row is selected with the explicit-ID form.

The generated callback/ref boundary is the only place where component types
are carried through the callback shape. `Query`, access declarations, scopes,
plans, iterators and row containers remain type-erased. The runtime/storage
kernel is shared by all generated shapes and contains no handwritten
variadic 1–4 matrix.

The source spelling remains `world.ForEach(...)`, but a component-bearing
member is emitted as an extension method in the consumer assembly. A consumer
must reference the DeltaECS analyzer/source-generator; the generator cannot add
instance members to a previously compiled `World`.

For maximum performance on the delegate-shaped hot path, enable the optional
Roslyn interceptor configuration in the consumer project:

```xml
<PropertyGroup>
  <InterceptorsNamespaces>Delta.ECS.Generated</InterceptorsNamespaces>
</PropertyGroup>
<ItemGroup>
  <CompilerVisibleProperty Include="InterceptorsNamespaces" />
</ItemGroup>
```

With this opt-in, supported static non-capturing `World.ForEach` and
`World.ForEachEntity` call sites are lowered at compile time to generated
trusted struct-functor execution without changing the user-facing API. The
interceptor is not a universal delegate replacement: capturing, instance,
pre-created, ambiguous, generic, async, sequence, and non-interceptable calls
use the ordinary delegate fallback. The analyzer is build-time only and is not
deployed with a NativeAOT application.

Configuration and exact eligibility rules are in the
[generator README](src/DeltaECS.Generators/README.md#optional-roslyn-interceptor-path).

Ordered sequence execution uses the non-owning fluent facade:

```csharp
world.From(entities).Where(in query).ForEachEntity(action);
```

It preserves input order and duplicate occurrences, skips stale entities and
uses the same generated delegate/functor matrix, including typed mixed
read/write callbacks. Typed sequence execution resolves entity records
directly, caches the last archetype row plan and invokes the same generated
state used by world-query execution; it does not loop through public single-item
`TryGet`/`Set` calls and does not introduce another storage model. Structural
`Add`/`Remove`/`Destroy` terminals forward to the existing batch kernels.

Generated `ForEach` owns query validation and access preparation.
Explicit `BeginScope` remains the advanced path for direct three-loop traversal.
Both routes use the existing type-erased query plan and chunk traversal; no
generic component type is carried by `Query`, an access token or an iterator.
The generator has a documented maximum callback arity of 256. This is an
independent source-generation limit and is not a component-mask capacity:
the runtime mask can address component IDs above 255. Calls above the
callback-parameter limit produce a diagnostic instead of silently falling back
to a handwritten matrix.

The root scope validates ownership and owns the lease. The archetype, chunk and
slot iterators are borrowed views over that execution; outer advancement and
row access validate the active session as required. They do not own a second
lease or structural state.

Structural mutation is invalid while a conflicting row lease is active. This
is a local lifetime rule, not a global barrier. External consumers keep their
own cursors/caches; one consumer never clears another's change state.

See [TODO.md](../TODO.md) before selecting work, [IDEAS.md](../IDEAS.md) for deferred
designs, [WORKFLOW.md](../WORKFLOW.md) for correctness checks and
[benchmark guide](benchmarks/README.md) for bounded assembly-guided work.
The isolated [call profiler](tools/DeltaECS.Profiling/README.md) provides
Metalama-instrumented self/inner call-tree timing without adding a dependency
to the production assembly. Full comparisons and version benchmarks are manual
only.
