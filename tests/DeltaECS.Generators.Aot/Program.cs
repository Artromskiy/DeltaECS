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

        var visitorLayouts = new ComponentLayoutRegistry();
        visitorLayouts.BindInterface<Position, IPositionComponent>();
        ComponentId visitorPositionId = visitorLayouts.Register<Position>(new SchemaId(3));
        var visitor = new AotPositionVisitor();
        visitorLayouts.Visit(visitorPositionId, visitor);
        if (visitor.ComponentId != visitorPositionId || visitor.VisitCount != 1)
        {
            throw new InvalidOperationException("The generated constraint-aware visitor proof failed.");
        }

        ComponentId positionId = layouts.GetPrimary<Position>();
        ComponentId velocityId = layouts.GetPrimary<Velocity>();
        var generatedVisitor = new AotUnmanagedVisitor();
        layouts.Visit(positionId, generatedVisitor);
        if (generatedVisitor.ComponentId != positionId || generatedVisitor.VisitCount != 1)
        {
            throw new InvalidOperationException("The generated component type visitor proof failed.");
        }

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

public interface IPositionComponent { }

[DeltaEcsComponent(SchemaId = 1UL)]
public struct Position : IPositionComponent
{
    public int Value;
}

[DeltaEcsComponent(SchemaId = 2UL)]
public struct Velocity
{
    public int Value;
}

public sealed class AotPositionVisitor : IComponentTypeVisitor<IPositionComponent>
{
    public RuntimeTypeHandle ConstraintType => typeof(IPositionComponent).TypeHandle;

    public ComponentId ComponentId { get; private set; }

    public int VisitCount { get; private set; }

    public void Visit<T>(ComponentId componentId) where T : IPositionComponent
    {
        ComponentId = componentId;
        VisitCount++;
    }
}

public sealed class AotUnmanagedVisitor : IUnmanagedComponentTypeVisitor
{
    public ComponentId ComponentId { get; private set; }

    public int VisitCount { get; private set; }

    public void Visit<T>(ComponentId componentId) where T : unmanaged
    {
        ComponentId = componentId;
        VisitCount++;
    }
}
