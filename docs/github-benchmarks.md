# GitHub benchmark runs

The `ECS benchmarks` workflow has two lanes:

- pull requests and pushes to `main` build the solution, run the correctness
  tests, and execute a BenchmarkDotNet `Dry` smoke. Its numbers are not
  performance results;
- a manual dispatch or the Monday schedule runs the selected comparative suite
  in Release and uploads JSON, CSV, Markdown, logs, and runner metadata.

To start a measured run, open **Actions → ECS benchmarks → Run workflow**. Keep
`full-comparison`, three warm-ups, five measured iterations, and one launch for
the normal report. The manual form can use either the standard x64 Linux runner
or the standard ARM64 Linux runner; smaller suites are available for focused
investigation. The weekly run uses x64.

The generated tables are appended to the Actions run summary. Complete output
is retained as the `ecs-benchmarks-*` artifact for 30 days.

GitHub-hosted runners are shared and their CPU model may change between jobs.
Use a single run to compare DeltaECS with the other ECS implementations because
all competitors then see the same machine. Do not treat small differences
between separate workflow runs as regressions. Stable cross-commit regression
gates require a dedicated self-hosted runner later.

The workflow intentionally does not request hardware performance counters:
standard GitHub-hosted runners do not provide reliable PMU access. It still
records BenchmarkDotNet time, ratios, managed allocations, GC data, and the
runner's CPU/runtime information.
