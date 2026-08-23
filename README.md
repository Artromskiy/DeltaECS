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
`ReadValues.Ref<T>`/`WriteValues.Ref<T>` call. Raw ordinal access remains
internal.

For explicit low-level traversal, `world.OpenQuery(in query)` exposes three
independent nested loops: archetype, chunk and forward slot. `World.Query` is
the callback form of the same dense component selection.

The dense API has three deliberately separate stages: `QuerySpec` describes
selection, `World.CreateQuery` returns the world-owned `Query`, and
`query.AccessRead(id)` or `query.AccessWrite(id)` declares component access and
returns the corresponding non-generic `ReadAccess` or `WriteAccess` token. Inside an
`OpenQuery` scope, `scope.Bind(access)` validates that declaration once;
`slots.Get(prepared)` then exposes a non-generic values
object whose terminal `Ref<T>` call provides the component reference.
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
        var row = slots.Get(position);
        while (slots.MoveNext())
        {
            _ = row.Ref<Position>(slots);
        }
    }
}
```

The planned high-level execution entry point is `World.ForEach`. It will own
query scope creation, validation, access preparation and disposal internally,
so the common user API does not expose scope management. Explicit
`OpenQuery` remains an advanced path for reusing prepared accesses across
several passes or combining `ForEach` with lower-level traversal. Both paths
must share the same dense execution kernel; the choice must not change the
hot-loop work.

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
