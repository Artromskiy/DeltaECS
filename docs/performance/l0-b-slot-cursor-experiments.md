# L0-B slot/cursor micro-optimization sweep

Baseline: `54e0b93` (`perf/query-cursor-version1`). All probes ran in the
isolated worktree `/Users/rum/GitProjects/DeltaECS-L0-B` on Apple M4 Pro,
macOS 26.5.2, .NET 8.0.29 Arm64 RyuJIT, with Release-optimized managed
code, tiering disabled by the JIT probe, and the same
`DenseIterationMicroBenchmarks.Movement4Components` filter. No tests, public API signatures, or
ECS storage structures were changed.

## Candidate matrix

| # | Candidate | Release/JIT result | Decision | Commit |
|---:|---|---|---|---|
| 1 | Local `SlotCount` cache in the retired filtered-slot path | 1408 B, 16 `blr`; Release build passed | reject: no JIT change | `de4b573` |
| 2 | Local current slot/index cache in row indexers | 1408 B, 16 `blr`; Release build passed | reject: no JIT change | `fab6d7a` |
| 3 | Store query/column references directly in slot iterator | 1408 B, 16 `blr`; Release build passed | reject: no JIT change | `60f3aa2` |
| 4 | `readonly`/`in` binding helpers | Release compile failed with span escape errors CS8166/CS8347 | reject: invalid candidate | `1ea954f` |
| 5 | Direct internal cursor/index field access | 1408 B, 16 `blr`; Release build passed | reject: no JIT change | `283e6fb` |
| 6 | Remove terminal redundant assignments in archetype/chunk `MoveNext` | **1376 B, 15 `blr`**; Release build passed | **keep** | `2b0f00b` |

JIT reports and raw disassembly for the successful probes are retained under
`artifacts/jit-disasm/l0b-{base,v1,v2,v3,v5,v6}.{md,txt}`. The final source
diff from baseline contains only the two deletions from candidate 6; cleanup
commit `26567dc` removed residual rejected edits from the experiment history.

## Winner BDN

BDN was run only for the JIT winner, using the existing microbenchmark and
default job. Runtime: .NET 8.0.29 Arm64 RyuJIT, concurrent workstation GC;
allocation stayed at 736 B per operation.

| Amount | Mean |
|---:|---:|
| 100 | 3.357 us |
| 1,000 | 28.869 us |
| 10,000 | 48.547 us |
| 100,000 | 237.113 us |

BDN output is under `artifacts/micro/l0b-v6-winner/`. These timings are
directional because the existing benchmark uses `InvocationCount=1` and
reports the standard low-duration warnings; the JIT code-size/instruction
change is the selection signal.

## Checks

- `dotnet build DeltaECS.slnx -c Release --no-restore --disable-build-servers -m:1 /p:UseSharedCompilation=false`: passed.
- `dotnet test tests/DeltaECSTests/DeltaECSTests.csproj -c Release --no-build --no-restore --disable-build-servers -m:1`: 66 passed, 0 failed, 0 skipped.
- Microbenchmark `contract-smoke`: passed.
- `git diff --check`: passed.
