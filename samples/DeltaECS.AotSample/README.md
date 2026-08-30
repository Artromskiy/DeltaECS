# DeltaECS NativeAOT sample

This sample exercises the typed registration and single-item API, a generated
static delegate callback, a generated struct-functor callback, an ordered
`Sequence` callback, query construction, and structural entity destruction.
The sample also enables the optional Roslyn interceptor path. The static
non-capturing `World.ForEach` callback is lowered to the generated trusted
struct-functor path while retaining the same source spelling. The analyzer
remains a build-time project reference; it is not part of the published
application.

For maximum iteration performance, consumer projects should opt in with the
same two settings when their SDK supports Roslyn interceptors:

```xml
<PropertyGroup>
  <InterceptorsNamespaces>Delta.ECS.Generated</InterceptorsNamespaces>
</PropertyGroup>
<ItemGroup>
  <CompilerVisibleProperty Include="InterceptorsNamespaces" />
</ItemGroup>
```

Only supported static non-capturing callbacks are intercepted. Capturing,
instance, pre-created, ambiguous, async, and sequence callbacks retain the
ordinary delegate path.

From the repository root, publish for the local Apple Silicon RID:

```bash
dotnet publish samples/DeltaECS.AotSample/DeltaECS.AotSample.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishAot=true \
  -p:StripSymbols=true \
  -p:PublishDir=artifacts/aot-sample/osx-arm64/
```

Run the resulting native executable:

```bash
artifacts/aot-sample/osx-arm64/DeltaECS.AotSample
```

Expected output is deterministic:

```text
AOT sample: entities=4, checksum=32.0, marker=True
```
