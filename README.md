# DeltaECS

Standalone archetype ECS kernel focused on dense iteration, immediate
structural changes, batch operations and predictable memory use.
Public namespace is `Delta.ECS`; project/assembly names remain `DeltaECS*`.

## Storage

- `Entity` is index + generation; `EntityRecord` resolves its location.
- `ComponentId` is dense runtime identity; schema IDs are stable tooling
  identity; `Type` is cold registration metadata.
- Archetypes currently use an opaque 256-bit mask. Registration beyond that
  checked capacity is an implementation-limit error; widening is not promised
  as ABI-compatible.
- Chunks store one typed CLR array per component in `Array[]` SoA rows.
- Different component IDs may use the same CLR type and retain separate rows.
- Value, reference and structs-with-reference components share one row model.

Create, destroy, add and remove are immediate; the world has no mandatory
command buffer/playback barrier. Batch APIs group by archetype/chunk rather
than loop through public atomic operations.

`World` implements the neutral `Delta.ECS.Integration.IEcsWorld` lifecycle and
tooling boundary. Its `Update` method validates lifecycle state and a finite,
non-negative delta, but intentionally performs no scheduling because this ECS
kernel has no system scheduler. Runtime hosts remain responsible for invoking
their systems. Integration structural and tooling operations are valid only
between `Initialize` and `Shutdown`.

## Queries and changes

Reusable `Query` values cache matching archetypes and row plans. Non-generic
access requests validate world/query ownership outside the entity loop. The
component type is supplied only at registration and at the terminal
`ReadRow.Ref<T>`/`WriteRow.Ref<T>` call. Raw ordinal access remains
internal.

For explicit low-level traversal, `world.OpenQuery(in query)` exposes three
independent nested loops: archetype, chunk and forward slot. `World.Execute` is
the callback form of the same dense component selection.

The dense API has three deliberately separate stages: `QuerySpec` describes
selection, `World.CreateQuery` returns the world-owned `Query`, and
`query.AccessRead(id)` or `query.AccessWrite(id)` declares component access and
returns the corresponding non-generic `ReadAccess` or `WriteAccess` token. Inside an
`OpenQuery` scope, `scope.Bind(access)` validates that declaration once;
`slots.GetRow(prepared)` then exposes a non-generic component row whose
terminal `Ref<T>` call provides the component reference.
`T` must match the component type registered for the access token. Controlled
pre-loop mismatch validation is selected correctness work; callers must not
use a different `T` to reinterpret row storage.

Queries use the thinner independent dense path:

```csharp
using var scope = world.OpenQuery(in query);
var positionAccess = query.AccessRead(positionId);
var position = scope.Bind(positionAccess);
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

`World.ForEach` is the high-level dense entry point. Its delegate and
struct-functor overloads are generated on demand in the consumer assembly by
the DeltaECS analyzer. The generator inspects the calls that the consumer
actually makes and emits only the requested callback shapes:

| Callback axis | Implemented forms |
|---|---|
| Context | no context, or one caller-provided `TContext` |
| Entity argument | no entity, or current `Entity` |
| Component arity | zero components and any practical arity up to the 256-component mask capacity |
| Component access | any read/write pattern for the requested arity |
| Component ID form | no-ID primary registration, or explicit IDs for secondary registrations of the same CLR type |
| Dense selection | prepared `Query`; the generator emits extension methods in the consumer assembly |

Read arguments are passed as `in T`; write arguments are passed as `ref T`.
The component type is validated against the registered `ComponentId` before
execution; row resolution occurs once per chunk, outside the entity loop.
Functors are structs constrained by the generated `IForEach*` interfaces.
No-ID calls resolve each component type independently through its registry
primary `ComponentId`; they do not infer IDs from the query's `All` mask, so a
query may contain additional required components. If one CLR type has multiple
registrations, the secondary row is selected with the explicit-ID form.

The generated callback/ref boundary is the only place where component types
are carried through the callback shape. `Query`, access declarations, scopes,
plans, iterators and row containers remain type-erased. The runtime/storage
kernel is shared by all generated shapes and contains no handwritten
variadic 1–4 matrix.

The source-compatible spelling remains `world.ForEach(...)`, but the generated
member is an extension method when the call is emitted for a consumer
assembly. A consumer must reference the DeltaECS analyzer/source-generator;
the generator cannot add instance members to a previously compiled `World`.
This is source-compatible API evolution, not a promise that an old binary
compiled against removed fixed-matrix instance methods will resolve the new
extension.

Ordered sequence execution is available both directly and through the
non-owning fluent facade:

```csharp
world.ForEachEntity(entities, action);
world.Entities(entities).Where(in query).ForEachEntity(action);
```

It preserves input order and duplicate occurrences, skips stale entities and
uses the same generated delegate/functor matrix, including typed mixed
read/write callbacks. Typed sequence execution resolves entity records
directly, caches the last archetype row plan and invokes the same generated
state used by dense execution; it does not loop through public single-item
`TryGet`/`Set` calls and does not introduce another storage model. Structural
`Add`/`Remove`/`Destroy` terminals forward to the existing batch kernels.

Generated dense `ForEach` owns query validation and access preparation.
Explicit `OpenQuery` remains the advanced path for direct three-loop traversal.
Both routes use the existing type-erased query plan and chunk cursor; no
generic component type is carried by `Query`, an access token or an iterator.
The generator has a documented maximum generated arity of 256, matching the
component-mask capacity. Calls above that limit produce a diagnostic instead
of silently falling back to a handwritten matrix.

The root scope validates ownership and owns the lease. The archetype, chunk and
slot iterators contain only their own traversal state; dense `MoveNext` methods
contain no world or lifetime branch.

Structural mutation is invalid while a conflicting row lease is active. This
is a local lifetime rule, not a global barrier. External consumers keep their
own cursors/caches; one consumer never clears another's change state.

See [TODO.md](TODO.md) before selecting work, [IDEAS.md](IDEAS.md) for deferred
designs, [WORKFLOW.md](WORKFLOW.md) for correctness checks and
[benchmarks/README.md](benchmarks/README.md) for bounded assembly-guided work.
Full comparisons and version benchmarks are manual only.
