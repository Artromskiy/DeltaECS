namespace Delta.ECS;

using System.Diagnostics.CodeAnalysis;

public sealed partial class ComponentLayoutRegistry
{
    /// <summary>Registers a component, inferring an empty non-primitive struct as a data-less tag.</summary>
    public ComponentId Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(SchemaId schemaId)
    {
        var layout = new ComponentLayout(schemaId, typeof(T));
        bool isTag = IsTagType(typeof(T));
        IGeneratedComponentTypeToken typeToken = GeneratedComponentTypeTokenRegistry.Get<T>();
        return isTag
            ? Register(layout, default, typeToken, isTag: true)
            : Register(layout, ComponentRowOperations.ForType<T>(), typeToken);
    }

    /// <summary>Tries to resolve the primary component registration for <typeparamref name="T"/>.</summary>
    public bool TryGetPrimary<T>(out ComponentId componentId)
        => TryGetPrimary(typeof(T), out componentId);

    /// <summary>Gets the primary component registration for <typeparamref name="T"/>.</summary>
    public ComponentId GetPrimary<T>()
        => GetPrimary(typeof(T));
}
