# Generator architecture

The generator is organized as a small pipeline with a shared model boundary:

```text
syntax provider -> InvocationCandidate -> semantic shape reader
                  -> ApiShape/SignatureKey -> ShapeRegistry
                  -> deterministic source renderer
```

`GeneratorSupport` owns the vocabulary shared by all generated APIs: API-name
classification, receiver and component type checks, access/ref modes, generic
parameter spelling, type display and stable generated names. Add a new method
name to `GeneratorSupport.ApiKind` there first so discovery and generated-source exclusion stay
in sync.

`GeneratorModel` contains the neutral intermediate representation. A shape
describes target, query mode, selector kind, context, callback source,
execution kind and component slots. Renderers consume this model and must not
re-derive its signature key. The key is constructed once, so independently
discovered call sites with the same emitted signature share one declaration.

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
