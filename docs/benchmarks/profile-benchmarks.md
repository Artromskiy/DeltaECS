# DeltaECS profile benchmarks

This suite compiles one public-API fixture against a frozen baseline checkout
and the candidate checkout. It covers shuffled component lookups after a
configurable number of registrations, stable repeated two-key ordering,
reversible add/remove and create/destroy operations, and dense entity traversal
as a guardrail. Every operation returns a checksum or state result.

Build and run the contract smoke with the configured baseline path:

```bash
dotnet build benchmarks/DeltaECS.ProfileBenchmarks/DeltaECS.ProfileBenchmarks.csproj -c Release -p:BaselineRoot=/path/to/frozen/checkout -p:CandidateRoot=/path/to/candidate -p:ProfileRuntimeFramework=net10.0
dotnet benchmarks/DeltaECS.ProfileBenchmarks/bin/Release/net10.0/DeltaECS.ProfileBenchmarks.dll smoke
```

BenchmarkDotNet workload values are command-line options parsed before BDN
starts and propagated to its child process through environment variables:

```bash
.../DeltaECS.ProfileBenchmarks.dll --amount 100000 --registered-components 2048 --archetypes 16 --filter '*Lookup*' --job Default --inProcess --warmupCount 8 --iterationCount 20 --iterationTime 300
```

Use `-p:ProfileRuntimeFramework=netstandard2.1` when the benchmark adapters
should compare the Unity-facing target framework. The benchmark host remains
on .NET 10 so BenchmarkDotNet can run it.

The smoke validates fixed expected checksums and state transitions on both
revisions. It does not run timing measurements.

The out-of-process toolchain needs distinct runtime artifact names (configured
in the runtime project) and may need `--buildTimeout 300` in constrained
environments. Use `--inProcess` for a prebuilt two-revision run. Keep
`DOTNET_TieredCompilation=0` and `DOTNET_ReadyToRun=0` consistent between runs.

`memory` inspects actual native index-table lengths and record stride;
`allocations` checks current-thread allocations over 1,000 warmed operations.
Both commands accept the same workload options. Preserve explicit stack
buffers in the single-component forwarders: older targets can allocate for
collection-expression span lowering.
