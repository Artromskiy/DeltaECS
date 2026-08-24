# GitHub benchmark runs

The `ECS benchmarks` workflow has two lanes:

- pull requests and pushes to `main` build the solution, run the correctness
  tests, validate the capability contract, and run a BenchmarkDotNet discovery
  list smoke. It does not measure performance;
- a manual dispatch or the Monday schedule runs the selected comparative suite
  in Release and uploads JSON, CSV, Markdown, logs, and runner metadata.

To start a measured run, open **Actions → ECS benchmarks → Run workflow**. The
default `adaptive` mode uses `Job.Default` and lets BenchmarkDotNet choose its
warm-up and measurement iteration counts. `launch_count` is independent and
always configurable; it defaults to one. Use `fixed` when exact warm-up and
measurement counts are required, or `short` for a quick exploratory run. The
manual form can use either the standard x64 Linux runner or the standard ARM64
Linux runner; smaller suites are available for focused investigation. The
weekly run uses adaptive mode on x64.

The generated raw BDN tables and the stable `comparative-report.md` / `.csv`
combined schema are appended to the Actions run summary. Unsupported native
batch capabilities are retained as `Supported=false`, `Mode=Unsupported`, and
`∞` mean/ratio rows. Complete output is retained as the `ecs-benchmarks-*`
artifact for 30 days.

The current matrix compares DeltaECS, Arch, Friflo.Engine.ECS, DefaultEcs and
LeoECS Lite. Additional focused routes are kept separate from
`full-comparison` so each report has one unambiguous scenario matrix.

GitHub-hosted runners are shared and their CPU model may change between jobs.
Use a single run to compare DeltaECS with the other ECS implementations because
all competitors then see the same machine. Do not treat small differences
between separate workflow runs as regressions. Stable cross-commit regression
gates require a dedicated self-hosted runner later.

The workflow intentionally does not request hardware performance counters:
standard GitHub-hosted runners do not provide reliable PMU access. It still
records BenchmarkDotNet time, ratios, managed allocations, GC data, and the
runner's CPU/runtime information.
