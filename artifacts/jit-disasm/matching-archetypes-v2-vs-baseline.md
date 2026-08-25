# MatchingArchetypes V2 JIT comparison

Release JIT dry capture on Apple Silicon ARM64, .NET 10. The comparison is
against baseline commit `6d3ecdf` (`Reduce MatchingArchetypes temporary
storage`). Both captures use the same `Movement4Components` benchmark filter;
the first generated `MatchingArchetypes(World)` body is counted.

## Summary

| Metric | Baseline | V2 | Change |
|---|---:|---:|---:|
| Machine-code size | 1624 B | 1004 B | -620 B (-38.2%) |
| Assembly instructions | 410 | 251 | -159 (-38.8%) |
| Calls (`bl` + `blr`) | 33 | 18 | -15 |
| Branch instructions | 74 | 41 | -33 |
| Loads (`ld*`) | 77 | 62 | -15 |
| Stores (`st*`) | 56 | 24 | -32 |

V2 uses reusable capacity buffers and writes the archetype id, plan and reverse
index during the same archetype scan. It removes the temporary tuple array and
the later copy into native and managed storage. The returned spans are sliced
by `_matchingCount`, so empty, single and multiple matches share one
representation.

## Complete instruction table

Counts are for the complete compiled method, including cold validation and
throw blocks. A positive delta means that V2 emits more instructions.

| Instruction | Baseline | V2 | Delta |
|---|---:|---:|---:|
| `add` | 36 | 22 | -14 |
| `addv` | 4 | 4 | 0 |
| `adr` | 1 | 0 | -1 |
| `align` | 4 | 0 | -4 |
| `b` | 7 | 2 | -5 |
| `beq` | 2 | 4 | +2 |
| `bhi` | 0 | 2 | +2 |
| `bhs` | 9 | 5 | -4 |
| `bl` | 21 | 10 | -11 |
| `ble` | 1 | 0 | -1 |
| `blr` | 12 | 8 | -4 |
| `blt` | 4 | 1 | -3 |
| `bne` | 1 | 0 | -1 |
| `brk` | 5 | 3 | -2 |
| `cbnz` | 2 | 1 | -1 |
| `cbz` | 5 | 2 | -3 |
| `cmp` | 17 | 12 | -5 |
| `cnt` | 4 | 4 | 0 |
| `ldapr` | 2 | 1 | -1 |
| `ldp` | 18 | 20 | +2 |
| `ldr` | 57 | 41 | -16 |
| `mov` | 37 | 27 | -10 |
| `movi` | 1 | 1 | 0 |
| `movk` | 54 | 28 | -26 |
| `movn` | 1 | 1 | 0 |
| `movz` | 27 | 14 | -13 |
| `mul` | 1 | 0 | -1 |
| `ret` | 1 | 2 | +1 |
| `stp` | 20 | 16 | -4 |
| `str` | 34 | 8 | -26 |
| `strb` | 2 | 0 | -2 |
| `sxtw` | 5 | 4 | -1 |
| `tbnz` | 3 | 2 | -1 |
| `tbz` | 2 | 1 | -1 |
| `ubfiz` | 3 | 0 | -3 |
| `umaddl` | 2 | 1 | -1 |
| `umov` | 4 | 4 | 0 |
| `umulh` | 1 | 0 | -1 |

## Evidence

- [V2 raw JIT](matching-archetypes-v2-release.txt)
- Source: [`QueryPlan.MatchingArchetypes`](../../src/DeltaECS/Core/QueryAccess.cs:66)
- Baseline raw capture was generated from commit `6d3ecdf` in an isolated
  worktree and is retained outside the working tree for this comparison.

This is a JIT/code-size result, not a throughput measurement. No BenchmarkDotNet
measurement was run.
