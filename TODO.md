# DeltaECS TODO

## In progress

- [ ] Split tagged query traversal into its own iterator family so tag-plan,
  overlay scratch and `None`/`Full`/`Partial` state never enter the dense types.
  Preserve snapshot-mask semantics and the root structural lease.

## Completed

- [x] Add an independent dense query scope with separate archetype, chunk and
  reverse slot iterators. Validation and lease ownership stay at scope setup;
  dense `MoveNext` methods contain no world, tag, scratch or disposal branch.
  The bounded Movement4 JIT probe reduced code size from 1956 B to 1552 B.

Deferred subscriptions and performance hypotheses live in
[IDEAS.md](IDEAS.md), not here.
