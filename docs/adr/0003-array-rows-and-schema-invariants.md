# ADR-0003: Array rows and stable schema registration

> The original row-wrapper API was removed. This record keeps the storage
> invariants; current generated callbacks use `GeneratedQuerySlots` directly.

## Context

Chunks need to support value types, direct references and structs containing
references without changing the type-erased component identity model. Schema
registration must also remain stable when a layout is requested more than once.

## Decision

- `ComponentLayout` stores the schema id and registered CLR runtime type used
  to create a component array. Array rows use the CLR type directly.
- `ComponentLayoutRegistry.Register` reuses an existing `SchemaId` only when
  the complete layout, including runtime type, matches. A mismatch throws.
- `Register<T>` is a convenience boundary. The registry records `typeof(T)`
  and determines whether the type contains managed references for row cleanup.
- Production chunks store direct `Array[]` component rows. Generated dense
  execution resolves a typed reference through `GeneratedQuerySlots` at the
  callback boundary.
- Component copy, remove-swap and destruction clear released slots according
  to the registered row operations, so references do not remain live in
  inactive storage.

## Consequences

The storage model supports value types, managed-field structs, class
references and multiple component IDs backed by the same CLR type. Component
identity remains `ComponentId`; CLR type and schema metadata are registration
data rather than query or transition identity.
