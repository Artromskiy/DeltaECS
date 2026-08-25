# QueryPlan MatchingArchetypes V3 JIT review

Comparison of the allocation-minimal V3 implementation against the baseline
before the experiment.

- Baseline: `38be81d`
- V3 code: `6d3ecdf`
- Target: macOS ARM64, Release, .NET 10, TieredCompilation=0, ReadyToRun=0
- Method: `Delta.ECS.QueryPlan:MatchingArchetypes(Delta.ECS.World)`
- Source: [QueryAccess.cs](../../src/DeltaECS/Core/QueryAccess.cs#L65)
- V3 raw JIT: [query-plan-matching-v3-release.txt](../../artifacts/jit-disasm/query-plan-matching-v3-release.txt)
- Baseline raw JIT: `/private/tmp/deltaecs-v3-baseline-2/artifacts/jit-disasm/query-plan-matching-baseline-release.txt`

The dry JIT capture compiled the same method four times for the benchmark
parameters. The first emitted body is used below; all V3 bodies were 1004 B.
The table counts instruction mnemonics in the complete method body, including
cold validation, allocation, helper and throw paths. It is not a throughput
measurement and does not prove cache behavior.

## Summary

| Variant | Code size | Instructions | Calls (`bl` + `blr`) | Branches* | Managed temporary arrays |
|---|---:|---:|---:|---:|---:|
| Baseline `38be81d` | 2744 B | 690 | 59 | 72 | `int[]` + `ArchetypePlan[]` |
| V3 `6d3ecdf` | 1004 B | 251 | 18 | 35 | one tuple array |
| Change | −63.4% | −63.6% | −69.5% | −51.4% | −1 temporary array |

\* Branches include conditional and unconditional branch mnemonics, excluding
`ret` and helper-call instructions.

## Complete mnemonic table

| Instruction | Baseline | V3 | Delta |
|---|---:|---:|---:|
| `add` | 53 | 22 | −31 |
| `addv` | 4 | 4 | 0 |
| `adr` | 1 | 0 | −1 |
| `align` | 4 | 0 | −4 |
| `b` | 15 | 2 | −13 |
| `beq` | 3 | 4 | +1 |
| `bge` | 1 | 0 | −1 |
| `bgt` | 1 | 0 | −1 |
| `bhi` | 6 | 2 | −4 |
| `bhs` | 5 | 5 | 0 |
| `bl` | 33 | 10 | −23 |
| `ble` | 2 | 0 | −2 |
| `blo` | 1 | 0 | −1 |
| `blr` | 26 | 8 | −18 |
| `blt` | 2 | 1 | −1 |
| `bne` | 1 | 0 | −1 |
| `brk` | 8 | 3 | −5 |
| `cbnz` | 1 | 1 | 0 |
| `cbz` | 11 | 2 | −9 |
| `cmp` | 21 | 12 | −9 |
| `cnt` | 4 | 4 | 0 |
| `ldapr` | 2 | 1 | −1 |
| `ldp` | 22 | 20 | −2 |
| `ldr` | 123 | 41 | −82 |
| `ldrsw` | 1 | 0 | −1 |
| `mov` | 87 | 27 | −60 |
| `movi` | 1 | 1 | 0 |
| `movk` | 94 | 28 | −66 |
| `movn` | 2 | 1 | −1 |
| `movz` | 47 | 14 | −33 |
| `mul` | 1 | 0 | −1 |
| `ret` | 1 | 2 | +1 |
| `stp` | 22 | 16 | −6 |
| `str` | 63 | 8 | −55 |
| `strb` | 2 | 0 | −2 |
| `subs` | 1 | 0 | −1 |
| `sxtw` | 3 | 4 | +1 |
| `tbnz` | 3 | 2 | −1 |
| `tbz` | 2 | 1 | −1 |
| `ubfiz` | 1 | 0 | −1 |
| `umaddl` | 4 | 1 | −3 |
| `umov` | 4 | 4 | 0 |
| `umulh` | 1 | 0 | −1 |

## Interpretation

V3 replaces two temporary arrays with one temporary tuple array, then copies
only the matching prefix into exact-sized final native and plan storage. The
large JIT reduction is primarily from removing `List`/`ToArray` and oversized
temporary-storage paths from the method body. The method still allocates one
temporary array and the final storage arrays; this report does not claim that
runtime allocation bytes or wall-clock time improved.

The next parallel implementation `eeec3ea` reuses the existing native and plan
buffers and is a separate candidate; it is intentionally not included in this
V3 comparison.
