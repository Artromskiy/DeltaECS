namespace Delta.ECS;

public sealed partial class ComponentLayoutRegistry
{
    internal ComponentId RegisterGeneratedComponent<T>(SchemaId schemaId, IGeneratedComponentTypeToken typeToken)
    {
        ThrowHelper.ThrowIfNull(typeToken, nameof(typeToken));
        ComponentRegistrationTypeToken registrationToken = CreateComponentRegistrationTypeToken(typeToken);
        return RegisterGeneratedComponent<T>(schemaId, typeToken, IsTagType(typeof(T)), registrationToken);
    }

    internal ComponentId RegisterGeneratedComponent<T>(
        SchemaId schemaId,
        IGeneratedComponentTypeToken typeToken,
        bool isTag,
        IComponentRegistrationToken registrationToken)
    {
        if (typeToken.ComponentType != typeof(T))
        {
            ThrowHelper.ThrowGeneratedComponentTypeTokenMismatch(typeof(T), typeToken.ComponentType);
        }

        var layout = new ComponentLayout(schemaId, typeof(T));
        return isTag
            ? Register(layout, default, typeToken, registrationToken, isTag: true)
            : Register(layout, ComponentRowOperations.ForType<T>(), typeToken, registrationToken);
    }

    /// <summary>Tries to resolve the primary component registration for <typeparamref name="T"/>.</summary>
    public bool TryGetPrimary<T>(out ComponentId componentId)
        => TryGetPrimary(typeof(T), out componentId);

    /// <summary>Gets the primary component registration for <typeparamref name="T"/>.</summary>
    public ComponentId GetPrimary<T>()
        => GetPrimary(typeof(T));
}
