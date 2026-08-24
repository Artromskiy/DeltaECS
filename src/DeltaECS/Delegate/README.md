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
