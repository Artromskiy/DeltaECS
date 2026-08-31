# Wide payload partial-read and prefetch experiments

Status: **rejected**. These experiments did not produce a stable throughput
win over the direct sequential generated loop, so no runtime prefetch code is
retained.

## Workload

The benchmark uses eight component rows. Every component is a sequential CLR
struct with `Size = 512` bytes. The query requires all eight components, while
the callback consumes only `WidePayload0.Value` and `WidePayload7.Value`; the
six middle callback values are intentionally discarded. Amounts are `100`,
`1,000`, `10,000` and `100,000` entities. Setup and population are outside the
measured method, and the checksum is `Amount * 9`.

The runs used .NET 10.0.9, Arm64 RyuJIT AdvSIMD on Apple M4 Pro, concurrent
workstation GC, `WarmupCount=3`, `IterationCount=10`, `LaunchCount=1` and
`IterationTime=100 ms`, unless stated otherwise. The benchmark source was
committed in main as `54dd044` and in the tiled experiment as `3da51a7`.

## Direct main versus tile-local `ref`

The direct main run is the control. The candidate used a 16-KiB tiled loop,
but replaced each `Span`/`Slice` row with a tile-local `ref` and
`Unsafe.Add(ref tileRow, tileIndex)`.

| Entities | Main direct | Candidate tile-local `ref` | Candidate / main |
| ---: | ---: | ---: | ---: |
| 100 | 86.86 ± 0.656 ns | 292.17 ± 3.109 ns | 3.36x |
| 1,000 | 1,038.73 ± 12.131 ns | 3,012.7 ± 58.07 ns | 2.90x |
| 10,000 | 10,332.77 ± 251.282 ns | 30,097.6 ± 308.66 ns | 2.91x |
| 100,000 | 351,706.73 ± 20,534.79 ns | 389,708.6 ± 5,294.91 ns | 1.11x |

The candidate was reverted before branch cleanup. The reports were:

- main: `artifacts/wide-payload-512-main-100ms-20260831`
- candidate: `artifacts/wide-payload-512-ref-candidate-100ms-20260831`

## Prefetch and tiling screening

The following mechanisms were also tested on the earlier 256-byte version of
the same wide-row workload:

- volatile loads of all rows four entities ahead;
- volatile loads of edge rows at distance 16;
- edge-row loads at tile boundaries;
- sparse edge-row loads every four entities at distance 8;
- 32-, 64-, 128- and 256-KiB tile budgets;
- one-tile thresholds for small counts at 1,024, 4,096 and 16,384;
- direct typed row references instead of `Span` row slices.

All per-entity or sparse-load variants added real loads and were slower. Tile
boundaries and larger tile budgets did not produce a repeatable win; adaptive
thresholds could improve the tiled candidate for one small amount, but stayed
behind the direct main path and regressed at larger amounts. The long 1-s
check also kept the direct path ahead at `1,000` and `100,000` entities.

On this managed Arm64 target there is no public .NET prefetch intrinsic. A
`Volatile.Read` or vector load is a data load, not a cache hint, so it adds
work to the hot loop. The retained conclusion is that the hardware prefetcher
already handles the sequential row walk better than these software emulations.
Any future prefetch experiment must change the memory layout or use measured
hardware-counter evidence; do not repeat these mechanisms unchanged.
