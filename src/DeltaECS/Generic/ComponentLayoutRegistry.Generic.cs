namespace Delta.ECS;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

public sealed partial class ComponentLayoutRegistry
{
    /// <summary>Registers a component, inferring an empty non-primitive struct as a data-less tag.</summary>
    public ComponentId Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(SchemaId schemaId)
    {
        var layout = new ComponentLayout(schemaId, typeof(T));
        bool isTag = typeof(T).IsValueType
            && !typeof(T).IsPrimitive
            && !typeof(T).IsEnum
            && typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 0;
        return isTag
            ? Register(layout, default, isTag: true)
            : Register(layout, ComponentRowOperations.ForType<T>());
    }

    /// <summary>Tries to resolve the primary component registration for <typeparamref name="T"/>.</summary>
    public bool TryGetPrimary<T>(out ComponentId componentId)
        => TryGetPrimary(typeof(T), out componentId);

    /// <summary>Gets the primary component registration for <typeparamref name="T"/>.</summary>
    public ComponentId GetPrimary<T>()
        => GetPrimary(typeof(T));
}
