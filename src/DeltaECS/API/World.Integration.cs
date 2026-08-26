namespace DeltaECS;

using System;
using DeltaECS.Integration;

public sealed partial class World : IEcsWorld
{
    private ComponentCatalog _integrationCatalog;
    private int _integrationCatalogLayoutCount = -1;
    private MutationStampSource _catalogStamps;
    private IntegrationLifecycleState _integrationLifecycle;

    ComponentCatalog IEcsWorld.Catalog
    {
        get
        {
            ThrowIfDisposed();
            RefreshIntegrationCatalog();
            return _integrationCatalog;
        }
    }

    void IEcsWorld.Initialize()
    {
        ThrowIfDisposed();
        if (_integrationLifecycle != IntegrationLifecycleState.Created)
        {
            throw new InvalidOperationException("The ECS world can be initialized exactly once.");
        }

        _integrationLifecycle = IntegrationLifecycleState.Initialized;
    }

    void IEcsWorld.Update()
    {
        EnsureIntegrationActive();
        EnsureNoActiveLease("update the integration world");

        // World has no scheduler or time source. Integration Update is a
        // lifecycle-validated safe point and performs no systems work.
    }

    void IEcsWorld.Shutdown()
    {
        EnsureIntegrationActive();
        EnsureNoActiveLease("shut down the integration world");
        _integrationLifecycle = IntegrationLifecycleState.Shutdown;
    }

    bool IEcsWorld.IsAlive(Entity entity)
    {
        EnsureIntegrationActive();
        EnsureNoActiveLease("inspect entities through the integration API");
        return IsAlive(entity);
    }

    Entity IEcsWorld.Create(ReadOnlySpan<ComponentId> components)
    {
        EnsureIntegrationActive();
        EnsureNoActiveLease("create entities");
        ValidateStructuralComponents(components);

        var mask = ComponentMask.From(components);
        Stamp stamp = _mutationStamps.Next();
        var archetype = GetOrCreateArchetype(mask);
        Span<Entity> created = stackalloc Entity[1];
        _ = CreateBatch(archetype, created, stamp);
        return created[0];
    }

    bool IEcsWorld.Destroy(Entity entity)
    {
        EnsureIntegrationActive();
        return Destroy(entity);
    }

    bool IEcsWorld.Add(Entity entity, ReadOnlySpan<ComponentId> components)
        => ApplyIntegrationComponents(entity, components, isAdd: true);

    bool IEcsWorld.Remove(Entity entity, ReadOnlySpan<ComponentId> components)
        => ApplyIntegrationComponents(entity, components, isAdd: false);

    bool IEcsWorld.TryGetComponents(
        Entity entity,
        Span<ComponentId> destination,
        out int totalCount)
    {
        EnsureIntegrationActive();
        EnsureNoActiveLease("inspect entities through the integration API");
        totalCount = 0;
        if (!TryResolve(entity, out int recordIndex))
        {
            return false;
        }

        ref readonly var record = ref RecordAt(recordIndex);
        ReadOnlySpan<ComponentId> components = _archetypes[record.Archetype].ComponentIds;
        totalCount = components.Length;
        components[..Math.Min(components.Length, destination.Length)].CopyTo(destination);
        return true;
    }

    bool IEcsWorld.TryRead(
        Entity entity,
        ComponentId component,
        out ComponentSnapshot snapshot,
        out EcsReadError error)
    {
        EnsureIntegrationActive();
        EnsureNoActiveLease("read components through the integration API");
        snapshot = default;

        if (!TryResolve(entity, out int recordIndex))
        {
            error = new EcsReadError(EcsReadErrorCode.EntityNotAlive);
            return false;
        }

        if (!_layouts.TryGet(component, out var layout))
        {
            error = new EcsReadError(EcsReadErrorCode.ComponentUnknown);
            return false;
        }

        ref readonly var record = ref RecordAt(recordIndex);
        var archetype = _archetypes[record.Archetype];
        if (!archetype.TryGetComponentIndex(component, out int componentIndex))
        {
            error = new EcsReadError(EcsReadErrorCode.ComponentMissing);
            return false;
        }

        if (!SupportsObjectAccess(layout))
        {
            error = new EcsReadError(EcsReadErrorCode.Unsupported);
            return false;
        }

        var chunk = archetype.GetChunk(record.Chunk);
        object? value = chunk.GetRawComponentRow(componentIndex).GetValue(record.SlotIndex);
        Stamp componentStamp = chunk.GetComponentStamp(componentIndex, record.SlotIndex);
        snapshot = new ComponentSnapshot(value, componentStamp);
        error = new EcsReadError(EcsReadErrorCode.None);
        return true;
    }

    bool IEcsWorld.TryWrite(
        Entity entity,
        ComponentId component,
        object? value,
        Stamp expectedStamp,
        out Stamp writtenStamp,
        out EcsWriteError error)
    {
        EnsureIntegrationActive();
        EnsureNoActiveLease("write components through the integration API");
        writtenStamp = default;

        if (!TryResolve(entity, out int recordIndex))
        {
            error = new EcsWriteError(EcsWriteErrorCode.EntityNotAlive);
            return false;
        }

        if (!_layouts.TryGet(component, out var layout))
        {
            error = new EcsWriteError(EcsWriteErrorCode.ComponentUnknown);
            return false;
        }

        ref readonly var record = ref RecordAt(recordIndex);
        var archetype = _archetypes[record.Archetype];
        if (!archetype.TryGetComponentIndex(component, out int componentIndex))
        {
            error = new EcsWriteError(EcsWriteErrorCode.ComponentMissing);
            return false;
        }

        if (layout.RuntimeType is not { IsByRefLike: false, IsPointer: false } valueType
            || Nullable.GetUnderlyingType(valueType) is not null)
        {
            error = new EcsWriteError(EcsWriteErrorCode.Unsupported);
            return false;
        }

        var chunk = archetype.GetChunk(record.Chunk);
        if (chunk.GetComponentStamp(componentIndex, record.SlotIndex) != expectedStamp)
        {
            error = new EcsWriteError(EcsWriteErrorCode.StaleStamp);
            return false;
        }

        bool allowsNull = AllowsNull(valueType);
        if ((value is null && !allowsNull)
            || (value is not null && value.GetType() != valueType))
        {
            error = new EcsWriteError(EcsWriteErrorCode.InvalidValue);
            return false;
        }

        writtenStamp = _mutationStamps.Next();
        chunk.GetRawComponentRow(componentIndex).SetValue(value, record.SlotIndex);
        chunk.MarkComponentStamped(componentIndex, record.SlotIndex, writtenStamp);
        error = new EcsWriteError(EcsWriteErrorCode.None);
        return true;
    }

    private bool ApplyIntegrationComponents(
        Entity entity,
        ReadOnlySpan<ComponentId> components,
        bool isAdd)
    {
        EnsureIntegrationActive();
        EnsureNoActiveLease(isAdd ? "add components" : "remove components");
        ValidateStructuralComponents(components);
        if (components.IsEmpty || !IsAlive(entity))
        {
            return false;
        }

        Span<Entity> entities = stackalloc Entity[1];
        entities[0] = entity;
        return ApplyComponents(isAdd, components.ToArray(), entities) != 0;
    }

    private void RefreshIntegrationCatalog()
    {
        int layoutCount = _layouts.Count;
        if (_integrationCatalogLayoutCount == layoutCount)
        {
            return;
        }

        var descriptors = new ComponentDescriptor[layoutCount];
        for (int index = 0; index < layoutCount; index++)
        {
            var id = new ComponentId(index);
            var layout = _layouts.Get(id);
            Type? runtimeType = layout.RuntimeType;
            Type valueType = runtimeType ?? typeof(object);
            ComponentCapabilities capabilities = SupportsObjectAccess(layout)
                ? ComponentCapabilities.Read | ComponentCapabilities.Write
                : ComponentCapabilities.None;
            string name = runtimeType is null
                ? $"Raw component {index}"
                : runtimeType.FullName ?? runtimeType.Name;
            descriptors[index] = new ComponentDescriptor(
                id,
                layout.SchemaId,
                name,
                valueType,
                capabilities,
                runtimeType is not null && AllowsNull(runtimeType));
        }

        _integrationCatalog = new ComponentCatalog(descriptors, _catalogStamps.Next());
        _integrationCatalogLayoutCount = layoutCount;
    }

    private void ValidateStructuralComponents(ReadOnlySpan<ComponentId> components)
    {
        for (int index = 0; index < components.Length; index++)
        {
            ComponentId component = components[index];
            if (!_layouts.TryGet(component, out var layout))
            {
                throw new ArgumentException($"Component {component.Value} is not registered in this world.", nameof(components));
            }

            if (layout.RuntimeType is null)
            {
                throw new NotSupportedException($"Component {component.Value} has a raw layout that the typed-array world cannot materialize.");
            }
        }
    }

    private void EnsureIntegrationActive()
    {
        ThrowIfDisposed();
        if (_integrationLifecycle != IntegrationLifecycleState.Initialized)
        {
            throw new InvalidOperationException("The ECS integration world is not initialized or has already shut down.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static bool SupportsObjectAccess(ComponentLayout layout)
        => layout.RuntimeType is { IsByRefLike: false, IsPointer: false } runtimeType
            && Nullable.GetUnderlyingType(runtimeType) is null;

    private static bool AllowsNull(Type valueType)
        => !valueType.IsValueType || Nullable.GetUnderlyingType(valueType) is not null;

    private enum IntegrationLifecycleState
    {
        Created,
        Initialized,
        Shutdown
    }
}
