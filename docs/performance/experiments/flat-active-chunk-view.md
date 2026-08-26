# Flat active chunk view experiment

**Verdict: REJECT CURRENT FORM.** The flat `QueryPlan` view gives `TwoWhile`
strong multi-chunk wins, but regresses the single-chunk `Amount=100` case by
92.7%. `Functor` is flat and `Delegate` improves only modestly, so the result
does not justify keeping the representation in its current unconditional form.

## Hypothesis and implementation

Hypothesis: a `QueryPlan`-owned flat active `ChunkPlan` view, maintained once
on chunk activation/deactivation, can remove the archetype scan from the public
two-loop `QueryChunks` path and make generated write-tick reservation use an
exact O(1) active count. Generated `ForEach` and the public two-loop path consume
the flat view; the public three-loop archetype path is unchanged.

Source commit: `6cc65a109c5e0a91b40d0a63a80c265e226afb8d`
(`Optimize active query chunk traversal`), based on
`fb6c8d0b4b87511ac121eff74c84956da96c94d2`.

The implementation preserves query/world ownership checks, the structural
lease lifetime, write stamps, empty-query behavior, and the existing public API
and generic/type-erasure boundaries. A review found that within-archetype
swap-back updated `ArchetypePlan._flatIndices` without updating the moved
chunk's `QueryPlan._activeChunkPositions` entry. The source commit includes the
fix and a targeted regression test that exercises three chunks in one
archetype, two in another, global compaction, reactivation, and a later
deactivation.

## Correctness

- Candidate focused active-chunk suite: 41/41 passed after the reverse-map fix.
- Candidate full Release tests: 134/134 passed, 0 failed, 0 skipped.
- Baseline and candidate `contract-smoke` both passed. The Movement4 fixture
  sets `D=4`, and all compared methods return `20 * Amount`; the smoke compares
  `TwoWhile`, `Functor`, and `Delegate` against `ThreeWhile` at `Amount=8`
  (checksum 160). Required measured amounts therefore have matching expected
  checksums 2,000, 20,000, and 20,000,000.
- The generated empty-query write-stamp behavior has a dedicated regression
  test and remains unchanged.

## Benchmark protocol

Both worktrees were built in Release with
`NuGetAudit=false RestoreIgnoreFailedSources=true`. Baseline and candidate were
run serially on the same otherwise-idle machine after checking that no other
BenchmarkDotNet process existed. Artifacts were removed before each run. Setup
remained in `[GlobalSetup]`, outside measurement. No paired-run customization
was used: both runs used BenchmarkDotNet 0.13.12 `DefaultJob`, one launch, the
existing `Movement4ApiComparisonMicroBenchmarks`, and the same filters for
`TwoWhile`, `Functor`, and `Delegate`. High-priority scheduling was unavailable
with `Permission denied` for both runs, so both used the same normal priority.

Machine: Apple M4 Pro, 14 physical/logical cores, 24 GiB, macOS 26.5.2 arm64;
.NET SDK 10.0.301; .NET 10.0.9 Arm64 RyuJIT AdvSIMD; concurrent workstation GC.
Baseline completed 18/18 cases in 6m26s; candidate completed 18/18 in 6m11s.

Command, run once in each fresh worktree:

```text
env NuGetAudit=false RestoreIgnoreFailedSources=true \
  dotnet benchmarks/DeltaECS.MicroBenchmarks/bin/Release/net10.0/DeltaECS.MicroBenchmarks.dll \
  --filter '*Movement4ApiComparisonMicroBenchmarks.TwoWhile' \
           '*Movement4ApiComparisonMicroBenchmarks.Functor' \
           '*Movement4ApiComparisonMicroBenchmarks.Delegate' \
  --artifacts artifacts/bdn/flat-active-chunk-view
```

## BenchmarkDotNet results

Ratio is candidate mean / main mean; lower is faster. Values below are the
fresh CSV values, including all existing parameter values rather than only the
required 100, 1,000, and 1,000,000. All cases allocated 0 B.

| Method | Amount | Main mean | Main error | Main StdDev | Main alloc | Candidate mean | Candidate error | Candidate StdDev | Candidate alloc | Ratio |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| TwoWhile | 100 | 123.5 ns | 0.26 ns | 0.23 ns | 0 B | 238.0 ns | 0.78 ns | 0.65 ns | 0 B | 1.9271 |
| Functor | 100 | 120.2 ns | 0.23 ns | 0.20 ns | 0 B | 120.2 ns | 0.26 ns | 0.24 ns | 0 B | 1.0000 |
| Delegate | 100 | 185.8 ns | 0.51 ns | 0.48 ns | 0 B | 182.7 ns | 0.67 ns | 0.62 ns | 0 B | 0.9833 |
| TwoWhile | 1,000 | 2,328.3 ns | 4.14 ns | 3.67 ns | 0 B | 1,606.6 ns | 3.19 ns | 2.99 ns | 0 B | 0.6900 |
| Functor | 1,000 | 1,048.5 ns | 1.98 ns | 1.85 ns | 0 B | 1,046.2 ns | 4.24 ns | 3.97 ns | 0 B | 0.9978 |
| Delegate | 1,000 | 1,674.4 ns | 30.12 ns | 30.93 ns | 0 B | 1,637.9 ns | 7.90 ns | 6.60 ns | 0 B | 0.9782 |
| TwoWhile | 10,000 | 23,947.1 ns | 89.63 ns | 83.84 ns | 0 B | 17,301.9 ns | 13.68 ns | 12.80 ns | 0 B | 0.7225 |
| Functor | 10,000 | 10,610.2 ns | 12.83 ns | 11.37 ns | 0 B | 10,656.3 ns | 89.98 ns | 84.17 ns | 0 B | 1.0043 |
| Delegate | 10,000 | 17,020.1 ns | 104.48 ns | 97.73 ns | 0 B | 16,858.4 ns | 228.11 ns | 190.48 ns | 0 B | 0.9905 |
| TwoWhile | 100,000 | 237,833.4 ns | 239.58 ns | 200.06 ns | 0 B | 175,503.6 ns | 347.12 ns | 324.70 ns | 0 B | 0.7379 |
| Functor | 100,000 | 105,665.9 ns | 313.81 ns | 293.54 ns | 0 B | 105,265.2 ns | 761.23 ns | 712.06 ns | 0 B | 0.9962 |
| Delegate | 100,000 | 171,801.6 ns | 362.15 ns | 338.75 ns | 0 B | 166,751.8 ns | 1,791.63 ns | 1,675.89 ns | 0 B | 0.9706 |
| TwoWhile | 1,000,000 | 2,438,296.2 ns | 4,586.32 ns | 3,829.78 ns | 0 B | 1,686,321.7 ns | 1,184.18 ns | 988.84 ns | 0 B | 0.6916 |
| Functor | 1,000,000 | 1,044,672.4 ns | 3,883.24 ns | 3,632.38 ns | 0 B | 1,047,673.7 ns | 4,485.92 ns | 4,196.13 ns | 0 B | 1.0029 |
| Delegate | 1,000,000 | 1,716,981.1 ns | 2,648.63 ns | 2,477.53 ns | 0 B | 1,713,271.3 ns | 3,461.42 ns | 3,068.46 ns | 0 B | 0.9978 |
| TwoWhile | 10,000,000 | 24,898,734.9 ns | 37,096.21 ns | 32,884.83 ns | 0 B | 16,856,823.8 ns | 12,111.54 ns | 10,113.68 ns | 0 B | 0.6770 |
| Functor | 10,000,000 | 10,458,157.5 ns | 17,168.07 ns | 14,336.11 ns | 0 B | 10,450,880.3 ns | 30,769.97 ns | 28,782.25 ns | 0 B | 0.9993 |
| Delegate | 10,000,000 | 17,245,244.5 ns | 59,345.21 ns | 52,607.98 ns | 0 B | 16,724,298.3 ns | 66,202.10 ns | 61,925.49 ns | 0 B | 0.9698 |

`TwoWhile` is 26.2%-32.3% faster from 1,000 through 10,000,000 entities, but
the 100-entity/single-chunk case is 92.7% slower. `Functor` stays within 0.4%
of main at every amount. `Delegate` ranges from 0.2% to 3.0% faster.

## ARM64 Release JIT

JIT captures used `benchmarks/jit-report.py`, which invokes
`benchmarks/run-jit-disasm.sh`, after the reverse-map fix. Counts cover the
first emitted code block. They are supporting evidence, not throughput proof.

| Directly affected method | Main code size | Candidate code size | Delta | Candidate/main |
|---|---:|---:|---:|---:|
| Generated `World.ExecuteGeneratedForEach<...>` driver | 888 B | 704 B | -184 B | 0.7928 |
| `Movement4ApiComparisonKernels.TwoWhile` | 2,384 B | 2,332 B | -52 B | 0.9782 |

Full compact instruction-count table for the generated driver:

| Operation | Main | Candidate | Delta |
|---|---:|---:|---:|
| `blr` | 5 | 4 | -1 |
| `bl` | 0 | 0 | 0 |
| `ret` | 2 | 2 | 0 |
| bounds branch | 0 | 0 | 0 |
| compare branch | 7 | 3 | -4 |
| test-bit branch | 0 | 0 | 0 |
| branch | 17 | 6 | -11 |
| compare | 12 | 6 | -6 |
| add/sub | 32 | 27 | -5 |
| `sbfiz` | 4 | 2 | -2 |
| shift/bitfield | 1 | 1 | 0 |
| `ldr` | 63 | 57 | -6 |
| `str` | 17 | 17 | 0 |
| `ldp`/`stp` | 18 | 17 | -1 |

Full compact instruction-count table for the exact `TwoWhile` kernel:

| Operation | Main | Candidate | Delta |
|---|---:|---:|---:|
| `blr` | 33 | 31 | -2 |
| `bl` | 16 | 16 | 0 |
| `ret` | 2 | 2 | 0 |
| bounds branch | 3 | 4 | +1 |
| compare branch | 9 | 10 | +1 |
| test-bit branch | 6 | 6 | 0 |
| branch | 30 | 32 | +2 |
| compare | 16 | 16 | 0 |
| add/sub | 40 | 37 | -3 |
| `sbfiz` | 3 | 2 | -1 |
| shift/bitfield | 1 | 1 | 0 |
| `ldr` | 142 | 145 | +3 |
| `str` | 42 | 41 | -1 |
| `ldp`/`stp` | 38 | 32 | -6 |

The generated driver is 20.7% smaller with substantially fewer control-flow
instructions. The exact `TwoWhile` kernel is 2.2% smaller, but its instruction
mix is mixed; the BDN result determines the verdict.

## Final gates

- `FORMAT_CHECK=1 ./eng/format.sh`: passed.
- Release solution build with restore/audit settings and build servers
  disabled: passed, 0 errors.
- Release tests: 134 passed, 0 failed, 0 skipped.
- `./eng/code-metrics.sh -v:q`: passed, 0 errors. Candidate emitted 899 build
  warnings / 425 SARIF results; untouched main emitted 785 build warnings / 417
  SARIF results. The changed source/test files contain no new analyzer errors.
- `git diff --check`: passed.

## Follow-up

Investigate one-time representation selection separately: retain the old direct
single-archetype/single-chunk route and select the flat representation once when
the query becomes multi-chunk. That is a new hypothesis and is not part of this
experiment.
