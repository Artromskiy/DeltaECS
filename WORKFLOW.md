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

To choose another report location:

```bash
CODE_METRICS_ERROR_LOG=/tmp/deltaecs-metrics.sarif ./eng/code-metrics.sh -v:q
```

Build benchmark projects and use dry contract smokes during review. Do not run
full BenchmarkDotNet measurements unless the user asks. For assembly-guided
micro-algorithms use [benchmarks/README.md](benchmarks/README.md) and
`benchmarks/run-jit-disasm.sh`. For GitHub/manual version comparison use
[docs/github-benchmarks.md](docs/github-benchmarks.md).
