# DeltaECS.Generators

`DeltaECS.Generators` is a build-time Roslyn analyzer/source generator for
consumer callback shapes. It is packaged as an analyzer and has no runtime
dependency on the generator assembly.

```xml
<PackageReference Include="DeltaECS" Version="0.0.10" />
<PackageReference Include="DeltaECS.Generators" Version="0.0.10"
                  PrivateAssets="all" />
```

The package places its assembly under `analyzers/dotnet/cs`. It generates
consumer-side `ForEach`/`ForEachEntity` callback forms on demand; storage and
runtime execution remain in `DeltaECS`.

For the optional interceptor path, configure the consumer project as described
in the [generator documentation](https://github.com/Artromskiy/DeltaECS/blob/main/docs/src/DeltaECS.Generators/README.md).
