# Delegate API

This folder contains callback contracts. It does not own query matching or
component storage.

## `ForEach` delegates

The zero-component overloads document the generated forms and throw
`InvalidOperationException` when called. Add one or more component parameters
so the analyzer emits the matching overload into the consumer assembly:

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
component-bearing callback shapes. See the generator README for the available
forms.

The same rule applies to the zero-component context and entity forms. Their
signatures remain available for source compatibility, but each throws until a
component-bearing generated overload is selected.

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
method. The interceptor is not applied to callbacks
whose shape cannot be proven safe by the generator.
