namespace Delta.ECS.Tests;

using NUnit.Framework;

[TestFixture]
internal sealed class NamedComponentTypeVisitorTests
{
    [Test]
    public void InterfaceRoutesInvokeTheMatchingNamedOverride()
    {
        var layouts = new ComponentLayoutRegistry();
        BindUnconstrained<UnconstrainedComponent, IGameComponent>(layouts);
        StructConstraint structConstraint = default;
        UnmanagedConstraint unmanagedConstraint = default;
        ClassConstraint classConstraint = default;
        NewConstraint newConstraint = default;
        ClassNewConstraint classNewConstraint = default;
        layouts.BindInterface<ManagedStructComponent, IGameComponent>(in structConstraint);
        layouts.BindInterface<UnmanagedComponent, IGameComponent>(in unmanagedConstraint);
        layouts.BindInterface<ClassComponent, IGameComponent>(in classConstraint);
        layouts.BindInterface<NewComponent, IGameComponent>(in newConstraint);
        layouts.BindInterface<ClassNewComponent, IGameComponent>(in classNewConstraint);

        ComponentId unconstrainedId = layouts.Register<UnconstrainedComponent>(new SchemaId(80_090));
        ComponentId structId = layouts.Register<ManagedStructComponent>(new SchemaId(80_091));
        ComponentId unmanagedId = layouts.Register<UnmanagedComponent>(new SchemaId(80_092));
        ComponentId classId = layouts.Register<ClassComponent>(new SchemaId(80_093));
        ComponentId newId = layouts.Register<NewComponent>(new SchemaId(80_094));
        ComponentId classNewId = layouts.Register<ClassNewComponent>(new SchemaId(80_095));
        var visitor = new GameVisitor();

        AssertVisited(layouts, visitor, unconstrainedId, typeof(UnconstrainedComponent), VisitKind.Unconstrained);
        AssertVisited(layouts, visitor, structId, typeof(ManagedStructComponent), VisitKind.Struct);
        AssertVisited(layouts, visitor, unmanagedId, typeof(UnmanagedComponent), VisitKind.Unmanaged);
        AssertVisited(layouts, visitor, classId, typeof(ClassComponent), VisitKind.Class);
        AssertVisited(layouts, visitor, newId, typeof(NewComponent), VisitKind.New);
        AssertVisited(layouts, visitor, classNewId, typeof(ClassNewComponent), VisitKind.ClassNew);
    }

    private static void BindUnconstrained<TComponent, TInterface>(ComponentLayoutRegistry layouts)
        where TComponent : TInterface
        => layouts.BindInterface<TComponent, TInterface>();

    private static void AssertVisited(
        ComponentLayoutRegistry layouts,
        GameVisitor visitor,
        ComponentId componentId,
        Type componentType,
        VisitKind expectedKind)
    {
        Assert.That(layouts.TryVisit(componentId, visitor), Is.True);
        Assert.That(visitor.LastComponentId, Is.EqualTo(componentId));
        Assert.That(visitor.LastComponentType, Is.EqualTo(componentType));
        Assert.That(visitor.LastVisitKind, Is.EqualTo(expectedKind));
    }

    private interface IGameComponent { }

    private sealed class UnconstrainedComponent : IGameComponent { }

    private struct ManagedStructComponent : IGameComponent
    {
        public string? Name { get; set; }
    }

    private struct UnmanagedComponent : IGameComponent
    {
        public int Value { get; set; }
    }

    private sealed class ClassComponent : IGameComponent
    {
        private ClassComponent() { }
    }

    private sealed class NewComponent : IGameComponent
    {
        public NewComponent() { }
    }

    private sealed class ClassNewComponent : IGameComponent
    {
        public ClassNewComponent() { }
    }

    private enum VisitKind
    {
        Unconstrained,
        Unmanaged,
        Struct,
        Class,
        New,
        ClassNew,
    }

    private sealed class GameVisitor : GeneralComponentTypeVisitor<IGameComponent>
    {
        internal ComponentId LastComponentId { get; private set; }

        internal Type? LastComponentType { get; private set; }

        internal VisitKind LastVisitKind { get; private set; }

        protected override void Visit<T>(ComponentId id)
            => Record<T>(id, VisitKind.Unconstrained);

        protected override void VisitUnmanaged<T>(ComponentId id)
            => Record<T>(id, VisitKind.Unmanaged);

        protected override void VisitStruct<T>(ComponentId id)
            => Record<T>(id, VisitKind.Struct);

        protected override void VisitClass<T>(ComponentId id)
            => Record<T>(id, VisitKind.Class);

        protected override void VisitConstructible<T>(ComponentId id)
            => Record<T>(id, VisitKind.New);

        protected override void VisitClassConstructible<T>(ComponentId id)
            => Record<T>(id, VisitKind.ClassNew);

        private void Record<T>(ComponentId id, VisitKind kind)
        {
            LastComponentId = id;
            LastComponentType = typeof(T);
            LastVisitKind = kind;
        }
    }
}
