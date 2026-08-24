# L4 type-erased access experiment

This experiment is based directly on `6fc0e9c` and is intentionally isolated
from the comparative/version benchmark migration.

## API under test

The dense path is:

```text
ReadAccess/WriteAccess -> ReadRow/WriteRow -> Ref<T>
```

`T` is present only at component registration and the terminal
`values.Ref<T>(slots)` operation. The benchmark uses non-generic
`ReadAccess`/`WriteAccess`, `Bind`, and `slots.GetRow`; no generic access
path remains.

The existing comparative and version benchmark projects are separate suites;
they are not part of this focused L4 measurement.

## Reproduction

From the repository root, build first:

```sh
cd /Users/rum/GitProjects/TheFurnace/DeltaECS
env NuGetAudit=false RestoreIgnoreFailedSources=true \
  dotnet build benchmarks/DeltaECS.MicroBenchmarks/DeltaECS.MicroBenchmarks.csproj \
  -c Release --no-restore
```

Run the narrow BDN workload from the project directory. Running from this
directory is required because BDN generates its child project relative to the
current directory:

```sh
cd /Users/rum/GitProjects/TheFurnace/DeltaECS/benchmarks/DeltaECS.MicroBenchmarks
env NuGetAudit=false RestoreIgnoreFailedSources=true \
  dotnet bin/Release/net8.0/DeltaECS.MicroBenchmarks.dll \
  --filter '*DenseIterationMicroBenchmarks.Movement4Components*' \
  --exporters json csv markdown \
  --artifacts ../../artifacts/l4-bdn
```

BDN chooses invocation, unroll, warmup, and iteration strategy automatically;
only the existing workload parameters are selected.

For Release JIT evidence, return to the repository root after the build:

```sh
cd /Users/rum/GitProjects/TheFurnace/DeltaECS
./benchmarks/run-jit-disasm.sh \
  --method '*Movement4Components*' \
  --filter '*DenseIterationMicroBenchmarks*' \
  --configuration Release --framework net8.0 --no-build \
  --output artifacts/l4-jit.txt

python3 -B benchmarks/jit-report.py \
  --method '*Movement4Components*' \
  --filter '*DenseIterationMicroBenchmarks*' \
  --mode release --no-build \
  --output artifacts/l4-jit-report.txt \
  --report artifacts/l4-jit-report.md
```

The JIT report counts instructions in the first emitted block only. Code size
is not a cache-miss measurement; throughput must come from BDN or hardware
counters.
