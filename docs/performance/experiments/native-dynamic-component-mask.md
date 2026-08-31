# Native dynamic component mask

Status: merged into `main` as the current functional mask representation;
rejected only as a throughput replacement. The public ECS contract was not
changed.

## Goal

Replace the four-word component mask with a native-memory array of `uint`
words, so component IDs are not limited by the legacy 256-bit representation.
The comparison target is the existing fixed-mask implementation at baseline
commit `50c1f60`.

Implementation commits:

- `97cf8e5` — initial native dynamic mask;
- `81ac418` — correctness and metadata hardening.
- `20efeb1` — merge into `main`.

The implementation is now part of `main`; the candidate paths below are
historical evidence locations.

## Implementation

`ComponentMask` stores a managed reference to an internal owner containing a
native `uint` buffer. The owner is released by its finalizer. The existing
`ComponentMask.Capacity` value remains only as a source-compatibility
constant; it is no longer a storage limit.

The implementation provides dynamic `Set`, `Contains`, `ContainsAll`,
`Intersects`, `Rank`, enumeration, equality, hashing and copying. Internal
query builders use one bulk `FromValidated` allocation. `Count` and the hash
are cached after construction or mutation. A regression test covers adding a
low component ID after a higher word has already been populated.

No public method signature changed. IDs above 255 are covered by tests,
including a world/archetype with 257 registered components.

## Correctness

The Release build completed without errors. The fresh test run passed
`110/110` tests. Existing repository warnings were unchanged.

## Movement4 comparison

Both sides used the same `ComparativeMovement4ComponentsBenchmarks` workload:
four `int` components, the same update kernel, zero measured allocations and
amounts `100`, `1,000`, `10,000` and `100,000` entities. Runtime was .NET
10.0.9, Arm64 RyuJIT AdvSIMD on Apple M4 Pro, with tiering and ReadyToRun
disabled, one launch, ten warmups and fifteen to twenty measured iterations.
The primary run used a one-second iteration target; it is preferred over the
short 100-ms screening because the latter was visibly noisy on this host.

| Entities | Baseline mean ± error | Candidate mean ± error | Candidate / baseline | Delta |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 113.5 ± 2.81 ns | 109.2 ± 1.33 ns | 0.962x | −3.8% |
| 1,000 | 1,102.3 ± 11.54 ns | 1,115.4 ± 20.38 ns | 1.012x | +1.2% |
| 10,000 | 11,040.1 ± 177.33 ns | 11,123.2 ± 253.36 ns | 1.008x | +0.8% |
| 100,000 | 114,027.3 ± 1,436.60 ns | 107,196.4 ± 1,257.16 ns | 0.940x | −6.0% |

All measurements allocated `0 B`. The middle-size errors overlap, while the
two endpoints differ in opposite directions in separate processes. Since the
sign changes with entity count, this is not a stable all-size throughput
improvement. The candidate therefore did not demonstrate a stable all-size
throughput win, but its functional dynamic-mask behavior is retained in
`main`.

The separate 100-ms full-matrix screen also changed with process order and
host load. It is retained as supplemental evidence, not as an acceptance
claim.

## Performance findings

1. Fixed masks perform a constant four-word operation. Dynamic matching makes
   `ContainsAll`, `Intersects` and `Rank` proportional to the number of
   native words. This is paid on query/archetype matching, not in the cached
   entity loop used by Movement4.
2. Every non-empty mask currently creates a managed owner and a native buffer;
   finalizer-based reclamation can add GC/finalizer pressure for transient
   masks. A `QuerySpec` commonly creates several masks.
3. Public repeated `Set` grows and copies a native buffer. The internal bulk
   builders avoid that cost, but repeated public mutation remains expensive.
4. Caching count and hash removes repeated cold scans, and the high-word
   preservation fix keeps dynamic `Set` correct, but neither changes the
   hot Movement4 path enough to produce a stable win.

The next meaningful storage experiment would be a world-owned arena or pooled
mask storage with an explicit lifetime. It was not implemented here because
it changes ownership and invalidation semantics and needs its own correctness
and allocation study.

## JIT evidence

The dry JIT probe captured the benchmark harness method at `2048 B` for both
baseline and candidate. It did not isolate the generated ECS execution method,
so no JIT-size improvement is claimed for this experiment. The dynamic mask
implementation is outside the cached Movement4 entity loop; the benchmark
result is the deciding evidence.

Evidence artifacts:

- Baseline long run: `/Users/rum/GitProjects/TheFurnace/DeltaECS/artifacts/native-dynamic-mask-baseline-long/results/Delta.ECS.Benchmarks.ComparativeMovement4ComponentsBenchmarks-report-default.md`;
- Candidate long run: `/private/tmp/deltaecs-native-dynamic-mask/artifacts/native-dynamic-mask-candidate-long/results/Delta.ECS.Benchmarks.ComparativeMovement4ComponentsBenchmarks-report-default.md`;
- Candidate JIT probe: `/private/tmp/deltaecs-native-dynamic-mask/artifacts/native-dynamic-mask-jit/candidate.log`;
- Baseline JIT probe: `/Users/rum/GitProjects/TheFurnace/DeltaECS/artifacts/native-dynamic-mask-jit-baseline/baseline.log`.
