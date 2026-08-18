# ADR-0005: DVG.ECS root namespace

## Context

The projects owned by DVG use the `DVG` namespace root. DeltaECS previously
published its own API under `DeltaECS`, while tests and benchmarks used
parallel `DeltaECSTests` and `DeltaECS.Benchmarks` namespaces.

## Decision

- Production public types use `DVG.ECS`.
- Test-only types use `DVG.ECS.Tests`.
- Benchmark types use `DVG.ECS.Benchmarks`.
- Each owned csproj declares the matching `RootNamespace`.
- Project and assembly names remain `DeltaECS`, `DeltaECSTests`, and
  `DeltaECS.Benchmarks`; Arch and Friflo source/projects are not renamed.

## Compatibility

Binary/project references remain compatible because assembly identities are
unchanged. Source consumers must replace `using DeltaECS;` with `using DVG.ECS;`
and update fully qualified `DeltaECS.*` type names. There is intentionally no
old-namespace forwarding layer, keeping the public API unambiguous.
