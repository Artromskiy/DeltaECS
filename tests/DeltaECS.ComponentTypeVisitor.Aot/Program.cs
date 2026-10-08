namespace Delta.ECS.ComponentTypeVisitor.Aot;

using Delta.ECS;

internal static class Program
{
    private static void Main()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId componentId = layouts.Register<Position>(new SchemaId(1));
        ComponentId componentIdClass = layouts.Register<ClassPosition>(new SchemaId(2));
        StructConstraint structConstraint = default;
        ClassConstraint classConstraint = default;
        layouts.BindInterface<Position, IGameComponent>(in structConstraint);
        layouts.BindInterface<ClassPosition, IGameComponent>(in classConstraint);
        var routedVisitor = new RoutedVisitor();
        if (!layouts.TryVisit(componentId, routedVisitor)
            || !layouts.TryVisit(componentIdClass, routedVisitor)
            || routedVisitor.VisitCount != 2)
        {
            throw new InvalidOperationException("The named component type visitor proof failed.");
        }

        Console.WriteLine("NativeAOT component type visitor proof passed.");
    }
}

internal interface IGameComponent
{
}

internal record struct Position(int Value) : IGameComponent;

internal sealed record ClassPosition(int Value) : IGameComponent;

internal sealed class RoutedVisitor : GeneralComponentTypeVisitor<IGameComponent>
{
    internal int VisitCount { get; private set; }

    protected override void VisitStruct<T>(ComponentId componentId)
        => VisitCount++;

    protected override void VisitClass<T>(ComponentId componentId)
        => VisitCount++;
}
