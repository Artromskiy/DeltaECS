# Core API

`Core` contains the type-erased ECS model. Component CLR types do not flow
through entity storage, query descriptions, access tokens or iterators.

## Identity and registration

- `Entity` is an index/generation handle. Destroyed handles become stale.
- `ComponentId` is a world-local component identity.
- `SchemaId` is stable tooling/schema identity.
- `ComponentLayoutRegistry` registers layouts and resolves primary component
  registrations by CLR `Type`.
- `ArchetypeHandle` is a world-owned cached component-set handle.

```csharp
var positionId = layouts.Register(
    typeof(Position),
    positionSchema);

var primaryPosition = layouts.GetPrimary(typeof(Position));
var moving = world.GetOrCreateArchetype(positionId, velocityId);
```

The generic registration convenience belongs to `Generic`, not this folder.

## Structural operations

Atomic and batch operations use the same names through overloads:

```csharp
Entity entity = world.Create(positionId, velocityId);
int created = world.Create(moving, destination);

bool destroyed = world.Destroy(entity);
int destroyedCount = world.Destroy(entities);

world.Add(componentIds, entity);
int added = world.Add(componentIds, entities);
int queryAdded = world.Add(in query, componentIds);
```

Structural changes are immediate. Mutation is rejected while a conflicting
query scope owns a row lease.

## Explicit query traversal

`QuerySpec` selects component masks. `Query` is world-owned and caches matching
archetype plans. `ReadAccess` and `WriteAccess` declare row intent without a
generic component type.

```csharp
var spec = QuerySpec.WhereAll(positionId, velocityId);
var query = world.CreateQuery(in spec);
var writePosition = query.AccessWrite(positionId);
var readVelocity = query.AccessRead(velocityId);

using var scope = world.BeginScope(in query);
var position = writePosition;
var velocity = readVelocity;
var archetypes = scope.Archetypes;

while (archetypes.MoveNext())
{
    var chunks = archetypes.Current.Chunks;
    while (chunks.MoveNext())
    {
        var slots = chunks.Current.Slots;
        WriteRow positions = slots.GetRow(position);
        ReadRow velocities = slots.GetRow(velocity);

        while (slots.MoveNext())
        {
            ref Position p = ref positions.Ref<Position>(slots);
            ref readonly Velocity v = ref velocities.Ref<Velocity>(slots);
            p.X += v.X;
        }
    }
}
```

`GetRow` validates the access token against the active query and resolves one
component row for the current chunk. The terminal `Ref<T>` is the typed boundary and `T` must
match the registered component type. `ReadRow`, `WriteRow`, and all iterators
are borrowed `ref struct` values and must not escape their execution scope.

For change detection, `QueryChunk.GetStampRow(ReadAccess)` prepares a
non-generic borrowed `StampRow` once per component and chunk. Its
`Get(in QuerySlots)` method returns the effective three-level component stamp
for the current entity without repeating entity, type or dictionary lookup.
The stamp combines entity/component, chunk/component and archetype/component
terms. There is no aggregate world mutation stamp; the world exposes only
exact component stamps.
Use
`World.TryGetComponentStamp(Entity, ComponentId, out Stamp)` for a single
entity outside a query scope. Stamps identify an entity/component pair; the
API does not synthesize an aggregate entity stamp.

Generated `ForEach` APIs use the same validated plan but enter a closed trusted
execution method. Dense callbacks resolve each requested row once per chunk and
advance typed references inside the generated slot loop. Ordered sequence
callbacks use the same direct reference endpoints against the current entity's
chunk; they do not construct `ReadRow`/`WriteRow` values for every callback.
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

There is no deferred `QuerySpec` facade. `World.From(entities)` is the
separate fluent entry point for ordered entity sequences; its `Where(in Query)`
method applies an existing query filter.

For type-erased tooling inside a query execution, `GetObject` returns
`ObjectReadValues` or `ObjectWriteValues`. Their `Get`/`Set` methods operate on
the current slot; object writes validate the supplied value against the
registered CLR type. This is a tooling path, not the typed hot-loop endpoint.

## Internal storage

`Archetype`, `Chunk`, `ArrayAccess`, `NativeMemory<T>`, query plans and row-copy
helpers are implementation details even when their members are public for
assembly-internal cooperation. Do not treat them as stable consumer API.
