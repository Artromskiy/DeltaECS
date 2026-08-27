# Roslyn delegate interception experiment

Status: compiling opt-in prototype with focused runtime and generator
coverage.

The user-facing call remains unchanged:

```csharp
world.ForEach(in query,
    static (ref Position position, in Velocity velocity) =>
        position.Value += velocity.Value);
```

For a synchronous static non-capturing lambda on `World.ForEach` or
`World.ForEachEntity`, the generator records Roslyn's interceptable location,
copies the lambda body into a generated `InterceptedFunctor_*`, and emits an
interceptor with the original delegate-compatible parameter list. The
interceptor intentionally ignores that parameter and enters the existing
closed generated functor execution method. The dense entity loop therefore
calls the struct functor directly; it does not invoke the user delegate.

An unambiguous non-generic static method group is supported as well. Its
resolved method symbol is used to emit a generated functor whose `Invoke`
forwards directly to the static target, preserving the original method-group
signature and `ref`/`in` modes. Instance, ambiguous, generic, or otherwise
unsupported method groups remain on the delegate fallback.

The generated helper reuses `GeneratedForEachRuntime.OpenDense`, prepared
read/write access routes, trusted chunk advancement, query ownership and
lease validation. No storage, query, access, reflection, `DynamicMethod`,
function-pointer replacement, generic delegate wrapper, or second ECS runtime
is introduced. The only concrete generic types are at the existing generated
callback/ref boundary.

## Safe configuration

The opt-in is deliberately project-local and names only the library-owned
namespace:

```xml
<PropertyGroup>
  <InterceptorsNamespaces>Delta.ECS.Generated</InterceptorsNamespaces>
</PropertyGroup>
<ItemGroup>
  <CompilerVisibleProperty Include="InterceptorsNamespaces" />
</ItemGroup>
```

The experiment does not enable `InterceptorsPreviewNamespaces` or a global
preview switch. The generator checks the compiler-visible property and emits
interceptor sources only when the exact namespace is present.

## Fallback contract

Capturing and async lambdas, instance or ambiguous method groups, pre-created
delegates, generic method-group targets, generic containing types/methods,
entity-sequence receivers, and calls for which Roslyn supplies no
interceptable location remain ordinary delegate calls. `DECSGEN005` reports
the reason at informational severity, so fallback does not break the build.

The focused tests cover generated interception, callback execution, `ref`/
`in`/write behavior, capturing lambda fallback, static method-group
interception, instance method-group fallback, pre-created delegate fallback,
and single invocation. The consumer fixture also exercises the static
method-group runtime path, explicit component IDs, context, mixed access and
higher arity.

The required design reference is
[Roslyn interceptors](https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md).
It defines the location encoding used by the generator and the compatible
interceptor method model.

## Current SDK

The worktree's .NET SDK 10.0.301 / Roslyn compiler accepts the generated
interceptors. The generator references Microsoft.CodeAnalysis.CSharp 4.13.0
because the earlier 4.9.2 package did not expose the interceptable-location
generation API used here. The implementation remains opt-in and does not
claim support for SDKs whose compiler does not expose that API.

Benchmark and JIT results for the baseline delegate path versus the
intercepted path are recorded in the experiment ledger after the focused
Movement4 run.

For the Movement4 JIT probe, `ApplyDelegate` carries an explicit
`AggressiveInlining` hint so the candidate measures the fully inlineable
static-target form. The interceptor does not add attributes to user methods;
without that hint, the generated functor still removes the delegate-object
load and `Invoke` indirection, but the JIT may retain a direct static-target
call in the entity loop.
