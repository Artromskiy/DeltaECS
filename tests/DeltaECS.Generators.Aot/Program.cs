namespace Delta.ECS.Generators.Aot;

using Delta.ECS;

internal static class Program
{
    private static void Main()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(1));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(2));
        using var world = new World(layouts);

        Entity entity = world.Create(stackalloc ComponentId[] { positionId, velocityId });
        world.GetRef<Position>(entity, positionId) = new Position { Value = 10 };
        world.GetRef<Velocity>(entity, velocityId) = new Velocity { Value = 3 };
        Query query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { positionId, velocityId }));

        world.ForEach(
            in query,
            static (ref Position position, in Velocity velocity) => position.Value += velocity.Value);

        if (world.Get<Position>(entity, positionId).Value != 13)
        {
            throw new InvalidOperationException("The generated AOT ForEach proof failed.");
        }

        Console.WriteLine("NativeAOT consumer proof passed.");
    }
}

public struct Position
{
    public int Value;
}

public struct Velocity
{
    public int Value;
}
