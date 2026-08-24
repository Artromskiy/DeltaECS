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

world.AddComponents(componentIds, entity);
int added = world.AddComponents(componentIds, entities);
int queryAdded = world.AddComponents(in query, componentIds);
```

Structural changes are immediate. Mutation is rejected while a conflicting
query scope owns a row lease.

## Explicit query traversal

`QuerySpec` selects component masks. `Query` is world-owned and caches matching
archetype plans. `ReadAccess` and `WriteAccess` declare row intent without a
generic component type.

```csharp
var spec = QuerySpec.ForComponents(positionId, velocityId);
var query = world.CreateQuery(in spec);
var writePosition = query.AccessWrite(positionId);
var readVelocity = query.AccessRead(velocityId);

using var scope = world.OpenQuery(in query);
var position = scope.Bind(writePosition);
var velocity = scope.Bind(readVelocity);
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

`Bind` validates access at scope setup. `GetRow` resolves one component row for
the current chunk. The terminal `Ref<T>` is the typed boundary and `T` must
match the registered component type. `ReadRow`, `WriteRow`, and all iterators
are borrowed `ref struct` values and must not escape their execution scope.

Generated `ForEach` APIs reuse `QuerySlots` internally and are documented in
`Delegate` and `Functor`.

## Query pipeline

For compact query code, `WorldQuery` defers query creation until execution:

```csharp
world
    .WhereAll(positionId, velocityId)
    .WhereNone(disabledId)
    .ForEach(static (ref Position position, in Velocity velocity) =>
        position.X += velocity.X);

using var scope = world
    .Query(QuerySpec.WhereAll(positionId, velocityId))
    .Open();
```

`WorldQuery` is an allocation-free `ref struct` facade. Generated `ForEach`
extensions use the pipeline as their receiver; `Add`, `Remove` and `Destroy`
are terminal operations on the facade itself. `World.From` is the fluent entry
point for `EntitySequence`, whose `Where` method provides optional query
filtering. The two pipeline facades are intentionally independent; neither
introduces interface dispatch into execution.

For type-erased tooling inside a query execution, `GetObject` returns
`ObjectReadValues` or `ObjectWriteValues`. Their `Get`/`Set` methods operate on
the current slot and validate object writes against the registered CLR type.

## Internal storage

`Archetype`, `Chunk`, `ArrayAccess`, `NativeMemory<T>`, query plans and row-copy
helpers are implementation details even when their members are public for
assembly-internal cooperation. Do not treat them as stable consumer API.
