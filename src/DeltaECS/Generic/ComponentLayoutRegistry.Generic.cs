namespace DeltaECS;

using System.Runtime.CompilerServices;

public sealed partial class ComponentLayoutRegistry
{
    public ComponentId Register<T>(SchemaId schemaId)
        => Register(
            typeof(T),
            schemaId,
            RuntimeHelpers.IsReferenceOrContainsReferences<T>());

    /// <summary>Tries to resolve the primary component registration for <typeparamref name="T"/>.</summary>
    public bool TryGetPrimary<T>(out ComponentId componentId)
        => TryGetPrimary(typeof(T), out componentId);

    /// <summary>Gets the primary component registration for <typeparamref name="T"/>.</summary>
    public ComponentId GetPrimary<T>()
        => GetPrimary(typeof(T));
}
