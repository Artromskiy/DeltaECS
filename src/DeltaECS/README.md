# DeltaECS source API map

The public namespace is `DeltaECS`. Source folders separate API roles; they
do not create separate storage models or namespaces.

| Folder | Responsibility | Documentation |
|---|---|---|
| `Core` | World, entities, component identities, structural operations and explicit query traversal | [Core API](Core/README.md) |
| `Generic` | Typed registration, single-component helpers and terminal row references | [Generic API](Generic/README.md) |
| `Delegate` | Delegate callbacks and low-level chunk execution | [Delegate API](Delegate/README.md) |
| `Functor` | Struct-functor contracts and generated runtime bridge | [Functor API](Functor/README.md) |
| `Sequence` | Ordered execution over an explicit entity span | [Sequence API](Sequence/README.md) |
| `API` | Neutral engine/editor integration contract | [Integration API](API/README.md) |
| `Stamps` | World and component mutation revisions | [Stamp contract](Stamps/README.md) |

`Properties` contains assembly metadata and has no user-facing API.

The consumer-facing source generator is a separate project documented in
[DeltaECS.Generators](../DeltaECS.Generators/README.md).

Start with the repository [README](../../README.md) for stable behavior and
use [docs/APIMAP.md](../../docs/APIMAP.md) for contributor navigation.
