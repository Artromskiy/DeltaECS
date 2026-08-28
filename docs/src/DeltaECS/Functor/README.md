# Functor API

The functor API is the struct-based counterpart to delegate `ForEach`. It uses
constrained value-type dispatch and carries mutable functor state by reference.

The stable marker contracts are:

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

The interfaces are markers only: they do not declare `Invoke` and never encode
component types or access patterns in their names. Concrete extension methods
are generated in the consumer assembly from the functor's `Invoke` signature.
`in T` means read and `ref T` means write. The generator diagnoses missing,
ambiguous, or incompatible `Invoke` implementations rather than selecting one
through reflection at runtime.

```csharp
struct Movement : IForEach
{
    public void Invoke(ref Position position, in Velocity velocity)
        => position.X += velocity.X;
}

var movement = new Movement();
world.ForEach(in query, ref movement);
```

`GeneratedForEachFunctorRuntime.cs` is compiler-support plumbing. Consumers
should call `World.ForEach`/`ForEachEntity`, not its runtime bridge directly.
