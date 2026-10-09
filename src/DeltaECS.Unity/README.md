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
    "com.artromskiy.deltaecs.unity": "0.0.45"
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

Generated module initializers expose one registration per marked component.
Register the full catalog into a layout registry, then create the world:

```csharp
var layouts = new ComponentLayoutRegistry();
foreach (IGeneratedComponentRegistration registration in GeneratedComponentCatalog.GetRegistrations())
{
    layouts.Register(registration);
}

using var world = new World(layouts);
```

To register only one generated component, pass its registration directly:

```csharp
var layouts = new ComponentLayoutRegistry();
layouts.Register(GeneratedComponentCatalog.GetRegistration<Health>());
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
