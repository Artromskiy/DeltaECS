# Dense Movement4 JIT evidence

This note is an assembly-review record for the current dense query path. It is
not a public API description; use [APIMAP](../APIMAP.md) and the folder
READMEs for that.

## Current code path

The production traversal is implemented by the generated runtime bridge:

- `src/DeltaECS/Generator/GeneratedRuntime.cs` for the execution lease and
  prepared chunk plans;
- `src/DeltaECS/Generator/GeneratedQuerySlots.cs` for typed row references;
- `benchmarks/DeltaECS.MicroBenchmarks/MicroBenchmarkImplementations.cs` for
  the observable Movement4 checksum.

The former `QueryScope`/iterator/row chain remains internal legacy support and
is not a consumer entry point.

Row-array selection occurs at the chunk boundary. The slot loop performs the
component arithmetic and checksum; query ownership, plan refresh and write
tracking are outside that loop or at its chunk boundary.

## How to interpret a report

Review the generated driver and slot-loop blocks separately. `blr`, setup
loads, prologue pair operations and lifetime helpers in the driver are not
per-entity instructions. A slot-loop branch may be the loop back-edge rather
than a bounds check. Code size and instruction counts do not prove cache
behavior or throughput.

Use the reproducible commands in
[benchmarks/README.md](../benchmarks/README.md). A Release report omits
source-line mapping; a Debug/checked-JIT report may provide approximate IL to
Portable PDB mapping when a matching checked JIT is available.

## Pending experiments

Open and completed candidates are tracked in the
[optimization experiment ledger](experiments/README.md). Active dense-loop
experiments must preserve the safe public traversal API and require paired
correctness, JIT and throughput evidence.
