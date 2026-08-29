# Stamp contract

`Stamp` is an opaque 64-bit revision used for equality-based change tracking.
It is not wall-clock time and public code must not infer elapsed time or rely on
arithmetic ordering.

- `World.Stamp` is the latest successful world mutation revision.
- `ComponentCatalog.Stamp` changes when the tooling catalog changes.
- Component stamps track the last recorded mutation for one entity/component.
- `World.TryGetComponentStamp` returns the exact stamp for one live entity and
  component without reading or boxing the component value.
- `TryRead` returns the observed component stamp.
- `TryWrite` compares `expectedStamp` and reports `StaleStamp` on conflict.

Successful writes reserve a new stamp. Read-only query access does not. Write
query access records the write intent according to the current chunk/component
tracking rules.

`MutationStampSource` and `ComponentStampStorage` are internal implementation
types. Consumers exchange only `Stamp` values and compare them for equality.

## Query stamp access

The cold single-entity contract is:

```csharp
public bool TryGetComponentStamp(
    Entity entity,
    ComponentId componentId,
    out Stamp stamp);
```

It returns `false` for a stale entity or when the entity does not contain the
component. It performs no CLR type lookup and does not return the component
value.

For dense query traversal, prepare a borrowed stamp row once per chunk:

```csharp
var transformAccess = query.AccessRead(transformId);
var parentAccess = query.AccessRead(parentId);

using var scope = world.BeginScope(in query);
var chunks = scope.Chunks;

while (chunks.MoveNext())
{
    var chunk = chunks.Current;
    var slots = chunk.Slots;
    var transformStamps = chunk.GetStampRow(transformAccess);
    var parentStamps = chunk.GetStampRow(parentAccess);

    while (slots.MoveNext())
    {
        Stamp transformStamp = transformStamps.Get(in slots);
        Stamp parentStamp = parentStamps.Get(in slots);
        // Compare with the consumer's cached revisions.
    }
}
```

`StampRow` is a non-generic borrowed `readonly ref struct`. `GetStampRow`
validates the access token once for the current chunk and does not mark a
component as written. `StampRow.Get` reads the stamp for the current slot;
it performs no entity lookup, CLR `Type` lookup, dictionary lookup or object
boxing. The row is valid only while its query scope and current chunk remain
active. There is deliberately no aggregate `EntityStamp`: the exact contract
is one stamp per entity/component pair.
