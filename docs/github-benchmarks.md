# Ecs.CSharp.Benchmark on GitHub

Open the [Ecs.CSharp.Benchmark workflow](https://github.com/Artromskiy/DeltaECS/actions/workflows/ecs-csharp-benchmark.yml)
to see recent runs and summaries, or run it manually from **Actions →
Ecs.CSharp.Benchmark → Run workflow**. The workflow builds the Release
benchmark project, executes each entity count in a separate BenchmarkDotNet
run, and adds the standard GitHub Markdown result tables to the workflow
summary.

Default parameters:

- entity counts: 32, 1,024, 131,072 and 1,048,576 (`2^5`, `2^10`, `2^17` and
  `2^20`);
- minimum measurement iterations: 20;
- target duration per measurement iteration: 200 ms.

The run form allows changing the comma-separated entity counts, minimum
iteration count and iteration duration. Slow adapters such as Morpeh,
MonoGame.Extended, RelEcs and Svelto.ECS are excluded by building with
`IncludeSlowBenchmarks=false`, matching the benchmark project's default setup.

BenchmarkDotNet CSV and JSON exports, the result Markdown files and
runner/runtime details are retained in the workflow artifact for 30 days.
GitHub-hosted runners are shared; compare runs only when their runner and
runtime information are sufficiently similar.
