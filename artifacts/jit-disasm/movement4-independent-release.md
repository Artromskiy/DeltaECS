| Operation | Count | Priority |
|:---|---:|:---:|
| `blr` | 16 | **P1** |
| `bl` | 3 | **P2** |
| `branch` | 39 | **P2** |
| `sbfiz` | 1 | **P1** |
| `ldr` | 96 | **P2** |
| `str` | 21 | **P2** |
| `ldp/stp` | 33 | **P1** |

---

## Probe details

- Mode: **release**
- Method: `Delta.ECS.MicroBenchmarks.MicroBenchmarkKernels:IterateMovement4IndependentIterators(Delta.ECS.MicroBenchmarks.MicroWorld,byref,Delta.ECS.CursorWriteBinding`1[Delta.ECS.MicroBenchmarks.Movement4A],Delta.ECS.CursorWriteBinding`1[Delta.ECS.MicroBenchmarks.Movement4B],Delta.ECS.CursorWriteBinding`1[Delta.ECS.MicroBenchmarks.Movement4C],Delta.ECS.CursorReadBinding`1[Delta.ECS.MicroBenchmarks.Movement4D]):int`
- Assembly: [movement4-independent-release.txt](vscode://file/Users/rum/GitProjects/TheFurnace/DeltaECS/artifacts/jit-disasm/movement4-independent-release.txt)
- First emitted code block: **1408 B**
- Reconstructed ARM64 instruction span: **1408 B**
- Counts are for the first emitted JIT block; repeated BDN parameter blocks are ignored.
- `bhs`/branches may belong to setup or chunk transitions, not necessarily the slot loop.
- Code size does not prove cache misses or throughput.

## Performance

BenchmarkDotNet `--job default`, one sequential run for the final current
branch after merging v17. Mean is arithmetic mean; Allocated is managed
allocation per operation.

| Amount | Mean | Allocated |
|---:|---:|---:|
| 100 | 432.6 ns | 736 B |
| 1000 | 2,826.5 ns | 736 B |
| 10000 | 27,643.6 ns | 736 B |
| 100000 | 252,969.1 ns | 736 B |

This is one BDN run with very short invocation times; treat it as directional
evidence. The JIT instruction comparison is the acceptance criterion for v17.
