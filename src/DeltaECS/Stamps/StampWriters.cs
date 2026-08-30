namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>Trusted writer for one entity/component stamp.</summary>
internal readonly struct EntityComponentStampWriter
{
    private readonly Chunk _chunk;
    private readonly int _componentIndex;
    private readonly int _slotIndex;
    private readonly Stamp _stamp;

    internal EntityComponentStampWriter(
        Chunk chunk,
        int componentIndex,
        int slotIndex,
        Stamp stamp)
    {
        _chunk = chunk;
        _componentIndex = componentIndex;
        _slotIndex = slotIndex;
        _stamp = stamp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Mark()
        => _chunk.MarkComponentWritten(_componentIndex, _slotIndex, _stamp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkPoint()
        => _chunk.MarkComponentStamped(_componentIndex, _slotIndex, _stamp);
}

/// <summary>Trusted writer for one component stamp shared by a chunk.</summary>
internal readonly struct ChunkComponentStampWriter
{
    private readonly NativeMemory<Stamp> _stamps;
    private readonly int _componentIndex;
    private readonly Stamp _stamp;

    internal ChunkComponentStampWriter(
        NativeMemory<Stamp> stamps,
        int componentIndex,
        Stamp stamp)
    {
        _stamps = stamps;
        _componentIndex = componentIndex;
        _stamp = stamp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Mark()
    {
        _stamps[_componentIndex] = _stamp;
    }
}

/// <summary>Trusted writer for one component stamp shared by an archetype.</summary>
internal readonly struct ArchetypeComponentStampWriter
{
    private readonly NativeMemory<Stamp> _stamps;
    private readonly int _componentIndex;
    private readonly Stamp _stamp;

    internal ArchetypeComponentStampWriter(
        NativeMemory<Stamp> stamps,
        int componentIndex,
        Stamp stamp)
    {
        _stamps = stamps;
        _componentIndex = componentIndex;
        _stamp = stamp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Mark() => _stamps[_componentIndex] = _stamp;
}

/// <summary>Trusted writer for one component stamp shared by the world.</summary>
internal readonly struct WorldComponentStampWriter
{
    private readonly NativeMemory<Stamp> _stamps;
    private readonly int _componentIndex;
    private readonly Stamp _stamp;

    internal WorldComponentStampWriter(
        NativeMemory<Stamp> stamps,
        int componentIndex,
        Stamp stamp)
    {
        _stamps = stamps;
        _componentIndex = componentIndex;
        _stamp = stamp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Mark() => _stamps[_componentIndex] = _stamp;
}
