# Functor API

The functor API is the struct-based counterpart to delegate `ForEach`. It uses
constrained value-type dispatch and carries mutable functor state by reference.

Zero-component contracts are handwritten:

- `IForEach`
- `IForEachEntity`
- `IForEachContext<TContext>`
- `IForEachContextEntity<TContext>`

```csharp
struct Counter : IForEachEntity
{
    public int Value;
    public void Invoke(Entity entity) => Value += entity.Index;
}

var counter = new Counter();
world.ForEachEntity(in query, ref counter);
```

Component-bearing functor interfaces and extension methods are generated in
the consumer assembly from the requested `Invoke` signature. `in T` means read
and `ref T` means write. The marker hierarchy identifies functor intent; the
generator diagnoses ambiguous or incompatible `Invoke` implementations rather
than selecting one through reflection at runtime.

`GeneratedForEachFunctorRuntime.cs` is compiler-support plumbing. Consumers
should call `World.ForEach`/`ForEachEntity`, not its runtime bridge directly.
