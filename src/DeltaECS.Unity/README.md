# DeltaECS Unity

Unity scene authoring and editor integration for DeltaECS. This package
requires Unity 6.7 (`6000.7`) or newer.

## Install with Unity Package Manager

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
    "com.artromskiy.deltaecs.unity": "0.0.78"
  }
}
```

Keep the project's existing `dependencies` and `scopedRegistries` entries when
adding these values. The extension automatically installs `com.artromskiy.deltaecs`,
which supplies the runtime and the source generator. For ECS without scene
authoring or the entity inspector, install `com.artromskiy.deltaecs` alone.

## Install with NuGetForUnity

In a Unity project with [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity)
installed, install `DeltaECS.Unity` from the NuGet window. It pulls in the same
version of `DeltaECS` and `DeltaECS.Generators`. The Runtime and Editor sources,
assets and Unity GUIDs are shared with the UPM package.

For projects with a minimal Unity manifest, enable the built-in JSON Serialize
and UI Toolkit modules (`com.unity.modules.jsonserialize` and
`com.unity.modules.uielements`). UPM installs both automatically.

Use one installation method per Unity project. Before switching, remove the
extension, runtime and generator from the previous package manager to avoid
duplicate assemblies and GUIDs.

## Register components

Mark component structs with `ComponentSchemaAttribute`:

```csharp
using Delta.ECS;

[ComponentSchema]
public struct Health
{
    public float Value;
}
```

Generated module initializers expose one registration per marked component.
Register the full catalog into a layout registry, then create the world:

```csharp
var layouts = new ComponentRegistry();
foreach (IGeneratedComponentRegistration registration in GeneratedComponentCatalog.GetRegistrations())
{
    layouts.Register(registration);
}

using var world = new World(layouts);
```

To register only one generated component, pass its registration directly:

```csharp
var layouts = new ComponentRegistry();
layouts.Register(GeneratedComponentCatalog.GetRegistration<Health>());
```

The generator reports the stable schema ID for each component. Set
`SchemaId` explicitly when a component's serialized identity must remain
unchanged after a type rename:

```csharp
[ComponentSchema(0x1234UL)]
public struct Position
{
    public float X;
    public float Y;
}
```

## Initialize views when they are bound

Implement `IEntityView` on a `MonoBehaviour` when a view needs to receive its
entity after binding. `OnBind` receives an `EntityContext` containing the
owning world and entity:

```csharp
using Delta.ECS.Unity;
using UnityEngine;

public sealed class UnitView : MonoBehaviour, IEntityView
{
    private EntityContext _context;

    public void OnBind(EntityContext context) => _context = context;
}
```

The binder calls `OnBind` for implementations on the view and its child
GameObjects, including inactive children. It runs after the view is associated
with the entity and after the ECS query has finished, so the callback can use
the regular `World` API. A stored context can later be checked with
`context.IsAlive` before using its entity.

See the [DeltaECS documentation](https://github.com/Artromskiy/DeltaECS/wiki)
for the runtime and generator APIs.
