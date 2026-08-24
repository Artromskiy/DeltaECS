| Operation | Count | Priority |
|:---|---:|:---:|
| `blr` | 1 | **P1** |
| `ret` | 1 | **P2** |
| `add/sub` | 7 | **P2** |
| `ldr` | 6 | **P2** |
| `str` | 4 | **P2** |
| `ldp/stp` | 10 | **P1** |

---

## Probe details

- Mode: **release**
- Method: `Delta.ECS.MicroBenchmarks.DenseIterationMicroBenchmarkImplementation:Movement4Components():int:this`
- Assembly: [l4-jit-report.txt](vscode://file/Users/rum/GitProjects/TheFurnace/DeltaECS/artifacts/l4-jit-report.txt)
- First emitted code block: **140 B**
- Reconstructed ARM64 instruction span: **140 B**
- Counts are for the first emitted JIT block; repeated BDN parameter blocks are ignored.
- `bhs`/branches may belong to setup or chunk transitions, not necessarily the slot loop.
- Code size does not prove cache misses or throughput.
