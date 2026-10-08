namespace Delta.ECS.Runtime.Consumer;

/// <summary>Public runtime API grammar proof that compiles and runs without a generator reference.</summary>
public static partial class RuntimeApiGrammarProof
{
    /// <summary>Runs the public API call shapes that do not require generated consumer code.</summary>
    public static void Run()
    {
        VerifyRegistrationAndVisitors();
        VerifyExplicitRegistrationRoutes();
        VerifyInterfaceBindingRoutes();
        VerifyQueries();
        VerifyStructuralOperations();
        VerifyTypedAccess();
        VerifyEntityIteration();
        VerifyIntegration();
    }

    private static void VerifyRegistrationAndVisitors()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.BindInterface<Position, IMovable>();

        ComponentId firstPositionId = layouts.Register<Position>(new SchemaId(930_001));
        ComponentId secondPositionId = layouts.Register<Position>(new SchemaId(930_002));
        ComponentId managedStructId = layouts.Register<ManagedStruct>(new SchemaId(930_003));
        ComponentId classId = layouts.Register<ClassComponent>(new SchemaId(930_004));
        ComponentId constructibleClassId = layouts.Register<ConstructibleClass>(new SchemaId(930_005));
        ComponentId unmanagedId = layouts.Register<int>(new SchemaId(930_006));

        Require(layouts.GetComponentType(firstPositionId) == typeof(Position));
        Require(layouts.TryGetPrimary<Position>(out ComponentId primaryPositionId));
        Require(primaryPositionId == firstPositionId);
        Require(layouts.GetPrimary<Position>() == firstPositionId);
#pragma warning disable CA2263 // Exercise the explicit CLR-Type lookup overloads.
        Require(layouts.TryGetPrimary(typeof(Position), out ComponentId runtimePrimaryPositionId));
        Require(runtimePrimaryPositionId == firstPositionId);
        Require(layouts.GetPrimary(typeof(Position)) == firstPositionId);
        Require(!layouts.TryGetPrimary(typeof(UnknownComponent), out _));
#pragma warning restore CA2263

        var interfaceVisitor = new MovableVisitor();
        Require(layouts.TryVisit(firstPositionId, interfaceVisitor));
        Require(layouts.TryVisit(secondPositionId, interfaceVisitor));
        Require(interfaceVisitor.VisitCount == 2);

        var structVisitor = new StructVisitor();
        Require(layouts.TryVisit(managedStructId, structVisitor));
        Require(structVisitor.ComponentType == typeof(ManagedStruct));

        var classVisitor = new ClassVisitor();
        Require(layouts.TryVisit(classId, classVisitor));
        Require(classVisitor.ComponentType == typeof(ClassComponent));

        var classNewVisitor = new ClassNewVisitor();
        Require(layouts.TryVisit(constructibleClassId, classNewVisitor));
        Require(classNewVisitor.ComponentType == typeof(ConstructibleClass));

        var unmanagedVisitor = new UnmanagedVisitor();
        Require(layouts.TryVisit(unmanagedId, unmanagedVisitor));
        Require(unmanagedVisitor.ComponentType == typeof(int));

        ComponentId[] componentIds =
        [
            firstPositionId,
            managedStructId,
            secondPositionId,
            unmanagedId,
        ];
        var loopVisitor = new MovableVisitor();
        int compatibleCount = 0;
        foreach (ComponentId componentId in componentIds)
        {
            if (layouts.TryVisit(componentId, loopVisitor))
            {
                compatibleCount++;
            }
        }

        Require(compatibleCount == 2);
        Require(loopVisitor.VisitCount == 2);

        var incompatibleVisitor = new OtherInterfaceVisitor();
        Require(!layouts.TryVisit(firstPositionId, incompatibleVisitor));
        Require(!layouts.TryVisit(ComponentId.Invalid, interfaceVisitor));
        Require(!layouts.TryVisit(new ComponentId(int.MaxValue), interfaceVisitor));
        Require(!layouts.TryVisit(firstPositionId, null));

        layouts.Visit(firstPositionId, interfaceVisitor);
        layouts.Visit(ComponentId.Invalid, interfaceVisitor);
        layouts.Visit(firstPositionId, incompatibleVisitor);
        layouts.Visit(firstPositionId, null);
        Require(interfaceVisitor.VisitCount == 3);
    }

    private static void VerifyExplicitRegistrationRoutes()
    {
        var layouts = new ComponentLayoutRegistry();
        var structConstraint = new StructConstraint();
        var classConstraint = new ClassConstraint();
        var unmanagedConstraint = new UnmanagedConstraint();
        var newConstraint = new NewConstraint();
        var classNewConstraint = new ClassNewConstraint();

        ComponentId structId = layouts.Register<ManagedStruct>(new SchemaId(930_011), in structConstraint);
        ComponentId classId = layouts.Register<ClassComponent>(new SchemaId(930_012), in classConstraint);
        ComponentId unmanagedId = layouts.Register<int>(new SchemaId(930_013), in unmanagedConstraint);
        ComponentId newId = layouts.Register<ConstructibleClass>(new SchemaId(930_014), in newConstraint);
        ComponentId classNewId = layouts.Register<ConstructibleClass>(new SchemaId(930_015), in classNewConstraint);
        ComponentId unconstrainedId = RegisterWithoutKnownConstraint<UnknownComponent>(layouts, new SchemaId(930_016));

        var structVisitor = new StructVisitor();
        var classVisitor = new ClassVisitor();
        var unmanagedVisitor = new UnmanagedVisitor();
        var newVisitor = new NewVisitor();
        var classNewVisitor = new ClassNewVisitor();
        var unconstrainedVisitor = new UnconstrainedVisitor();
        Require(layouts.TryVisit(structId, structVisitor));
        Require(layouts.TryVisit(classId, classVisitor));
        Require(layouts.TryVisit(unmanagedId, unmanagedVisitor));
        Require(layouts.TryVisit(newId, newVisitor));
        Require(layouts.TryVisit(classNewId, classNewVisitor));
        Require(layouts.TryVisit(unconstrainedId, unconstrainedVisitor));
        Require(structVisitor.ComponentType == typeof(ManagedStruct));
        Require(classVisitor.ComponentType == typeof(ClassComponent));
        Require(unmanagedVisitor.ComponentType == typeof(int));
        Require(newVisitor.ComponentType == typeof(ConstructibleClass));
        Require(classNewVisitor.ComponentType == typeof(ConstructibleClass));
        Require(unconstrainedVisitor.ComponentType == typeof(UnknownComponent));
    }

    private static ComponentId RegisterWithoutKnownConstraint<T>(ComponentLayoutRegistry layouts, SchemaId schemaId)
        => layouts.Register<T>(schemaId);

    private static void VerifyInterfaceBindingRoutes()
    {
        var structLayouts = new ComponentLayoutRegistry();
        var structConstraint = new StructConstraint();
        structLayouts.BindInterface<ManagedStruct, IMovable>(in structConstraint);
        ComponentId structId = structLayouts.Register<ManagedStruct>(new SchemaId(930_021), in structConstraint);
        Require(structLayouts.TryVisit(structId, new MovableVisitor()));

        var classLayouts = new ComponentLayoutRegistry();
        var classConstraint = new ClassConstraint();
        classLayouts.BindInterface<ClassComponent, IMovable>(in classConstraint);
        ComponentId classId = classLayouts.Register<ClassComponent>(new SchemaId(930_022), in classConstraint);
        Require(classLayouts.TryVisit(classId, new MovableVisitor()));

        var unmanagedLayouts = new ComponentLayoutRegistry();
        var unmanagedConstraint = new UnmanagedConstraint();
        unmanagedLayouts.BindInterface<Position, IMovable>(in unmanagedConstraint);
        ComponentId unmanagedId = unmanagedLayouts.Register<Position>(new SchemaId(930_023), in unmanagedConstraint);
        Require(unmanagedLayouts.TryVisit(unmanagedId, new MovableVisitor()));

        var newLayouts = new ComponentLayoutRegistry();
        var newConstraint = new NewConstraint();
        newLayouts.BindInterface<NewComponent, IMovable>(in newConstraint);
        ComponentId newId = newLayouts.Register<NewComponent>(new SchemaId(930_024), in newConstraint);
        var newVisitor = new NewMovableVisitor();
        Require(newLayouts.TryVisit(newId, newVisitor));
        Require(newVisitor.ComponentType == typeof(NewComponent));

        var classNewLayouts = new ComponentLayoutRegistry();
        var classNewConstraint = new ClassNewConstraint();
        classNewLayouts.BindInterface<ConstructibleClass, IMovable>(in classNewConstraint);
        ComponentId classNewId = classNewLayouts.Register<ConstructibleClass>(new SchemaId(930_025), in classNewConstraint);
        var classNewVisitor = new ClassNewMovableVisitor();
        Require(classNewLayouts.TryVisit(classNewId, classNewVisitor));
        Require(classNewVisitor.ComponentType == typeof(ConstructibleClass));

        var unconstrainedLayouts = new ComponentLayoutRegistry();
        BindInterfaceWithoutKnownConstraint<Position, IMovable>(unconstrainedLayouts);
        ComponentId unconstrainedId = RegisterWithoutKnownConstraint<Position>(unconstrainedLayouts, new SchemaId(930_026));
        Require(unconstrainedLayouts.TryVisit(unconstrainedId, new MovableVisitor()));
    }

    private static void BindInterfaceWithoutKnownConstraint<TComponent, TInterface>(ComponentLayoutRegistry layouts)
        where TComponent : TInterface
        => layouts.BindInterface<TComponent, TInterface>();

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Runtime API grammar proof failed.");
        }
    }

    private interface IMovable { }

    private interface IOther { }

    private struct Position : IMovable
    {
        public int Value;
    }

    private struct Velocity
    {
        public int Value;
    }

    private struct Marker { }

    private struct UnknownComponent { }

    private struct CountContext
    {
        public int Count;
    }

    private sealed class SharedCounter
    {
        public int Count;
    }

    private struct IterationContext
    {
        public SharedCounter Counter;
    }

    private struct ManagedStruct : IMovable
    {
        public string? Name { get; set; }
    }

    private sealed class ClassComponent : IMovable
    {
        private ClassComponent() { }
    }

    private sealed class ConstructibleClass : IMovable
    {
        public ConstructibleClass() { }
    }

    private sealed class NewComponent : IMovable
    {
        public NewComponent() { }
    }

    private sealed class MovableVisitor : IComponentVisitor<IMovable>
    {
        public int VisitCount { get; private set; }

        public RuntimeTypeHandle ConstraintType => typeof(IMovable).TypeHandle;

        public void Visit<TComponent>(ComponentId componentId) where TComponent : IMovable
        {
            _ = componentId;
            VisitCount++;
        }
    }

    private sealed class OtherInterfaceVisitor : IComponentVisitor<IOther>
    {
        public RuntimeTypeHandle ConstraintType => typeof(IOther).TypeHandle;

        public void Visit<TComponent>(ComponentId componentId) where TComponent : IOther
            => _ = componentId;
    }

    private sealed class StructVisitor : IStructVisitor
    {
        public Type? ComponentType { get; private set; }

        public void Visit<TComponent>(ComponentId componentId) where TComponent : struct
        {
            _ = componentId;
            ComponentType = typeof(TComponent);
        }
    }

    private sealed class ClassVisitor : IClassVisitor
    {
        public Type? ComponentType { get; private set; }

        public void Visit<TComponent>(ComponentId componentId) where TComponent : class
        {
            _ = componentId;
            ComponentType = typeof(TComponent);
        }
    }

    private sealed class ClassNewVisitor : IClassNewVisitor
    {
        public Type? ComponentType { get; private set; }

        public void Visit<TComponent>(ComponentId componentId) where TComponent : class, new()
        {
            _ = componentId;
            ComponentType = typeof(TComponent);
        }
    }

    private sealed class UnmanagedVisitor : IUnmanagedVisitor
    {
        public Type? ComponentType { get; private set; }

        public void Visit<TComponent>(ComponentId componentId) where TComponent : unmanaged
        {
            _ = componentId;
            ComponentType = typeof(TComponent);
        }
    }

    private sealed class NewVisitor : INewVisitor
    {
        public Type? ComponentType { get; private set; }

        public void Visit<TComponent>(ComponentId componentId) where TComponent : new()
        {
            _ = componentId;
            ComponentType = typeof(TComponent);
        }
    }

    private sealed class UnconstrainedVisitor : IUnconstrainedVisitor
    {
        public Type? ComponentType { get; private set; }

        public void Visit<TComponent>(ComponentId componentId)
        {
            _ = componentId;
            ComponentType = typeof(TComponent);
        }
    }

    private sealed class NewMovableVisitor : INewVisitor<IMovable>
    {
        public RuntimeTypeHandle ConstraintType => typeof(IMovable).TypeHandle;

        public Type? ComponentType { get; private set; }

        public void Visit<TComponent>(ComponentId componentId) where TComponent : IMovable, new()
        {
            _ = componentId;
            ComponentType = typeof(TComponent);
        }
    }

    private sealed class ClassNewMovableVisitor : IClassNewVisitor<IMovable>
    {
        public RuntimeTypeHandle ConstraintType => typeof(IMovable).TypeHandle;

        public Type? ComponentType { get; private set; }

        public void Visit<TComponent>(ComponentId componentId) where TComponent : class, IMovable, new()
        {
            _ = componentId;
            ComponentType = typeof(TComponent);
        }
    }
}
