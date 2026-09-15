# Stamp contract

`Stamp` is a 64-bit counter token used for equality-based change tracking. Its
`ulong Value` is public for transport and diagnostics, but consumers must treat
the value as an equality token: it is not wall-clock time and arithmetic
ordering is not a supported semantic.

- `ComponentCatalog.Stamp` changes when the tooling catalog changes.
- A component stamp is the combined revision for one entity/component pair.
- `World.TryGetComponentStamp` returns the exact stamp for one live entity and
  component without reading or boxing the component value.
- `TryRead` returns the observed component stamp.
- `TryWrite` compares `expectedStamp` and reports `StaleStamp` on conflict.

Successful point writes advance the affected entity/component cell. Generated
dense writes advance the archetype/component override once for each matching
archetype. Read-only query access does not change stamps.

The effective component stamp is a new opaque value whose unchecked `ulong`
payload is the sum of two independent terms:

```text
entity/component + archetype/component
```

The entity/component term is the per-slot stamp stored with the component row.
Archetype overrides are centralized in `World`-owned storage, so `Archetype`
does not grow for stamp tracking. The override is addressed by the stable
world-local archetype id and physical component ordinal. Equality is the only
supported interpretation: the sum is a change token, not an ordered timestamp,
and wraparound is allowed.

The generated archetype-write route keeps its override cells as a managed
`Stamp[]` indexed by the prepared physical component ordinal. It does not pass
raw addresses or pointers across the generator/runtime boundary. Native storage
remains an internal implementation detail of the entity stamp layer.

The default mutation paths use the entity term for a point write and the
archetype override for a generated dense query write. Structural migration
copies the existing entity terms, initializes newly added components and leaves
archetype overrides at archetype scope. There is no aggregate world mutation
stamp; consumers compare the exact component stamp they observed.

The trusted runtime keeps the write state proportional to the operation:

| Operation | Trusted stamp state carried into the hot path |
| --- | --- |
| `Set` or integration point write | `EntityComponentStampWriter` for the current entity/component |
| Generated dense `ForEach` write | `ArchetypeComponentStampWriter` for the matching archetype/component |
| Generated read-only traversal or zero-arity anchor | no write stamp or writer state |

This distinction is intentional: read-only and entity-selected paths do not
carry broader write data, while a dense generated write marks the archetype
override once before its entity loop. It is an internal lowering choice; the
public delegate, functor and query APIs remain unchanged.

`StampCounter`, `ComponentStampStorage` and the archetype override buffer are
internal implementation types. Consumers exchange only `Stamp` values and
compare them for equality. Mutating fields inside a reference-type component
obtained by reference remains the component owner's responsibility; that
operation is outside ECS write tracking unless it goes through an ECS write
endpoint.

## Stamp access

The cold single-entity contract is:

```csharp
public bool TryGetComponentStamp(
    Entity entity,
    ComponentId componentId,
    out Stamp stamp);
```

It returns `false` for a stale entity or when the entity does not contain the
component. It performs no CLR type lookup and does not return the component
value. Generated `ForEach` callbacks receive the appropriate read/write intent
and stamp behavior automatically.

There is deliberately no aggregate `EntityStamp`: the exact contract is one
stamp per entity/component pair.
