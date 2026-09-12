# ECS C# benchmark fork

`benchmarks/Ecs.CSharp.Benchmark` is a vendored fork of the complete workload
suite from [doraku/ecs.csharp.benchmark](https://github.com/doraku/ecs.csharp.benchmark).
The upstream scenarios and competitor implementations remain in the project;
DeltaECS is implemented as an additional backend, not as an adapter around the
maintained DeltaECS benchmark project.

The fork contains the complete upstream scenario groups:

- `CreateEntityWithOneComponent`
- `CreateEntityWithTwoComponents`
- `CreateEntityWithThreeComponents`
- `SystemWithOneComponent`
- `SystemWithTwoComponents`
- `SystemWithThreeComponents`
- `SystemWithTwoComponentsMultipleComposition`

Each group keeps the upstream ECS implementations and has a `DeltaECS`
benchmark using the same component cardinality, padding rules and terminal
operation. The three create groups also expose separate `DeltaECS_Batch` and
`DeltaECS_Batch_Generic` measurements for the no-output batch-create APIs. The
first reuses an `ArchetypeHandle`; the second resolves the archetype through
the generated variadic generic overload. Neither benchmark returns entity
handles. They are reported separately because the upstream create workload is
one entity per operation and must stay an apples-to-apples comparison. System
contexts use batch create only during setup; setup is outside the measured
method.

The fork references the local `DeltaECS` and `DeltaECS.Generators` projects so
the benchmark always exercises the current source APIs. For a published
package comparison, replace those references with matching package versions;
the generator package is attached as an analyzer, and no runtime adapter
assembly or third-party dependency is added to the ECS library.

The default build omits the slowest upstream implementations from comparative
runs: Morpeh, RelEcs, MonoGame.Extended, Svelto.ECS and Myriad's enumerable
path. Their source remains available for targeted investigation. Re-enable
them with `-p:IncludeSlowBenchmarks=true` when a complete upstream comparison
is required.

## Workload parameters

Entity amounts and padding are command-line inputs handled before
BenchmarkDotNet starts. This keeps the workload reproducible without
`[Params]` attributes:

```bash
dotnet run --project benchmarks/Ecs.CSharp.Benchmark/Ecs.CSharp.Benchmark.csproj \
  -c Release --no-restore -- \
  --amounts 100,1000,10000 \
  --padding 0 \
  --filter '*SystemWithTwoComponents*' \
  --job Default --iterationTime 100
```

Supported fork options:

- `--amount N` or `--entity-count N` — one entity amount;
- `--amounts N,N,...` — several amounts, executed as separate BDN runs;
- `--padding N` or `--entity-padding N` — empty-entity padding for system
  scenarios.

All other arguments are passed unchanged to BenchmarkDotNet, including
`--filter`, `--job`, `--iterationTime`, `--launchCount`, `--exporters` and
`--artifacts`. If no amount is supplied, the default is `100000`; padding
defaults to `0`.

## Build and smoke

```bash
dotnet build benchmarks/Ecs.CSharp.Benchmark/Ecs.CSharp.Benchmark.csproj \
  -c Release --no-restore --disable-build-servers -m:1 \
  /p:UseSharedCompilation=false

dotnet run --project benchmarks/Ecs.CSharp.Benchmark/Ecs.CSharp.Benchmark.csproj \
  -c Release --no-build --no-restore -- contract-smoke
```

The smoke path executes the DeltaECS one-, two-, three-component and multiple-
composition queries. BenchmarkDotNet discovery can be checked with:

```bash
dotnet run --project benchmarks/Ecs.CSharp.Benchmark/Ecs.CSharp.Benchmark.csproj \
  -c Release --no-build --no-restore -- --list flat
```

To include the temporarily excluded slow implementations:

```bash
dotnet run --project benchmarks/Ecs.CSharp.Benchmark/Ecs.CSharp.Benchmark.csproj \
  -c Release -p:IncludeSlowBenchmarks=true -- \
  --filter '*' --list flat
```

The upstream project is retained under its original license and attribution;
the fork is used for comparative engineering measurements. Its upstream
README explicitly warns that the workloads are not a universal ECS evaluation;
interpret results only for the workload and runtime reported by the run.
