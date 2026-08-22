# L1-B dense-plan/state sweep

Date: 2026-08-22  
Repository baseline: `54e0b93`  
Worktree: `/Users/rum/GitProjects/TheFurnace-DeltaECS-L1B`  
Runtime: .NET 8.0.29, Arm64 RyuJIT AdvSIMD, Apple M4 Pro, macOS 26.5.2

The probe was the same Release/JIT capture for
`DenseIterationMicroBenchmarks.Movement4Components`, with
`DOTNET_TieredCompilation=0`, `DOTNET_ReadyToRun=0`, and the first emitted ARM64
code block reported by `jit-report.py`. Code size is a JIT signal, not a
throughput claim.

| Variant | Commit | JIT size | `blr` | `bl` | Branch | `ldr` | `str` | Decision |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Baseline | `54e0b93` | 1408 B | 16 | 3 | 39 | 96 | 21 | reference |
| 1. `ref readonly DenseArchetypePlan` access | `e3dd5ed` | compile error | — | — | — | — | — | reject: `CS8170`/`CS8347` from ref-struct escape rules |
| 2. Flat plan rows array reuse | `dd159a5` | 1508 B | 16 | 4 | 39 | 104 | 26 | reject |
| 3. Direct row-index plan lookup | `437ca78` | 1448 B | 16 | 4 | 39 | 98 | 23 | reject |
| 4. Cache component row indexes per archetype | `215fd3b` | 1408 B | 16 | 3 | 39 | 96 | 21 | no JIT effect; reject |
| 5. Direct current chunk/slot state fields | `4f4745e` | 1412 B | 16 | 3 | 39 | 97 | 21 | reject |
| 6. Reduce owner/description indirections | `002c0f6` | **1160 B** | **12** | 3 | **35** | **76** | 14 | **winner** |

Variant 6 carries the validated `CachedQuery` directly through the dense
iterator state and removes the unused query owner from `DenseArchetypePlan`.
No public API or tag path was changed.

## Winner BDN probe

Only the winner received a BDN run, using the default job and the existing
`InvocationCount=1` benchmark contract. Results are directional because the
operation is short and BDN reported `MinIterationTime` warnings.

| Amount | Mean | Allocated |
|---:|---:|---:|
| 100 | 4.083 us | 736 B |
| 1000 | 29.116 us | 736 B |
| 10000 | 43.970 us | 736 B |
| 100000 | 198.328 us | 736 B |

The baseline commit has an earlier same-method BDN record of 252.969 us at
100000 entities in `artifacts/jit-disasm/movement4-independent-release.md`.
That is supporting context rather than a paired statistical run; the exact
baseline comparison for this sweep is the JIT probe above.

## Gates

- DeltaECS solution Release build: passed.
- DeltaECS tests: 66 passed, 0 failed.
- `git diff --check`: passed before report commit.
- No public API changes, tags, or benchmark suite-wide run.

Raw ignored JIT/BDN artifacts are under `artifacts/jit-disasm/l1b-*` and
`artifacts/bdn/l1b-v6-winner` in the isolated worktree.
