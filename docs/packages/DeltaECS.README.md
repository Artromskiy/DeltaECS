# DeltaECS

`DeltaECS` is the runtime package for the DeltaECS archetype-based entity
component system.

```xml
<PackageReference Include="DeltaECS" Version="*" />
```

The runtime package contains the `Delta.ECS` assembly and does not depend on
the build-time source generator. Install `DeltaECS.Generators` separately when
consumer-assembly generation for typed callbacks is required.

The package ships `netstandard2.1` and `net10.0` runtime assets. Use the
`netstandard2.1` asset for hosts exposing that API profile, including Unity
projects whose selected compatibility profile is .NET Standard 2.1. The
`net10.0` asset uses the runtime's ref-backed row implementation; the
`netstandard2.1` asset uses the equivalent compatibility implementation while
preserving the same public API.

Repository documentation: <https://github.com/Artromskiy/DeltaECS/tree/main/docs>.
