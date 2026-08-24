# Slot-iterator micro-optimization evidence

This is an archived measurement record for the dense `Movement4Components`
slot iterator. It is evidence only; the current API and source path are
documented in [performance README](README.md) and [APIMAP](../APIMAP.md).

## Candidate matrix

| Candidate | Release/JIT result | Decision |
|---|---|---|
| Local slot-count cache | 1408 B, 16 `blr` | No code-generation change |
| Local current-index cache | 1408 B, 16 `blr` | No code-generation change |
| Direct query/row references in slot state | 1408 B, 16 `blr` | No code-generation change |
| `readonly`/`in` binding helpers | Release compile failed with span escape diagnostics | Not valid |
| Direct iterator field access | 1408 B, 16 `blr` | No code-generation change |
| Remove terminal assignments from outer iterator advancement | 1376 B, 15 `blr` | Retained in the measured source line |

The experiment changed no public callback or storage contract. The reported
code size is a JIT observation, not a throughput or cache result.

## Throughput sample

The retained variant was measured with the default BenchmarkDotNet job on
Apple arm64. The operation uses one invocation because it mutates component
rows; short amounts are directional.

| Amount | Mean |
|---:|---:|
| 100 | 3.357 us |
| 1,000 | 28.869 us |
| 10,000 | 48.547 us |
| 100,000 | 237.113 us |

The result is retained as historical evidence only. Re-run current source with
the commands in `benchmarks/README.md` before using it for a regression claim.
