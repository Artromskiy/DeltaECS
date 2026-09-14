# Generator architecture

The generator is organized as a small pipeline with a shared model boundary:

```text
syntax provider -> InvocationCandidate -> semantic shape reader
                  -> ApiModel/SignatureKey -> ShapeRegistry
                  -> RenderModel -> pure templates -> generated C#
```

`GeneratorSupport` owns the vocabulary shared by all generated APIs: API-name
classification, receiver and component type checks, access/ref modes, generic
parameter spelling, type display and stable generated names. Add a new method
name to `GeneratorSupport.ApiKind` there first so discovery and generated-source exclusion stay
in sync.

`GeneratorModel` contains the neutral API representation. A shape
describes target, query mode, selector kind, context, callback source,
execution kind and component slots. Renderers consume this model and must not
re-derive its signature key. The key is constructed once, so independently
discovered call sites with the same emitted signature share one declaration.

`GeneratorRenderModel` is the second boundary. `GeneratedFileModel` contains
only a namespace, imports and already-renderable members. Each named template
(`FileTemplate`, `ExtensionTemplate`, `WhereViewTemplate`,
`ActionDelegateTemplate`, `InvokerTemplate` and `InterceptorTemplate`) lives in
its own source file and joins the shared partial `GeneratorTemplates` facade.
Semantic readers never pass syntax nodes or `SemanticModel` into these
templates. `InvokerModel` carries the already-rendered invoker fragment, so
optional initializer and access blocks are assembled before the template
boundary rather than selected inside a file-level branch. Fixed source
skeletons use raw interpolated literals; repeated lines use joins and
conditional fragments instead of append chains.

The four generators use the same path: their semantic readers produce an
`ApiModel`-backed model, render fragments become `RenderModel` values, and the
file template joins deterministic members. Interception readers materialize
lambda bodies, parameter names and method targets as strings before rendering;
the interception templates therefore do not inspect syntax nodes.

`GeneratorPipeline.ShapeProvider` is the preferred entry point when a shape can
be resolved from one invocation's semantic model. It keeps query factories and
structural façades incremental per syntax node. APIs that need compilation-wide
configuration or a second pass, such as interception and `Where` terminal
linking, use `GeneratorPipeline.Input` and the same `InvocationProvider`.

`ShapeRegistry` is the single deduplication and ordering boundary. A new
generator should register shapes there and emit through `EmitShapes` or
`EmitDistinct`; it must not keep a local unordered dictionary or scan every
syntax tree. A merge callback is available for call-site data such as static
method-group component types.

Generated output remains consumer-side code. Runtime execution and validation
stay in DeltaECS; the generator only selects and renders a closed typed path.
When a replacement generator is introduced for an existing public path, mark
the old path obsolete before migrating its callers and delete the obsolete
implementation and tests in the same migration.
