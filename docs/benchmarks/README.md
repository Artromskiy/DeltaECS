# DeltaECS benchmarks

The benchmark directory contains one supported measurement family: iteration.
Every comparative result answers the same question: how fast does each ECS
iterate the same data shape?

## Projects

- `DeltaECS.Benchmarks` compares DeltaECS, Arch, Friflo.Engine.ECS, DefaultEcs
  and LeoEcsLite on the unified iteration matrix.
- `DeltaECS.VersionBenchmarks` compares two DeltaECS checkouts (baseline and
  candidate) on the same dense, Movement2 and Movement4 workloads.
- `DeltaECS.MicroBenchmarks` isolates DeltaECS iteration/API shapes for JIT and
  focused throughput work.

The supported comparative route is `iteration`. The version suite is also
iteration-only; it is intentionally separate because it builds the same
scenario against two source revisions.

The isolated parallel-iteration route is `parallel`. It runs
`ParallelMovement4IterationBenchmarks` against the generated sequential
Movement4 baseline and is intentionally not part of the five-ECS comparative
manifest. Its callback contains the same Movement4 operation without a
checksum; see the [parallel API notes](../src/DeltaECS/Parallel/README.md).

## Safe workflow

Build the required project once, then run contract smoke and discovery before a
measurement:

```bash
cd /Users/rum/GitProjects/TheFurnace/DeltaECS
env NuGetAudit=false RestoreIgnoreFailedSources=true \
  dotnet build benchmarks/DeltaECS.Benchmarks/DeltaECS.Benchmarks.csproj \
  -c Release --no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false

dotnet benchmarks/DeltaECS.Benchmarks/bin/Release/net10.0/DeltaECS.Benchmarks.dll \
  contract-smoke
dotnet benchmarks/DeltaECS.Benchmarks/bin/Release/net10.0/DeltaECS.Benchmarks.dll \
  iteration --list flat --filter '*'
```

For adaptive throughput, use BenchmarkDotNet's default job and set only the
target measurement duration. Do not add fixed invocation counts to iteration
benchmarks:

```bash
env NuGetAudit=false RestoreIgnoreFailedSources=true \
  dotnet benchmarks/DeltaECS.Benchmarks/bin/Release/net10.0/DeltaECS.Benchmarks.dll \
  iteration --filter '*Movement4Components*' --job Default --iterationTime 100 \
  --exporters json csv markdown github --artifacts artifacts/iteration
```

The parallel Movement4 route accepts its matrix as runner arguments. These
options are removed before the remaining arguments are passed to
BenchmarkDotNet:

```bash
dotnet benchmarks/DeltaECS.Benchmarks/bin/Release/net10.0/DeltaECS.Benchmarks.dll \
  parallel \
  --amount 1_000 \
  --workers 2 \
  --warmupCount 10 --iterationCount 20 --iterationTime 500 --launchCount 1 \
  --filter '*ParallelMovement4IterationBenchmarks*' \
  --exporters csv markdown github \
  --artifacts artifacts/parallel-movement4-1000-2workers
```

Use `--amounts 100,1000,10000` and `--worker-counts 2,4` to run a selected
matrix. Without these options the route keeps its default matrix of all
entity amounts and four workers.

The GitHub workflow uses the same `iteration` route. Pull requests and pushes
run build, tests, contract smoke and discovery only. Manual and scheduled
measurements use adaptive 100 ms iterations by default; fixed and short modes
are available only when explicitly selected.

## Two-revision comparison

The version harness needs two checkouts and their matching project roots:

```bash
BASELINE_ROOT=/absolute/path/to/baseline \
CANDIDATE_ROOT=/absolute/path/to/candidate \
  dotnet run --project benchmarks/DeltaECS.VersionBenchmarks \
  -c Release -- --filter '*VersionMovement4Benchmarks*' \
  --job Default --iterationTime 100
```

Before measuring, build both revisions in Release and run:

```bash
dotnet benchmarks/DeltaECS.VersionBenchmarks/bin/Release/net10.0/DeltaECS.VersionBenchmarks.dll smoke
```

Both revisions receive the same entity amounts (`100`, `1_000`, `10_000`,
`100_000`), runtime, architecture and BenchmarkDotNet arguments. Setup and
reset work stay outside the measured methods.

## Microbenchmarks and JIT

`MicroBenchmarks.cs` is only the short BDN catalog and wrappers. Fixture and
kernel code belongs in `MicroBenchmarkImplementations.cs`; the only retained
microbenchmark family is iteration/API shape comparison.

For a JIT probe after the project is built:

```bash
./benchmarks/run-jit-disasm.sh \
  --method '*Movement4Components*' \
  --filter '*DenseIterationMicroBenchmarks.Movement4Components*' \
  --no-build \
  --output artifacts/jit-disasm/movement4.txt
```

Use `jit-report.py` when a compact instruction report is needed. Keep raw
output under ignored `artifacts/`; a dry JIT probe is not a throughput result.
On local macOS runs, retain `NuGetAudit=false` and
`RestoreIgnoreFailedSources=true` because BenchmarkDotNet restores its
generated project.
