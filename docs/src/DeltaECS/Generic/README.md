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
first primary registration. An empty non-primitive struct is automatically
treated as a data-less tag by the same registration method.

Tags participate in membership queries and structural operations, but do not
store a value. `Get<T>` returns `default(T)` for a present tag, while `TryGet<T>`
returns `true` with `default(T)` when present and `false` when absent. Adding a
tag through a value-taking overload adds only membership and ignores the value.
`GetRef<T>` returns a shared default placeholder for tags; writes through that
reference are not retained.

## Single-component operations

```csharp
Entity defaultPosition = world.Create<Position>();
Entity primaryEntity = world.Create(positionId, new Position());
Span<Entity> primaryDestination = stackalloc Entity[10];
int primaryCreated = world.Create<Position>(primaryDestination.Length, primaryDestination);
Span<Entity> explicitBatch = stackalloc Entity[10];
int explicitBatchCreated = world.Create<Position>(positionId, explicitBatch.Length, explicitBatch);
bool primaryAdded = world.Add(primaryEntity, new Velocity());
int primaryBatchAdded = world.Add<Velocity>(primaryDestination, new Velocity());

bool hasPosition = world.Has<Position>(primaryEntity);
bool hasExplicitPosition = world.Has<Position>(primaryEntity, positionId);
bool hasStamp = world.TryGetComponentStamp<Position>(primaryEntity, out Stamp stamp);

if (world.TryGet(primaryEntity, out Position primaryPosition))
{
    primaryPosition.X++;
    world.GetRef<Position>(primaryEntity) = primaryPosition;
}

world.Remove<Velocity>(primaryEntity);
int primaryRemoved = world.Remove<Velocity>(primaryDestination);

Entity entity = world.Create(positionId, new Position());
Span<Entity> entities = stackalloc Entity[10];
int createdEntities = world.Create(stackalloc ComponentId[] { positionId }, entities.Length, entities);
Span<Entity> destination = stackalloc Entity[10];
int typedCreated = world.Create<Position>(positionId, destination.Length, destination);
world.Add(entity, velocityId, new Velocity());
int added = world.Add(entities, velocityId, new Velocity());

if (world.TryGet(entity, positionId, out Position position))
{
    position.X++;
    world.GetRef<Position>(entity, positionId) = position;
}

world.Remove<Velocity>(entity, velocityId);
int removed = world.Remove<Velocity>(entities, velocityId);
```

The overloads without a `ComponentId` resolve the registered primary component
for `T` once at the API boundary, so the type and component ID cannot be
supplied inconsistently. The explicit-ID overloads remain available for
secondary registrations and validate `ComponentId` against `T`. `GetRef<T>`
returns a writable reference and throws when the entity is stale or lacks the
component. For tags, `GetRef<T>` returns a shared placeholder and does not store
writes. Use `TryGet` when the component is optional. Batch `Add<T>`
initializes the newly added row with the same value for every eligible entity;
batch `Remove<T>` returns the number of structural transitions. Batch
structural operations skip stale handles and entities that already have or do
not have the component.

## Generated primary-component batches

With the `DeltaECS.Generators` analyzer, generic structural façades are emitted
for the arities used by the consumer:

```csharp
int added = world.Add<Position, Velocity>(entities);
int removed = world.Remove<Position, Velocity>(entities);
bool addedToEntity = world.Add<Position, Velocity>(entity);
bool removedFromEntity = world.Remove<Position, Velocity>(entity);

Entity created = world.Create<Position, Velocity>();
int createdCount = world.Create<Position, Velocity>(10);
Span<Entity> output = stackalloc Entity[10];
int createdIntoOutput = world.Create<Position, Velocity>(10, output);
int queryAdded = world.Add<Position, Velocity>(in query);
int queryRemoved = world.Remove<Position, Velocity>(in query);

```

These forms operate on the primary registration of each type and pass a
generated stack-only component-ID span to the existing structural kernels.
Single-entity `Add`/`Remove` return `bool`; batch and query forms return the
number of changed entities. `Create` returns an `Entity` for one entity and an
`int` for batches; the output overload writes to caller-owned storage. Added
rows are default-initialized. Generated generic methods follow the component
lists used by the consumer.

Generated `ForEach` terminals are the consumer row-access surface.
