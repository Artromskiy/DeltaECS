namespace Delta.ECS;

using System.Runtime.CompilerServices;

public sealed partial class ComponentLayoutRegistry
{
    public ComponentId Register<T>(SchemaId schemaId, ComponentStorageClass storageClass = ComponentStorageClass.Dense)
        => Register(
            new ComponentLayout(schemaId, typeof(T), storageClass),
            ComponentRowOperations.ForRuntimeType(RuntimeHelpers.IsReferenceOrContainsReferences<T>()));

    public ComponentId RegisterUnmanaged<T>(SchemaId schemaId, ComponentStorageClass storageClass = ComponentStorageClass.Dense)
        where T : unmanaged
        => Register(
            new ComponentLayout(schemaId, typeof(T), storageClass, Unsafe.SizeOf<T>()),
            ComponentRowOperations.ForRuntimeType(RuntimeHelpers.IsReferenceOrContainsReferences<T>()));
}
