# DeltaECS Unity

Unity scene authoring and editor integration for DeltaECS. This package
requires Unity 6.7 (`6000.7`) or newer.

## Install

Add the npm-compatible registry and package to the Unity project's
`Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "Artromskiy",
      "url": "https://registry.npmjs.org",
      "scopes": ["com.artromskiy"]
    }
  ],
  "dependencies": {
    "com.artromskiy.deltaecs.unity": "0.0.38"
  }
}
```

Keep the project's existing `dependencies` and `scopedRegistries` entries when
adding these values. The core `DeltaECS` runtime and `DeltaECS.Generators` are
separate NuGet packages and are not included in this UPM package.

## Register components

Install `DeltaECS.Generators` in the assembly that declares components, then
mark component structs with `DeltaEcsComponentAttribute`:

```csharp
using Delta.ECS;

[DeltaEcsComponent]
public struct Health
{
    public float Value;
}
```

Generated module initializers register typed component factories. Create a
world with the generated layout catalog:

```csharp
using var world = new World(GeneratedComponentCatalog.CreateLayoutRegistry());
```

The generator reports the stable schema ID for each component. Set
`SchemaId` explicitly when a component's serialized identity must remain
unchanged after a type rename:

```csharp
[DeltaEcsComponent(SchemaId = 0x1234UL)]
public struct Position
{
    public float X;
    public float Y;
}
```

See the [DeltaECS documentation](https://github.com/Artromskiy/DeltaECS/wiki)
for the runtime and generator APIs.
