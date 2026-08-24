# Sequence API

Sequence execution processes an explicit ordered `ReadOnlySpan<Entity>`. It is
not query-wide archetype traversal and it does not own or copy the input span.

## Entry points

Direct and fluent spellings share the same implementation:

```csharp
world.ForEachEntity(entities, action);
world.ForEachEntity(entities, in query, action);

world.Entities(entities).ForEachEntity(action);
world.Entities(entities).Where(in query).ForEachEntity(action);
```

The query overload treats the sequence as candidates and filters only those
entities. It does not enumerate every entity matching the query. Valid entries
retain input order and duplicate occurrences; stale, destroyed and foreign
handles are skipped.

`EntitySequence` and `FilteredEntitySequence` are borrowed `ref struct` facades.
They cannot escape the lifetime of their input span.

## Typed callbacks

Component-bearing delegate and functor overloads use the same consumer-demand
generator as world-wide `ForEach`:

```csharp
world.Entities(entities).Where(in query)
    .ForEachEntity<Position, Velocity>(
        static (Entity entity, ref Position position, in Velocity velocity) =>
        {
            position.X += velocity.X;
        });
```

Reads are `in T`; writes are `ref T`. Entity records are resolved directly and
the last archetype row plan is cached. Sequence execution does not loop through
public atomic `TryGet`/`Set` calls and does not introduce a second storage model.

Zero-component delegate and struct-functor forms are handwritten. Generated
component-bearing forms support context, entity/no-entity callback shapes,
primary registrations and explicit component IDs.

## Structural terminals

```csharp
int added = world.Entities(entities).Add(componentIds);
int removed = world.Entities(entities).Where(in query).Remove(componentIds);
int destroyed = world.Entities(entities).Destroy();
```

`Add`, `Remove`, and `Destroy` forward to the world batch kernels. Filtered
terminals collect matching candidates in reusable world-owned scratch before
calling those kernels; they do not rent a new array per call.

Sequence APIs preserve order for callbacks. Structural terminals return the
number of entities actually changed or destroyed.
