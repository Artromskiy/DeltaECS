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

The generator targets `netstandard2.0` and is shipped from
`analyzers/dotnet/cs`. Its target is independent from the target framework of
the consumer project.

Consumers using C# 9 or C# 10, including Unity projects with a
`netstandard2.1` API profile, automatically use ordinary generated `ForEach`
overloads. Interceptor source is emitted only when the consumer language
version can parse the required interceptor declarations.

For the optional interceptor path, configure the consumer project as described
in the [generator documentation](https://github.com/Artromskiy/DeltaECS/blob/main/docs/src/DeltaECS.Generators/README.md).
