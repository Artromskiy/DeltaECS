namespace Delta.ECS;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

/// <summary>Routes component registry extension calls through the selected registration constraints.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IComponentLayoutRegistryRoute
{
}

/// <summary>Registration route marker for types that satisfy <c>new()</c>.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IComponentLayoutRegistryNewRoute : IComponentLayoutRegistryRoute
{
}

/// <summary>Registration route marker for struct types.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IComponentLayoutRegistryStructRoute : IComponentLayoutRegistryNewRoute
{
}

/// <summary>Registration route marker for unmanaged types.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IComponentLayoutRegistryUnmanagedRoute : IComponentLayoutRegistryStructRoute
{
}

/// <summary>Registration route marker for class types.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IComponentLayoutRegistryClassRoute : IComponentLayoutRegistryRoute
{
}

/// <summary>Registration route marker for constructible class types.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IComponentLayoutRegistryClassNewRoute :
    IComponentLayoutRegistryClassRoute,
    IComponentLayoutRegistryNewRoute
{
}

public sealed partial class ComponentLayoutRegistry :
    IComponentLayoutRegistryUnmanagedRoute,
    IComponentLayoutRegistryClassNewRoute
{
}

/// <summary>Provides constraint-selected registration and interface binding for component layouts.</summary>
public static class ComponentLayoutRegistryRegistrationExtensions
{
    /// <summary>Registers a component using the constraints available for <typeparamref name="T"/>.</summary>
    public static ComponentId Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        this IComponentLayoutRegistryRoute layouts,
        SchemaId schemaId)
        => ResolveRegistry(layouts).RegisterTypedComponent<T>(schemaId, new UnconstrainedComponentTypeRegistrationRoute<T>());

    /// <summary>Registers a component with a public parameterless constructor.</summary>
    public static ComponentId Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        this IComponentLayoutRegistryNewRoute layouts,
        SchemaId schemaId)
        where T : new()
        => ResolveRegistry(layouts).RegisterTypedComponent<T>(schemaId, new NewComponentTypeRegistrationRoute<T>());

    /// <summary>Registers a struct component.</summary>
    public static ComponentId Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        this IComponentLayoutRegistryStructRoute layouts,
        SchemaId schemaId)
        where T : struct
        => ResolveRegistry(layouts).RegisterTypedComponent<T>(schemaId, new StructComponentTypeRegistrationRoute<T>());

    /// <summary>Registers an unmanaged component.</summary>
    public static ComponentId Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        this IComponentLayoutRegistryUnmanagedRoute layouts,
        SchemaId schemaId)
        where T : unmanaged
        => ResolveRegistry(layouts).RegisterTypedComponent<T>(schemaId, new UnmanagedComponentTypeRegistrationRoute<T>());

    /// <summary>Registers a class component.</summary>
    public static ComponentId Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        this IComponentLayoutRegistryClassRoute layouts,
        SchemaId schemaId)
        where T : class
        => ResolveRegistry(layouts).RegisterTypedComponent<T>(schemaId, new ClassComponentTypeRegistrationRoute<T>());

    /// <summary>Registers a constructible class component.</summary>
    public static ComponentId Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        this IComponentLayoutRegistryClassNewRoute layouts,
        SchemaId schemaId)
        where T : class, new()
        => ResolveRegistry(layouts).RegisterTypedComponent<T>(schemaId, new ClassNewComponentTypeRegistrationRoute<T>());

    /// <summary>Binds an interface visitor route for every registration of one component CLR type.</summary>
    public static void BindInterface<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(
        this IComponentLayoutRegistryRoute layouts)
        where TComponent : TInterface
        => ResolveRegistry(layouts).BindInterfaceRoute<TComponent, TInterface>(
            new UnconstrainedComponentInterfaceVisitorRoute<TComponent, TInterface>());

    /// <summary>Binds an interface visitor route to a component with a public parameterless constructor.</summary>
    public static void BindInterface<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(
        this IComponentLayoutRegistryNewRoute layouts)
        where TComponent : TInterface, new()
        => ResolveRegistry(layouts).BindInterfaceRoute<TComponent, TInterface>(
            new NewComponentInterfaceVisitorRoute<TComponent, TInterface>());

    /// <summary>Binds an interface visitor route to a struct component CLR type.</summary>
    public static void BindInterface<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(
        this IComponentLayoutRegistryStructRoute layouts)
        where TComponent : struct, TInterface
        => ResolveRegistry(layouts).BindInterfaceRoute<TComponent, TInterface>(
            new StructComponentInterfaceVisitorRoute<TComponent, TInterface>());

    /// <summary>Binds an interface visitor route to an unmanaged component CLR type.</summary>
    public static void BindInterface<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(
        this IComponentLayoutRegistryUnmanagedRoute layouts)
        where TComponent : unmanaged, TInterface
        => ResolveRegistry(layouts).BindInterfaceRoute<TComponent, TInterface>(
            new UnmanagedComponentInterfaceVisitorRoute<TComponent, TInterface>());

    /// <summary>Binds an interface visitor route to a class component CLR type.</summary>
    public static void BindInterface<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(
        this IComponentLayoutRegistryClassRoute layouts)
        where TComponent : class, TInterface
        => ResolveRegistry(layouts).BindInterfaceRoute<TComponent, TInterface>(
            new ClassComponentInterfaceVisitorRoute<TComponent, TInterface>());

    /// <summary>Binds an interface visitor route to a constructible class component CLR type.</summary>
    public static void BindInterface<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterface>(
        this IComponentLayoutRegistryClassNewRoute layouts)
        where TComponent : class, TInterface, new()
        => ResolveRegistry(layouts).BindInterfaceRoute<TComponent, TInterface>(
            new ClassNewComponentInterfaceVisitorRoute<TComponent, TInterface>());

    private static ComponentLayoutRegistry ResolveRegistry(IComponentLayoutRegistryRoute layouts)
    {
        ThrowHelper.ThrowIfNull(layouts, nameof(layouts));

        if (layouts is ComponentLayoutRegistry registry)
        {
            return registry;
        }

        return ThrowHelper.ThrowInvalidComponentLayoutRegistryRoute(layouts);
    }
}
