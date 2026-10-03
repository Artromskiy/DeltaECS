# DeltaECS.Unity

This folder contains the Unity runtime authoring bridge and editor tools for
DeltaECS. It is not packaged for UPM. Copy this folder under the Unity
project's `Assets` directory (or link it there) to make Unity import its
runtime and editor assemblies.

## Add the integration and generator

Add the `DeltaECS` runtime assembly to the Unity project using your existing
dependency workflow, then copy this `DeltaECS.Unity` folder under `Assets`.

Unity does not load Roslyn analyzers and source generators directly from
NuGet. Download the `DeltaECS.Generators` `.nupkg`, extract
`analyzers/dotnet/cs/DeltaECS.Generators.dll`, and put the DLL in the `Assets`
folder for the assembly that declares your components. In Unity's Plugin
Inspector, disable **Any Platform**, disable **Editor** and **Standalone** in
the included platforms, and add the case-sensitive `RoslynAnalyzer` asset
label. See Unity's [Roslyn analyzer installation guide](https://docs.unity3d.com/6000.0/Documentation/Manual/install-existing-analyzer.html)
for editor steps and analyzer scope rules.

## Register components

The generator derives schema IDs and emits the component registration catalog
for marked types. Mark component structs with `DeltaEcsComponentAttribute`:

```csharp
using Delta.ECS;

[DeltaEcsComponent]
public struct Health
{
    public float Value;
}
```

The generator derives a stable schema ID from the component's metadata name. To pin an ID so a type rename does not change its serialized identity, provide a non-zero value:

```csharp
[DeltaEcsComponent(SchemaId = 0x1234UL)]
public struct Position
{
    public float X;
    public float Y;
}
```

Use `GeneratedComponentCatalog` to create the layout registry for each world.
Generated module initializers add typed registration factories to the catalog;
world setup does not scan assemblies or use reflection:

```csharp
using var world = new World(GeneratedComponentCatalog.CreateLayoutRegistry());
```

The schema-ID analyzer reports the generated ID so it can be pinned in source.
The generator reports zero IDs and collisions within one compilation; the
generated catalog checks collisions across loaded component assemblies when
the layout registry is created.
