# ADR-0005: DeltaECS root namespace

## Context

The projects owned by Delta use the `Delta` namespace root. DeltaECS previously
published its own API under `DeltaECS`, while tests and benchmarks used
parallel `DeltaECSTests` and `DeltaECS.Benchmarks` namespaces.

## Decision

- Production public types use `DeltaECS`.
- Test-only types use `DeltaECS.Tests`.
- Benchmark types use `DeltaECS.Benchmarks`.
- Each owned csproj declares the matching `RootNamespace`.
- Project and assembly names remain `DeltaECS`, `DeltaECSTests`, and
  `DeltaECS.Benchmarks`; Arch and Friflo source/projects are not renamed.

## Compatibility

Binary/project references remain compatible because assembly identities are
unchanged. Source consumers must replace `using DeltaECS;` with `using DeltaECS;`
and update fully qualified `DeltaECS.*` type names. There is intentionally no
old-namespace forwarding layer, keeping the public API unambiguous.
