# Dense Movement4 JIT evidence

This note is an assembly-review record for the current dense query path. It is
not a public API description; use [APIMAP](../APIMAP.md) and the folder
READMEs for that.

## Current code path

The production traversal is implemented by:

- `src/DeltaECS/Core/QueryScope.cs` for the execution lease;
- `src/DeltaECS/Core/QueryArchetypes.cs` and `QueryChunks.cs` for outer loops;
- `src/DeltaECS/Core/QuerySlots.cs` for slot state and row preparation;
- `src/DeltaECS/Core/Rows.cs` and `src/DeltaECS/Generic/Rows.cs` for the final
  `Ref<T>` endpoint;
- `benchmarks/DeltaECS.MicroBenchmarks/MicroBenchmarkImplementations.cs` for
  the observable Movement4 checksum.

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
[benchmarks/README.md](../../benchmarks/README.md). A Release report omits
source-line mapping; a Debug/checked-JIT report may provide approximate IL to
Portable PDB mapping when a matching checked JIT is available.

## Pending experiments

The only open dense-loop experiments are an internal trusted row packet and a
possible AArch64 adjacent-load experiment. Both must preserve the safe public
three-loop API and require paired correctness, JIT and throughput evidence.
