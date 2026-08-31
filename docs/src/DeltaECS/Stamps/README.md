# Stamp contract

`Stamp` is an opaque 64-bit revision used for equality-based change tracking.
It is not wall-clock time and public code must not infer elapsed time or rely on
arithmetic ordering.

- `ComponentCatalog.Stamp` changes when the tooling catalog changes.
- A component stamp is the combined revision for one entity/component pair.
- `World.TryGetComponentStamp` returns the exact stamp for one live entity and
  component without reading or boxing the component value.
- `TryRead` returns the observed component stamp.
- `TryWrite` compares `expectedStamp` and reports `StaleStamp` on conflict.

Successful writes advance only the stamp cell for the affected
entity/component, chunk/component, or archetype/component. Read-only query
access does not change stamps. Write query access records the write intent
through the operation-specific stamp route.

The effective component stamp is a new opaque value whose unchecked `ulong`
payload is the sum of three independent overrides:

```text
entity/component + chunk/component + archetype/component
```

The entity/component term is the existing per-slot stamp. The chunk and
archetype overrides are centralized in `World`-owned storage; they are
deliberately not fields on `Chunk` or `Archetype`, so those hot storage objects
do not grow for the hierarchy. The terms are addressed by stable world-local
ids and physical component ordinals. A trusted internal operation can update
the appropriate level without changing the public API. Equality is the only
supported interpretation: the sum is a change token, not an ordered
timestamp, and wraparound is allowed.

The default mutation paths use the entity term for a point write and the chunk
term for a validated dense query row write. A generated dense query write uses
the archetype override once for each matching archetype. When a physical chunk
becomes empty, its chunk-level terms are cleared before the chunk can be
reused; archetype-level terms remain at archetype scope. There is no aggregate
world mutation stamp; consumers compare the exact component stamp they
observed.

The trusted runtime keeps the write state proportional to the operation:

| Operation | Trusted stamp state carried into the hot path |
| --- | --- |
| `Set`, integration point write, or selected-entity sequence | `EntityComponentStampWriter` for the current entity/component |
| `QuerySlots.GetRow(WriteAccess)` / complete row traversal | `ChunkComponentStampWriter` for the current chunk/component |
| Generated dense `ForEach` write | `ArchetypeComponentStampWriter` for the matching archetype/component |
| Generated read-only or zero-arity traversal | no write stamp or writer state |

This distinction is intentional: read-only and entity-selected paths do not
carry broader write data, while a dense generated write marks the archetype
override once before its entity loop. It is an internal lowering choice; the
public delegate, functor, sequence and query APIs remain unchanged.

`StampCounter`, `ComponentStampStorage` and the centralized hierarchy
buffers are internal implementation types. Consumers exchange only `Stamp`
values and compare them for equality. Mutating fields inside a reference-type
component obtained by reference remains the component owner's responsibility;
that operation is outside ECS write tracking unless it goes through an ECS
write endpoint.

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
