# GitHub benchmark runs

The `ECS benchmarks` workflow has two lanes:

- pull requests and pushes to `main` build the solution, run the correctness
  tests, validate the capability contract, and run a BenchmarkDotNet discovery
  list smoke. It does not measure performance;
- a manual dispatch or the Monday schedule runs the selected comparative suite
  in Release and uploads JSON, CSV, Markdown, logs, and runner metadata.

To start a measured run, open **Actions → ECS benchmarks → Run workflow**. Keep
`full-comparison`, three warm-ups, five measured iterations, and one launch for
the normal report. The manual form can use either the standard x64 Linux runner
or the standard ARM64 Linux runner; smaller suites are available for focused
investigation. The weekly run uses x64.

The generated raw BDN tables and the stable `comparative-report.md` / `.csv`
combined schema are appended to the Actions run summary. Unsupported native
batch capabilities are retained as `Supported=false`, `Mode=Unsupported`, and
`∞` mean/ratio rows. Complete output is retained as the `ecs-benchmarks-*`
artifact for 30 days.

The new matrix compares DeltaECS, Arch, Friflo.Engine.ECS, DefaultEcs, and
LeoECS Lite. Legacy is intentionally excluded from `full-comparison`; older
Legacy-containing classes remain only on their historical focused routes.

GitHub-hosted runners are shared and their CPU model may change between jobs.
Use a single run to compare DeltaECS with the other ECS implementations because
all competitors then see the same machine. Do not treat small differences
between separate workflow runs as regressions. Stable cross-commit regression
gates require a dedicated self-hosted runner later.

The workflow intentionally does not request hardware performance counters:
standard GitHub-hosted runners do not provide reliable PMU access. It still
records BenchmarkDotNet time, ratios, managed allocations, GC data, and the
runner's CPU/runtime information.
