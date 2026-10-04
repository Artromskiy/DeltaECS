namespace Delta.ECS.Integration;

using System;

/// <summary>Capabilities available for object-based component tooling.</summary>
[Flags]
public enum ComponentCapabilities
{
    /// <summary>No object-based read or write is supported.</summary>
    None = 0,
    /// <summary>The component value can be read as an object.</summary>
    Read = 1,
    /// <summary>The component value can be written as an object.</summary>
    Write = 2
}

/// <summary>Describes one component registration exposed to tooling.</summary>
/// <param name="Id">The runtime identifier used to address this registration.</param>
/// <param name="Schema">The stable schema identifier assigned during registration.</param>
/// <param name="Name">The display name of the registered CLR type.</param>
/// <param name="ValueType">The CLR type of values stored for this registration.</param>
/// <param name="Capabilities">The object-based operations supported by the registration.</param>
/// <param name="AllowsNull">Whether null is a valid component value.</param>
public readonly record struct ComponentDescriptor(
    ComponentId Id,
    SchemaId Schema,
    string Name,
    Type ValueType,
    ComponentCapabilities Capabilities,
    bool AllowsNull)
{
    /// <summary>Gets whether this registration represents membership without component data.</summary>
    public bool IsTag { get; init; }
}

/// <summary>
/// Represents the value and exact component revision observed by a tooling read.
/// Reference values may retain storage identity; mutating such an object directly
/// bypasses revision tracking and is the caller's responsibility. For a data-less
/// tag, the value is its boxed default and the stamp is default because tags have
/// membership but no per-entity value revision.
/// </summary>
public readonly record struct ComponentSnapshot(
    object? Value,
    Stamp Stamp);

/// <summary>A snapshot of component registrations exposed by a world.</summary>
/// <param name="Components">The descriptors in component-id order.</param>
/// <param name="Stamp">The revision of the catalog snapshot.</param>
public readonly record struct RuntimeComponentCatalog(
    ReadOnlyMemory<ComponentDescriptor> Components,
    Stamp Stamp);

/// <summary>Identifies why a tooling component read failed.</summary>
public enum EcsReadErrorCode
{
    /// <summary>The read succeeded.</summary>
    None,
    /// <summary>The entity handle is not alive in the world.</summary>
    EntityNotAlive,
    /// <summary>The component identifier is not registered.</summary>
    ComponentUnknown,
    /// <summary>The entity does not have the requested component.</summary>
    ComponentMissing,
    /// <summary>The component type cannot be read through the object API.</summary>
    Unsupported
}

/// <summary>Describes the result of a tooling component read.</summary>
/// <param name="Code">The read result code.</param>
public readonly record struct EcsReadError(
    EcsReadErrorCode Code);

/// <summary>Identifies why a tooling component write failed.</summary>
public enum EcsWriteErrorCode
{
    /// <summary>The write succeeded.</summary>
    None,
    /// <summary>The entity handle is not alive in the world.</summary>
    EntityNotAlive,
    /// <summary>The component identifier is not registered.</summary>
    ComponentUnknown,
    /// <summary>The entity does not have the requested component.</summary>
    ComponentMissing,
    /// <summary>The expected stamp does not match the current component revision.</summary>
    StaleStamp,
    /// <summary>The supplied value is null or has an incompatible CLR type.</summary>
    InvalidValue,
    /// <summary>The component type cannot be written through the object API.</summary>
    Unsupported
}

/// <summary>Describes the result of a tooling component write.</summary>
/// <param name="Code">The write result code.</param>
public readonly record struct EcsWriteError(
    EcsWriteErrorCode Code);

/// <summary>
/// Defines the neutral local-world boundary used by runtime, structural and
/// object-based tooling integrations.
/// </summary>
public interface IEcsWorld
{
    /// <summary>Gets the current catalog of component registrations.</summary>
    RuntimeComponentCatalog Catalog { get; }

    /// <summary>Initializes the integration lifecycle for this world.</summary>
    void Initialize();

    /// <summary>Advances the integration lifecycle at a safe point.</summary>
    void Update();

    /// <summary>Shuts down the integration lifecycle.</summary>
    void Shutdown();

    /// <summary>Determines whether an entity handle is alive in this world.</summary>
    bool IsAlive(Entity entity);

    /// <summary>Creates an entity with the supplied component registrations.</summary>
    Entity Create(ReadOnlySpan<ComponentId> components);

    /// <summary>Destroys an entity and returns whether it was alive.</summary>
    bool Destroy(Entity entity);

    /// <summary>Attempts to add the supplied registrations to an entity.</summary>
    bool Add(Entity entity, ReadOnlySpan<ComponentId> components);

    /// <summary>Attempts to remove the supplied registrations from an entity.</summary>
    bool Remove(Entity entity, ReadOnlySpan<ComponentId> components);

    /// <summary>
    /// Reports the full component count and writes the ascending prefix that
    /// fits in <paramref name="destination"/>. A live zero-component entity
    /// succeeds with a total count of zero.
    /// </summary>
    bool TryGetComponents(
        Entity entity,
        Span<ComponentId> destination,
        out int totalCount);

    /// <summary>Reads a component value and its current revision through the object API.</summary>
    bool TryRead(
        Entity entity,
        ComponentId component,
        out ComponentSnapshot snapshot,
        out EcsReadError error);

    /// <summary>Writes a component value if its current revision matches the expected stamp.</summary>
    bool TryWrite(
        Entity entity,
        ComponentId component,
        object? value,
        Stamp expectedStamp,
        out Stamp writtenStamp,
        out EcsWriteError error);
}
