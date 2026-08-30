# Delegate API

This folder contains callback contracts. It does not own query matching or
component storage.

## `ForEach` delegates

Zero-component forms are handwritten:

```csharp
world.ForEach(in query, static () => Tick());
world.ForEachEntity(in query, static entity => Observe(entity));
```

Component-bearing overloads are emitted into the consumer assembly on demand:

```csharp
world.ForEach<Position, Velocity>(
    in query,
    static (ref Position position, in Velocity velocity) =>
    {
        position.X += velocity.X;
    });
```

`in T` declares read access and `ref T` declares write access. Generated forms
also support an `Entity` argument, caller context, explicit component IDs, and
arities up to the component-mask capacity. See the generator README for the
generation boundary.

With the project-local Roslyn interceptor opt-in enabled, supported static
lambdas and static method groups keep this delegate-shaped source API but are
lowered to the generated struct-functor execution path. Capturing callbacks,
pre-created delegates and unsupported call sites continue through the normal
delegate path. See the
[interceptor configuration](../../DeltaECS.Generators/README.md#optional-roslyn-interceptor-path).

For the maximum performance of delegate-shaped iteration, enable that opt-in
in the consumer project. This is a compile-time lowering contract: the public
delegate API and callback source spelling stay unchanged, while eligible
static non-capturing `World.ForEach` calls enter the closed trusted execution
method. The interceptor is not applied to sequence callbacks or callbacks
whose shape cannot be proven safe by the generator.
