| Operation | Count | Priority |
|:---|---:|:---:|
| `blr` | 16 | **P1** |
| `bl` | 4 | **P2** |
| `bhs` | 3 | **P2** |
| `branch` | 38 | **P2** |
| `sbfiz` | 1 | **P1** |
| `umull` | 1 | **P2** |
| `ldr` | 100 | **P2** |
| `str` | 23 | **P2** |
| `ldp/stp` | 34 | **P1** |

---

## Probe details

- Mode: **release**
- Method: `Delta.ECS.MicroBenchmarks.MicroBenchmarkKernels:IterateMovement4IndependentIterators(Delta.ECS.MicroBenchmarks.MicroWorld,byref,Delta.ECS.CursorWriteBinding`1[Delta.ECS.MicroBenchmarks.Movement4A],Delta.ECS.CursorWriteBinding`1[Delta.ECS.MicroBenchmarks.Movement4B],Delta.ECS.CursorWriteBinding`1[Delta.ECS.MicroBenchmarks.Movement4C],Delta.ECS.CursorReadBinding`1[Delta.ECS.MicroBenchmarks.Movement4D]):int`
- Assembly: [jit-release.txt](vscode://file/private/tmp/deltaecs-jit-variants.MFroFA/v17/artifacts/jit-release.txt)
- First emitted code block: **1456 B**
- Reconstructed ARM64 instruction span: **1456 B**
- Counts are for the first emitted JIT block; repeated BDN parameter blocks are ignored.
- `bhs`/branches may belong to setup or chunk transitions, not necessarily the slot loop.
- Code size does not prove cache misses or throughput.

## Performance

BenchmarkDotNet `--job default`, one sequential run per variant, with
`Amount` values 100, 1000, 10000 and 100000. Mean is arithmetic mean;
Allocated is managed allocation per operation.

| Amount | Mean | Allocated |
|---:|---:|---:|
| 100 | 380.5 ns | 25360 B |
| 1000 | 3,121.8 ns | 736 B |
| 10000 | 27,169.0 ns | 736 B |
| 100000 | 242,985.1 ns | 736 B |

The small allocation outliers (25360 B) are retained as observed in the CSV.
Use the 100000-entity row for the main throughput comparison; this report is
evidence from one run and is not a confidence-ranked performance conclusion.
