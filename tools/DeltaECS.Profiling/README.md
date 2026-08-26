# DeltaECS call profiler

This tool produces a bounded single-threaded call tree for an ECS workload.
The production `src/DeltaECS` project has no profiling dependency. Instead,
`tools/DeltaECS.Profiled` compiles the ECS sources into a separate assembly and
uses Metalama to inject `ProfilerRuntime.Enter(int)` and
`ProfilerRuntime.Leave(int)` around every eligible first-party `DeltaECS`
method reachable below the profiled root. Demand-generated `ForEach` methods,
invokers and callback boundaries emit the same numeric probes when the separate
profiling runtime is present. Normal DeltaECS builds emit none of these probes.

The measured path contains only numeric method IDs, timestamps, primitive
samples, and preallocated arrays. It performs no string formatting, reflection,
aggregation or dictionary lookup while collection is active. Metalama emits a metadata attribute
containing each ID and method signature. Reflection reads those attributes
after collection, so method-name strings and dictionaries never enter the hot
path. Mono.Cecil is not used.

## Quick start

Run from the repository root:

```bash
tools/profile-hotpath.sh \
  --movement4 \
  --depth 16 \
  --warmups 2 \
  --correction optional \
  --sort adjusted \
  --destination file \
  --output artifacts/profiling/movement4.txt
```

The script automatically builds the profiling-only ECS assembly. It does not
modify or instrument the production `DeltaECS.dll`.

To collect only a generated `world.ForEach(...)` call and everything nested
inside it, add the root selector:

```bash
tools/profile-hotpath.sh \
  --movement4 \
  --root World.ForEach \
  --depth 32 \
  --warmups 10 \
  --correction optional \
  --sample-capacity 4000000 \
  --sections all \
  --format text \
  --sort adjusted \
  --destination file \
  --output artifacts/profiling/movement4-foreach-only.txt
```

The selector is resolved to numeric method IDs before collection. Outside the
selected root, woven methods perform no timestamp or sample-buffer work. The
root call and every instrumented descendant are collected until that root
returns. `World.ForEach` is a stable alias for generated delegate `ForEach`
extension methods; other selectors use a case-insensitive method-name match.

## Architecture

| Project/file | Responsibility |
|:--|:--|
| `DeltaECS.Profiled` | Recompiles ECS sources and applies the Metalama fabric to eligible `DeltaECS` methods. |
| `DeltaECS.Profiling.Runtime` | Owns the thread-local runtime, preallocated collector and report model. |
| `DeltaECS.Profiling` | Selects probes, calibrates overhead, resolves method names and writes reports. |
| `DemandDrivenForEachGenerator` | Emits numeric probes for generated extension, invoker and callback boundaries only when the profiling runtime is referenced. |
| `profile-hotpath.sh` | Selects the profiled build for `--movement4` and forwards all CLI arguments. |

Collection follows one path:

```text
Metalama/generated Enter(methodId)
    -> timestamp + stack frame
    -> original method
    -> timestamp + primitive sample
    -> post-run name resolution, aggregation, correction and rendering
```

## Movement4 profile

Full explicit example:

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

Tree nodes are aggregated by their complete call path, not only by
`parent method + method`. The same method reached through two branches is
therefore reported independently in each branch. Adjusted tree totals are
computed bottom-up as `adjusted self + adjusted children`; this keeps every
parent/root percentage within `0..100%`. The flat table intentionally remains
method-based and combines all call sites of the same method.

## Overhead calibration

Before the measured workload, the tool warms one deterministic nested path and
runs it with collection depths `0..N`. Depth zero executes the same injected
entry/exit calls with no active collector. For every other depth, the profiler
retains its preallocated buffers while measurement samples are reset.

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
| `--root` | method selector | Collect only matching roots and their instrumented descendants; `World.ForEach` selects generated delegate iteration. |
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
| `--help` | flag | Print the CLI contract without running a probe. |

Defaults are smoke probe, depth 16, no warmups, 1,048,576 samples, text output
sorted by adjusted time, and console output unless `--output` is supplied.
Movement4 enables optional correction by default and chooses its launch count
automatically from one pilot run and the sample capacity.

## Depth and buffer limits

`--depth` controls how many nested calls produce samples. Calls below the limit
still execute the small suppressed-depth path so exits remain balanced. The
report shows captured and dropped sample counts; a result with dropped samples
must not be used for correction.

The collector is deliberately single-threaded. Attaching one collector from a
second thread is rejected; parallel workloads require independent collectors
and a future post-run merge step.

## Smoke profile

Without `--movement4`, the executable runs a small numeric-probe smoke test:

```bash
tools/profile-hotpath.sh --depth 16
```

This checks balanced entry/exit collection and report generation only. It is
not an ECS performance measurement and does not build `DeltaECS.Profiled`.

## Adding or changing a probe

Keep workload configuration in the probe type rather than adding entity-count
or iteration arguments to the generic runner. A probe root must be one method
whose body contains the complete path to inspect:

```csharp
[ProfileMethod(0)]
internal static long Run()
{
    // Setup, execution and teardown belong here when the full path is desired.
}
```

Then add a `ProfileProbe` value, its command-line flag in
`ProfileArgumentNames`, and one dispatch case in `Program.cs`. Do not add
manual scopes inside ECS methods: the profiling-only assembly and source
generator own instrumentation below the root.

## Validation

Use a smoke before a longer profile:

```bash
tools/profile-hotpath.sh \
  --smoke \
  --depth 8 \
  --sections summary,tree \
  --format text
```

For the real instrumented path:

```bash
tools/profile-hotpath.sh \
  --movement4 \
  --depth 8 \
  --warmups 1 \
  --correction off \
  --sample-capacity 200000 \
  --sections summary,tree \
  --format text
```

Treat a report as invalid when samples were dropped. Corrected timings are an
estimate of the instrumented call tree, not a replacement for BenchmarkDotNet
throughput or hardware counters.
