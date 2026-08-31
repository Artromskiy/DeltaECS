# ADR-0001: Archetype and chunk storage model

> Status: historical decision record. The current public contract is in
> `docs/README.md`; this record keeps only the storage assumptions that still
> apply.

## Context

DeltaECS needs a type-erased storage kernel with immediate structural changes,
batch operations and predictable component iteration. Component CLR types must
not become part of entity identity, query plans or structural transition keys.

## Decision

- Entities are identified by `Entity(Index, Generation)` and resolved through
  an `EntityRecord`.
- `ComponentId` and `ComponentLayout` describe registered component rows.
- Archetypes own a dynamically sized native-word component mask and chunks.
  Each chunk stores one CLR array per component row in a structure-of-arrays
  layout.
- One CLR type may have multiple component IDs; each ID owns an independent
  physical row.
- CLR `Type` is used during registration and array creation only. It is not
  part of component identity or the query hot loop.
- `Create`, `Destroy`, add and remove operations complete immediately. Batch
  operations group work by archetype and chunk while preserving their public
  result semantics.
- A query caches matching archetypes and refreshes its plans when the world
  creates a new archetype.

## Consequences

The core remains type-erased while supporting value types, reference types and
structs containing references in the same row model. Query execution can
prepare physical rows once per chunk, and structural transitions can copy rows
by component identity rather than by CLR type.

Schema registration is stable: an existing `SchemaId` may be reused only when
the complete layout matches; an incompatible registration is rejected.
