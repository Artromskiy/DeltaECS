# DeltaECS agent guide

Scope: standalone archetype ECS kernel, typed array rows, immediate structural
changes, dense queries and measured performance work.

The current public API redesign is owned directly by the user. Do not select,
delegate or implement API/query/performance tasks from repository TODOs unless
the user explicitly assigns that bounded work. Preserve their active branch
and dirty changes.

- [docs/README.md](docs/README.md) — stable storage/API contract.
- [TODO.md](TODO.md) — selected implementation work; always read before task
  selection.
- [IDEAS.md](IDEAS.md) — deferred ECS designs; never implement without an
  explicit decision and workload.
- [WORKFLOW.md](WORKFLOW.md) — correctness checks and benchmark routing.
- [docs/APIMAP.md](docs/APIMAP.md) — source/API navigation map for focused reads.
- [docs/benchmarks/README.md](docs/benchmarks/README.md) — micro/JIT procedure.
- [docs/performance/README.md](docs/performance/README.md) — evidence and
  optimization candidates, not automatic tasks.
- ADRs record decisions; generated benchmark reports are evidence, not TODOs.
- [../CONTRACTS.md](../CONTRACTS.md) tracks cross-project integration
  boundaries; ECS redesign remains user-owned in this repository's `TODO.md`.

Do not add editor/render dependencies or mandatory command buffers. Public hot
loops use validated typed bindings; raw row lookup stays internal.

Skills: `performance-benchmark` for bounded measurements,
`assembly-arm` and `apple-silicon` for macOS AArch64 JIT analysis,
`cpu-cache-opt`/`memory-hierarchy-and-caches` for locality,
`vectorization` for proven loop opportunities, and `memory-model` only when
concurrency is explicitly introduced.
