# Sky Pirates real-game replay

This optional benchmark runs the headless Sky Pirates simulation over a
recorded gameplay replay. It checks the replay's final world hash on every
warmup and measured run, and reports setup, simulation, allocation, GC, and
total timings. It does not launch Unity or measure rendering/FPS.

The pinned game revision and replay live in the `benchmarks/SkyPirates`
submodule. The runner normally consumes the published DeltaECS 0.0.58 package;
the submodule has an opt-in `UseLocalDeltaECS=true` mode that references this
checkout's `src/DeltaECS` project. The benchmark script always selects that
mode, so performance changes in the active DeltaECS branch are exercised by
the real replay while the game and replay remain fixed.

Initialize the submodule and run the standard three warmups and thirty measured
replays:

```bash
git submodule update --init benchmarks/SkyPirates
bash benchmarks/run-skypirates-replay.sh
```

The script restores and builds the runner in Release, disables tiered
compilation and tiered PGO, pins the process to CPU 1, verifies the replay hash,
and writes a timestamped CSV under `/tmp`. Set `DOTNET` if the .NET SDK is not
on `PATH`; use `CPU_AFFINITY=none` when CPU pinning is unavailable. `WARMUPS`,
`REPEATS`, and `CSV` override the defaults. For example:

```bash
DOTNET=/path/to/dotnet WARMUPS=5 REPEATS=50 \
  CSV=/tmp/skypirates-candidate.csv bash benchmarks/run-skypirates-replay.sh
```

The runner also retains the package-based default for standalone Sky Pirates
builds. To compare a candidate with a baseline, run the script from each
DeltaECS revision using the same pinned submodule commit and runtime settings;
compare simulation medians, spread, allocations, and hash results. The game's
historical NuGet-package measurements and their raw samples are in the
[Sky Pirates benchmark report](../../benchmarks/SkyPirates/benchmarks/REPLAY_BENCHMARK.md).
