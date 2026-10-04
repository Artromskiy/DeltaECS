namespace Delta.ECS.Generators.Aot;

using Delta.ECS;

internal static class Program
{
    private static void Main()
    {
        var layouts = new ComponentLayoutRegistry();
        foreach (IGeneratedComponentRegistration registration in GeneratedComponentCatalog.GetRegistrations())
        {
            layouts.Register(registration);
        }

        var singleLayout = new ComponentLayoutRegistry();
        ComponentId singlePositionId = singleLayout.Register(GeneratedComponentCatalog.GetRegistration<Position>());
        if (singleLayout.GetComponentType(singlePositionId) != typeof(Position))
        {
            throw new InvalidOperationException("The generated single-component registration proof failed.");
        }

        ComponentId positionId = layouts.GetPrimary<Position>();
        ComponentId velocityId = layouts.GetPrimary<Velocity>();
        using var world = new World(layouts);

        Entity entity = world.Create(stackalloc ComponentId[] { positionId, velocityId });
        world.GetRef<Position>(entity, positionId) = new Position { Value = 10 };
        world.GetRef<Velocity>(entity, velocityId) = new Velocity { Value = 3 };
        int initialPosition = world.Get<Position>(entity, positionId).Value;
        int initialVelocity = world.Get<Velocity>(entity, velocityId).Value;
        if (initialPosition != 10 || initialVelocity != 3)
        {
            throw new InvalidOperationException($"Generated component registration failed: ids=({positionId.Value}, {velocityId.Value}), values=({initialPosition}, {initialVelocity}).");
        }

        Query query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { positionId, velocityId }));

        int calls = 0;
        world.ForEach(
            in query,
            ref calls,
            static (ref int count, ref Position position, in Velocity velocity) =>
            {
                position.Value += velocity.Value;
                count++;
            });

        int actualPosition = world.Get<Position>(entity, positionId).Value;
        if (actualPosition != 13 || calls != 1)
        {
            throw new InvalidOperationException($"The generated AOT ForEach proof failed: expected Position 13, got {actualPosition}; calls={calls}; entities={world.AliveEntityCount}.");
        }

        Console.WriteLine("NativeAOT consumer proof passed.");
    }
}

[DeltaEcsComponent(SchemaId = 1UL)]
public struct Position
{
    public int Value;
}

[DeltaEcsComponent(SchemaId = 2UL)]
public struct Velocity
{
    public int Value;
}
