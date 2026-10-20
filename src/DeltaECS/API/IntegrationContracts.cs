namespace Delta.ECS.Integration;

using System;

/// <summary>Represents a component value observed by a tooling read.</summary>
[Obsolete("Use World and typed component APIs; remove during the DeltaECS 1.0 integration API migration.")]
public readonly record struct ComponentSnapshot(object? Value);

/// <summary>A snapshot of component registrations exposed by a world.</summary>
/// <param name="Components">The registered IDs in component-id order.</param>
public readonly record struct RuntimeComponentCatalog(ReadOnlyMemory<ComponentId> Components);

/// <summary>Identifies why a tooling component read failed.</summary>
[Obsolete("Use World and typed component APIs; remove during the DeltaECS 1.0 integration API migration.")]
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
[Obsolete("Use World and typed component APIs; remove during the DeltaECS 1.0 integration API migration.")]
public readonly record struct EcsReadError(
    EcsReadErrorCode Code);

/// <summary>Identifies why a tooling component write failed.</summary>
[Obsolete("Use World and typed component APIs; remove during the DeltaECS 1.0 integration API migration.")]
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
    /// <summary>The supplied value is null or has an incompatible CLR type.</summary>
    InvalidValue,
    /// <summary>The component type cannot be written through the object API.</summary>
    Unsupported
}

/// <summary>Describes the result of a tooling component write.</summary>
/// <param name="Code">The write result code.</param>
[Obsolete("Use World and typed component APIs; remove during the DeltaECS 1.0 integration API migration.")]
public readonly record struct EcsWriteError(
    EcsWriteErrorCode Code);

/// <summary>
/// Defines the neutral local-world boundary used by runtime, structural and
/// object-based tooling integrations.
/// </summary>
[Obsolete("Use World and typed component APIs; remove during the DeltaECS 1.0 integration API migration.")]
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

    /// <summary>Determines whether an entity handle is alive in this world; safe during query iteration.</summary>
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
    /// succeeds with a total count of zero. Safe during query iteration.
    /// </summary>
    bool TryGetComponents(
        Entity entity,
        Span<ComponentId> destination,
        out int totalCount);

    /// <summary>Reads a component value through the object API; safe during query iteration.</summary>
    bool TryRead(
        Entity entity,
        ComponentId component,
        out ComponentSnapshot snapshot,
        out EcsReadError readError);

    /// <summary>Writes a component value through the object API.</summary>
    bool TryWrite(
        Entity entity,
        ComponentId component,
        object? value,
        out EcsWriteError writeError);
}
