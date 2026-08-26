# Arity trusted chunk kernel experiment

Status: **rejected**. A fresh serialized comparison showed material regressions
for both generated Functor and Delegate paths at every measured size. An older
candidate BenchmarkDotNet run started on 2026-08-26 was interrupted after
other agents started concurrent measurements on the same machine. That run is
excluded in full: no partial mean, deviation, allocation result or ratio from
it is evidence.

## Hypothesis and boundaries

- Branch: `perf/arity-trusted-chunk-kernel`.
- Baseline: `fb6c8d0b4b87511ac121eff74c84956da96c94d2`.
- Candidate source: `ee92c97c8f8adeea1f1593ef03c52a79051d612b`.
- Hypothesis: move each generated arity's row/ordinal preparation into one
  private non-inlined chunk helper, while keeping the entity loop directly in
  the generated invoker.
- Scope: generated dense `World.ForEach` invokers only. Public and generated
  public signatures, query/world ownership, lease lifetime, write stamps,
  empty-query behavior, sequence execution, and generic/type-erasure
  boundaries are unchanged.
- Managed rows remain CLR interior references held in stack-only `ReadRow` and
  `WriteRow` values. The candidate adds no rents, allocations, pointers,
  function pointers, or advancing `ref byte` cursor.

The Movement4 workload has one archetype with four `int` components. A/B/C are
writes, D is read-only, and each entity contributes checksum 20. Setup creates
the world, entities, query and access routes outside the measured method.

## Correctness and build evidence

- Release build of `benchmarks/DeltaECS.MicroBenchmarks`: passed, 0 errors and
  52 existing analyzer warnings.
- `contract-smoke`: passed. At `Amount=8`, ThreeWhile, TwoWhile, Functor,
  Delegate, DelegateContext and FunctorContext all returned checksum 160.
- The smoke also passed dense Movement2/Movement4 and Add/Remove/Create/Destroy
  invariants.
- Restore/build settings: `NuGetAudit=false`,
  `RestoreIgnoreFailedSources=true`, build servers disabled, one MSBuild node,
  Release, and shared compilation disabled.

## ARM64 Release JIT

Environment: Apple M4 Pro, 14 physical/logical CPUs, 25,769,803,776 bytes RAM,
128-byte cache line, 16,384-byte page, macOS Darwin 25.5.0, .NET runtime
10.0.9, Arm64 RyuJIT AdvSIMD, concurrent workstation GC. The Release dry probe
used `DOTNET_TieredCompilation=0`, `DOTNET_ReadyToRun=0` and diffable JIT
disassembly. Timings from the dry job are not performance evidence.

The generated `Invoke` inlines into `World.ExecuteGeneratedForEach`; therefore
that generic driver is the directly affected Movement4 hot-loop block. The
candidate `PrepareChunk` block is called once per selected chunk.

| Method/block | Main | Candidate | Candidate/main | Interpretation |
| --- | ---: | ---: | ---: | --- |
| Functor generated driver | 888 B | 856 B | 0.964 | Driver is 32 B smaller, with one additional indirect call |
| Delegate generated driver | 908 B | 780 B | 0.859 | Driver is 128 B smaller, with one additional indirect call |
| Functor `PrepareChunk` helper | — | 292 B | — | New separately emitted per-shape setup block |
| Delegate `PrepareChunk` helper | — | 292 B | — | Same 292 B / 73-instruction helper shape |
| Functor driver + helper | 888 B | 1,148 B | 1.293 | Total emitted code grows 260 B / 29.3% |
| Delegate driver + helper | 908 B | 1,072 B | 1.181 | Total emitted code grows 164 B / 18.1% |

Full compact instruction counts for the first emitted block follow. Alignment
directives are excluded from instruction-line totals; code size above includes
the JIT's emitted alignment bytes.

| Mnemonic | Main functor | Candidate functor | Main delegate | Candidate delegate | Candidate helper¹ |
| --- | ---: | ---: | ---: | ---: | ---: |
| `add` | 30 | 29 | 26 | 21 | 3 |
| `asr` | 1 | 1 | 0 | 0 | 0 |
| `b` | 6 | 6 | 6 | 6 | 0 |
| `beq` | 1 | 1 | 1 | 1 | 0 |
| `bge` | 2 | 3 | 2 | 3 | 0 |
| `ble` | 2 | 1 | 2 | 1 | 0 |
| `blo` | 3 | 3 | 3 | 3 | 0 |
| `blr` | 5 | 6 | 6 | 7 | 0 |
| `blt` | 4 | 4 | 4 | 4 | 0 |
| `brk` | 2 | 2 | 2 | 2 | 0 |
| `cbnz` | 4 | 4 | 4 | 4 | 0 |
| `cbz` | 3 | 3 | 3 | 3 | 0 |
| `cmp` | 11 | 11 | 11 | 11 | 0 |
| `ldp` | 10 | 14 | 15 | 14 | 1 |
| `ldr` | 56 | 43 | 54 | 37 | 39 |
| `ldrsb` | 7 | 2 | 7 | 1 | 11 |
| `mov` | 20 | 17 | 19 | 17 | 1 |
| `movi` | 0 | 1 | 0 | 1 | 0 |
| `movk` | 10 | 12 | 10 | 12 | 0 |
| `movn` | 0 | 1 | 0 | 1 | 0 |
| `movz` | 5 | 6 | 5 | 6 | 0 |
| `ret` | 2 | 2 | 2 | 2 | 1 |
| `sbfiz` | 4 | 4 | 4 | 4 | 0 |
| `stp` | 8 | 18 | 12 | 18 | 1 |
| `str` | 17 | 15 | 20 | 11 | 13 |
| `sub` | 2 | 2 | 2 | 2 | 0 |
| `sxtw` | 6 | 2 | 6 | 2 | 3 |
| `tst` | 1 | 1 | 1 | 1 | 0 |
| **Instruction lines** | **222** | **214** | **227** | **195** | **73** |

¹ The separately captured Functor and Delegate helpers are each 292 B and have
the same 73-instruction mnemonic table. Including the helper, candidate totals
are 287 instruction lines for Functor versus 222 on main (1.293), and 268 for
Delegate versus 227 on main (1.181). The driver has one additional `blr` in
both generated shapes; it calls the non-inlined helper once per chunk.

Raw output and generated compact reports remain under ignored
`artifacts/jit-disasm/` in the baseline and candidate worktrees. The exact
filters were the no-context
`Movement4ApiComparisonMicroBenchmarks.Functor` and `Delegate` methods, with
JIT method `ExecuteGeneratedForEach`; the helper used method `PrepareChunk`.

## BenchmarkDotNet comparison

The exact detached baseline at `fb6c8d0` ran first, followed immediately by
candidate `ee92c97`, with no concurrent benchmark process. Both used the same
Release binary shape, BenchmarkDotNet 0.13.12 `DefaultJob`, .NET 10.0.9 Arm64
RyuJIT AdvSIMD, concurrent workstation GC and the existing no-context Functor
and Delegate methods. `Params` was temporarily and identically restricted to
100, 1,000 and 1,000,000 in both worktrees, then restored without commit.
`NuGetAudit=false` and `RestoreIgnoreFailedSources=true` were set. Setup stayed
in `GlobalSetup`; the observable checksum matched in contract smoke.

| Method | Amount | Main Mean | Main Error | Main StdDev | Candidate Mean | Candidate Error | Candidate StdDev | Allocated | Candidate/main |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Functor | 100 | 121.1 ns | 0.37 ns | 0.33 ns | 180.9 ns | 0.23 ns | 0.19 ns | 0 B | 1.494 |
| Functor | 1,000 | 1,050.5 ns | 3.11 ns | 2.91 ns | 1,622.3 ns | 1.51 ns | 1.26 ns | 0 B | 1.544 |
| Functor | 1,000,000 | 1,046,665.8 ns | 4,352.86 ns | 3,858.69 ns | 1,700,261.6 ns | 11,994.38 ns | 11,219.55 ns | 0 B | 1.624 |
| Delegate | 100 | 184.3 ns | 0.81 ns | 0.68 ns | 201.6 ns | 0.38 ns | 0.33 ns | 0 B | 1.094 |
| Delegate | 1,000 | 1,661.7 ns | 11.52 ns | 9.62 ns | 1,762.5 ns | 3.06 ns | 2.86 ns | 0 B | 1.061 |
| Delegate | 1,000,000 | 1,715,041.8 ns | 4,033.26 ns | 3,772.72 ns | 1,855,798.9 ns | 1,205.43 ns | 1,068.58 ns | 0 B | 1.082 |

Functor regressed 49.38%, 54.43% and 62.45%; Delegate regressed 9.39%,
6.07% and 8.21% at 100, 1,000 and 1,000,000 entities respectively. Fresh raw
results remain under ignored `artifacts/bdn/serialized-baseline/` and
`artifacts/bdn/serialized-candidate/` in their corresponding worktrees.

The excluded command selected the existing no-context Functor and Delegate
methods and all six existing amounts. It was interrupted with exit code 130
after concurrent BDN activity was discovered. Its partial log remains under
`artifacts/bdn/arity-trusted-chunk-candidate/` only as an invalid-run audit
trail and must not be summarized as throughput evidence.

## Verdict

**Rejected.** Moving trusted row preparation behind a non-inlined per-chunk
helper prevents the preparation from participating in the generated driver's
inlining/code generation and adds one call per chunk. Although each driver
block is smaller in isolation, total emitted driver-plus-helper code grows by
29.3% for Functor and 18.1% for Delegate. The serialized throughput regressions
are large and consistent, so the smaller isolated driver does not justify the
extra helper block or call overhead.

## Final gates

- `FORMAT_CHECK=1 ./eng/format.sh`: passed.
- Release solution build with restore performed once, build servers disabled,
  one MSBuild node and shared compilation disabled: passed with 690 existing
  standard-analysis warnings and 0 errors.
- `DeltaECSTests`: 131 passed, 0 failed, 0 skipped.
- `DeltaECS.Generators.Tests`: 13 passed, 0 failed, 0 skipped. The generated
  consumer compilation test covers the explicit `ReadRow`/`WriteRow` helper
  output.
- Code metrics: passed with 893 advisory warning lines and 417 SARIF results,
  exactly equal to baseline (`delta 0`). SARIF categories were CA1067 1,
  CA1307 2, CA1502 2, CA1505 15, CA1506 8, CA1515 15, CA1707 62, CA1822 1,
  CA2000 50, CA2263 85, CA5394 33, CS0436 9,
  EnableGenerateDocumentationFile 1, IDE0008 123, IDE0021 1, IDE0022 1,
  IDE0059 4, IDE0090 3 and SYSLIB1045 1.
- Temporary benchmark `Params` edits were restored in both worktrees and were
  not committed.
- `git diff --check` and staged `git diff --cached --check`: passed.
