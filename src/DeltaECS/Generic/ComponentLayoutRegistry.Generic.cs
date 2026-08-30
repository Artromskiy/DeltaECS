namespace Delta.ECS;

using System.Diagnostics.CodeAnalysis;

public sealed partial class ComponentLayoutRegistry
{
    public ComponentId Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(SchemaId schemaId)
        => Register(
            new ComponentLayout(schemaId, typeof(T)),
            ComponentRowOperations.ForType<T>());

    /// <summary>Tries to resolve the primary component registration for <typeparamref name="T"/>.</summary>
    public bool TryGetPrimary<T>(out ComponentId componentId)
        => TryGetPrimary(typeof(T), out componentId);

    /// <summary>Gets the primary component registration for <typeparamref name="T"/>.</summary>
    public ComponentId GetPrimary<T>()
        => GetPrimary(typeof(T));
}
