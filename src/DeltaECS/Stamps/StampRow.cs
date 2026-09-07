namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>Borrowed read-only view of one component's mutation stamps in a chunk.</summary>
public readonly ref struct StampRow
{
    private readonly Chunk _chunk;
    private readonly int _componentIndex;
    private readonly int _count;
    private readonly ref Stamp _chunkStamp;
    private readonly ref Stamp _archetypeStamp;

    internal StampRow(
        Chunk chunk,
        int componentIndex,
        int count,
        ref Stamp chunkStamp,
        ref Stamp archetypeStamp)
    {
        _chunk = chunk;
        _componentIndex = componentIndex;
        _count = count;
        _chunkStamp = ref chunkStamp;
        _archetypeStamp = ref archetypeStamp;
    }

    /// <summary>Reads the stamp for the current slot of the supplied chunk slot iterator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stamp Get(in QuerySlots slots)
    {
        int slotIndex = slots.CurrentIndex;
        if ((uint)slotIndex >= (uint)_count)
        {
            ThrowHelper.ThrowSlotIteratorNotPositioned();
        }

        return GetTrusted(slotIndex);
    }

    /// <summary>Reads a stamp after the owning query and slot bounds have been validated.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp GetTrusted(int slotIndex)
        => StampMath.Sum(
            _chunk.GetComponentStampTrusted(_componentIndex, slotIndex),
            _chunkStamp,
            _archetypeStamp);
}
