# DeltaECS for Unity

ECS runtime and C# source generator for Unity 6.7 or newer. Add the scoped
registry and runtime package to the project's `Packages/manifest.json`:

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
    "com.artromskiy.deltaecs": "0.0.77"
  }
}
```

Install `com.artromskiy.deltaecs.unity` to add scene authoring and the entity
inspector. That package installs this runtime automatically. When switching
from NuGetForUnity, remove its DeltaECS runtime and generator packages before
importing the UPM copies.

- [Choose an API](Documentation~/API-SELECTION.md)
- [API examples](Documentation~/API-EXAMPLES.md)
- [API grammar](Documentation~/API-GRAMMAR.md)
