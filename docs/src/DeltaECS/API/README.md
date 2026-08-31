# Integration API

`Delta.ECS.Integration.IEcsWorld` is the neutral local boundary for engine and
editor tooling. `World` implements it without introducing an adapter storage
model.

The interface combines:

- lifecycle: `Initialize`, `Update`, `Shutdown`;
- entity structure: `Create`, `Destroy`, `Add`, `Remove`;
- component discovery: `Catalog`, `TryGetComponents`;
- object tooling: symmetric `TryRead` and optimistic `TryWrite`.

```csharp
if (ecs.TryRead(entity, component, out var snapshot, out var readError))
{
    bool written = ecs.TryWrite(
        entity,
        component,
        editedValue,
        snapshot.Stamp,
        out Stamp writtenStamp,
        out EcsWriteError writeError);
}
```

`TryWrite` accepts the exact stamp observed by `TryRead`; a stale stamp reports
`StaleStamp`. Catalog entries expose world-local `ComponentId`, stable
`SchemaId`, CLR `ValueType`, nullability and read/write capabilities.

This is an in-process .NET contract. `object?` values are not an IPC encoding.
Reference components may preserve object identity; mutating a returned object
directly bypasses ECS write tracking and is the caller's responsibility.

Integration operations are valid between `Initialize` and `Shutdown`. A host
owns scheduling and all time domains; parameterless `IEcsWorld.Update()` is
only a lifecycle safe point and does not introduce an ECS system scheduler or
clock.
