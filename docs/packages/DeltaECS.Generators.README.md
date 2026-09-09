# DeltaECS.Generators

`DeltaECS.Generators` is a build-time Roslyn analyzer/source generator for
consumer callback shapes. It is packaged as an analyzer and has no runtime
dependency on the generator assembly.

```xml
<PackageReference Include="DeltaECS" Version="*" />
<PackageReference Include="DeltaECS.Generators" Version="*"
                  PrivateAssets="all" />
```

The package places its analyzer assembly under `analyzers/dotnet/cs`. It
generates consumer-side `ForEach`/`ForEachEntity` callback forms, generic
primary-component `Add`/`Remove` structural façades, and typed `World` query
factories on demand; storage and runtime execution remain in `DeltaECS`.

```csharp
Query all = world.WhereAll<Position, Velocity>();
Query any = world.WhereAny<Velocity, Acceleration>();
Query none = world.WhereNone<Lifetime>();
```

The generator project targets both `netstandard2.0` and `netstandard2.1`.
The package uses the `netstandard2.0` analyzer asset for compatibility with
Roslyn hosts while the `netstandard2.1` target is built and verified as part
of the generator project.

For the optional interceptor path, configure the consumer project as described
in the [generator documentation](https://github.com/Artromskiy/DeltaECS/blob/main/docs/src/DeltaECS.Generators/README.md).
