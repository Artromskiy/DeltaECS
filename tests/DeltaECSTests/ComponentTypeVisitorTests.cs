using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed class ComponentTypeVisitorTests
{
    [Test]
    public void UnmanagedRegistrationAcceptsUnmanagedStructAndNewVisitors()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId componentId = layouts.Register<int>(new SchemaId(80_001));

        var unmanagedVisitor = new UnmanagedVisitor();
        var structVisitor = new StructVisitor();
        var newVisitor = new NewVisitor();
        layouts.Visit(componentId, unmanagedVisitor);
        layouts.Visit(componentId, structVisitor);
        layouts.Visit(componentId, newVisitor);

        AssertVisited<int>(unmanagedVisitor, componentId);
        AssertVisited<int>(structVisitor, componentId);
        AssertVisited<int>(newVisitor, componentId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new UnsupportedVisitor()));
    }

    [Test]
    public void StructRegistrationDoesNotAcceptUnmanagedVisitor()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId componentId = layouts.Register<ManagedStructComponent>(new SchemaId(80_002));

        var structVisitor = new StructVisitor();
        var newVisitor = new NewVisitor();
        layouts.Visit(componentId, structVisitor);
        layouts.Visit(componentId, newVisitor);

        AssertVisited<ManagedStructComponent>(structVisitor, componentId);
        AssertVisited<ManagedStructComponent>(newVisitor, componentId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new UnmanagedVisitor()));
    }

    [Test]
    public void ClassNewRegistrationFallsBackToClassAndNewVisitors()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId componentId = layouts.Register<ClassNewComponent>(new SchemaId(80_003));

        var classNewVisitor = new ClassNewVisitor();
        var classVisitor = new ClassVisitor();
        var newVisitor = new NewVisitor();
        layouts.Visit(componentId, classNewVisitor);
        layouts.Visit(componentId, classVisitor);
        layouts.Visit(componentId, newVisitor);

        AssertVisited<ClassNewComponent>(classNewVisitor, componentId);
        AssertVisited<ClassNewComponent>(classVisitor, componentId);
        AssertVisited<ClassNewComponent>(newVisitor, componentId);
    }

    [Test]
    public void ExplicitNewConstraintCanSelectLessSpecificRegistrationShape()
    {
        var layouts = new ComponentLayoutRegistry();
        NewConstraint newConstraint = default;
        ComponentId componentId = layouts.Register<ClassNewComponent>(new SchemaId(80_004), in newConstraint);

        var newVisitor = new NewVisitor();
        layouts.Visit(componentId, newVisitor);

        AssertVisited<ClassNewComponent>(newVisitor, componentId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new ClassVisitor()));
    }

    [Test]
    public void GenericHelperUsesItsDeclaredConstraintForRegistrationShape()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId componentId = RegisterAsStruct<int>(layouts, new SchemaId(80_005));
        var structVisitor = new StructVisitor();

        layouts.Visit(componentId, structVisitor);

        AssertVisited<int>(structVisitor, componentId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new UnmanagedVisitor()));
    }

    [Test]
    public void InterfaceRoutesCanBeAddedBeforeAndAfterRegistration()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.BindInterface<GameComponent, IGameComponent>();
        layouts.BindInterface<GameComponent, INamedComponent>();
        ComponentId firstComponentId = layouts.Register<GameComponent>(new SchemaId(80_006));
        ComponentId secondComponentId = layouts.Register<GameComponent>(new SchemaId(80_017));

        var gameVisitor = new StructGameVisitor();
        var namedVisitor = new StructNamedVisitor();
        layouts.Visit(firstComponentId, gameVisitor);
        layouts.Visit(secondComponentId, namedVisitor);

        AssertVisited<GameComponent>(gameVisitor, firstComponentId);
        AssertVisited<GameComponent>(namedVisitor, secondComponentId);
    }

    [Test]
    public void InterfaceBindingAppliesToEveryRegistrationOfItsComponentType()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.BindInterface<GameComponent, IGameComponent>();
        layouts.BindInterface<GameComponent, IGameComponent>();
        ComponentId firstId = layouts.Register<GameComponent>(new SchemaId(80_012));
        ComponentId secondId = layouts.Register<GameComponent>(new SchemaId(80_013));
        var firstVisitor = new GameComponentVisitor();
        var secondVisitor = new GameComponentVisitor();

        layouts.Visit(firstId, firstVisitor);
        layouts.Visit(secondId, secondVisitor);

        AssertVisited<GameComponent>(firstVisitor, firstId);
        AssertVisited<GameComponent>(secondVisitor, secondId);
    }

    [Test]
    public void InterfaceBindingDoesNotApplyToOtherTypesImplementingTheInterface()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.BindInterface<GameComponent, IGameComponent>();
        ComponentId otherId = layouts.Register<OtherGameComponent>(new SchemaId(80_014));

        Assert.Throws<InvalidCastException>(() => layouts.Visit(otherId, new GameComponentVisitor()));
    }

    [Test]
    public void InterfaceBindingSupportsConstructibleClassComponents()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.BindInterface<ClassGameComponent, IGameComponent>();
        ComponentId componentId = layouts.Register<ClassGameComponent>(new SchemaId(80_018));
        var visitor = new GameComponentVisitor();

        layouts.Visit(componentId, visitor);

        AssertVisited<ClassGameComponent>(visitor, componentId);
    }

    [Test]
    public void InterfaceBindingRequiresAnInterfaceType()
    {
        var layouts = new ComponentLayoutRegistry();

        Assert.Throws<ArgumentException>(() => layouts.BindInterface<ClassNewComponent, object>());
    }

    [Test]
    public void InterfaceBindingWorksWhenAddedAfterComponentRegistration()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId componentId = layouts.Register<GameComponent>(new SchemaId(80_011));
        layouts.BindInterface<GameComponent, INamedComponent>();

        var visitor = new StructNamedVisitor();
        layouts.Visit(componentId, visitor);

        AssertVisited<GameComponent>(visitor, componentId);
    }

    [Test]
    public void InterfaceRouteMustBeExplicitAndMustMatchVisitorConstraint()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.BindInterface<GameComponent, IGameComponent>();
        ComponentId componentId = layouts.Register<GameComponent>(new SchemaId(80_007));

        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new StructNamedVisitor()));

        layouts.BindInterface<GameComponent, INamedComponent>();
        ComponentId namedId = layouts.Register<GameComponent>(new SchemaId(80_008));
        var wrongConstraint = new WrongConstraintVisitor();
        Assert.Throws<InvalidCastException>(() => layouts.Visit(namedId, wrongConstraint));
    }

    [Test]
    public void RegisterReturnsTheComponentId()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId componentId = layouts.Register<GameComponent>(new SchemaId(80_009));

        Assert.That(layouts.GetPrimary<GameComponent>(), Is.EqualTo(componentId));
    }

    [Test]
    public void UnconstrainedGenericRegistrationRequiresUnconstrainedVisitor()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId componentId = RegisterWithoutConstraint<int>(layouts, new SchemaId(80_010));
        var visitor = new UnconstrainedVisitor();

        layouts.Visit(componentId, visitor);

        AssertVisited<int>(visitor, componentId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new StructVisitor()));
    }

    [Test]
    public void ClassRegistrationAcceptsOnlyClassVisitor()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId componentId = layouts.Register<ClassOnlyComponent>(new SchemaId(80_019));
        var visitor = new ClassVisitor();

        layouts.Visit(componentId, visitor);

        AssertVisited<ClassOnlyComponent>(visitor, componentId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new NewVisitor()));
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new UnconstrainedVisitor()));
    }

    [Test]
    public void ClassNewRegistrationRejectsUnsupportedVisitors()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId componentId = layouts.Register<ClassNewComponent>(new SchemaId(80_020));

        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new UnconstrainedVisitor()));
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new UnmanagedVisitor()));
    }

    [Test]
    public void GenericRegistrationPreservesItsDeclaredConstraintShape()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId unmanagedId = RegisterAsUnmanaged<int>(layouts, new SchemaId(80_031));
        ComponentId classId = RegisterAsClass<ClassNewComponent>(layouts, new SchemaId(80_021));
        ComponentId newId = RegisterAsNew<ClassNewComponent>(layouts, new SchemaId(80_022));
        ComponentId classNewId = RegisterAsClassNew<ClassNewComponent>(layouts, new SchemaId(80_023));
        var unmanagedVisitor = new UnmanagedVisitor();
        var classVisitor = new ClassVisitor();
        var newVisitor = new NewVisitor();
        var classNewVisitor = new ClassNewVisitor();

        layouts.Visit(unmanagedId, unmanagedVisitor);
        layouts.Visit(classId, classVisitor);
        layouts.Visit(newId, newVisitor);
        layouts.Visit(classNewId, classNewVisitor);

        AssertVisited<int>(unmanagedVisitor, unmanagedId);
        AssertVisited<ClassNewComponent>(classVisitor, classId);
        AssertVisited<ClassNewComponent>(newVisitor, newId);
        AssertVisited<ClassNewComponent>(classNewVisitor, classNewId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(classId, new NewVisitor()));
        Assert.Throws<InvalidCastException>(() => layouts.Visit(newId, new ClassVisitor()));
        Assert.Throws<InvalidCastException>(() => layouts.Visit(classNewId, new UnconstrainedVisitor()));
    }

    [Test]
    public void VisitPrefersTheMostSpecificVisitorRouteInComponentIdLoops()
    {
        var layouts = new ComponentLayoutRegistry();
        var visitor = new MultiRouteVisitor();
        ComponentId unconstrainedId = RegisterWithoutConstraint<int>(layouts, new SchemaId(80_033));
        ComponentId structId = layouts.Register<ManagedStructComponent>(new SchemaId(80_034));
        ComponentId classId = layouts.Register<ClassOnlyComponent>(new SchemaId(80_035));
        NewConstraint newConstraint = default;
        ComponentId newId = layouts.Register<ClassNewComponent>(new SchemaId(80_036), in newConstraint);
        ComponentId classNewId = layouts.Register<ClassNewComponent>(new SchemaId(80_037));
        ComponentId unmanagedId = layouts.Register<int>(new SchemaId(80_038));

        ComponentId[] componentIds =
        [
            unconstrainedId,
            structId,
            classId,
            newId,
            classNewId,
            unmanagedId,
        ];
        VisitorRoute[] expectedRoutes =
        [
            VisitorRoute.Unconstrained,
            VisitorRoute.New,
            VisitorRoute.Class,
            VisitorRoute.New,
            VisitorRoute.ClassNew,
            VisitorRoute.New,
        ];

        int routeIndex = 0;
        foreach (ComponentId componentId in componentIds)
        {
            layouts.Visit(componentId, visitor);
            AssertRoute(expectedRoutes[routeIndex], visitor);
            routeIndex++;
        }

        BindInterfaceWithoutConstraint<UnconstrainedGameComponent, IGameComponent>(layouts);
        layouts.BindInterface<ManagedGameComponent, IGameComponent>();
        layouts.BindInterface<UnmanagedGameComponent, IGameComponent>();
        layouts.BindInterface<ClassOnlyGameComponent, IGameComponent>();
        layouts.BindInterface<NewGameComponent, IGameComponent>(in newConstraint);
        layouts.BindInterface<ClassGameComponent, IGameComponent>();
        ComponentId unconstrainedInterfaceId = layouts.Register<UnconstrainedGameComponent>(new SchemaId(80_039));
        ComponentId structInterfaceId = layouts.Register<ManagedGameComponent>(new SchemaId(80_040));
        ComponentId unmanagedInterfaceId = layouts.Register<UnmanagedGameComponent>(new SchemaId(80_041));
        ComponentId classInterfaceId = layouts.Register<ClassOnlyGameComponent>(new SchemaId(80_042));
        ComponentId newInterfaceId = layouts.Register<NewGameComponent>(new SchemaId(80_043));
        ComponentId classNewInterfaceId = layouts.Register<ClassGameComponent>(new SchemaId(80_044));

        ComponentId[] interfaceComponentIds =
        [
            unconstrainedInterfaceId,
            structInterfaceId,
            unmanagedInterfaceId,
            classInterfaceId,
            newInterfaceId,
            classNewInterfaceId,
        ];
        VisitorRoute[] expectedInterfaceRoutes =
        [
            VisitorRoute.Interface,
            VisitorRoute.NewInterface,
            VisitorRoute.NewInterface,
            VisitorRoute.ClassInterface,
            VisitorRoute.NewInterface,
            VisitorRoute.ClassNewInterface,
        ];

        routeIndex = 0;
        foreach (ComponentId componentId in interfaceComponentIds)
        {
            layouts.Visit(componentId, visitor);
            AssertRoute(expectedInterfaceRoutes[routeIndex], visitor);
            routeIndex++;
        }
    }

    [Test]
    public void GeneratedRegistrationPreservesStructAndUnmanagedVisitorRoutes()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId unmanagedId = layouts.Register(
            GeneratedComponentCatalog.GetRegistration<GeneratedUnmanagedVisitorComponent>());
        ComponentId structId = layouts.Register(
            GeneratedComponentCatalog.GetRegistration<GeneratedManagedVisitorComponent>());
        var unmanagedVisitor = new UnmanagedVisitor();
        var unmanagedStructVisitor = new StructVisitor();
        var managedStructVisitor = new StructVisitor();

        layouts.Visit(unmanagedId, unmanagedVisitor);
        layouts.Visit(unmanagedId, unmanagedStructVisitor);
        layouts.Visit(structId, managedStructVisitor);

        AssertVisited<GeneratedUnmanagedVisitorComponent>(unmanagedVisitor, unmanagedId);
        AssertVisited<GeneratedUnmanagedVisitorComponent>(unmanagedStructVisitor, unmanagedId);
        AssertVisited<GeneratedManagedVisitorComponent>(managedStructVisitor, structId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(structId, new UnmanagedVisitor()));
    }

    [Test]
    public void ClosedGenericRegistrationPreservesItsVisitorConstraintRoute()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId componentId = layouts.Register<ManagedStructComponent>(new SchemaId(80_046));
        ComponentId closedGenericId = layouts.Register(
            typeof(GenericRuntimeFunctorTests.History<>),
            componentId,
            new SchemaId(80_047));
        var visitor = new StructVisitor();

        layouts.Visit(closedGenericId, visitor);

        AssertVisited<GenericRuntimeFunctorTests.History<ManagedStructComponent>>(visitor, closedGenericId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(closedGenericId, new UnmanagedVisitor()));
    }

    [Test]
    public void UnconstrainedInterfaceBindingUsesItsDeclaredConstraintShape()
    {
        var layouts = new ComponentLayoutRegistry();
        BindInterfaceWithoutConstraint<ManagedGameComponent, IGameComponent>(layouts);
        ComponentId componentId = layouts.Register<ManagedGameComponent>(new SchemaId(80_024));
        var visitor = new GameComponentVisitor();

        layouts.Visit(componentId, visitor);

        AssertVisited<ManagedGameComponent>(visitor, componentId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new StructGameVisitor()));
    }

    [Test]
    public void StructInterfaceBindingSupportsNewStructAndUnconstrainedVisitors()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.BindInterface<ManagedGameComponent, IGameComponent>();
        ComponentId componentId = layouts.Register<ManagedGameComponent>(new SchemaId(80_025));
        var newVisitor = new NewGameVisitor();
        var structVisitor = new StructGameVisitor();
        var visitor = new GameComponentVisitor();

        layouts.Visit(componentId, newVisitor);
        layouts.Visit(componentId, structVisitor);
        layouts.Visit(componentId, visitor);

        AssertVisited<ManagedGameComponent>(newVisitor, componentId);
        AssertVisited<ManagedGameComponent>(structVisitor, componentId);
        AssertVisited<ManagedGameComponent>(visitor, componentId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new ClassGameVisitor()));
    }

    [Test]
    public void ClassInterfaceBindingSupportsClassAndUnconstrainedVisitors()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.BindInterface<ClassOnlyGameComponent, IGameComponent>();
        ComponentId componentId = layouts.Register<ClassOnlyGameComponent>(new SchemaId(80_026));
        var classVisitor = new ClassGameVisitor();
        var visitor = new GameComponentVisitor();

        layouts.Visit(componentId, classVisitor);
        layouts.Visit(componentId, visitor);

        AssertVisited<ClassOnlyGameComponent>(classVisitor, componentId);
        AssertVisited<ClassOnlyGameComponent>(visitor, componentId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new NewGameVisitor()));
    }

    [Test]
    public void UnmanagedInterfaceBindingSupportsEveryCompatibleVisitorShape()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.BindInterface<UnmanagedGameComponent, IGameComponent>();
        ComponentId componentId = layouts.Register<UnmanagedGameComponent>(new SchemaId(80_027));
        var newVisitor = new NewGameVisitor();
        var unmanagedVisitor = new UnmanagedGameVisitor();
        var structVisitor = new StructGameVisitor();
        var visitor = new GameComponentVisitor();

        layouts.Visit(componentId, newVisitor);
        layouts.Visit(componentId, unmanagedVisitor);
        layouts.Visit(componentId, structVisitor);
        layouts.Visit(componentId, visitor);

        AssertVisited<UnmanagedGameComponent>(newVisitor, componentId);
        AssertVisited<UnmanagedGameComponent>(unmanagedVisitor, componentId);
        AssertVisited<UnmanagedGameComponent>(structVisitor, componentId);
        AssertVisited<UnmanagedGameComponent>(visitor, componentId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new UnsupportedVisitor()));
    }

    [Test]
    public void NewInterfaceBindingRequiresNewVisitor()
    {
        var layouts = new ComponentLayoutRegistry();
        NewConstraint newConstraint = default;
        layouts.BindInterface<ClassGameComponent, IGameComponent>(in newConstraint);
        ComponentId componentId = layouts.Register<ClassGameComponent>(new SchemaId(80_028));
        var visitor = new NewGameVisitor();

        layouts.Visit(componentId, visitor);

        AssertVisited<ClassGameComponent>(visitor, componentId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new GameComponentVisitor()));
    }

    [Test]
    public void ClassNewInterfaceBindingSupportsAllLessSpecificVisitors()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.BindInterface<ClassGameComponent, IGameComponent>();
        ComponentId componentId = layouts.Register<ClassGameComponent>(new SchemaId(80_029));
        var classNewVisitor = new ClassNewGameVisitor();
        var classVisitor = new ClassGameVisitor();
        var newVisitor = new NewGameVisitor();
        var visitor = new GameComponentVisitor();

        layouts.Visit(componentId, classNewVisitor);
        layouts.Visit(componentId, classVisitor);
        layouts.Visit(componentId, newVisitor);
        layouts.Visit(componentId, visitor);

        AssertVisited<ClassGameComponent>(classNewVisitor, componentId);
        AssertVisited<ClassGameComponent>(classVisitor, componentId);
        AssertVisited<ClassGameComponent>(newVisitor, componentId);
        AssertVisited<ClassGameComponent>(visitor, componentId);
        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new UnsupportedVisitor()));
    }

    [Test]
    public void InterfaceBindingRejectsVisitorWhoseConstraintTypeAndGenericRouteDisagree()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.BindInterface<GameComponent, IGameComponent>();
        layouts.BindInterface<GameComponent, INamedComponent>();
        ComponentId componentId = layouts.Register<GameComponent>(new SchemaId(80_030));

        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new MismatchedConstraintVisitor()));
    }

    [Test]
    public void InterfaceBindingRejectsConstraintTypeThatChangesDuringVisit()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.BindInterface<GameComponent, IGameComponent>();
        ComponentId componentId = layouts.Register<GameComponent>(new SchemaId(80_045));

        Assert.Throws<InvalidCastException>(() => layouts.Visit(componentId, new ChangingConstraintVisitor()));
    }

    [Test]
    public void VisitRejectsInvalidIds()
    {
        var layouts = new ComponentLayoutRegistry();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            layouts.Visit(ComponentId.Invalid, new StructVisitor()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            layouts.Visit(new ComponentId(0), new StructVisitor()));
        Assert.Throws<ArgumentNullException>(() =>
            layouts.Visit(ComponentId.Invalid, null!));
    }

    private static ComponentId RegisterAsStruct<T>(ComponentLayoutRegistry layouts, SchemaId schemaId)
        where T : struct
        => layouts.Register<T>(schemaId);

    private static ComponentId RegisterAsClass<T>(ComponentLayoutRegistry layouts, SchemaId schemaId)
        where T : class
        => layouts.Register<T>(schemaId);

    private static ComponentId RegisterAsUnmanaged<T>(ComponentLayoutRegistry layouts, SchemaId schemaId)
        where T : unmanaged
        => layouts.Register<T>(schemaId);

    private static ComponentId RegisterAsNew<T>(ComponentLayoutRegistry layouts, SchemaId schemaId)
        where T : new()
        => layouts.Register<T>(schemaId);

    private static ComponentId RegisterAsClassNew<T>(ComponentLayoutRegistry layouts, SchemaId schemaId)
        where T : class, new()
        => layouts.Register<T>(schemaId);

    private static ComponentId RegisterWithoutConstraint<T>(ComponentLayoutRegistry layouts, SchemaId schemaId)
        => layouts.Register<T>(schemaId);

    private static void BindInterfaceWithoutConstraint<TComponent, TInterface>(ComponentLayoutRegistry layouts)
        where TComponent : TInterface
        => layouts.BindInterface<TComponent, TInterface>();

    private static void AssertRoute(VisitorRoute expected, MultiRouteVisitor visitor)
        => Assert.That(visitor.SelectedRoute, Is.EqualTo(expected));

    private static void AssertVisited<T>(VisitState visitor, ComponentId componentId)
    {
        Assert.That(visitor.ComponentId, Is.EqualTo(componentId));
        Assert.That(visitor.ComponentType, Is.EqualTo(typeof(T)));
        Assert.That(visitor.VisitCount, Is.EqualTo(1));
    }

    private interface IGameComponent { }

    private interface INamedComponent { }

    private struct ManagedStructComponent
    {
        public string? Name { get; set; }
    }

    private sealed class ClassOnlyComponent
    {
        private ClassOnlyComponent() { }
    }

    private sealed class ClassNewComponent
    {
        public ClassNewComponent() { }
    }

    private readonly struct GameComponent : IGameComponent, INamedComponent { }

    private struct ManagedGameComponent : IGameComponent
    {
        public string? Name { get; set; }
    }

    private struct UnmanagedGameComponent : IGameComponent
    {
        public int Value { get; set; }
    }

    private readonly struct OtherGameComponent : IGameComponent { }

    private readonly struct UnrelatedComponent { }

    private sealed class ClassOnlyGameComponent : IGameComponent
    {
        private ClassOnlyGameComponent() { }
    }

    private struct UnconstrainedGameComponent : IGameComponent
    {
        public string? Name { get; set; }
    }

    private sealed class ClassGameComponent : IGameComponent
    {
        public ClassGameComponent() { }
    }

    private sealed class NewGameComponent : IGameComponent
    {
        public NewGameComponent() { }
    }

    private abstract class VisitState
    {
        public ComponentId ComponentId;
        public Type? ComponentType;
        public int VisitCount;

        protected void Record<T>(ComponentId componentId)
        {
            ComponentId = componentId;
            ComponentType = typeof(T);
            VisitCount++;
        }
    }

    private sealed class UnconstrainedVisitor : VisitState, IUnconstrainedComponentTypeVisitor
    {
        public void Visit<T>(ComponentId componentId) => Record<T>(componentId);
    }

    private sealed class StructVisitor : VisitState, IStructComponentTypeVisitor
    {
        public void Visit<T>(ComponentId componentId) where T : struct => Record<T>(componentId);
    }

    private sealed class ClassVisitor : VisitState, IClassComponentTypeVisitor
    {
        public void Visit<T>(ComponentId componentId) where T : class => Record<T>(componentId);
    }

    private sealed class UnmanagedVisitor : VisitState, IUnmanagedComponentTypeVisitor
    {
        public void Visit<T>(ComponentId componentId) where T : unmanaged => Record<T>(componentId);
    }

    private sealed class NewVisitor : VisitState, INewComponentTypeVisitor
    {
        public void Visit<T>(ComponentId componentId) where T : new() => Record<T>(componentId);
    }

    private sealed class ClassNewVisitor : VisitState, IClassNewComponentTypeVisitor
    {
        public void Visit<T>(ComponentId componentId) where T : class, new() => Record<T>(componentId);
    }

    private sealed class UnsupportedVisitor : IComponentTypeVisitor { }

    private enum VisitorRoute
    {
        None,
        Unconstrained,
        Struct,
        Class,
        Unmanaged,
        New,
        ClassNew,
        Interface,
        StructInterface,
        ClassInterface,
        UnmanagedInterface,
        NewInterface,
        ClassNewInterface,
    }

    private sealed class MultiRouteVisitor : VisitState,
        IUnconstrainedComponentTypeVisitor,
        IStructComponentTypeVisitor,
        IClassComponentTypeVisitor,
        IUnmanagedComponentTypeVisitor,
        INewComponentTypeVisitor,
        IClassNewComponentTypeVisitor,
        IComponentTypeVisitor<IGameComponent>,
        IStructComponentTypeVisitor<IGameComponent>,
        IClassComponentTypeVisitor<IGameComponent>,
        IUnmanagedComponentTypeVisitor<IGameComponent>,
        INewComponentTypeVisitor<IGameComponent>,
        IClassNewComponentTypeVisitor<IGameComponent>
    {
        public RuntimeTypeHandle ConstraintType => typeof(IGameComponent).TypeHandle;

        internal VisitorRoute SelectedRoute { get; private set; }

        void IUnconstrainedComponentTypeVisitor.Visit<T>(ComponentId componentId)
            => SelectedRoute = VisitorRoute.Unconstrained;

        void IStructComponentTypeVisitor.Visit<T>(ComponentId componentId)
            => SelectedRoute = VisitorRoute.Struct;

        void IClassComponentTypeVisitor.Visit<T>(ComponentId componentId)
            => SelectedRoute = VisitorRoute.Class;

        void IUnmanagedComponentTypeVisitor.Visit<T>(ComponentId componentId)
            => SelectedRoute = VisitorRoute.Unmanaged;

        void INewComponentTypeVisitor.Visit<T>(ComponentId componentId)
            => SelectedRoute = VisitorRoute.New;

        void IClassNewComponentTypeVisitor.Visit<T>(ComponentId componentId)
            => SelectedRoute = VisitorRoute.ClassNew;

        void IComponentTypeVisitor<IGameComponent>.Visit<T>(ComponentId componentId)
            => SelectedRoute = VisitorRoute.Interface;

        void IStructComponentTypeVisitor<IGameComponent>.Visit<T>(ComponentId componentId)
            => SelectedRoute = VisitorRoute.StructInterface;

        void IClassComponentTypeVisitor<IGameComponent>.Visit<T>(ComponentId componentId)
            => SelectedRoute = VisitorRoute.ClassInterface;

        void IUnmanagedComponentTypeVisitor<IGameComponent>.Visit<T>(ComponentId componentId)
            => SelectedRoute = VisitorRoute.UnmanagedInterface;

        void INewComponentTypeVisitor<IGameComponent>.Visit<T>(ComponentId componentId)
            => SelectedRoute = VisitorRoute.NewInterface;

        void IClassNewComponentTypeVisitor<IGameComponent>.Visit<T>(ComponentId componentId)
            => SelectedRoute = VisitorRoute.ClassNewInterface;
    }

    private sealed class StructGameVisitor : VisitState, IStructComponentTypeVisitor<IGameComponent>
    {
        public RuntimeTypeHandle ConstraintType => typeof(IGameComponent).TypeHandle;

        public void Visit<T>(ComponentId componentId) where T : struct, IGameComponent => Record<T>(componentId);
    }

    private sealed class GameComponentVisitor : VisitState, IComponentTypeVisitor<IGameComponent>
    {
        public RuntimeTypeHandle ConstraintType => typeof(IGameComponent).TypeHandle;

        public void Visit<T>(ComponentId componentId) where T : IGameComponent => Record<T>(componentId);
    }

    private sealed class ClassGameVisitor : VisitState, IClassComponentTypeVisitor<IGameComponent>
    {
        public RuntimeTypeHandle ConstraintType => typeof(IGameComponent).TypeHandle;

        public void Visit<T>(ComponentId componentId) where T : class, IGameComponent => Record<T>(componentId);
    }

    private sealed class UnmanagedGameVisitor : VisitState, IUnmanagedComponentTypeVisitor<IGameComponent>
    {
        public RuntimeTypeHandle ConstraintType => typeof(IGameComponent).TypeHandle;

        public void Visit<T>(ComponentId componentId) where T : unmanaged, IGameComponent => Record<T>(componentId);
    }

    private sealed class NewGameVisitor : VisitState, INewComponentTypeVisitor<IGameComponent>
    {
        public RuntimeTypeHandle ConstraintType => typeof(IGameComponent).TypeHandle;

        public void Visit<T>(ComponentId componentId) where T : IGameComponent, new() => Record<T>(componentId);
    }

    private sealed class ClassNewGameVisitor : VisitState, IClassNewComponentTypeVisitor<IGameComponent>
    {
        public RuntimeTypeHandle ConstraintType => typeof(IGameComponent).TypeHandle;

        public void Visit<T>(ComponentId componentId) where T : class, IGameComponent, new() => Record<T>(componentId);
    }

    private sealed class StructNamedVisitor : VisitState, IStructComponentTypeVisitor<INamedComponent>
    {
        public RuntimeTypeHandle ConstraintType => typeof(INamedComponent).TypeHandle;

        public void Visit<T>(ComponentId componentId) where T : struct, INamedComponent => Record<T>(componentId);
    }

    private sealed class WrongConstraintVisitor : VisitState, IStructComponentTypeVisitor<INamedComponent>
    {
        public RuntimeTypeHandle ConstraintType => typeof(IGameComponent).TypeHandle;

        public void Visit<T>(ComponentId componentId) where T : struct, INamedComponent => Record<T>(componentId);
    }

    private sealed class MismatchedConstraintVisitor : VisitState, IComponentTypeVisitor<IGameComponent>
    {
        public RuntimeTypeHandle ConstraintType => typeof(INamedComponent).TypeHandle;

        public void Visit<T>(ComponentId componentId) where T : IGameComponent => Record<T>(componentId);
    }

    private sealed class ChangingConstraintVisitor : VisitState, IComponentTypeVisitor<IGameComponent>
    {
        private int _constraintReads;

        public RuntimeTypeHandle ConstraintType => ++_constraintReads == 1
            ? typeof(IGameComponent).TypeHandle
            : typeof(INamedComponent).TypeHandle;

        public void Visit<T>(ComponentId componentId) where T : IGameComponent => Record<T>(componentId);
    }
}

[DeltaEcsComponent(SchemaId = 80_031)]
internal struct GeneratedUnmanagedVisitorComponent
{
    public int Value { get; set; }
}

[DeltaEcsComponent(SchemaId = 80_032)]
internal struct GeneratedManagedVisitorComponent
{
    public string? Value { get; set; }
}
