namespace Delta.ECS.Systems;

using Delta.ECS;

/// <summary>One world-bound unit of simulation work.</summary>
public interface ISystem
{
    /// <summary>Gets the world owned by this system.</summary>
    World World { get; init; }

    /// <summary>Gets the components and world effects used by this system.</summary>
    SystemAccess Access { get; }

    /// <summary>Executes the system once.</summary>
    void Tick();
}
