# Functor API

The functor API is the struct-based counterpart to delegate `ForEach`. It uses
constrained value-type dispatch and carries mutable functor state by reference.
Component-bearing shapes are generated on demand for arities 1 through 256.
The runtime component mask is dynamic; this callback arity limit does not limit
registered component IDs.

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
The handwritten marker overloads are compiler anchors and report a clear error
if called without generated lowering; they are not a silent no-op runtime
fallback. A functor call must be lowered by the analyzer to a generated
extension, including a zero-component `Invoke()` shape. Use the handwritten
delegate overloads when a runtime delegate callback is required.
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

Query-wide filters use the same value-type callback model. Implement
`IWherePredicate` for a read-only predicate and keep writes in the terminal
`IForEach*` functor. Use `Where` when the predicate needs only components and
`WhereEntity` when it also needs the current entity:

```csharp
var predicateState = new PredicateState();
var predicate = new IsDeadPredicate();
var actionState = new ActionState();
var action = new ResetHealthAction();
world.WhereEntity(in query, ref predicateState, ref predicate)
    .ForEachEntity(ref actionState, ref action);
```

The predicate `Invoke` order is `ref context` when a context is supplied,
then `Entity` for `WhereEntity`, then read-only components. Terminal functors
follow the regular `ForEach` ordering and keep their caller-owned context by
reference. A no-entity predicate can be as small as:

```csharp
struct IsDead : IWherePredicate
{
    public bool Invoke(in Health health) => health.Value <= 0;
}

world.Where(in query, ref predicate).Destroy();
```

The compiler-support runtime bridge is implemented under
`src/DeltaECS/Generator/GeneratedRuntime.cs`. Consumers should call
`World.ForEach`/`ForEachEntity`, not the runtime bridge directly.
