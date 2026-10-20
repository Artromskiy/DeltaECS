# ADR-0005: Delta.ECS root namespace

## Context

The projects owned by Delta use the `Delta` namespace root. The ECS production
API is published under `Delta.ECS`. Tests, benchmarks and the analyzer use
their own `Delta.ECS.*` namespaces.

## Decision

- Production public types use `Delta.ECS`.
- Test-only types use `Delta.ECS.Tests`.
- Benchmark types use `Delta.ECS.Benchmarks`.
- Generator types use `Delta.ECS.Generators`.
- Each owned csproj declares the matching `RootNamespace` where applicable.
- Project and assembly names remain `DeltaECS`, `DeltaECSTests`, and
  `DeltaECS.Benchmarks`; Arch and Friflo source/projects are not renamed.

## Compatibility

Binary/project references remain compatible because assembly identities are
unchanged. Source consumers should use `using Delta.ECS;`. There is no
old-namespace forwarding layer; project and assembly names remain
`DeltaECS`, `DeltaECSTests` and `DeltaECS.Benchmarks`.
