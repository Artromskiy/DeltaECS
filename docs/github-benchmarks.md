# GitHub iteration benchmarks

The `ECS benchmarks` workflow has two iteration lanes:

- pull requests and pushes to `main` build the solution, run correctness tests,
  validate the iteration contract and run BenchmarkDotNet discovery only;
- a manual dispatch or the Monday schedule runs the unified iteration matrix in
  Release and uploads JSON, CSV, Markdown, logs and runner metadata.

The manual `iteration` route compares DeltaECS, Arch, Friflo.Engine.ECS,
DefaultEcs and LeoEcsLite using the same workloads and entity amounts. The
separate `version-comparison` route compares two DeltaECS revisions using the
same dense, Movement2 and Movement4 iteration scenarios.

Adaptive mode uses `Job.Default` with a 100 ms iteration target and lets
BenchmarkDotNet choose invocation, warm-up and measurement counts. Fixed and
short modes are available only for explicitly requested exploratory runs.

The workflow no longer dispatches structural, capacity, hardware-profile or
one-off benchmark categories. This keeps every published comparative result in
the iteration scope and avoids mixing incompatible setup and correctness
contracts.

GitHub-hosted runners are shared and their CPU model may change between jobs.
Use a single run to compare all ECS implementations because they then see the
same machine. The workflow records BenchmarkDotNet timing, ratios, allocations,
GC data and runner/runtime information; it does not request hardware counters.
