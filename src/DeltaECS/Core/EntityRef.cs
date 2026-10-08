namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>A borrowed view of the current entity in an entity-aware iteration callback.</summary>
/// <remarks>
/// Use this value only during the callback that received it. It refers directly
/// to the current chunk and slot, so it becomes invalid after the callback or a
/// structural change. Store <see cref="Handle"/> when an entity handle must be
/// retained.
/// </remarks>
public readonly ref struct EntityRef
{
    private readonly World _world;
    private readonly Chunk _chunk;
    private readonly Archetype _archetype;
    private readonly int _slotIndex;

    internal EntityRef(World world, Chunk chunk, Archetype archetype, int slotIndex)
    {
        _world = world;
        _chunk = chunk;
        _archetype = archetype;
        _slotIndex = slotIndex;
    }

    /// <summary>Gets the stable handle for the current entity.</summary>
    public Entity Handle => _chunk.RawEntities[_slotIndex];

    /// <summary>Gets the current entity's index.</summary>
    public int Index => Handle.Index;

    /// <summary>Gets the current entity's lifetime generation.</summary>
    public int Generation => Handle.Generation;

    /// <summary>Reports whether the current entity has the specified component registration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has(ComponentId componentId)
        => _world.Has(_chunk, _archetype, _slotIndex, componentId);

    /// <summary>Attempts to read a component by registration, returning its default value when absent or mismatched.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>(ComponentId componentId, out T value)
        => _world.TryGet(_chunk, _archetype, _slotIndex, componentId, out value);

    /// <summary>Gets a writable component reference and marks its change stamp.</summary>
    /// <remarks>For a tag, this returns the shared default placeholder; writes through it are not stored.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef<T>(ComponentId componentId)
        => ref _world.GetRef<T>(_chunk, _archetype, _slotIndex, componentId);
}

public sealed partial class World
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal EntityRef CreateEntityRef(Chunk chunk, int slotIndex)
        => new(this, chunk, _archetypes[chunk.ArchetypeId], slotIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Has(Chunk chunk, Archetype archetype, int slotIndex, ComponentId componentId)
    {
        EnsureExecutionAccess();
        return _layouts.TryGetTagIndex(componentId, out int tagIndex)
            ? chunk.HasTag(tagIndex, slotIndex)
            : archetype.Contains(componentId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGet<T>(Chunk chunk, Archetype archetype, int slotIndex, ComponentId componentId, out T value)
    {
        EnsureExecutionAccess();
        if (!_layouts.TryGet(componentId, out ComponentLayout layout) || !IsCompatibleComponentType<T>(layout))
        {
            value = default!;
            return false;
        }

        if (_layouts.TryGetTagIndex(componentId, out int tagIndex))
        {
            value = default!;
            return chunk.HasTag(tagIndex, slotIndex);
        }

        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            value = default!;
            return false;
        }

        value = chunk.GetComponentRef<T>(componentIndex, slotIndex);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref T GetRef<T>(Chunk chunk, Archetype archetype, int slotIndex, ComponentId componentId)
    {
        EnsureExecutionAccess();
        EnsureRegisteredType<T>(componentId);
        Entity entity = chunk.RawEntities[slotIndex];
        if (_layouts.TryGetTagIndex(componentId, out int tagIndex))
        {
            if (!chunk.HasTag(tagIndex, slotIndex))
            {
                ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
            }

            return ref GeneratedTagRows.GetReference<T>(0);
        }

        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
        }

        Stamp stamp = chunk.IncrementComponentStamp(componentIndex, slotIndex);
        CreateEntityComponentStampWriter(chunk, componentIndex, slotIndex, stamp).MarkPoint();
        return ref chunk.GetComponentRef<T>(componentIndex, slotIndex);
    }
}
