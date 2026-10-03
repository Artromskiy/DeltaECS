# Profiler collector smoke test

`tools/profile-hotpath.sh` runs a small instrumented call-tree workload to
check the profiler's collection and report output. It does not measure DeltaECS
iteration performance. Use the dedicated benchmark projects for performance
measurements.

```bash
tools/profile-hotpath.sh --smoke --depth 16 --warmups 2 \
  --sections summary,table,tree --format text
```

Use `--sample-capacity N` to change the preallocated sample buffer. Reports can
be written to a file with `--destination file --output PATH`, printed to the
console, or sent to both destinations. `--help` lists all supported options.

The workload checks nested method entry/exit, sample aggregation, and report
generation. Its timings are profiler diagnostics, not application timings.
