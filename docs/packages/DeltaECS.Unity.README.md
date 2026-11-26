# DeltaECS Unity

Scene authoring and an entity inspector for Unity 6.7 or newer.

Install [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) in your
Unity project, then install `DeltaECS.Unity` from its NuGet window. It pulls in
the matching versions of `DeltaECS` and `DeltaECS.Generators`. The extension
includes the Unity runtime and editor sources, inspector resources and `.meta`
files.

For projects with a minimal Unity manifest, enable the built-in JSON Serialize
and UI Toolkit modules (`com.unity.modules.jsonserialize` and
`com.unity.modules.uielements`).

The same integration is available as `com.artromskiy.deltaecs.unity` through
Unity Package Manager. When switching, uninstall the extension, runtime and
generator from your previous package manager before installing the new copies.

[Unity installation guide](https://github.com/Artromskiy/DeltaECS/tree/main/src/DeltaECS.Unity#install-with-unity-package-manager)
