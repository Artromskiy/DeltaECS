# Generator architecture

The generator is organized as a small pipeline with a shared model boundary:

```text
syntax provider -> InvocationCandidate
                -> ApiDescriptor + InvocationCursor + CallbackReader
                -> family model backed by ApiModel
                -> declaration SignatureKey -> ShapeRegistry
                -> SignatureProjection
                -> family render fragments -> GeneratedFileModel
                -> raw-string templates -> generated C#
```

`ApiDescriptor` owns the names and fixed grammar facts shared by all generated
APIs. `InvocationCursor` reads the ordered target, query, registration,
context, callback and option slots. `CallbackReader` normalizes lambda,
method-group and functor signatures. Add a public generated method name to the
descriptor table first so discovery and parsing stay in sync.

`GeneratorSupport` owns Roslyn and symbol operations: receiver and component
type checks, access/ref interpretation, type display, interception locations
and stable generated names. It does not own emitted generic, parameter or
argument spelling; those are template concerns.

`GeneratorModel` contains the neutral API representation. A shape
describes target, query mode, selector kind, context, callback source,
execution kind and component slots. Renderers consume this model and must not
re-derive its signature key. The key is constructed once, so independently
discovered call sites with the same emitted signature share one declaration.
Concrete callback targets, closed call-site types, source locations and local
names belong to a separate call-site binding. They may specialize an
interceptor, but they must never split or contaminate the public declaration
identity.

Each `ApiModel` materializes one `SignatureProjection`. The projection owns the
ordered arity-dependent C# slots: generic type names, positional
`ComponentId` selectors, component/value/access parameters and arguments, and
context modifiers. Declaration modifiers and invocation modifiers remain
separate, so a renderer cannot accidentally emit a `ref readonly` declaration
as a call-site modifier. Family renderers still decide the semantic order of
target, query, callback and operation-specific options.

`GeneratorRenderModel` is the next boundary. `GeneratedFileModel` contains
only a namespace, imports and already-renderable member strings. `FileTemplate`,
`ExtensionTemplate`, `DocumentationTemplate` and `InterceptorTemplate` remain
named templates in separate files; the former one-line member wrappers were
removed because they only stored a string alongside the `ApiModel` that had
already produced it. Semantic readers never pass syntax nodes or
`SemanticModel` into these templates. Family renderers assemble optional
initializer and access blocks before the raw-string template boundary.

`GeneratorTemplates` owns syntax-neutral composition helpers: deterministic
joins, documentation, declarations, code blocks, write-index selection and
row/element references. Query, structural, iteration and `Where` renderers use
`SignatureProjection` for repeated arity fragments while retaining their
specialized dense, entity-list, parallel, stamp and fused structural kernels.

The four generators use the same semantic and arity vocabulary. Their readers
produce an `ApiModel`-backed model; pure renderers project it into deterministic
strings. Ordinary generated files pass those strings through
`GeneratedFileModel` and `FileTemplate`. Interceptor files use the dedicated
interceptor template because their source location and imports are call-site
data. Templates never receive syntax nodes, a `SemanticModel` or a
`Compilation`.

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

## Runtime-selected generic functor adapters

`GenericFunctorGenerator` discovers accessible generic `IForEach` and
`IForEachEntity` struct contracts, including their context variants, with one
supported `Invoke`. Its semantic model contains type names, generic
constraints, context type and mode, component modes and registration selectors.
`GenericFunctorTemplates` emits a closed typed adapter, reusing
`DemandDrivenForEachTemplates.RenderGenericFunctorKernel` for the ordinary
iteration kernel. No additional iteration implementation is maintained.

`GeneratedGenericBindingsGenerator` finds generic component registrations and
open generic functor calls in the consumer compilation. It emits one module
initializer that registers direct typed factories in
`GeneratedGenericBindingRegistry`. A component factory calls
`ComponentLayoutRegistry.Register<ClosedComponent>`; a functor factory
constructs its closed generated executor directly. The runtime matches the
generic definition and the positional CLR component types, then invokes that
factory. It does not close types with reflection or create executors through
`Activator`. Unary bindings can cover registered component types selected
through a runtime `ComponentId`; multi-parameter bindings are emitted for the
concrete ordered tuples visible to the generator, avoiding a cartesian product
of every component type.

The arity-specific `World.ForEach*` extensions cover query-wide iteration,
entity-list iteration with an optional query, entity-aware callbacks,
caller-owned context, and parallel execution. The IDs close the functor's
generic parameters and are independent of the number of rows in `Invoke`. The
adapter resolves row registrations once; subsequent calls execute the existing
typed dense, entity-list, or parallel kernel without reflection inside the
entity loop. Context is strongly typed and passed by reference through the
generated adapter; the `Invoke` signature determines whether it is mutable,
read-only, or by value. Mutable `ref` context is rejected for parallel forms. A
default functor belongs to one complete invocation and its fields are local
functor state. If a runtime-selected multi-parameter tuple was not visible to
the generator, the registry fails before iteration with a missing generated
binding error.
