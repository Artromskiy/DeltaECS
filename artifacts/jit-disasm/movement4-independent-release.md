| Operation | Count | Priority |
|:---|---:|:---:|
| `blr` | 16 | **P1** |
| `bl` | 4 | **P2** |
| `bhs` | 2 | **P2** |
| `branch` | 40 | **P2** |
| `sbfiz` | 1 | **P1** |
| `umull` | 1 | **P2** |
| `ldr` | 97 | **P2** |
| `str` | 22 | **P2** |
| `ldp/stp` | 33 | **P1** |

---

## Probe details

- Mode: **release**
- Method: `Delta.ECS.MicroBenchmarks.MicroBenchmarkKernels:IterateMovement4IndependentIterators(Delta.ECS.MicroBenchmarks.MicroWorld,byref,Delta.ECS.CursorWriteBinding`1[Delta.ECS.MicroBenchmarks.Movement4A],Delta.ECS.CursorWriteBinding`1[Delta.ECS.MicroBenchmarks.Movement4B],Delta.ECS.CursorWriteBinding`1[Delta.ECS.MicroBenchmarks.Movement4C],Delta.ECS.CursorReadBinding`1[Delta.ECS.MicroBenchmarks.Movement4D]):int`
- Assembly: [movement4-independent-release.txt](vscode://file/Users/rum/GitProjects/TheFurnace/DeltaECS/artifacts/jit-disasm/movement4-independent-release.txt)
- First emitted code block: **1440 B**
- Reconstructed ARM64 instruction span: **1440 B**
- Counts are for the first emitted JIT block; repeated BDN parameter blocks are ignored.
- `bhs`/branches may belong to setup or chunk transitions, not necessarily the slot loop.
- Code size does not prove cache misses or throughput.

## Performance

BenchmarkDotNet `--job default`, one sequential run for the selected `v16`
variant. Mean is arithmetic mean; Allocated is managed allocation per
operation.

| Amount | Mean | Allocated |
|---:|---:|---:|
| 100 | 356.6 ns | 736 B |
| 1000 | 2,955.7 ns | 736 B |
| 10000 | 26,518.9 ns | 736 B |
| 100000 | 242,439.9 ns | 736 B |

This is one BDN run and should be treated as directional evidence, not a
confidence-ranked performance conclusion.
