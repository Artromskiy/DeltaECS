# L4 evidence summary

| Variant | Representation | Bytes/entity | JIT block | `blr` | `bhs`/branches | `ldr`/`str` | `ldp`/`stp` |
|---|---|---:|---:|---:|---:|---:|---:|
| Type-erased L4 `Movement4Components` | non-generic request/access/value chain, terminal `Ref<T>` | 16 (four `int` values) | 140 B | 1 | not emitted in first block | 6 / 4 | 10 |

The generated report records `ret=1`, `add/sub=7`, and no slot-loop `bhs` in
the first emitted block. The report parser intentionally does not infer cache
misses from code size.

## Paired BDN workload

The compatibility control uses the same fixture, layout, reset, checksum, and
non-generic hot loop. It differs only in setup calls (`Access<T>` versus
`Access(ComponentId, mode)`). The measured result therefore checks that the
generic compatibility check is outside the loop.

| Amount | L4 erased Mean | Error | StdDev | Allocated | Compatibility Mean | Error | StdDev | Allocated |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | 3.663 us | 0.1914 us | 0.5430 us | 736 B | 3.653 us | 0.1897 us | 0.5472 us | 736 B |
| 1,000 | 29.714 us | 1.7611 us | 5.1092 us | 736 B | 30.419 us | 1.3880 us | 4.0267 us | 736 B |
| 10,000 | 71.792 us | 2.7769 us | 8.1003 us | 736 B | 65.157 us | 2.7790 us | 7.9735 us | 736 B |
| 100,000 | 375.158 us | 9.4902 us | 26.4549 us | 736 B | 394.636 us | 15.3403 us | 44.9904 us | 736 B |

The run used the default adaptive BDN strategy. BDN reported low iteration
time/multimodal-distribution warnings, so these are directional paired
results, not a claim of a stable production speedup. No separate full
comparative suite was run; that suite remains on legacy compatibility callers.
