namespace Delta.ECS.Registration.CSharp9
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Delta.ECS;

    internal static class Program
    {
        private static int Main()
        {
            RegistrationGrammarProof.Run();
            return 0;
        }
    }

    /// <summary>Runs the registration API proof compiled with C# 9 and no consumer generator.</summary>
    public static class RegistrationGrammarProof
    {
        /// <summary>Validates constraint-selected registration and interface visitor routes.</summary>
        public static void Run()
        {
            var layouts = new ComponentLayoutRegistry();

            ComponentId unmanagedId = layouts.Register<int>(new SchemaId(1));
            AssertVisited<int>(layouts, unmanagedId, new UnmanagedVisitor());
            AssertVisited<int>(layouts, unmanagedId, new StructVisitor());
            AssertVisited<int>(layouts, unmanagedId, new NewVisitor());
            AssertVisited<int>(layouts, unmanagedId, new UnconstrainedVisitor());

            ComponentId managedStructId = RegisterStruct<ManagedComponent>(layouts, new SchemaId(2));
            AssertVisited<ManagedComponent>(layouts, managedStructId, new StructVisitor());
            AssertVisited<ManagedComponent>(layouts, managedStructId, new NewVisitor());
            AssertRejected(layouts, managedStructId, new UnmanagedVisitor());

            ComponentId unconstrainedId = RegisterUnconstrained<int>(layouts, new SchemaId(7));
            AssertVisited<int>(layouts, unconstrainedId, new UnconstrainedVisitor());
            AssertRejected(layouts, unconstrainedId, new StructVisitor());

            ComponentId newRouteId = RegisterNew<ConstructibleComponent>(layouts, new SchemaId(8));
            AssertVisited<ConstructibleComponent>(layouts, newRouteId, new NewVisitor());
            AssertRejected(layouts, newRouteId, new ClassNewVisitor());

            ComponentId constructibleClassId = layouts.Register<ConstructibleComponent>(new SchemaId(3));
            AssertVisited<ConstructibleComponent>(layouts, constructibleClassId, new ClassNewVisitor());
            AssertVisited<ConstructibleComponent>(layouts, constructibleClassId, new ClassVisitor());
            AssertVisited<ConstructibleComponent>(layouts, constructibleClassId, new NewVisitor());

            ComponentId classOnlyId = layouts.Register<ClassOnlyComponent>(new SchemaId(5));
            AssertVisited<ClassOnlyComponent>(layouts, classOnlyId, new ClassVisitor());
            AssertRejected(layouts, classOnlyId, new NewVisitor());

            ComponentId directManagedStructId = layouts.Register<ManagedComponent>(new SchemaId(6));
            AssertVisited<ManagedComponent>(layouts, directManagedStructId, new StructVisitor());
            AssertRejected(layouts, directManagedStructId, new UnmanagedVisitor());

            ComponentId interfaceId = BindAndRegisterStruct<ManagedComponent, IGameComponent>(layouts, new SchemaId(4));
            AssertVisited<ManagedComponent>(layouts, interfaceId, new StructInterfaceVisitor());
            AssertVisited<ManagedComponent>(layouts, interfaceId, new NewInterfaceVisitor());
            AssertVisited<ManagedComponent>(layouts, interfaceId, new InterfaceVisitor());
            AssertRejected(layouts, interfaceId, new UnmanagedInterfaceVisitor());

            AssertInterfaceRoute<ManagedComponent>(
                "unconstrained",
                registry => BindAndRegisterInterface<ManagedComponent, IGameComponent>(registry, new SchemaId(9)));
            AssertInterfaceRoute<ManagedComponent>(
                "struct",
                registry => BindAndRegisterStruct<ManagedComponent, IGameComponent>(registry, new SchemaId(10)));
            AssertInterfaceRoute<UnmanagedComponent>(
                "unmanaged",
                registry => BindAndRegisterUnmanagedInterface<UnmanagedComponent, IGameComponent>(registry, new SchemaId(11)));
            AssertInterfaceRoute<ClassOnlyComponent>(
                "class",
                registry => BindAndRegisterClassInterface<ClassOnlyComponent, IGameComponent>(registry, new SchemaId(12)));
            AssertInterfaceRoute<ConstructibleComponent>(
                "new",
                registry => BindAndRegisterNewInterface<ConstructibleComponent, IGameComponent>(registry, new SchemaId(13)));
            AssertInterfaceRoute<ConstructibleComponent>(
                "class-new",
                registry => BindAndRegisterClassNewInterface<ConstructibleComponent, IGameComponent>(registry, new SchemaId(14)));

            Console.WriteLine("C# 9 registration and interface-binding proof passed.");
        }

        private static ComponentId RegisterStruct<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            ComponentLayoutRegistry layouts,
            SchemaId schemaId)
            where T : struct
            => layouts.Register<T>(schemaId);

        private static ComponentId RegisterUnconstrained<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            ComponentLayoutRegistry layouts,
            SchemaId schemaId)
            => layouts.Register<T>(schemaId);

        private static ComponentId RegisterNew<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            ComponentLayoutRegistry layouts,
            SchemaId schemaId)
            where T : new()
            => layouts.Register<T>(schemaId);

        private static ComponentId BindAndRegisterStruct<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(
            ComponentLayoutRegistry layouts,
            SchemaId schemaId)
            where T : struct, TInterface
        {
            layouts.BindInterface<T, TInterface>();
            return layouts.Register<T>(schemaId);
        }

        private static ComponentId BindAndRegisterInterface<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(
            ComponentLayoutRegistry layouts,
            SchemaId schemaId)
            where T : TInterface
        {
            layouts.BindInterface<T, TInterface>();
            return layouts.Register<T>(schemaId);
        }

        private static ComponentId BindAndRegisterUnmanagedInterface<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(
            ComponentLayoutRegistry layouts,
            SchemaId schemaId)
            where T : unmanaged, TInterface
        {
            layouts.BindInterface<T, TInterface>();
            return layouts.Register<T>(schemaId);
        }

        private static ComponentId BindAndRegisterClassInterface<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(
            ComponentLayoutRegistry layouts,
            SchemaId schemaId)
            where T : class, TInterface
        {
            layouts.BindInterface<T, TInterface>();
            return layouts.Register<T>(schemaId);
        }

        private static ComponentId BindAndRegisterNewInterface<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(
            ComponentLayoutRegistry layouts,
            SchemaId schemaId)
            where T : TInterface, new()
        {
            layouts.BindInterface<T, TInterface>();
            return layouts.Register<T>(schemaId);
        }

        private static ComponentId BindAndRegisterClassNewInterface<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(
            ComponentLayoutRegistry layouts,
            SchemaId schemaId)
            where T : class, TInterface, new()
        {
            layouts.BindInterface<T, TInterface>();
            return layouts.Register<T>(schemaId);
        }

        private static void AssertInterfaceRoute<T>(
            string expectedRoute,
            Func<ComponentLayoutRegistry, ComponentId> bindAndRegister)
            where T : IGameComponent
        {
            var layouts = new ComponentLayoutRegistry();
            ComponentId id = bindAndRegister(layouts);
            var visitor = new InterfaceRouteVisitor();
            if (!layouts.TryVisit(id, visitor)
                || visitor.Route != expectedRoute
                || visitor.ComponentType != typeof(T)
                || visitor.ComponentId != id
                || visitor.VisitCount != 1)
            {
                throw new InvalidOperationException("The interface visitor route did not match the call-site constraints.");
            }
        }

        private sealed class ClassOnlyComponent : IGameComponent
        {
            private ClassOnlyComponent()
            {
            }
        }

        private static void AssertVisited<T>(ComponentLayoutRegistry layouts, ComponentId componentId, ProbeVisitor visitor)
        {
            if (!layouts.TryVisit(componentId, visitor)
                || visitor.ComponentType != typeof(T)
                || visitor.ComponentId != componentId
                || visitor.VisitCount != 1)
            {
                throw new InvalidOperationException("The visitor route did not receive the registered component.");
            }
        }

        private static void AssertRejected(ComponentLayoutRegistry layouts, ComponentId componentId, ProbeVisitor visitor)
        {
            if (layouts.TryVisit(componentId, visitor) || visitor.VisitCount != 0)
            {
                throw new InvalidOperationException("The visitor route accepted an incompatible component.");
            }
        }

        private interface IGameComponent
        {
        }

        private struct ManagedComponent : IGameComponent
        {
            public string? Value;

            public ManagedComponent(string? value)
            {
                Value = value;
            }
        }

        private struct UnmanagedComponent : IGameComponent
        {
            public byte Value;

            public UnmanagedComponent(byte value)
            {
                Value = value;
            }
        }

        private sealed class ConstructibleComponent : IGameComponent
        {
            public ConstructibleComponent()
            {
            }
        }

        private abstract class ProbeVisitor : IVisitor
        {
            public Type ComponentType { get; private set; } = typeof(void);
            public ComponentId ComponentId { get; private set; }
            public int VisitCount { get; private set; }

            protected void Mark<T>(ComponentId componentId)
            {
                ComponentType = typeof(T);
                ComponentId = componentId;
                VisitCount++;
            }
        }

        private sealed class InterfaceRouteVisitor : GeneralComponentTypeVisitor<IGameComponent>
        {
            public string? Route { get; private set; }
            public Type ComponentType { get; private set; } = typeof(void);
            public ComponentId ComponentId { get; private set; }
            public int VisitCount { get; private set; }

            protected override void Visit<T>(ComponentId componentId)
            {
                Mark<T>(componentId, "unconstrained");
            }

            protected override void VisitUnmanaged<T>(ComponentId componentId)
            {
                Mark<T>(componentId, "unmanaged");
            }

            protected override void VisitStruct<T>(ComponentId componentId)
            {
                Mark<T>(componentId, "struct");
            }

            protected override void VisitClass<T>(ComponentId componentId)
            {
                Mark<T>(componentId, "class");
            }

            protected override void VisitConstructible<T>(ComponentId componentId)
            {
                Mark<T>(componentId, "new");
            }

            protected override void VisitClassConstructible<T>(ComponentId componentId)
            {
                Mark<T>(componentId, "class-new");
            }

            private void Mark<T>(ComponentId componentId, string route)
            {
                Route = route;
                ComponentType = typeof(T);
                ComponentId = componentId;
                VisitCount++;
            }
        }

        private sealed class UnconstrainedVisitor : ProbeVisitor, IUnconstrainedVisitor
        {
            public void Visit<T>(ComponentId componentId) => Mark<T>(componentId);
        }

        private sealed class StructVisitor : ProbeVisitor, IStructVisitor
        {
            public void Visit<T>(ComponentId componentId) where T : struct => Mark<T>(componentId);
        }

        private sealed class UnmanagedVisitor : ProbeVisitor, IUnmanagedVisitor
        {
            public void Visit<T>(ComponentId componentId) where T : unmanaged => Mark<T>(componentId);
        }

        private sealed class NewVisitor : ProbeVisitor, INewVisitor
        {
            public void Visit<T>(ComponentId componentId) where T : new() => Mark<T>(componentId);
        }

        private sealed class ClassVisitor : ProbeVisitor, IClassVisitor
        {
            public void Visit<T>(ComponentId componentId) where T : class => Mark<T>(componentId);
        }

        private sealed class ClassNewVisitor : ProbeVisitor, IClassNewVisitor
        {
            public void Visit<T>(ComponentId componentId) where T : class, new() => Mark<T>(componentId);
        }

        private sealed class InterfaceVisitor : ProbeVisitor, IComponentVisitor<IGameComponent>
        {
            public RuntimeTypeHandle ConstraintType => typeof(IGameComponent).TypeHandle;
            public void Visit<T>(ComponentId componentId) where T : IGameComponent => Mark<T>(componentId);
        }

        private sealed class StructInterfaceVisitor : ProbeVisitor, IStructVisitor<IGameComponent>
        {
            public RuntimeTypeHandle ConstraintType => typeof(IGameComponent).TypeHandle;
            public void Visit<T>(ComponentId componentId) where T : struct, IGameComponent => Mark<T>(componentId);
        }

        private sealed class UnmanagedInterfaceVisitor : ProbeVisitor, IUnmanagedVisitor<IGameComponent>
        {
            public RuntimeTypeHandle ConstraintType => typeof(IGameComponent).TypeHandle;
            public void Visit<T>(ComponentId componentId) where T : unmanaged, IGameComponent => Mark<T>(componentId);
        }

        private sealed class NewInterfaceVisitor : ProbeVisitor, INewVisitor<IGameComponent>
        {
            public RuntimeTypeHandle ConstraintType => typeof(IGameComponent).TypeHandle;
            public void Visit<T>(ComponentId componentId) where T : IGameComponent, new() => Mark<T>(componentId);
        }
    }
}
