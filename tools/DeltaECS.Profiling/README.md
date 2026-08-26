# DeltaECS profiling-only build

This tool produces a bounded single-threaded call profile for an ECS workload.
The production `src/DeltaECS` project has no profiling dependency. Instead,
`tools/DeltaECS.Profiled` compiles the ECS sources into a separate assembly and
uses Metalama to inject `ProfilerRuntime.Enter(int)` and
`ProfilerRuntime.Leave(int)` around every eligible first-party `Delta.ECS`
method reachable below the profiled root. Demand-generated `ForEach` methods,
invokers and callback boundaries emit the same numeric probes when the separate
profiling runtime is present. Normal DeltaECS builds emit none of these probes.

The measured path contains only numeric method IDs, timestamps, primitive
samples, and preallocated buffers. Metalama also emits a metadata attribute
containing each ID and method signature. Reflection reads those attributes
after collection, so method-name strings and dictionaries never enter the hot
path. Mono.Cecil is not used.

## Movement4 profile

From the repository root:

```bash
tools/profile-hotpath.sh \
  --movement4 \
  --warmups 2 \
  --depth 8 \
  --correction required \
  --correction-min-r2 0.8 \
  --sections summary,table,tree \
  --format text \
  --sort adjusted \
  --destination file \
  --output artifacts/profiling/movement4-delegate.txt
```

One `Movement4` run includes layout registration, world creation, entity
creation, component initialization, query creation, generated delegate
iteration, and teardown. No manual profiling scopes are present in that probe.
The probe fixes the workload at 100 entities. Before measurement, one pilot
launch determines the sample count and the runner automatically selects enough
launches to consume approximately 90% of `--sample-capacity` without overflow.

The report contains:

- `Raw`: observed instrumented duration;
- `Overhead`: estimated active-collector cost contained by the method;
- `Adjusted`: `max(0, Raw - Overhead)`;
- raw and adjusted self/inner time;
- a call tree before the flat table, with resolved method signatures, raw total,
  corrected self/inner time, estimated profiler overhead, and corrected total;
  percentages show self/inner composition, overhead versus raw time, and each
  node's raw share of its direct parent and corrected share of its root. Each
  node occupies one aligned ASCII row; the active `--sort` key is the first
  metric after method name and call count.

## Overhead calibration

Before the measured workload, the tool warms one deterministic nested path and
runs it with collection depths `0..N`. Depth zero executes the same injected
entry/exit calls with no active collector. For every other depth, the profiler
retains warmed method routing and buffers while measurement samples are reset.

The median elapsed time at each depth is compared against the number of active
samples. A Theil-Sen median slope estimates timestamp ticks per active probe;
the report also prints the fit `R²`. This makes the correction resistant to an
individual noisy depth or run.

Calibration defaults are two warmups, seven measurements, and 65536 path
iterations. They can be changed explicitly:

```bash
tools/profile-hotpath.sh \
  --movement4 \
  --depth 12 \
  --calibration-warmups 3 \
  --calibration-runs 11 \
  --calibration-iterations 16384
```

Use `--correction off` to skip calibration. `optional` reports the correction
with a warning when its fit is below the requested R²; `required` fails the
run on a weak fit or dropped samples. The dormant cost of the Metalama wrapper
is deliberately not removed: depth zero already contains that code. The
adjusted metric estimates active collection overhead, not uninstrumented
production throughput. Use BenchmarkDotNet for production throughput.

## Command-line contract

All measurement and report policy is supplied through command-line arguments.
Argument names are centralized in `ProfileArgumentNames`.

| Argument | Values | Purpose |
|:--|:--|:--|
| `--movement4`, `--smoke` | flag | Select the real probe or collector smoke. |
| `--depth` | positive integer | Maximum captured call depth. |
| `--warmups` | non-negative integer | Unmeasured workload calls before collection. |
| `--sample-capacity` | positive integer | Preallocated raw sample capacity. |
| `--correction` | `off`, `optional`, `required` | Active-overhead correction policy. |
| `--correction-min-r2` | `0..1` | Required calibration quality. |
| `--calibration-warmups` | non-negative integer | Warmups at each calibration depth. |
| `--calibration-runs` | positive integer | Paired active/inactive measurements per depth. |
| `--calibration-iterations` | positive integer | Calls in each calibration path measurement. |
| `--sections` | `summary,table,tree` | Included report sections; `all` is accepted. |
| `--format` | `text`, `markdown` | Fixed-width plain text (default) or the same ASCII report embedded in Markdown. |
| `--sort` | `raw`, `adjusted`, `self`, `calls` | Sibling ordering in the tree and flat-table ordering. |
| `--destination` | `console`, `file`, `both` | Report destination. |
| `--output` | path | Required by `file` and `both`. |

## Depth and buffer limits

`--depth` controls how many nested calls produce samples. Calls below the limit
still execute the small suppressed-depth path so exits remain balanced. The
report shows captured and dropped sample counts; a result with dropped samples
must not be used for correction.

The collector is single-threaded. Parallel workloads require one profiler per
thread and post-run report merging.

## Smoke profile

Without `--movement4`, the executable runs a small manual profiler smoke test:

```bash
tools/profile-hotpath.sh --depth 16
```

This checks sample collection only and is not an ECS performance measurement.
