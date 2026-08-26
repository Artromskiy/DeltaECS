# DeltaECS workflow

Correctness first:

```bash
dotnet restore DeltaECS.slnx
dotnet build DeltaECS.slnx -c Release --no-restore \
  --disable-build-servers -m:1 /p:UseSharedCompilation=false -v:minimal
dotnet test tests/DeltaECSTests/DeltaECSTests.csproj \
  -c Release --no-build --no-restore --disable-build-servers
git diff --check
```

For the analyzer/code-metrics build, use the repository wrapper instead of
passing a relative `ErrorLog` property directly to `dotnet build`:

```bash
./eng/code-metrics.sh -v:q
```

`eng/code-metrics.sh` converts `CODE_METRICS_ERROR_LOG` (or its default
`artifacts/code-metrics/diagnostics.sarif`) to an absolute path before MSBuild
starts. Roslyn otherwise resolves a relative SARIF path separately for every
project and can fail when that project-local directory does not exist.

Internal boundary rule: every member declared inside an `internal` type must be
explicitly marked `internal` (or `private`). This marks the point where
validation has already completed and prevents accidentally exposing trusted
runtime operations as public API.

To choose another report location:

```bash
CODE_METRICS_ERROR_LOG=/tmp/deltaecs-metrics.sarif ./eng/code-metrics.sh -v:q
```

Build benchmark projects and use dry contract smokes during review. Do not run
full BenchmarkDotNet measurements unless the user asks. For assembly-guided
micro-algorithms use [benchmarks/README.md](benchmarks/README.md) and
`benchmarks/run-jit-disasm.sh`. For GitHub/manual version comparison use
[docs/github-benchmarks.md](docs/github-benchmarks.md).

Every measured optimization, including rejected and inconclusive candidates,
must be recorded in
[docs/performance/experiments/README.md](docs/performance/experiments/README.md)
before its branch is merged or deleted. Check the ledger before implementing a
candidate. Record the baseline/candidate commits, workload and data shape,
runtime/architecture/job, correctness evidence, Mean/Error/StdDev/Allocated,
ratio, JIT code size and instruction summary, and the final decision. Raw BDN,
JIT and profiler output remains under ignored `artifacts/`; the durable result
belongs in the ledger. A rejected mechanism may be retried only when the entry
states what materially changed in the implementation or measurement.

For hierarchical self/inner timing, use the isolated Metalama profiling build:

```bash
tools/profile-hotpath.sh --movement4 --depth 16 \
  --correction optional --sort adjusted \
  --destination file --output artifacts/profiling/movement4.txt
```

The profiler does not modify production `DeltaECS.dll`. Its architecture,
metric definitions, CLI and smoke commands are documented in
[tools/DeltaECS.Profiling/README.md](tools/DeltaECS.Profiling/README.md).
