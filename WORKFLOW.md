# DeltaECS workflow

## Benchmark parameter policy

BenchmarkDotNet attributes may describe benchmark methods, categories and
lifecycle hooks, but they must not define workload or run parameters. Do not add
`[Params]`, `[ParamsSource]`, `[Arguments]`, `[ArgumentsSource]` or equivalent
parameter attributes. Parse every workload/configuration value from application
command-line arguments (or the invoking script) before BenchmarkDotNet starts,
and pass the resulting values into the benchmark runner. Keep BDN runner
switches such as `--filter` and `--job` separate from workload input. Existing
parameter attributes are migration debt: do not add new uses and replace them
when that benchmark is next modified.


## Documentation layout

The repository root contains only the four control documents used to route
agent work: `AGENTS.md`, `TODO.md`, `WORKFLOW.md` and `IDEAS.md`. All
substantive documentation is under `docs/`: the stable API entry point is
`docs/README.md`, source-area guides are under `docs/src/`, benchmark guides
under `docs/benchmarks/`, and decisions under `docs/adr/`. Generated benchmark,
JIT and profiler output remains under `artifacts/` and is not moved into the
documentation tree. Tool-owned metadata such as
`src/DeltaECS.Generators/AnalyzerReleases.Unshipped.md` stays beside the tool
because Roslyn consumes that file by convention; it is not project
documentation.

## Repository layout gate

The repository must follow the shared first-party layout documented in the
Furnace project standard. Before restore/build or a structural handoff, run:

```bash
./eng/check-layout.sh
```

The gate checks the mandatory top-level directories, rejects unexpected
tracked top-level folders, requires src/DeltaECS/ as the primary source
project, and requires source siblings to use the src/DeltaECS.<Area>/ form.
samples/ contains runnable examples; probes/ contains bounded
headless/compiler/contract checks. Empty mandatory domains stay tracked with
.gitkeep.

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

For multiline C# source generation and template payloads, prefer C# raw string
literals (`\"\"\"...\"\"\"`) over escaped regular strings or concatenation. Keep
the generated text readable and use another representation only when the
target language or interpolation requirements make a raw string unsuitable.

To choose another report location:

```bash
CODE_METRICS_ERROR_LOG=/tmp/deltaecs-metrics.sarif ./eng/code-metrics.sh -v:q
```

## NuGet packages

The runtime and generator packages must be published from the same commit and
must keep the same version. The current package version is `0.0.10`. From the
repository root, restore once and pack both projects into one temporary
directory:

```bash
package_dir="$(mktemp -d "${TMPDIR:-/tmp}/deltaecs-pack.XXXXXX")"
dotnet restore DeltaECS.slnx
dotnet pack src/DeltaECS/DeltaECS.csproj -c Release --no-restore -o "$package_dir"
dotnet pack src/DeltaECS.Generators/DeltaECS.Generators.csproj -c Release --no-restore -o "$package_dir"
```

Inspect both packages before publishing. The runtime package must contain the
`lib/net10.0/DeltaECS.dll` asset and the generator package must contain only
the analyzer asset under `analyzers/dotnet/cs`. Publish with a key supplied by
the shell environment; never write the key into this repository:

```bash
: "${NUGET_API_KEY:?Set NUGET_API_KEY in the shell; do not store it in the repository}"
dotnet nuget push "$package_dir/DeltaECS.0.0.10.nupkg" \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
dotnet nuget push "$package_dir/DeltaECS.Generators.0.0.10.nupkg" \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

Build benchmark projects and use dry contract smokes during review. Do not run
full BenchmarkDotNet measurements unless the user asks. For assembly-guided
micro-algorithms use [docs/benchmarks/README.md](docs/benchmarks/README.md) and
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
[docs/tools/DeltaECS.Profiling/README.md](docs/tools/DeltaECS.Profiling/README.md).
