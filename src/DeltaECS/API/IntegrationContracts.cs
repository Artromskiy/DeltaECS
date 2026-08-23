namespace Delta.ECS.Integration;

using System;

[Flags]
public enum ComponentCapabilities
{
    None = 0,
    Read = 1,
    Write = 2
}

public readonly record struct ComponentDescriptor(
    ComponentId Id,
    SchemaId Schema,
    string Name,
    Type ValueType,
    ComponentCapabilities Capabilities,
    bool AllowsNull);

/// <summary>
/// Represents the value and exact component revision observed by a tooling read.
/// Reference values may retain storage identity; mutating such an object directly
/// bypasses revision tracking and is the caller's responsibility.
/// </summary>
public readonly record struct ComponentSnapshot(
    object? Value,
    Stamp Stamp);

public readonly record struct ComponentCatalog(
    ReadOnlyMemory<ComponentDescriptor> Components,
    Stamp Stamp);

public enum EcsReadErrorCode
{
    None,
    EntityNotAlive,
    ComponentUnknown,
    ComponentMissing,
    Unsupported
}

public readonly record struct EcsReadError(
    EcsReadErrorCode Code);

public enum EcsWriteErrorCode
{
    None,
    EntityNotAlive,
    ComponentUnknown,
    ComponentMissing,
    StaleStamp,
    InvalidValue,
    Unsupported
}

public readonly record struct EcsWriteError(
    EcsWriteErrorCode Code);

/// <summary>
/// Defines the neutral local-world boundary used by runtime, structural and
/// object-based tooling integrations.
/// </summary>
public interface IEcsWorld
{
    Stamp Stamp { get; }

    ComponentCatalog Catalog { get; }

    void Initialize();

    void Update(float deltaSeconds);

    void Shutdown();

    bool IsAlive(EntityId entity);

    EntityId Create(ReadOnlySpan<ComponentId> components);

    bool Destroy(EntityId entity);

    bool Add(EntityId entity, ReadOnlySpan<ComponentId> components);

    bool Remove(EntityId entity, ReadOnlySpan<ComponentId> components);

    /// <summary>
    /// Reports the full component count and writes the ascending prefix that
    /// fits in <paramref name="destination"/>. A live zero-component entity
    /// succeeds with a total count of zero.
    /// </summary>
    bool TryGetComponents(
        EntityId entity,
        Span<ComponentId> destination,
        out int totalCount);

    bool TryRead(
        EntityId entity,
        ComponentId component,
        out ComponentSnapshot snapshot,
        out EcsReadError error);

    bool TryWrite(
        EntityId entity,
        ComponentId component,
        object? value,
        Stamp expectedStamp,
        out Stamp writtenStamp,
        out EcsWriteError error);
}
