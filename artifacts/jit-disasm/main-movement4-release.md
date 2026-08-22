| Operation | Count | Priority |
|:---|---:|:---:|
| `blr` | 11 | **P1** |
| `bl` | 3 | **P2** |
| `ret` | 2 | **P2** |
| `compare branch` | 15 | **P2** |
| `test-bit branch` | 1 | **P2** |
| `branch` | 33 | **P2** |
| `compare` | 11 | **P2** |
| `add/sub` | 38 | **P2** |
| `sbfiz` | 2 | **P1** |
| `shift/bitfield` | 1 | **P2** |
| `ldr` | 66 | **P2** |
| `str` | 12 | **P2** |
| `ldp/stp` | 28 | **P1** |

---

## Probe details

- Mode: **release**
- Method: `Delta.ECS.MicroBenchmarks.MicroBenchmarkKernels:IterateMovement4Dense(Delta.ECS.MicroBenchmarks.MicroWorld,byref,Delta.ECS.WriteRequest`1[Delta.ECS.MicroBenchmarks.Movement4A],Delta.ECS.WriteRequest`1[Delta.ECS.MicroBenchmarks.Movement4B],Delta.ECS.WriteRequest`1[Delta.ECS.MicroBenchmarks.Movement4C],Delta.ECS.ReadRequest`1[Delta.ECS.MicroBenchmarks.Movement4D]):int`
- Assembly: [main-movement4-release.txt](vscode://file/private/tmp/deltaecs-l7/artifacts/jit-disasm/main-movement4-release.txt)
- First emitted code block: **1084 B**
- Reconstructed ARM64 instruction span: **1084 B**
- Counts are for the first emitted JIT block; repeated BDN parameter blocks are ignored.
- `bhs`/branches may belong to setup or chunk transitions, not necessarily the slot loop.
- Code size does not prove cache misses or throughput.
