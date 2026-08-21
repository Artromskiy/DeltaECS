# DeltaECS agent guide

Scope: standalone archetype ECS kernel, typed array rows, immediate structural
changes, tags, queries and measured performance work.

- [README.md](README.md) — stable storage/API contract.
- [TODO.md](TODO.md) — selected implementation work; always read before task
  selection.
- [IDEAS.md](IDEAS.md) — deferred ECS designs; never implement without an
  explicit decision and workload.
- [WORKFLOW.md](WORKFLOW.md) — correctness checks and benchmark routing.
- [benchmarks/README.md](benchmarks/README.md) — micro/JIT procedure.
- [docs/performance/README.md](docs/performance/README.md) — evidence and
  optimization candidates, not automatic tasks.
- ADRs record decisions; generated benchmark reports are evidence, not TODOs.

Do not add editor/render dependencies or mandatory command buffers. Public hot
loops use validated typed bindings; raw row lookup stays internal.

Skills: `performance-benchmark` for bounded measurements,
`assembly-arm` and `apple-silicon` for macOS AArch64 JIT analysis,
`cpu-cache-opt`/`memory-hierarchy-and-caches` for locality,
`vectorization` for proven loop opportunities, and `memory-model` only when
concurrency is explicitly introduced.
