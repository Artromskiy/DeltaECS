# Core API

`Core` contains the type-erased ECS model. Component CLR types do not flow
through entity storage, query descriptions, access tokens or iterators.

## Identity and registration

- `Entity` is an index/generation handle. `default(Entity)` (`[0:0]`) is the
  only invalid sentinel; valid handles have a non-negative index and a
  positive generation. Use `World.IsAlive(entity)` to test liveness in a
  particular world. Destroyed handles become stale.
- `ComponentId` is a world-local component identity.
- `SchemaId` is stable tooling/schema identity.
- `ComponentLayoutRegistry` registers layouts and resolves primary component
  registrations by CLR `Type`.

```csharp
var positionId = layouts.Register(
    typeof(Position),
    positionSchema);

var primaryPosition = layouts.GetPrimary(typeof(Position));
```

The generic registration convenience belongs to `Generic`, not this folder.

## Structural operations

Atomic and batch operations use the same names through overloads:

```csharp
Entity entity = world.Create(positionId, velocityId);
var destination = new Entity[1_000];
int created = world.Create(stackalloc[] { positionId, velocityId }, 1_000, destination);
Entity[] createdBatch = world.Create(stackalloc[] { positionId, velocityId }, 1_000);
int createdIntoBuffer = world.Create(stackalloc[] { positionId }, 1_000, destination);

bool destroyed = world.Destroy(entity);
int destroyedCount = world.Destroy(entities);

bool addedToEntity = world.Add(entity, componentIds);
bool addedFromSpan = world.Add(entity, stackalloc[] { positionId });
bool addedFromIds = world.Add(entity, positionId, velocityId);
int added = world.Add(entities, componentIds);
int queryAdded = world.Add(in query, componentIds);
```

The same kernels also accept `ReadOnlySpan<ComponentId>` for caller-owned
stack-only component sets. The `DeltaECS.Generators` analyzer builds generic
primary-component façades such as `world.Add<Position, Velocity>(entities)`
and `world.Remove<Position, Velocity>(in query)` on demand. It also emits
`world.Create(positionId, velocityId, count, output)` when a runtime-selected
component set must be created with the canonical positional-ID order.

Structural changes are immediate. Mutation is rejected while a conflicting
query scope owns a row lease.

The single-entity `Add` and `Remove` overloads return `true` only when the
entity made a structural transition. Stale entities, duplicate additions and
missing removals return `false`; batch overloads return the number of changed
entities.

## Query factories

`QuerySpec` is the type-erased selection description used by runtime and
integration code. For consumer code, use the direct `World` and `Query`
factories so every chain step returns a cached `Query`:

```csharp
var explicitQuery = world
    .WhereAll(positionId, velocityId)
    .WhereNone(deadId)
    .WhereAny(armedId, berserkId);
```

The `ComponentId` forms return a new `Query` at every step and reuse the same
world query-plan cache as the generated typed forms. `Query` becomes invalid
when its owning world is disposed.

The three-loop row traversal and its access tokens are internal compiler and
runtime support. Consumers use generated `ForEach` terminals, which prepare
typed rows and manage the structural lease automatically. For change
detection outside a traversal, use
`World.TryGetComponentStamp(Entity, ComponentId, out Stamp)`.

Generated `ForEach` APIs use the same validated plan but enter a closed trusted
execution method. Dense callbacks resolve each requested row once per chunk and
advance typed references inside the generated slot loop.
The public callback/ref boundary remains typed, while validation and lifetime
checks stay in the runtime bridge.

## Generated callback execution

Generated `ForEach` callbacks execute against an explicit world-owned `Query`:

```csharp
var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
world.ForEach(in query,
    static (ref Position position, in Velocity velocity) =>
        position.X += velocity.X);
```

There is no deferred `QuerySpec` facade. Structural operations use direct
`World` overloads for entities, queries and caller-owned spans.

## Internal storage

`Archetype`, `Chunk`, `ArrayAccess`, `NativeMemory<T>`, query plans and row-copy
helpers are implementation details even when their members are public for
assembly-internal cooperation. Do not treat them as stable consumer API.
