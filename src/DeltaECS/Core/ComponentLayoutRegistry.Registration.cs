namespace Delta.ECS;

using System.Diagnostics.CodeAnalysis;

public sealed partial class ComponentLayoutRegistry
{
    internal void BindInterfaceRoute<TComponent, TInterface>(IComponentInterfaceVisitorRoute route)
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

    internal ComponentId RegisterTypedComponent<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        SchemaId schemaId,
        IComponentTypeRegistrationRoute route)
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
