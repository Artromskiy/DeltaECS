# V4 MatchingArchetypes JIT summary

Release JIT dry-run, macOS arm64 / .NET 10 Arm64 RyuJIT. No BenchmarkDotNet
measurements were run.

## Scope

The code change is limited to `QueryPlan.MatchingArchetypes` and its storage:

- [QueryAccess.cs:65](../../src/DeltaECS/Core/QueryAccess.cs:65)
- [QueryAccess.cs:72](../../src/DeltaECS/Core/QueryAccess.cs:72)
- [QueryAccess.cs:90](../../src/DeltaECS/Core/QueryAccess.cs:90)
- [QueryAccess.cs:115](../../src/DeltaECS/Core/QueryAccess.cs:115)

V4 keeps a single dense storage path for zero, one, and many matches. The
matching buffers are resized only when archetype capacity changes; the method
publishes the valid prefix using `_matchingCount`. This removes `List<T>`,
`CollectionsMarshal.AsSpan`, and `ToArray()` from the rebuild path.

## Code size summary

| Method | Baseline code | V4 code | Delta |
|---|---:|---:|---:|
| `QueryPlan.MatchingArchetypes` | 2744 B | 1620 B | -1124 B (-41.0%) |
| `World.ExecuteGeneratedForEach` | not captured in the old artifact | 780 B | reference only |

The old matching artifact predates the final V4 storage shape and includes the
previous single/multiple-plan implementation. It is therefore a JIT reference,
not a perfectly isolated A/B baseline for only the buffer rewrite.

## Full instruction table: MatchingArchetypes

Counts are mnemonic occurrences in the complete native method, including cold
branches and helper calls. `Delta` is V4 minus baseline.

| Instruction | Baseline | V4 | Delta |
|---|---:|---:|---:|
| `add` | 53 | 36 | -17 |
| `addv` | 4 | 4 | 0 |
| `adr` | 1 | 1 | 0 |
| `b` | 15 | 7 | -8 |
| `beq` | 3 | 2 | -1 |
| `bge` | 1 | 0 | -1 |
| `bgt` | 1 | 0 | -1 |
| `bhi` | 6 | 0 | -6 |
| `bhs` | 5 | 9 | +4 |
| `bl` | 33 | 21 | -12 |
| `ble` | 2 | 1 | -1 |
| `blo` | 1 | 0 | -1 |
| `blr` | 26 | 12 | -14 |
| `blt` | 2 | 4 | +2 |
| `bne` | 1 | 1 | 0 |
| `brk` | 8 | 5 | -3 |
| `cbnz` | 1 | 2 | +1 |
| `cbz` | 11 | 5 | -6 |
| `cmp` | 21 | 17 | -4 |
| `cnt` | 4 | 4 | 0 |
| `ldapr` | 2 | 2 | 0 |
| `ldp` | 22 | 18 | -4 |
| `ldr` | 123 | 56 | -67 |
| `ldrsw` | 1 | 0 | -1 |
| `mov` | 87 | 37 | -50 |
| `movi` | 1 | 1 | 0 |
| `movk` | 94 | 54 | -40 |
| `movn` | 2 | 1 | -1 |
| `movz` | 47 | 27 | -20 |
| `mul` | 1 | 1 | 0 |
| `ret` | 1 | 1 | 0 |
| `stp` | 22 | 20 | -2 |
| `str` | 63 | 34 | -29 |
| `strb` | 2 | 2 | 0 |
| `subs` | 1 | 0 | -1 |
| `sxtw` | 3 | 5 | +2 |
| `tbnz` | 3 | 3 | 0 |
| `tbz` | 2 | 2 | 0 |
| `ubfiz` | 1 | 3 | +2 |
| `umaddl` | 4 | 2 | -2 |
| `umov` | 4 | 4 | 0 |
| `umulh` | 1 | 1 | 0 |
| **Total** | **686** | **405** | **-281** |

## Caller table: ExecuteGeneratedForEach

This is the current caller/driver capture, kept separate from the matching
rebuild method so driver size is not confused with query-plan code.

| Instruction | Count |
|---|---:|
| `add` | 30 |
| `b` | 4 |
| `beq` | 1 |
| `bge` | 1 |
| `ble` | 2 |
| `blr` | 4 |
| `blt` | 2 |
| `cbnz` | 3 |
| `cbz` | 1 |
| `cmp` | 5 |
| `ldp` | 10 |
| `ldr` | 53 |
| `ldrsb` | 7 |
| `mov` | 20 |
| `movk` | 8 |
| `movz` | 4 |
| `ret` | 2 |
| `sbfiz` | 1 |
| `stp` | 8 |
| `str` | 17 |
| `sub` | 4 |
| `sxtw` | 6 |
| `tst` | 1 |
| `umull` | 1 |
| **Total** | **195** |

## Interpretation

- The largest reductions are `ldr` (-67), `mov` (-50), `movk` (-40), and
  `str` (-29), consistent with removing intermediate managed-list and array
  materialization paths.
- `bhs` increased by four and `ubfiz` by two: the capacity/count and slicing
  path has more explicit bounds/offset work. This is not a slot-loop result.
- The method still has 21 direct `bl` and 12 indirect `blr`; these belong to
  allocation, runtime helpers, and cold rebuild work. They are not per-entity
  calls.
- Assembly size is not a cache-miss or throughput measurement. A benchmark is
  required before accepting a runtime-performance claim.

## Raw captures

- [V4 MatchingArchetypes disassembly](v4-matching-archetypes-release.txt)
- [V4 ExecuteGeneratedForEach disassembly](v4-matching-caller-release.txt)
- [Stored baseline matching disassembly](cold-callchain-query-plan-release.txt)

Command used:

```bash
./benchmarks/run-jit-disasm.sh \
  --method '*MatchingArchetypes*' \
  --filter '*GeneratedFunctorMovement4MicroBenchmarks.Movement4GeneratedFunctor*' \
  --job dry --configuration Release --framework net10.0 --no-build \
  --output artifacts/jit-disasm/v4-matching-archetypes-release.txt
```
