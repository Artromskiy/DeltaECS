# ADR-0003: Direct ArrayRows and stable schema registration

## Context

The legacy byte backend cannot represent a struct containing references without
unsafe raw-byte semantics. The first ArrayRows experiment also treated every
repeat registration as a new ID, which broke stable schema lookup.

## Decision

- `ComponentLayout(Type)` is metadata for direct `Array.CreateInstance` only.
  It does not call `Buffer.ByteLength` or `Marshal.SizeOf`; its byte `Size` and
  `Stride` are zero because those values have no meaning for managed ArrayRows
  elements.
- `ComponentLayoutRegistry.Register` deduplicates an existing `SchemaId` only
  when the complete layout matches, including runtime type. A mismatch throws.
- `Register<T>` is the minimal cold registration convenience for ArrayRows
  element types, including reference types. `RegisterUnmanaged<T>` remains the
  constrained convenience for unmanaged element types.
- Production chunks store direct `Array[]` rows only. The legacy `byte[][]`
  implementation is benchmark-only reference code. ArrayRows casts each row to
  `T[]` once per chunk view and then returns a span; it never uses
  `Array.GetValue` or `Array.SetValue`.
- Transition copy maps source component indices to target component indices by
  `ComponentId`. Array `CopySlot`, remove-swap-back, and destroy clear the
  released slot, so reference fields do not remain live in inactive storage.

## Consequences

The ArrayRows backend supports value types, managed-field structs, direct class
references, and virtual components with the same element type while retaining
the type-erased identity model. The byte backend remains available for honest
A/B benchmarks. Type-backed layouts cannot be used with ByteRows, and the
ArrayRows `Size`/`Stride` properties are not byte allocation contracts.
