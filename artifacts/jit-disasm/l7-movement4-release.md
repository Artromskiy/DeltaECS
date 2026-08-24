| Operation | Count | Priority |
|:---|---:|:---:|
| `blr` | 10 | **P1** |
| `bl` | 3 | **P2** |
| `ret` | 2 | **P2** |
| `compare branch` | 11 | **P2** |
| `test-bit branch` | 1 | **P2** |
| `branch` | 25 | **P2** |
| `compare` | 7 | **P2** |
| `add/sub` | 38 | **P2** |
| `sbfiz` | 2 | **P1** |
| `shift/bitfield` | 1 | **P2** |
| `ldr` | 61 | **P2** |
| `str` | 12 | **P2** |
| `ldp/stp` | 28 | **P1** |

---

## Probe details

- Mode: **release**
- Method: `Delta.ECS.MicroBenchmarks.MicroBenchmarkKernels:IterateMovement4Dense(Delta.ECS.MicroBenchmarks.MicroWorld,byref,Delta.ECS.WriteRequest,Delta.ECS.WriteRequest,Delta.ECS.WriteRequest,Delta.ECS.ReadRequest):int`
- Assembly: [l7-movement4-release.txt](vscode://file/private/tmp/deltaecs-l7/artifacts/jit-disasm/l7-movement4-release.txt)
- First emitted code block: **1016 B**
- Reconstructed ARM64 instruction span: **1016 B**
- Counts are for the first emitted JIT block; repeated BDN parameter blocks are ignored.
- `bhs`/branches may belong to setup or chunk transitions, not necessarily the slot loop.
- Code size does not prove cache misses or throughput.
