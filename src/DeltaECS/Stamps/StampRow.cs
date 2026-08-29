namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>Borrowed read-only view of one component's mutation stamps in a chunk.</summary>
public readonly ref struct StampRow
{
    private readonly Chunk _chunk;
    private readonly int _componentIndex;
    private readonly int _count;

    internal StampRow(Chunk chunk, int componentIndex, int count)
    {
        _chunk = chunk;
        _componentIndex = componentIndex;
        _count = count;
    }

    /// <summary>Reads the stamp for the current slot of the supplied chunk slot iterator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stamp Get(in QuerySlots slots)
    {
        int slotIndex = slots.CurrentIndex;
        if ((uint)slotIndex >= (uint)_count)
        {
            QueryThrowHelper.ThrowSlotIteratorNotPositioned();
        }

        return GetTrusted(slotIndex);
    }

    /// <summary>Reads a stamp after the owning query and slot bounds have been validated.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp GetTrusted(int slotIndex) => _chunk.GetComponentStampTrusted(_componentIndex, slotIndex);
}
