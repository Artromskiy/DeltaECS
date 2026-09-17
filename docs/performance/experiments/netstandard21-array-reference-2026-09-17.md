# .NET Standard 2.1 array-reference experiment — 2026-09-17

The question was whether replacing the generic array-to-`Span<T>` conversion
could speed up the real generated `ForEach<T>` loop when the consumer targets
`netstandard2.1`.

The retained [microbenchmark](../../benchmarks/README.md#array-reference-microbenchmark)
creates 200,000 entities with one component, producing 391 full/partial chunk
rows at the current capacity of 512. A generated, intercepted `ForEach<T>`
reads every row and adds its value to a `long` checksum. Setup checks the
expected result (`19,999,900,000`) before timing. World and query setup stay
outside the measured method.

## Measurement

Both versions were built from baseline commit `e6e4167`, changing only
`src/DeltaECS/Core/ArrayAccess.cs` between runs. Measurements used Release,
BenchmarkDotNet 0.13.12 in-process, Mono 6.12.0.206 LegacyJit, x64 on an Apple
M4 Pro running macOS 26.5.2, one launch, five warmups, and twenty 200 ms
iterations. This is evidence for that Mono/runtime/architecture combination,
not a result for .NET 10 or every Unity runtime.

| Variant | Independent run means | Median | Allocated |
| --- | ---: | ---: | ---: |
| Baseline: `array.AsSpan()` then `MemoryMarshal.GetReference` | `160.3, 158.3, 158.5, 158.1, 158.1 µs` | `158.3 µs` | `0 B` |
| Candidate: direct `ref array[0]` and `Unsafe.Add(ref array[0], index)` | `111.4, 111.1, 111.4, 111.1, 110.7, 155.0, 108.0 µs` | `111.1 µs` | `0 B` |

The candidate median is about `1.42x` faster (`29.8%` less elapsed time) for
this one-component 200,000-entity pass. Six of seven candidate runs were between
`108.0` and `111.4 µs`; one run was `155.0 µs`, close to baseline. Its
within-run error was low, so that between-run discrepancy remains unexplained
and is recorded rather than discarded. Recheck on the target Unity/Mono version
before making a release performance claim.

Raw BDN CSV, markdown and HTML reports are under the ignored
`artifacts/array-ref-netstandard21/` directory.

## Decision and limits

The `netstandard2.1` implementation now acquires the row-start managed ref
inside a `fixed` block and advances with `Unsafe.Add(ref array[0], index)`.
Only the GC-tracked managed byref escapes the pinning scope; the pointer does
not. This removes the array-to-span path without depending on CLR object-header
layout. Current `GetRefAtZero` call sites pass allocated arrays with positive
capacity; `RefAt` callers use in-range indices.

`MemoryMarshal.GetArrayDataReference` was tried directly, but this project's
actual `netstandard2.1` reference pack rejects it with `CS0117`. A variant that
branches for empty arrays and uses `array[0]` measured about 2.5% faster than
baseline in two runs; `MemoryMarshal.CreateSpan(ref array[0], array.Length)`
was unstable and was not retained. The pasted object-header sketch was not
used as written: it starts from the local array-reference slot, not the array
payload.

## `fixed` and `Unsafe.As` follow-up

The follow-up holds `RefAt(array, index)` constant and changes only
`GetRefAtZero`, the operation under test. Each mode runs the same generated
200,000-entity `ForEach<T>`, checks the checksum `19,999,900,000`, and measures
70 iterations at 100 ms. BDN retained at least 65 observations after its
outlier filter. Results are from separate runs and must not be compared across
runtimes.

| Runtime / mode | Mean ± BDN error | StdDev | Retained N | Allocated |
| --- | ---: | ---: | ---: | ---: |
| Mono 6.12, `array[0]` | `228.6 ± 0.44 µs` | `1.07 µs` | 70 | `0 B` |
| Mono 6.12, `Span` | `164.5 ± 0.24 µs` | `0.59 µs` | 69 | `0 B` |
| Mono 6.12, `fixed` | `114.7 ± 0.17 µs` | `0.41 µs` | 67 | `0 B` |
| .NET 10.0.9, `array[0]` | `55.12 ± 0.37 µs` | `0.90 µs` | 69 | `0 B` |
| .NET 10.0.9, `Span` | `55.74 ± 0.30 µs` | `0.72 µs` | 70 | `0 B` |
| .NET 10.0.9, `fixed` | `55.84 ± 0.44 µs` | `1.02 µs` | 65 | `0 B` |
| .NET 10.0.9, `Unsafe.As` + offset | `56.29 ± 0.41 µs` | `1.00 µs` | 69 | `0 B` |

Mono ran as x64 LegacyJit; .NET 10 ran on Apple M4 Pro arm64 RyuJIT. The two
runtime series are separate comparisons. During this final run Unity consumed
about 97% CPU when checked immediately afterward. The large shift in the Mono
direct-index result between independent passes makes that runtime comparison
inconclusive under the observed host load. The .NET 10 modes are close; neither
`fixed` nor the offset candidate showed a meaningful gain, and offset was
slower than direct indexing in this pass.

### Mono repeat after Unity shutdown

The Mono-only modes were repeated with the same 200,000-entity workload and
70 × 100 ms job after Unity was shut down. BenchmarkDotNet retained 65–69
observations per mode:

| Variant | Mean ± BDN error | StdDev | Retained N | Allocated |
| --- | ---: | ---: | ---: | ---: |
| `array[0]` | `236.9 ± 2.62 µs` | `6.12 µs` | 65 | `0 B` |
| `Span` | `160.8 ± 0.39 µs` | `0.95 µs` | 69 | `0 B` |
| `fixed` | `112.2 ± 0.25 µs` | `0.61 µs` | 69 | `0 B` |

This repeat preserves the same ordering as the prior Mono pass, with `fixed`
about `2.11x` faster than direct indexing for this benchmark. Unity's editor
process was still present at low CPU usage (~2%) in the post-run process
snapshot, so this is a repeat with Unity mostly idle rather than a fully clean
host. The independent fixed and Span means stayed within about 2.2% of the
previous run; direct indexing varied by about 3.6%. Raw reports are under
`artifacts/array-ref-row-start-mono-rerun-20260917/`.

### CoreCLR repeat after Unity shutdown

The CoreCLR modes were also repeated with the same workload and job settings.
BenchmarkDotNet retained 65–70 observations per mode:

| Variant | Mean ± BDN error | StdDev | Retained N | Allocated |
| --- | ---: | ---: | ---: | ---: |
| `array[0]` | `53.78 ± 0.66 µs` | `1.55 µs` | 65 | `0 B` |
| `Span` | `57.54 ± 0.64 µs` | `1.56 µs` | 70 | `0 B` |
| `fixed` | `56.52 ± 0.83 µs` | `2.02 µs` | 70 | `0 B` |
| `Unsafe.As` + offset | `53.73 ± 0.20 µs` | `0.49 µs` | 69 | `0 B` |

Direct indexing and the CoreCLR-only offset mode are effectively tied: their
means differ by `0.05 µs`, less than either BDN error. `Span` and `fixed` did
not improve this workload. The Unity editor process remained resident around
`2%` CPU in the post-run snapshot, so the host was not fully idle. Raw reports
are under `artifacts/array-ref-row-start-coreclr-rerun-20260917/`. This was a
historical candidate comparison; production `net10.0` continues to use its
existing `MemoryMarshal.GetArrayDataReference` implementation.

The exact `Unsafe.As<object, byte>(ref ...)` snippet is not a valid way to get
the array payload from a local `T[]` reference. The tested .NET 10 candidate
uses CoreCLR's `Unsafe.As<ArrayHeader>(array)` object-reference intrinsic,
then applies the pointer-sized shift from the array length field. CoreCLR's
[`RawArrayData` layout](https://github.com/dotnet/runtime/blob/main/src/coreclr/System.Private.CoreLib/src/System/Runtime/CompilerServices/RuntimeHelpers.CoreCLR.cs)
documents this runtime-specific representation. The same helper failed setup
under Mono with `Missing component layout for 1`; no Mono offset result is
claimed. During the experiment, the candidate was compiled only for
`net10.0`; that temporary mode has since been removed.

The `fixed` returned managed ref also survived 1,000 forced compacting
collections in small Mono and CoreCLR probes. That only checks this lifetime
case. The Mono repeat supports using `fixed` for `netstandard2.1`; the CoreCLR
comparison does not justify changing the existing `net10.0` implementation.

The current runner builds only production target paths, without experimental
compile-time symbols:
`benchmarks/DeltaECS.ArrayRefBenchmarks/run-array-reference-benchmark.sh`.
Historical raw reports remain under the ignored
`artifacts/array-ref-row-start-20260917/` directory.

The production change is merged into `main` as `78c439b`. For
`netstandard2.1`, array row starts use `fixed` and `Unsafe.Add`; for `net10.0`,
the original `MemoryMarshal.GetArrayDataReference` path remains. Generated
source confirms that the intercepted callback stays fused into the chunk loop
and acquires the component row reference once per chunk. The candidate modes
and their raw results are retained here as historical evidence, but are no
longer selectable in the project.

After removing the candidate compile-time switches, the retained runner
measured the actual shipping paths again with the same workload and job:

| Target/runtime | Mean ± BDN error | StdDev | Retained N |
| --- | ---: | ---: | ---: |
| `netstandard2.1` / Mono 6.12 | `113.6 ± 0.29 µs` | `0.68 µs` | 67 |
| `net10.0` / CoreCLR 10.0.9 | `55.27 ± 0.164 µs` | `0.398 µs` | 70 |

These are separate runtime measurements, not a cross-runtime comparison. Raw
reports are under `artifacts/array-ref-production-20260917/`.
