# DeltaECS source API map

The public namespace is `Delta.ECS`. Source folders separate API roles; their
documentation is centralized under `docs/src/` so source trees remain focused
on implementation. They do not create separate storage models or namespaces.

| Folder | Responsibility | Documentation |
|---|---|---|
| `Core` | World, entities, component identities, structural operations and explicit query traversal | [Core API](Core/README.md) |
| `Generic` | Typed registration, single-component helpers and terminal row references | [Generic API](Generic/README.md) |
| `Delegate` | Delegate callback contracts and zero-component entry points | [Delegate API](Delegate/README.md) |
| `Functor` | Struct-functor marker contracts | [Functor API](Functor/README.md) |
| `Sequence` | Ordered execution over an explicit entity span | [Sequence API](Sequence/README.md) |
| `Parallel` | Chunk-disjoint multi-threaded query execution | [Parallel API](Parallel/README.md) |
| `API` | Neutral engine/editor integration contract | [Integration API](API/README.md) |
| `Stamps` | Catalog and entity/component mutation revisions | [Stamp contract](Stamps/README.md) |

`Properties` contains assembly metadata and has no user-facing API.

The consumer-facing source generator is a separate project documented in
[DeltaECS.Generators](../DeltaECS.Generators/README.md).

Start with the repository [README](../../README.md) for stable behavior and
use the [API map](../../APIMAP.md) for contributor navigation.
