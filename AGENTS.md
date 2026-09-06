# DeltaECS agent router

Scope: standalone archetype ECS kernel, typed array rows, immediate structural
changes, dense queries and measured performance work. The public API redesign
is user-owned; do not select or implement ECS API work without an explicit
bounded request. Preserve active branches and unrelated dirty changes.

## Map — open only as needed

- ../CODE_STYLE.md — technical hot-loop, storage, type and mutation rules.
- ../CONTRACTS.md — integration boundaries; open only for a cross-project task.
- IDEAS.md — ECS research/options only when explicitly requested.
- WORKFLOW.md — correctness, format/metrics and benchmark routing.
- docs/APIMAP.md — source/API navigation map.
- docs/README.md — nested leaf documentation; open only for a named docs task.
- src/DeltaECS and src/DeltaECS.Generators — production kernel and generator.
- tests, benchmarks, samples — verification, measured workloads and runnable leaves.

Do not add editor/render dependencies or mandatory command buffers. Raw row
lookup stays internal; public hot loops use validated typed bindings.
