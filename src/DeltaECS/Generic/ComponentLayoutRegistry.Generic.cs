namespace Delta.ECS;

using System.Runtime.CompilerServices;

public sealed partial class ComponentLayoutRegistry
{
    public ComponentId Register<T>(SchemaId schemaId)
        => Register(
            typeof(T),
            schemaId,
            RuntimeHelpers.IsReferenceOrContainsReferences<T>());
}
