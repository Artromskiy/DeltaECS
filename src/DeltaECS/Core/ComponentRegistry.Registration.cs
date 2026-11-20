namespace Delta.ECS;

using System.Diagnostics.CodeAnalysis;

public sealed partial class ComponentRegistry
{
    internal void BindInterfaceRoute<TComponent, TInterface>(IComponentInterfaceVisitorRoute route)
        where TComponent : TInterface
    {
        Type interfaceType = typeof(TInterface);
        if (!interfaceType.IsInterface)
        {
            ThrowHelper.ThrowComponentVisitorConstraintMustBeInterface(interfaceType);
        }

        var key = (typeof(TComponent), interfaceType);
        _interfaceVisitorRoutes.TryAdd(key, route);
    }

    internal ComponentId RegisterTypedComponent<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        SchemaId schemaId,
        IComponentTypeRegistrationRoute route,
        bool group = false)
    {
        if (group && (!typeof(T).IsValueType || !typeof(IEquatable<T>).IsAssignableFrom(typeof(T))))
        {
            ThrowHelper.ThrowInvalidGroupingComponent(typeof(T));
        }
        if (TryGetComponentId(schemaId, out ComponentId existing) && (GetGrouping(existing) is not null) != group)
        {
            ThrowHelper.ThrowSchemaConflict(schemaId);
        }
        var layout = new ComponentLayout(schemaId, typeof(T));
        IGeneratedComponentTypeToken typeToken = GeneratedComponentTypeTokenRegistry.Get<T>();
        var registrationToken = new ComponentRegistrationTypeToken(route);
        bool isTag = IsTagType(typeof(T));
        ComponentId id = isTag
            ? Register(layout, default, typeToken, registrationToken, isTag: true)
            : Register(layout, ComponentRowOperations.ForType<T>(), typeToken, registrationToken);
        if (group && GetGrouping(id) is null)
        {
            if (_groupings.Length <= id.Value)
            {
                Array.Resize(ref _groupings, id.Value + 1);
            }
            _groupings[id.Value] = new ComponentGrouping<T>(id);
        }
        return id;
    }
}
