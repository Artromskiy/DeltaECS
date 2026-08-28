# Stamp contract

`Stamp` is an opaque 64-bit revision used for equality-based change tracking.
It is not wall-clock time and public code must not infer elapsed time or rely on
arithmetic ordering.

- `World.Stamp` is the latest successful world mutation revision.
- `ComponentCatalog.Stamp` changes when the tooling catalog changes.
- Component stamps track the last recorded mutation for one entity/component.
- `TryRead` returns the observed component stamp.
- `TryWrite` compares `expectedStamp` and reports `StaleStamp` on conflict.

Successful writes reserve a new stamp. Read-only query access does not. Write
query access records the write intent according to the current chunk/component
tracking rules.

`MutationStampSource` and `ComponentStampStorage` are internal implementation
types. Consumers exchange only `Stamp` values and compare them for equality.
