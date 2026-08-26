# DeltaECS microbenchmark and JIT workflow

This directory contains two different kinds of performance work:

- `DeltaECS.Benchmarks` is the manual cross-ECS comparison suite.
- `DeltaECS.VersionBenchmarks` compares two API-compatible DeltaECS revisions.
- `DeltaECS.MicroBenchmarks` isolates one hot operation at a time. It is the
  evidence source for assembly-guided optimization; it does not replace the
  comparison suite.

Do not run a full BenchmarkDotNet measurement during normal review or CI. Build,
test, contract smoke and benchmark discovery are safe gates. A human explicitly
starts measurements on a stable, otherwise idle machine.

## Fast path

Use the smallest command set that answers the current question:

| Situation | Action |
|---|---|
| Existing JIT probe, unchanged source | `run-jit-disasm.sh --no-build` |
| Microbenchmark or ECS source changed | Build `DeltaECS.MicroBenchmarks` once |
| New/renamed benchmark class | Run `--list flat` once, then `contract-smoke` once |
| Need throughput | Run one quoted `--filter` with BDN's default job |
| Before commit | Run the Release build/tests from `WORKFLOW.md` and `git diff --check` |

Do not repeat restore, solution build, discovery, or contract smoke for every
assembly edit. Do not read the full test project before locating the benchmark
class and hot method. Full comparative BenchmarkDotNet runs are separate manual
evidence and are not part of this loop.

## Microbenchmark file layout

Keep the benchmark-facing file deliberately small:

- `DeltaECS.MicroBenchmarks/MicroBenchmarks.cs` contains only the catalog and
  public BDN wrappers. It is the short API surface for selecting iteration,
  add/remove, create and destroy scenarios.
- `DeltaECS.MicroBenchmarks/MicroBenchmarkImplementations.cs` contains fixture
  creation, reset/setup, kernels, checksums and contract smoke. It is where an
  implementation variant belongs.
- `DeltaECS.MicroBenchmarks/Program.cs` only dispatches `--list`, BDN filters
  and `contract-smoke`.

To add one comparable operation: add the implementation class and its direct
benchmark methods to `MicroBenchmarkImplementations.cs`, add one empty public
wrapper plus one catalog entry to `MicroBenchmarks.cs`, then run discovery and
contract smoke once. Do not copy fixture/setup code into the wrapper file.

The current scaffold exposes these operation families:

| Family | Entry points |
|---|---|
| Iteration | `Movement2Components`, `Movement4Components` through `BeginScope` |
| Structural | `Add`, `Remove`, `Create`, `Destroy` |
| Width | `ChangeWidth=1` and `ChangeWidth=4` for Add/Remove |

## Microbenchmark contract

One benchmark method answers one question. Setup belongs in `GlobalSetup`; the
method must return a checksum, entity, count or other observable result so the
JIT cannot remove useful work. Keep allocation, world creation, random input,
validation and report formatting outside the measured method.

Every microbenchmark must document:

| Field | Required value |
|---|---|
| Operation | Exact public or internal kernel operation under study |
| Data shape | Entity count, chunk capacity, archetype/component width |
| Baseline | Existing implementation or prior DeltaECS revision |
| Correctness | Test or post-operation invariant that proves equivalent results |
| Runtime | .NET version, architecture and GC mode |
| Result | Mean, allocation, code size and relevant assembly observation |

Do not publish a timing from a `Dry` job as a performance result. Do not compare
different machines, power modes, runtimes or architectures as if their ratios
were one result.

The default `World` chunk capacity is 512 entities. Capacity remains an explicit
benchmark parameter when testing a different storage layout; do not assume that
the default is optimal for every workload.

## First micro-algorithm matrix

The first implementation should create small, deterministic algorithm fixtures
and correctness tests for these operations. It must not change the public ECS
API merely to make a benchmark easier to write.

| Group | Algorithms | Typical parameters |
|---|---|---|
| Dense iteration | `Movement2Components`, `Movement4Components` through `BeginScope` | 100, 1k, 10k, 100k entities |
| Atomic structure | `Add`, `Remove`, `Create`, `Destroy` | one entity; width 1/4 for changes |

Use fixture names that describe domain work, not an implementation trick:
`Movement2Components`, `Movement4Components`, `Add`, `Remove`, `Create` and
`Destroy`. The current microbenchmark catalog contains one current
implementation. A future alternative must be added as a separate, explicitly
named scaffold entry.

## Assembly-guided loop

For one source change, use this order:

1. Build the microbenchmark project once; do not build the full solution.
2. Capture only the target method with `run-jit-disasm.sh --no-build`.
3. Run the same narrow BDN filter only if throughput/allocation is needed.
4. Change one thing and repeat from step 2, reusing the already-built project
   whenever the binary did not change.
5. Run correctness and full Release gates only before review/commit.

The benchmark method must return a checksum, entity, count or other observable
result. Setup, world creation, bindings, query construction, reset and report
formatting stay outside the measured method. Never use a `dry` result as timing
evidence.

Review only the selected hot method and its immediate driver. Look for bounds
checks in the entity loop, failed inlining, delegate/interface dispatch,
repeated row lookup, dynamic stride arithmetic, extra loads/stores and missed
vectorization. Assembly is evidence for a source change, never an artifact to
edit.

On Apple Silicon use `assembly-arm`, `apple-silicon` and `vectorization` when
reviewing the output. Use `assembly-x86` for the GitHub Linux runner output; do
not equate instruction sequences across architectures.

## Commands

Build the isolated microbenchmark project once after source changes:

```bash
dotnet build benchmarks/DeltaECS.MicroBenchmarks/DeltaECS.MicroBenchmarks.csproj \
  -c Release --no-restore --disable-build-servers -m:1 \
  /p:UseSharedCompilation=false /p:NuGetAudit=false -v:minimal
```

Run restore only when the project or package graph changed. A micro/JIT
iteration does not require `dotnet build DeltaECS.slnx` or the test project.

### VSTest host and local sockets

`dotnet test` starts a separate VSTest host. In a restricted runner it may
fail before loading tests with:

```text
System.Net.Sockets.SocketException: Permission denied
```

This is a test-host permission failure, not a test failure. Do not change the
test command or code and do not rerun a benchmark. Repeat the same bounded
Release `dotnet test` command with permission for the local test-host socket
(for Codex runs, request the escalated test-host execution). Record the first
socket-denied attempt as infrastructure-blocked and the second result as the
actual test result.

Comparative runner routes print a local timestamp when the runner starts, emit
a heartbeat every 30 seconds while BenchmarkDotNet is running, and print the
total elapsed time when the route finishes. The heartbeat belongs to the parent
runner and does not execute inside the measured benchmark process, so it does
not affect the measurements. Direct microbenchmark DLL runs do not provide this
runner heartbeat.

For a long local iteration comparison at elevated priority, run:

```bash
cd /Users/rum/GitProjects/TheFurnace/DeltaECS
benchmarks/run-sudo-iteration-comparison.sh
```

The script builds as the current user, requests `sudo` only for the measured
runner, applies `nice -n -20`, emits the existing 30-second heartbeat and
restores artifact ownership afterwards. Its defaults are 5 warmups, 15
measurement iterations and 1 launch, targeting roughly 25-35 minutes on an
Apple M4 Pro. Override them with `--warmups`, `--iterations` and `--launches`.
The current unified matrix contains five ECS implementations total: DeltaECS,
Arch, Friflo, DefaultEcs and LeoEcsLite.

When a class or method is new, discover and smoke it once. Do not repeat these
commands before every assembly edit:

```bash
dotnet benchmarks/DeltaECS.MicroBenchmarks/bin/Release/net10.0/DeltaECS.MicroBenchmarks.dll \
  --list flat
dotnet benchmarks/DeltaECS.MicroBenchmarks/bin/Release/net10.0/DeltaECS.MicroBenchmarks.dll \
  contract-smoke
```

Always quote filters so the shell does not expand `*`:

```bash
env NuGetAudit=false RestoreIgnoreFailedSources=true \
dotnet benchmarks/DeltaECS.MicroBenchmarks/bin/Release/net10.0/DeltaECS.MicroBenchmarks.dll \
  --filter '*DenseIterationMicroBenchmarks.Movement4Components*' \
  --artifacts artifacts/micro/movement4
```

Always keep these two environment settings on local BDN runs. BenchmarkDotNet
creates an autogenerated project and restores it even when the benchmark DLL
was already built. Without them, an unavailable NuGet vulnerability service
can turn `NU1900` into a build error before any benchmark executes. The
settings do not change measured code; they only make restore non-blocking.

BDN's default job automatically chooses invocation, warmup, iteration and
launch strategy. Do not add manual loops, `ShortRun`, `InvocationCount` or
fixed warmup counts unless the experiment explicitly requires them.

For a JIT-only capture, reuse the same DLL and do not rebuild:

```bash
./benchmarks/run-jit-disasm.sh \
  --method '*Movement4Components*' \
  --filter '*DenseIterationMicroBenchmarks.Movement4Components*' \
  --no-build \
  --output artifacts/jit-disasm/movement4-forward.txt
```

The helper sets `DOTNET_TieredCompilation=0`, `DOTNET_ReadyToRun=0` and
`DOTNET_JitDisasmDiffable=1`. Its default `dry` job executes the selected path
to emit JIT output; its timing is not a performance result. Use
`--job default` only for an intentionally measured BDN run. Do not use
`DisassemblyDiagnoser` for this workflow.

The helper accepts another already-built probe without compiling it:

```bash
./benchmarks/run-jit-disasm.sh \
  --project /private/tmp/deltaecs-dense-jit-probe/DenseJitProbe.csproj \
  --method 'RunMovement4' --filter '*' --no-build \
  --output artifacts/jit-disasm/dense-probe.txt
```

The helper is safe to invoke from the repository root, including for a probe
outside the repository. Its fixed order is: resolve the project, optionally
build it, verify the target DLL, change into that project's directory so
BenchmarkDotNet can find its `.csproj`, then run the selected JIT filter and
write the output file. Do not manually start BDN from the repository root for
an external probe; invoke the helper as shown above.

For an external probe the exact execution order is:

1. `dotnet build <probe>.csproj -c Release` once after source changes.
2. Run `./benchmarks/run-jit-disasm.sh --project <probe>.csproj ... --no-build`.
3. Read the JIT output from the requested `--output` path.

The helper changes directory internally before BDN starts, so step 2 works
even when the command is issued from the DeltaECS repository root.

Store raw BDN and JIT output under ignored `artifacts/` paths. For a before/
after comparison, use the exact same filter, runtime, architecture, machine
conditions and BDN arguments on both checkouts.

For assembly review, record calls (`bl`/`blr`), returns, unconditional and
conditional branches, compare-and-branch/test-bit branches, bounds branches
(`b.hs`/`bhs`), compare instructions, address arithmetic, multiply/divide,
bitfield/shift operations, scalar and pair loads/stores, prefetch and AdvSIMD
loads/arithmetic. The report categories intentionally overlap: for example a
`cbnz` contributes to both `compare branch` and the aggregate `branch` count.
Assembly size does not prove cache misses or throughput; use targeted BDN for
throughput and hardware counters only where the platform supports them.

For a compact repeatable instruction table, use the report wrapper. It invokes
the existing JIT helper, keeps the first emitted assembly block, counts the
relevant instructions and writes a Markdown table with assembly locations and
priority hints:

```bash
python3 benchmarks/jit-report.py \
  --method '*Movement4Components*' \
  --filter '*DenseIterationMicroBenchmarks.Movement4Components*' \
  --mode release \
  --output artifacts/jit-disasm/movement4-independent.txt \
  --report artifacts/jit-disasm/movement4-independent.md \
  --auto-open
```

After the probe DLL is already built, add `--no-build`; otherwise the helper
performs a non-blocking restore/build for the selected configuration. On macOS,
`--auto-open` opens the generated Markdown through the Obsidian URI handler,
copying reports outside the vault into the active vault under
`DeltaECS Reports/jit-disasm/`. Use `--obsidian-vault <path>` to override the
active vault selected from Obsidian's local configuration. The wrapper uses the
same non-blocking restore settings and `DOTNET_TieredCompilation=0`,
`DOTNET_ReadyToRun=0` settings as `run-jit-disasm.sh`; it does not run a
measuring BDN job unless `--job` is explicitly changed from `dry`.

Links inside the report use `vscode://file/...:line`, so clicking a source or
assembly location opens the exact line in VS Code while the report remains in
Obsidian. The generated report starts with a compact operation/count/priority
summary, followed by one source/assembly row per matched instruction; probe
metadata and interpretation notes are placed below both tables. A large probe
can therefore produce hundreds of detailed rows intentionally.

Use `--mode debug` to include the detailed source/assembly mapping table. This
mode requires a version-compatible Debug/Checked CoreCLR JIT, supplied as
`--checked-jit <path-to-libclrjit.dylib>`, through `DELTAECS_CHECKED_JIT`, or
discovered under `artifacts/toolchains/runtime-v*/artifacts/bin/coreclr/`
`osx.arm64.Checked/libclrjit.dylib`. A normal `dotnet build -c Debug` is not enough:
`DOTNET_JitDump` is compiled only into Debug/Checked JIT builds. Keep the
managed probe in Release because BenchmarkDotNet rejects non-optimized Debug
benchmark assemblies.

The runner copies the installed matching .NET runtime into ignored
`artifacts/toolchains/jit-runtime-<version>`, replaces the JIT only in that
isolated copy, and directs BenchmarkDotNet's generated host to the same local
CLI. It never replaces the system JIT.

The debug report uses the JIT's `genIPmappingGen()` table to map native offsets
to IL offsets, then reads the matching Portable PDB with the small
`JitSourceMap` helper to map each IL offset to its nearest sequence point. On
ARM64 every emitted instruction is four bytes; explicit alignment bytes are
also included. The mapping is approximate after optimization and inlining, but
it is derived from real JIT/PDB data. Prolog, epilog and `NO_MAP` ranges receive
no source link. The script deliberately fails if the supplied JIT emits no IP
mapping instead of falling back to representative source hints.

Example after building the matching Checked JIT in the auto-discovery path:

```bash
python3 benchmarks/jit-report.py \
  --method '*Movement4Components*' \
  --filter '*DenseIterationMicroBenchmarks.Movement4Components*' \
  --mode debug \
  --no-build \
  --output artifacts/jit-disasm/movement4-independent-debug.txt \
  --report artifacts/jit-disasm/movement4-independent-debug.md \
  --auto-open
```

Release mode intentionally omits the detailed table and does not require a
Checked JIT.

## Full comparison gate

The comparative project is manual evidence. Build it and run its non-measuring
contract smoke only when comparative source, manifest or report code changed:

```bash
dotnet build benchmarks/DeltaECS.Benchmarks/DeltaECS.Benchmarks.csproj \
  -c Release --no-restore --disable-build-servers -m:1 \
  /p:UseSharedCompilation=false /p:NuGetAudit=false -v:minimal
env NuGetAudit=false RestoreIgnoreFailedSources=true \
dotnet run --project benchmarks/DeltaECS.Benchmarks/DeltaECS.Benchmarks.csproj \
  -c Release --no-build --no-restore -- contract-smoke
```

Do not run the full comparative BenchmarkDotNet suite for a micro/JIT question.
Use its README and a quoted narrow `--filter` when a comparative measurement
is explicitly requested. The runner prints a start timestamp, a 30-second
heartbeat and total elapsed time; the heartbeat is outside the measured process.

## Dense iterator API

The microbenchmark dense path uses `World.BeginScope(in query)` and keeps the
archetype, chunk and slot traversal explicit. Typed access requests are created
in setup; values are obtained once when a chunk is selected, not in the slot loop:

```csharp
using var scope = world.BeginScope(in query);
// access is created in GlobalSetup.
var archetypes = scope.Archetypes;
while (archetypes.MoveNext())
{
    var chunks = archetypes.Current.Chunks;
    while (chunks.MoveNext())
    {
        var slots = chunks.Current.Slots;
        var values = slots.GetRow(access);
        while (slots.MoveNext())
        {
            ref readonly Value value = ref values[slots];
        }
    }
}
```

Queries use the component path through `BeginScope`; generated `ForEach`
overloads reuse the same slot iterator internally.
The microbenchmark catalog contains this dense path and the four direct
structural operations below it; removed duplicate traversal fixtures are not
part of discovery or measurement routes.

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
