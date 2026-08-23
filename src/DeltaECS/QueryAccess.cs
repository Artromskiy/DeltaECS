namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>Short-lived dense chunk access used only by <see cref="World.Query{TContext}"/>.</summary>
public ref struct QueryChunkCursor
{
    private readonly QueryPlan _query;
    private readonly Chunk _chunk;
    private readonly ReadOnlySpan<int> _componentRows;
    private readonly uint _writeTick;
    private readonly ulong[]? _overlayMask;
    private readonly bool _fullMask;
    private readonly int _count;
    private int _index;

    internal QueryChunkCursor(QueryPlan query, int archetypeId, Chunk chunk, ReadOnlySpan<int> componentRows, uint writeTick, ulong[]? overlayMask, OverlayMaskResult overlayResult)
    {
        _query = query;
        ArchetypeId = archetypeId;
        _chunk = chunk;
        _componentRows = componentRows;
        _writeTick = writeTick;
        _overlayMask = overlayResult == OverlayMaskResult.Partial ? overlayMask : null;
        _fullMask = overlayResult == OverlayMaskResult.Full;
        _count = chunk.Count;
        _index = -1;
    }

    public int SlotCount => _count;
    public int CurrentIndex => _index;
    public int ArchetypeId { get; }
    public int GlobalChunkId => _chunk.GlobalId;
    public ReadOnlySpan<Entity> Entities => _chunk.Entities;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsActiveSlot(int slotIndex)
    {
        if ((uint)slotIndex >= (uint)_count)
        {
            return false;
        }

        return _fullMask || (_overlayMask is not null && (_overlayMask[slotIndex >> 6] & (1UL << (slotIndex & 63))) != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        int next = _index + 1;
        if (next >= _count)
        {
            _index = _count;
            return false;
        }

        _index = next;
        return true;
    }

    public ReadValues Get(ReadAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        return new ReadValues(_chunk.GetRawComponentRow(physicalRow));
    }

    public WriteValues Get(WriteAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new WriteValues(_chunk.GetRawComponentRow(physicalRow));
    }

    public ReadValues GetRead(ReadAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        return new ReadValues(_chunk.GetRawComponentRow(physicalRow));
    }

    public ReadValues GetRead(AccessRequest access)
    {
        if (access.IsWrite || !ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        return new ReadValues(_chunk.GetRawComponentRow(physicalRow));
    }

    public WriteValues GetWrite(WriteAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new WriteValues(_chunk.GetRawComponentRow(physicalRow));
    }

    public WriteValues GetWrite(AccessRequest access)
    {
        if (!access.IsWrite || !ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        if (_writeTick == 0)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        _chunk.MarkComponentWritten(physicalRow, _writeTick);
        return new WriteValues(_chunk.GetRawComponentRow(physicalRow));
    }
}

/// <summary>Prepared read-only values for one component row in one current chunk.</summary>
public ref struct ReadValues
{
    private readonly ref byte _data;

    internal ReadValues(Array row)
    {
        _data = ref GetArrayDataReference(row);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Ref<T>(QueryChunkCursor cursor) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), cursor.CurrentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Ref<T>(QuerySlots slots) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), slots.CurrentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Ref<T>(int slotIndex) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), slotIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref byte GetArrayDataReference(Array row)
        => ref MemoryMarshal.GetArrayDataReference(Unsafe.As<byte[]>(row));
}

/// <summary>Prepared writable values for one component row in one current chunk.</summary>
public ref struct WriteValues
{
    private readonly ref byte _data;

    internal WriteValues(Array row)
    {
        _data = ref GetArrayDataReference(row);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(QueryChunkCursor cursor) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), cursor.CurrentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(QuerySlots slots) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), slots.CurrentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(int slotIndex) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), slotIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref byte GetArrayDataReference(Array row)
        => ref MemoryMarshal.GetArrayDataReference(Unsafe.As<byte[]>(row));
}

/// <summary>Compatibility carrier for callback state that does not need static read/write typing.</summary>
public readonly struct AccessRequest
{
    internal AccessRequest(QueryPlan query, int queryComponentIndex, bool write)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
        IsWrite = write;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }
    internal bool IsWrite { get; }

    public static implicit operator AccessRequest(ReadAccess access)
    {
        if (access.Query is not { } query)
        {
            throw new InvalidOperationException("Cannot convert a default read access token.");
        }

        return new AccessRequest(query, access.QueryComponentIndex, write: false);
    }

    public static implicit operator AccessRequest(WriteAccess access)
    {
        if (access.Query is not { } query)
        {
            throw new InvalidOperationException("Cannot convert a default write access token.");
        }

        return new AccessRequest(query, access.QueryComponentIndex, write: true);
    }
}

/// <summary>Non-generic query access token for a read row.</summary>
public readonly struct ReadAccess
{
    internal ReadAccess(QueryPlan query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }
}

/// <summary>Non-generic query access token for a write row.</summary>
public readonly struct WriteAccess
{
    internal WriteAccess(QueryPlan query, int queryComponentIndex)
    {
        Query = query;
        QueryComponentIndex = queryComponentIndex;
    }

    internal QueryPlan? Query { get; }
    internal int QueryComponentIndex { get; }
}

internal sealed class QueryPlan
{
    private readonly QuerySpec _description;
    private int _version = -1;
    private NativeMemory<int> _matchingArchetypes = new(0);
    private DenseArchetypePlan[] _matchingPlans = Array.Empty<DenseArchetypePlan>();
    private bool _hasWriteAccess;

    public QueryPlan(QuerySpec spec) => _description = spec;

    public bool HasTags => !_description.AllTags.IsEmpty || !_description.AnyTags.IsEmpty || !_description.NoneTags.IsEmpty;
    public bool HasWriteAccess => _hasWriteAccess;
    public void RegisterWriteAccess() => _hasWriteAccess = true;

    public ReadOnlySpan<int> MatchingArchetypes(World world)
    {
        if (_version == world.ArchetypeVersion)
        {
            return _matchingArchetypes.ReadOnlySpan;
        }

        var matches = new List<int>(world.Archetypes.Count);
        var plans = new List<DenseArchetypePlan>(world.Archetypes.Count);
        for (int archetypeId = 0; archetypeId < world.Archetypes.Count; archetypeId++)
        {
            var archetype = world.Archetypes[archetypeId];
            if (!Matches(archetype))
            {
                continue;
            }

            int[] indices = new int[_description.AllMask.Count];
            int componentIndex = 0;
            foreach (var componentId in _description.AllMask)
            {
                indices[componentIndex++] = archetype.Mask.Rank(componentId);
            }

            matches.Add(archetypeId);
            plans.Add(new DenseArchetypePlan(archetype, indices));
        }

        _matchingArchetypes.Dispose();
        _matchingArchetypes = new NativeMemory<int>(CollectionsMarshal.AsSpan(matches));
        _matchingPlans = plans.ToArray();
        _version = world.ArchetypeVersion;
        return _matchingArchetypes.ReadOnlySpan;
    }

    public DenseArchetypePlan[] MatchingPlans(World world)
    {
        MatchingArchetypes(world);
        return _matchingPlans;
    }

    public ReadOnlySpan<int> ComponentRowIndices(int matchingIndex) => _matchingPlans[matchingIndex].ComponentRows;

    internal void Dispose() => _matchingArchetypes.Dispose();

    private bool Matches(Archetype archetype) => archetype.Mask.ContainsAll(_description.AllMask)
        && (_description.AnyMask.IsEmpty || archetype.Mask.Intersects(_description.AnyMask))
        && !archetype.Mask.Intersects(_description.NoneMask);
}

internal readonly struct DenseArchetypePlan
{
    public DenseArchetypePlan(Archetype archetype, int[] componentRows)
    {
        Archetype = archetype;
        ComponentRows = componentRows;
    }

    public Archetype Archetype { get; }
    public int[] ComponentRows { get; }
}

internal static class QueryThrowHelper
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowAccessMismatch() => throw new InvalidOperationException("The row access does not belong to this query or world.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowAccessTypeMismatch() => throw new InvalidOperationException("The row access type does not match the registered component type.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowMissingWriteIntent() => throw new InvalidOperationException("The query did not register its write row access.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowAccessModeMismatch() => throw new InvalidOperationException("The access mode does not match the requested row operation.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArchetypeIteratorNotPositioned() => throw new InvalidOperationException("The archetype iterator is not positioned on an archetype.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowChunkIteratorNotPositioned() => throw new InvalidOperationException("The chunk iterator is not positioned on a chunk.");
}
