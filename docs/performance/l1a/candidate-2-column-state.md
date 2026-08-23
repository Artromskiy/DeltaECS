| Operation | Count | Priority |
|:---|---:|:---:|
| `blr` | 16 | **P1** |
| `bl` | 3 | **P2** |
| `branch` | 39 | **P2** |
| `sbfiz` | 1 | **P1** |
| `ldr` | 94 | **P2** |
| `str` | 20 | **P2** |
| `ldp/stp` | 34 | **P1** |

---

## Candidate 2 — column state caches row-array metadata

`QuerySlots` now carries the chunk's `Array[]` component-row table and
uses it directly for its once-per-chunk typed row resolution. Public API and
query execution is unchanged. JIT improved from the baseline 1408 B to 1400 B:
`ldr 96→94`, `str 21→20`, `ldp/stp 33→34`.

The narrow BDN run was directional (default job, .NET 8.0.29, Apple M4 Pro,
arm64, 736 B/op): 3.605 us / 28.286 us / 45.585 us / 218.606 us for amounts
100 / 1000 / 10000 / 100000. Invocation times are short and no paired baseline
run was made, so retain this candidate on JIT evidence pending final gates.

## Probe details

- Mode: **release**
- Method: historical L1A probe; its old generic request signature is no longer part of `ecs-next`.
- Assembly: [l1a-c2.txt](vscode://file/Users/rum/GitProjects/TheFurnace-DeltaECS-L1A/artifacts/jit-disasm/l1a-c2.txt)
- First emitted code block: **1400 B**
- Reconstructed ARM64 instruction span: **1400 B**
- Counts are for the first emitted JIT block; repeated BDN parameter blocks are ignored.
- `bhs`/branches may belong to setup or chunk transitions, not necessarily the slot loop.
- Code size does not prove cache misses or throughput.
