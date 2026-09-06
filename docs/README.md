# DeltaECS documentation

The [project README](../README.md) is the public entry point. This page is a
navigation index; detailed contracts, implementation notes and historical
performance evidence stay in their dedicated documents.

## Public and integration API

- [API map](APIMAP.md)
- [Integration API](src/DeltaECS/API/README.md)
- [Stamp contract](src/DeltaECS/Stamps/README.md)
- [Runtime package guide](packages/DeltaECS.README.md)
- [Generator package guide](packages/DeltaECS.Generators.README.md)

## Implementation and decisions

- [Core storage](src/DeltaECS/Core/README.md)
- [Generic API](src/DeltaECS/Generic/README.md)
- [Sequence API](src/DeltaECS/Sequence/README.md)
- [Parallel API](src/DeltaECS/Parallel/README.md)
- [Architecture decisions](adr/0001-archetype-and-chunk-storage.md)

## Performance evidence

- [Performance index](performance/README.md)
- [Benchmark guide](benchmarks/README.md)
- [C# benchmark details](benchmarks/ecs-csharp-benchmark.md)
- [GitHub benchmark workflow](github-benchmarks.md)

Historical JIT and BenchmarkDotNet documents are evidence for past decisions,
not current performance promises. Use the project [workflow](../WORKFLOW.md)
for reproducible local commands.
