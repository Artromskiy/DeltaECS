# Dense plan-state evidence

This archived probe compared small internal representations of dense query
plan state. The public three-loop API and the storage contract were unchanged.

## JIT result

The useful variant carried the already validated `QueryPlan` directly through
the dense iterator state and removed an unused owner indirection. In the probe
it reduced the first emitted ARM64 block from 1408 B to 1160 B, with:

| Metric | Probe baseline | Variant |
|---|---:|---:|
| `blr` | 16 | 12 |
| `bl` | 3 | 3 |
| aggregate branch | 39 | 35 |
| `ldr` | 96 | 76 |
| `str` | 21 | 14 |

The result is assembly evidence, not a general throughput claim. Reproduce
the current source with the narrow JIT and BenchmarkDotNet commands in
[benchmarks/README.md](../benchmarks/README.md) before making a regression
decision.
