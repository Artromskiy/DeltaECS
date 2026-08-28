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
Entity entity = world.Create(positionId, new Position());
world.Add(entity, velocityId, new Velocity());

if (world.TryGet(entity, positionId, out Position position))
{
    position.X++;
    world.Set(entity, positionId, in position);
}

world.Remove<Velocity>(entity, velocityId);
```

These helpers validate `ComponentId` against `T` and delegate to the core
structural/storage operations. They are intended for individual entities, not
as an iteration kernel.

## Terminal row access

`ReadRow.Ref<T>` returns `ref readonly T`; `WriteRow.Ref<T>` returns `ref T`.
The overload accepts `QuerySlots` or an explicit slot index. The row must come
from the same active query execution and `T` must match the access
registration.
