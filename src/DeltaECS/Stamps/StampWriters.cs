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
    internal void MarkPoint()
        => _chunk.MarkComponentStamped(_componentIndex, _slotIndex, _stamp);
}
