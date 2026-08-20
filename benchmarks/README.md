# DeltaECS microbenchmark and JIT workflow

This directory contains two different kinds of performance work:

- `DeltaECS.Benchmarks` is the manual cross-ECS comparison suite.
- `DeltaECS.VersionBenchmarks` compares two API-compatible DeltaECS revisions.
- Future microbenchmark projects isolate one hot operation at a time. They are
  the evidence source for assembly-guided optimization; they do not replace the
  comparison suite.

Do not run a full BenchmarkDotNet measurement during normal review or CI. Build,
test, contract smoke and benchmark discovery are safe gates. A human explicitly
starts measurements on a stable, otherwise idle machine.

## Microbenchmark contract

One benchmark method answers one question. Setup belongs in `GlobalSetup`; the
method must return a checksum, entity, count or other observable result so the
JIT cannot remove useful work. Keep allocation, world creation, random input,
validation and report formatting outside the measured method.

Every microbenchmark must document:

| Field | Required value |
|---|---|
| Operation | Exact public or internal kernel operation under study |
| Data shape | Entity count, chunk capacity, archetype/component width, tags |
| Baseline | Existing implementation or prior DeltaECS revision |
| Correctness | Test or post-operation invariant that proves equivalent results |
| Runtime | .NET version, architecture and GC mode |
| Result | Mean, allocation, code size and relevant assembly observation |

Do not publish a timing from a `Dry` job as a performance result. Do not compare
different machines, power modes, runtimes or architectures as if their ratios
were one result.

## First micro-algorithm matrix

The first implementation should create small, deterministic algorithm fixtures
and correctness tests for these operations. It must not change the public ECS
API merely to make a benchmark easier to write.

| Group | Algorithms | Typical parameters |
|---|---|---|
| Entity records | resolve by `ref`, generation validation, location update | 1, 100, 10k entities |
| Creation | `Create(archetype)` and normal create with known component set | empty/partially full/full chunk |
| Dense access | cached `ReadRowBinding`/`WriteRowBinding` and `GetRow` | one, two and four rows |
| Iteration | `Movement2` and `Movement4`, direct/reverse chunk traversal | 100, 1k, 10k, 100k entities |
| Atomic structure | create, destroy, add/remove one and several components | transition cached/cold |
| List batches | create, destroy, add/remove for an `Entity[]` | same chunk/many chunks |
| Query batches | add/remove/destroy matching archetypes | untagged; tagged fallback separately |
| Storage primitives | swap-back, typed copy, clear reference-containing row only | value row and reference row |
| Tags | add/remove and query full/partial/empty overlay masks | full chunk and sparse slots |

Use fixture names that describe domain work, not an implementation trick:
`Movement2Components`, `DestroyEntitiesInOneChunk`,
`RemoveVelocityFromMovingEntities`, and so on. Candidate variants may add a
suffix such as `CachedBinding` or `UnsafeReference`; the baseline name remains
stable.

## Assembly-guided loop

1. Add the smallest fixture and an equivalent correctness test.
2. Build Release with build servers disabled. Record the exact command and
   machine/runtime details.
3. Run the microbenchmark once to establish a timing/allocation baseline.
4. Capture JIT output for only the hot method; use a stable JIT configuration:

   ```bash
   DOTNET_TieredCompilation=0 \
   DOTNET_ReadyToRun=0 \
   DOTNET_JitDisasm='*Movement2Components*' \
   DOTNET_JitDisasmDiffable=1 \
   dotnet <microbenchmark dll> --filter '*Movement2Components*'
   ```

   `BenchmarkDotNet.DisassemblyDiagnoser` may be used when it works on the
   current OS. Prefer the JIT environment variables above when it does not.

5. Review C# and generated assembly together. Look for bounds checks inside
   the entity loop, failed inlining, delegate/interface dispatch, repeated row
   lookup, dynamic stride arithmetic, extra loads/stores, branches and missed
   vectorization. Assembly is evidence for a source-level change, never an
   artifact to edit.
6. Make one narrowly scoped source change. Re-run correctness tests, the same
   microbenchmark and the same JIT capture.
7. Keep a change only if semantics remain identical and the intended workload
   improves reproducibly. Record regressions and inconclusive results too.

On Apple Silicon use `assembly-arm`, `apple-silicon` and `vectorization` when
reviewing the output. Use `assembly-x86` for the GitHub Linux runner output; do
not equate instruction sequences across architectures.

## Commands

Build the normal suite and run its non-measuring contract smoke:

```bash
dotnet build benchmarks/DeltaECS.Benchmarks/DeltaECS.Benchmarks.csproj \
  -c Release --no-restore --disable-build-servers -m:1 \
  /p:UseSharedCompilation=false

dotnet run --project benchmarks/DeltaECS.Benchmarks/DeltaECS.Benchmarks.csproj \
  -c Release --no-build --no-restore -- contract-smoke
```

When a microbenchmark project is added, it must expose `--list flat` for
discovery and a narrow `--filter` path. Store raw BenchmarkDotNet and JIT output
under an ignored `artifacts/` directory; commit only compact, reproducible
reports when a decision requires them.

The assembly-guided fixtures live in `DeltaECS.MicroBenchmarks`, outside the
normal solution and outside the version-comparison suite:

```bash
dotnet restore benchmarks/DeltaECS.MicroBenchmarks/DeltaECS.MicroBenchmarks.csproj
dotnet build benchmarks/DeltaECS.MicroBenchmarks/DeltaECS.MicroBenchmarks.csproj \
  -c Release --no-restore --disable-build-servers -m:1 \
  /p:UseSharedCompilation=false
dotnet benchmarks/DeltaECS.MicroBenchmarks/bin/Release/net8.0/DeltaECS.MicroBenchmarks.dll \
  --list flat
dotnet benchmarks/DeltaECS.MicroBenchmarks/bin/Release/net8.0/DeltaECS.MicroBenchmarks.dll \
  contract-smoke
```

For a later, explicitly requested JIT capture, keep setup outside the measured
method and use a narrow filter:

```bash
DOTNET_TieredCompilation=0 DOTNET_ReadyToRun=0 \
DOTNET_JitDisasm='*Movement2ComponentsReverse*' DOTNET_JitDisasmDiffable=1 \
dotnet benchmarks/DeltaECS.MicroBenchmarks/bin/Release/net8.0/DeltaECS.MicroBenchmarks.dll \
  --filter '*Movement2ComponentsReverse*'
```

## Version comparison

`DeltaECS.VersionBenchmarks` requires two checkouts and is intentionally not a
normal solution-build project:

```bash
dotnet build benchmarks/DeltaECS.VersionBenchmarks/DeltaECS.VersionBenchmarks.csproj \
  -c Release \
  -p:BaselineRoot=/absolute/path/to/baseline \
  -p:CandidateRoot=/absolute/path/to/candidate
```

The GitHub Actions manual version-comparison workflow is the preferred way to
run this against committed revisions. It is not an automatic gate. Its
`adaptive` mode uses an unconstrained BenchmarkDotNet `Job.Default`; `fixed`
uses the explicitly entered warm-up and measurement counts, and `short` uses
the built-in short job. The independently configurable launch count applies to
all three modes and defaults to one.
