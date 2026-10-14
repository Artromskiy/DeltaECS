# Reuse component row arrays across Worlds

## Change

When a `World` is disposed, its already-cleared component row arrays can be reused by later Worlds that register the same runtime component type. This avoids allocating and zeroing those arrays again when an application replaces Worlds. A single process-wide pool retains at most an estimated 8 MiB across at most 512 component types; collectible runtime types are excluded. The pool does not alter the public API or entity generation behavior.

This only reduces allocations after a prior World has populated the pool. The first World that creates a given component layout still allocates its arrays. Reusing arrays in the same World continues to use its existing local pool.

## Benchmark

Compared baseline `d79451f` (no process-wide row pool) with the candidate, using the same BenchmarkDotNet project and runtime adapters. Each measured operation creates 100,000 entities. Append-only setup disposes and replaces the World outside the timed call, so the candidate begins with row arrays returned by the preceding World. Recycled setup reuses the same World. The benchmark ran on .NET 10.0.13 x64 RyuJIT, Debian 13, Xeon Platinum 8573C, with tiered compilation and ReadyToRun disabled, 10 warmups, 40 iterations, and 5 launches. Values are mean ± 99.9% confidence interval; medians are included because some baseline runs were noisy.

| Scenario | Baseline time (median) | Candidate time (median) | Managed bytes per create |
| --- | ---: | ---: | ---: |
| Append-only, returns entity handles | `627.7 ± 62.3 μs` (`542.2 μs`) | `514.2 ± 8.8 μs` (`508.9 μs`) | `427.98 KB → 34.22 KB` |
| Append-only, no output array | `617.6 ± 54.5 μs` (`532.1 μs`) | `564.4 ± 60.1 μs` (`501.5 μs`) | `423.76 KB → 35.53 KB` |
| Reused World, returns entity handles | `654.2 ± 37.0 μs` (`596.7 μs`) | `626.7 ± 20.1 μs` (`607.0 μs`) | `8.89 KB → 8.89 KB` |
| Reused World, no output array* | `489.0 ± 13.0 μs` (`473.8 μs`) | `501.7 ± 15.5 μs` (`490.5 μs`) | `7.58 KB → 7.91 KB` |
| Warm single-entity Add/Remove toggle in a 100K World | `262.5 ± 14.2 ns` (`229.2 ns`) | `250.9 ± 17.0 ns` (`221.8 ns`) | `0 B → 0 B` |

The append-only cases reduce managed allocation by about 92%. Their timing intervals are wide, but the candidate medians improve by about 6% in both cases. Neither recycled-World timing has a repeatable regression, and both confidence intervals overlap. The isolated final-row allocation difference is small (`0.33 KB`); that scenario reuses its local arrays, and the measured timing difference is below the 5% threshold. The warmed Add/Remove toggle also remains allocation-free and within the same timing range.

`*` The no-output recycled case was rerun by itself to avoid the process-wide pool state left by the append-only benchmarks. The isolated run used the same settings and showed a 2.6% mean timing difference, below the 5–10% acceptance threshold.

The timing comparison measures `Create` itself. A separate series of eight Create/Destroy operations, with World replacement in the append-only case, had wide timing intervals: query destruction, World setup/disposal, and GC added substantial noise around the sub-millisecond Create call. I do not use that composite run to claim throughput; its append-only allocations moved in the same direction as the direct run.

## Validation

- ECS tests: 260 passed.
- Profile benchmark smoke test passed.
- `netstandard2.1` Release build passed with zero warnings and errors.
- `ComponentRowArrayPoolTests` verifies cross-World reuse with cleared contents and confirms collectible component types are not kept alive by the static pool.

Raw BenchmarkDotNet reports are in `/workspace/scratch/structural-array-pool-direct-v4`, the isolated no-output report is in `/workspace/scratch/structural-array-pool-nooutput-isolated`, and the Add/Remove report is in `/workspace/scratch/structural-array-pool-structural-toggle`.
