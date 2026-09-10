# Generic API

Generic types appear only where a CLR component type is required. The core
world, query, access and row objects remain non-generic.

## Registration

```csharp
ComponentId positionId = layouts.Register<Position>(positionSchema);
ComponentId primary = layouts.GetPrimary<Position>();
```

`Register<T>` records `typeof(T)` and whether `T` contains managed references.
Multiple component IDs may use the same CLR type; `GetPrimary<T>` resolves the
first primary registration.

## Single-component operations

```csharp
Entity primaryEntity = world.Create(positionId, new Position());
var primaryDestination = new Entity[10];
int primaryCreated = world.Create<Position>(10, primaryDestination);
bool primaryAdded = world.Add(primaryEntity, new Velocity());
int primaryBatchAdded = world.Add<Velocity>(primaryDestination, new Velocity());

if (world.TryGet(primaryEntity, out Position primaryPosition))
{
    primaryPosition.X++;
    world.Set(primaryEntity, in primaryPosition);
}

world.Remove<Velocity>(primaryEntity);
int primaryRemoved = world.Remove<Velocity>(primaryDestination);

Entity entity = world.Create(positionId, new Position());
Entity[] entities = world.Create(stackalloc[] { positionId }, 10);
var destination = new Entity[10];
int typedCreated = world.Create<Position>(positionId, 10, destination);
world.Add(entity, velocityId, new Velocity());
int added = world.Add(entities, velocityId, new Velocity());

if (world.TryGet(entity, positionId, out Position position))
{
    position.X++;
    world.Set(entity, positionId, in position);
}

world.Remove<Velocity>(entity, velocityId);
int removed = world.Remove<Velocity>(entities, velocityId);
```

The overloads without a `ComponentId` resolve the registered primary component
for `T` once at the API boundary, so the type and component ID cannot be
supplied inconsistently. The explicit-ID overloads remain available for
secondary registrations and validate `ComponentId` against `T`. `Set<T>`
expects the row to exist and throws when the entity is stale or lacks it; use
`TryGet` when the component is optional. Batch `Add<T>` initializes the newly
added row with the same value for every eligible entity; batch `Remove<T>`
returns the number of structural transitions. Batch structural operations skip
stale handles and entities that already have or do not have the component.

## Generated primary-component batches

With the `DeltaECS.Generators` analyzer, generic structural façades are emitted
for the arities used by the consumer:

```csharp
int added = world.Add<Position, Velocity>(entities);
int removed = world.Remove<Position, Velocity>(entities);
int queryAdded = world.Add<Position, Velocity>(in query);
int queryRemoved = world.Remove<Position, Velocity>(in query);

int sequenceAdded = world.From(entities).Add<Position, Velocity>();
int sequenceRemoved = world.From(entities).Remove<Position, Velocity>();
```

These forms operate on the primary registration of each type, pass a generated
stack-only component-ID span to the existing structural kernels, and return the
number of changed entities. Added rows are default-initialized. The generator
supports arities one through 256 on demand; the limit applies to generated
generic methods, not to the number of component IDs registered by a world.

## Terminal row access

`ReadRow.Ref<T>` returns `ref readonly T`; `WriteRow.Ref<T>` returns `ref T`.
The overload accepts `QuerySlots` or an explicit slot index. The row must come
from the same active query execution and `T` must match the access
registration.
