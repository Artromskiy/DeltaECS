# MatchingArchetypes V1 JIT review

Release JIT dry capture on macOS arm64, .NET 10 / Arm64 RyuJIT AdvSIMD.
No BenchmarkDotNet measurement was used.

## Scope

V1 is commit `6d3ecdf`, compared with baseline commit `38be81d`.
Only `QueryPlan.MatchingArchetypes` storage was changed: V1 uses two
pre-sized managed arrays and a `matchingCount`; the public API and consumers
remain unchanged. The implementation is in
[`QueryAccess.cs`](../../src/DeltaECS/Core/QueryAccess.cs#L66).

The JIT method was captured while running the three-while Movement4 probe:
`Movement4ApiComparisonMicroBenchmarks.ThreeWhile`.

## Method summary

| Method | Baseline code | V1 code | Baseline instructions | V1 instructions | Calls | Branches | Loads | Stores |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `QueryPlan.MatchingArchetypes` | 1004 B | 1004 B | 251 | 251 | 18 | 19 | 41 | 24 |
| `QueryPlan.MatchingPlans` | 212 B | 212 B | 53 | 53 | 5 | 8 | 11 | 2 |

The counts are from the first identical JIT block in each dry capture. Calls
include `bl`, `blr`, and `br`; branches include conditional branches and
`cb*`/`tb*` instructions. Loads include `ldr`, `ldp`, and `ldapr`; stores
include `str` and `stp`.

## Full ARM64 mnemonic table: MatchingArchetypes

| Mnemonic | Baseline | V1 | Delta |
| --- | ---: | ---: | ---: |
| `add` | 22 | 22 | 0 |
| `addv` | 4 | 4 | 0 |
| `b` | 2 | 2 | 0 |
| `beq` | 4 | 4 | 0 |
| `bhi` | 2 | 2 | 0 |
| `bhs` | 5 | 5 | 0 |
| `bl` | 10 | 10 | 0 |
| `blr` | 8 | 8 | 0 |
| `blt` | 1 | 1 | 0 |
| `brk` | 3 | 3 | 0 |
| `cbnz` | 1 | 1 | 0 |
| `cbz` | 2 | 2 | 0 |
| `cmp` | 12 | 12 | 0 |
| `cnt` | 4 | 4 | 0 |
| `ldapr` | 1 | 1 | 0 |
| `ldp` | 20 | 20 | 0 |
| `ldr` | 41 | 41 | 0 |
| `mov` | 27 | 27 | 0 |
| `movi` | 1 | 1 | 0 |
| `movk` | 28 | 28 | 0 |
| `movn` | 1 | 1 | 0 |
| `movz` | 14 | 14 | 0 |
| `ret` | 2 | 2 | 0 |
| `stp` | 16 | 16 | 0 |
| `str` | 8 | 8 | 0 |
| `sxtw` | 4 | 4 | 0 |
| `tbnz` | 2 | 2 | 0 |
| `tbz` | 1 | 1 | 0 |
| `umaddl` | 1 | 1 | 0 |
| `umov` | 4 | 4 | 0 |
| **Total** | **251** | **251** | **0** |

## Full ARM64 mnemonic table: MatchingPlans

| Mnemonic | Baseline | V1 | Delta |
| --- | ---: | ---: | ---: |
| `add` | 3 | 3 | 0 |
| `b` | 1 | 1 | 0 |
| `beq` | 1 | 0 | -1 |
| `bgt` | 1 | 0 | -1 |
| `bhs` | 1 | 1 | 0 |
| `bl` | 1 | 1 | 0 |
| `ble` | 1 | 1 | 0 |
| `blo` | 0 | 1 | +1 |
| `blr` | 3 | 3 | 0 |
| `br` | 1 | 0 | -1 |
| `brk` | 1 | 2 | +1 |
| `blt` | 0 | 1 | +1 |
| `cbnz` | 0 | 1 | +1 |
| `cbz` | 0 | 1 | +1 |
| `cmp` | 4 | 4 | 0 |
| `ldp` | 2 | 2 | 0 |
| `ldr` | 11 | 10 | -1 |
| `mov` | 7 | 8 | +1 |
| `movk` | 8 | 6 | -2 |
| `movz` | 4 | 3 | -1 |
| `ret` | 0 | 1 | +1 |
| `stp` | 2 | 2 | 0 |
| `umaddl` | 1 | 1 | 0 |
| **Total** | **53** | **53** | **0** |

The small mnemonic redistribution in `MatchingPlans` is equivalent code size
and instruction count, not a measured throughput improvement.

## Allocation and conclusion

Baseline creates `List<int>` and `List<ArchetypePlan>` only after the first
match, then materializes both through `CollectionsMarshal.AsSpan`/`ToArray`.
V1 allocates two arrays sized to `world.Archetypes.Count`, writes matches by
index, and keeps the count in the query plan. This removes the temporary List
objects and the `ToArray()` materialization, while preserving the single
storage shape for zero, one, and many matches. It may retain unused capacity
when only a few archetypes match; that trade-off must be checked with an
allocation benchmark separately.

The JIT evidence does **not** show a code-size win: `MatchingArchetypes` is
identical at 1004 B / 251 instructions. The demonstrated benefit is reduced
temporary managed-object structure, not a faster hot loop. No cache-miss or
throughput conclusion is made from assembly size alone.

## Artifacts and reproduction

- [V1 raw JIT](../../artifacts/jit-disasm/matching-archetypes-v1-release.txt)
- [V1 `MatchingPlans` raw JIT](../../artifacts/jit-disasm/matching-plans-v1-release.txt)
- [V1 source](../../src/DeltaECS/Core/QueryAccess.cs#L66)

Baseline was captured from commit `38be81d` with the same Release dry command,
method pattern `*MatchingArchetypes*` and benchmark filter
`*Movement4ApiComparisonMicroBenchmarks.ThreeWhile*`.
