namespace Delta.ECS;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

public sealed partial class ComponentLayoutRegistry
{
    /// <summary>Registers an interface visitor route for every registration of one component CLR type.</summary>
    public void BindInterface<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>()
        where TComponent : TInterface
        => BindInterface<TComponent, TInterface>(new UnconstrainedComponentInterfaceVisitorRoute<TComponent, TInterface>());

    /// <summary>Binds an interface visitor route to a struct component CLR type.</summary>
    [OverloadResolutionPriority(4)]
    public void BindInterface<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(in StructConstraint _ = default)
        where TComponent : struct, TInterface
        => BindInterface<TComponent, TInterface>(new StructComponentInterfaceVisitorRoute<TComponent, TInterface>());

    /// <summary>Binds an interface visitor route to a class component CLR type.</summary>
    [OverloadResolutionPriority(2)]
    public void BindInterface<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(in ClassConstraint _ = default)
        where TComponent : class, TInterface
        => BindInterface<TComponent, TInterface>(new ClassComponentInterfaceVisitorRoute<TComponent, TInterface>());

    /// <summary>Binds an interface visitor route to an unmanaged component CLR type.</summary>
    [OverloadResolutionPriority(5)]
    public void BindInterface<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(in UnmanagedConstraint _ = default)
        where TComponent : unmanaged, TInterface
        => BindInterface<TComponent, TInterface>(new UnmanagedComponentInterfaceVisitorRoute<TComponent, TInterface>());

    /// <summary>Binds an interface visitor route to a component with a public parameterless constructor.</summary>
    [OverloadResolutionPriority(3)]
    public void BindInterface<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(in NewConstraint _ = default)
        where TComponent : TInterface, new()
        => BindInterface<TComponent, TInterface>(new NewComponentInterfaceVisitorRoute<TComponent, TInterface>());

    /// <summary>Binds an interface visitor route to a constructible class component CLR type.</summary>
    [OverloadResolutionPriority(6)]
    public void BindInterface<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(in ClassNewConstraint _ = default)
        where TComponent : class, TInterface, new()
        => BindInterface<TComponent, TInterface>(new ClassNewComponentInterfaceVisitorRoute<TComponent, TInterface>());

    private void BindInterface<TComponent, TInterface>(IComponentInterfaceVisitorRoute route)
        where TComponent : TInterface
    {
        Type interfaceType = typeof(TInterface);
        if (!interfaceType.IsInterface)
        {
            ThrowHelper.ThrowComponentVisitorConstraintMustBeInterface(interfaceType);
        }

        var key = (typeof(TComponent).TypeHandle, interfaceType.TypeHandle);
        _interfaceVisitorRoutes.TryAdd(key, route);
    }

    /// <summary>Registers a component using the constraints known for <typeparamref name="T"/>.</summary>
    public ComponentId Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(SchemaId schemaId)
        => RegisterTypedComponent<T>(schemaId, new UnconstrainedComponentTypeRegistrationRoute<T>());

    /// <summary>Registers a struct component when <typeparamref name="T"/> is a value type.</summary>
    [OverloadResolutionPriority(4)]
    public ComponentId Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        SchemaId schemaId,
        in StructConstraint _ = default)
        where T : struct
        => RegisterTypedComponent<T>(schemaId, new StructComponentTypeRegistrationRoute<T>());

    /// <summary>Registers a class component when <typeparamref name="T"/> is a reference type.</summary>
    [OverloadResolutionPriority(2)]
    public ComponentId Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        SchemaId schemaId,
        in ClassConstraint _ = default)
        where T : class
        => RegisterTypedComponent<T>(schemaId, new ClassComponentTypeRegistrationRoute<T>());

    /// <summary>Registers an unmanaged component when <typeparamref name="T"/> is unmanaged.</summary>
    [OverloadResolutionPriority(5)]
    public ComponentId Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        SchemaId schemaId,
        in UnmanagedConstraint _ = default)
        where T : unmanaged
        => RegisterTypedComponent<T>(schemaId, new UnmanagedComponentTypeRegistrationRoute<T>());

    /// <summary>Registers a component with a public parameterless constructor.</summary>
    [OverloadResolutionPriority(3)]
    public ComponentId Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        SchemaId schemaId,
        in NewConstraint _ = default)
        where T : new()
        => RegisterTypedComponent<T>(schemaId, new NewComponentTypeRegistrationRoute<T>());

    /// <summary>Registers a class component with a public parameterless constructor.</summary>
    [OverloadResolutionPriority(6)]
    public ComponentId Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        SchemaId schemaId,
        in ClassNewConstraint _ = default)
        where T : class, new()
        => RegisterTypedComponent<T>(schemaId, new ClassNewComponentTypeRegistrationRoute<T>());

    private ComponentId RegisterTypedComponent<T>(SchemaId schemaId, IComponentTypeRegistrationRoute route)
    {
        var layout = new ComponentLayout(schemaId, typeof(T));
        IGeneratedComponentTypeToken typeToken = GeneratedComponentTypeTokenRegistry.Get<T>();
        var registrationToken = new ComponentRegistrationTypeToken(route);
        bool isTag = IsTagType(typeof(T));
        return isTag
            ? Register(layout, default, typeToken, registrationToken, isTag: true)
            : Register(layout, ComponentRowOperations.ForType<T>(), typeToken, registrationToken);
    }
}
