namespace Delta.ECS;

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

internal sealed class EntityRefStampBatch
{
    [ThreadStatic]
    private static EntityRefStampBatch? _current;

    private readonly QueryPlan _queryPlan;
    private EntityRefStampBatch? _previous;
    private int _generation;
    private int _ownerThreadId;
    private bool _active;
    private bool _useLazyPinnedCursor;
    private Chunk? _pinnedChunk;
    private int _pinnedGeneration;
    private GCHandle[] _rowPins = Array.Empty<GCHandle>();
    private bool[] _rowPinAttempted = Array.Empty<bool>();
    private nint[] _rowPointers = Array.Empty<nint>();
    private nint[] _rowCursors = Array.Empty<nint>();
    private int[] _lastCursorSlots = Array.Empty<int>();
    private int _pinnedRouteCount;

    internal EntityRefStampBatch(QueryPlan queryPlan) => _queryPlan = queryPlan;

    internal int Generation => _generation;

    internal bool IsCurrentOnThisThread
        => _active
            && _ownerThreadId == Environment.CurrentManagedThreadId
            && ReferenceEquals(_current, this);

    internal static EntityRefStampBatch? GetCurrent(QueryPlan queryPlan)
    {
        for (EntityRefStampBatch? batch = _current; batch is not null; batch = batch._previous)
        {
            if (ReferenceEquals(batch._queryPlan, queryPlan) && batch._active)
            {
                return batch;
            }
        }

        return null;
    }

    internal void Begin(bool useLazyPinnedCursor)
    {
        ReleasePinnedRows();
        _generation = _generation == int.MaxValue ? 1 : _generation + 1;
        _ownerThreadId = Environment.CurrentManagedThreadId;
        _useLazyPinnedCursor = useLazyPinnedCursor;
        ReadOnlySpan<ArchetypePlan> plans = _queryPlan.MatchingPlans();
        for (int planIndex = 0; planIndex < plans.Length; planIndex++)
        {
            ref readonly ArchetypePlan plan = ref plans.RefAt(planIndex);
            plan.EntityRefStampState?.Begin(_generation, plan.ChunkCount * Chunk.Capacity);
        }

        _previous = _current;
        _active = true;
        _current = this;
    }

    internal void PrepareChunk(int generation, Chunk chunk, int routeCount)
    {
        if (!_useLazyPinnedCursor
            || !_active
            || generation != _generation)
        {
            return;
        }

        if (ReferenceEquals(_pinnedChunk, chunk) && _pinnedGeneration == generation)
        {
            return;
        }

        ReleasePinnedRows();
        EnsurePointerCapacity(routeCount);
        _pinnedChunk = chunk;
        _pinnedGeneration = generation;
        _pinnedRouteCount = routeCount;
        for (int route = 0; route < routeCount; route++)
        {
            _rowPinAttempted.RefAt(route) = false;
            _lastCursorSlots.RefAt(route) = -1;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryPinRow(int generation, int route, Chunk chunk, int componentIndex)
    {
        if (!_useLazyPinnedCursor
            || !_active
            || generation != _generation
            || _pinnedGeneration != generation
            || !ReferenceEquals(_pinnedChunk, chunk)
            || (uint)route >= (uint)_pinnedRouteCount
            || componentIndex < 0)
        {
            return false;
        }

        if (_rowPins.RefAt(route).IsAllocated)
        {
            return true;
        }

        if (_rowPinAttempted.RefAt(route))
        {
            return false;
        }

        _rowPinAttempted.RefAt(route) = true;
        if (chunk.ComponentRowContainsReferences(componentIndex))
        {
            return false;
        }

        GCHandle pin = GCHandle.Alloc(chunk.GetRawComponentRowTrusted(componentIndex), GCHandleType.Pinned);
        try
        {
            nint rowPointer = pin.AddrOfPinnedObject();
            _rowPins.RefAt(route) = pin;
            _rowPointers.RefAt(route) = rowPointer;
            _rowCursors.RefAt(route) = rowPointer;
            return true;
        }
        catch (ArgumentException)
        {
            pin.Free();
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal unsafe ref T GetPinnedReference<T>(int route, int slotIndex)
    {
        nint pointer = _rowCursors.RefAt(route);
        int previousSlot = _lastCursorSlots.RefAt(route);
        if (slotIndex != previousSlot + 1)
        {
            pointer = _rowPointers.RefAt(route) + slotIndex * Unsafe.SizeOf<T>();
        }

        _rowCursors.RefAt(route) = pointer + Unsafe.SizeOf<T>();
        _lastCursorSlots.RefAt(route) = slotIndex;
        return ref Unsafe.AsRef<T>((void*)pointer);
    }

    internal bool Mark(
        int generation,
        EntityRefStampState? state,
        int planEntityIndex,
        int route,
        int componentIndex)
        => _active
            && generation == _generation
            && _ownerThreadId == Environment.CurrentManagedThreadId
            && state is not null
            && state.Mark(generation, planEntityIndex, route, componentIndex);

    internal void Complete()
    {
        if (!IsCurrentOnThisThread)
        {
            return;
        }

        try
        {
            Flush();
        }
        finally
        {
            ClearPendingWrites();
            ReleasePinnedRows();
            _active = false;
            _ownerThreadId = 0;
            _current = _previous;
            _previous = null;
        }
    }

    private void EnsurePointerCapacity(int required)
    {
        if (_rowPins.Length >= required)
        {
            return;
        }

        Array.Resize(ref _rowPins, required);
        Array.Resize(ref _rowPinAttempted, required);
        Array.Resize(ref _rowPointers, required);
        Array.Resize(ref _rowCursors, required);
        Array.Resize(ref _lastCursorSlots, required);
    }

    private void ReleasePinnedRows()
    {
        for (int route = 0; route < _pinnedRouteCount; route++)
        {
            if (_rowPins.RefAt(route).IsAllocated)
            {
                _rowPins.RefAt(route).Free();
            }

            _rowPointers.RefAt(route) = 0;
            _rowCursors.RefAt(route) = 0;
            _rowPinAttempted.RefAt(route) = false;
            _lastCursorSlots.RefAt(route) = -1;
        }

        _pinnedChunk = null;
        _pinnedGeneration = 0;
        _pinnedRouteCount = 0;
    }

    internal static bool TryGetPending(
        World world,
        Chunk chunk,
        int componentIndex,
        int slotIndex,
        out int pendingCount)
    {
        int threadId = Environment.CurrentManagedThreadId;
        pendingCount = 0;
        bool foundPending = false;
        for (EntityRefStampBatch? batch = _current; batch is not null; batch = batch._previous)
        {
            if (!batch._active
                || batch._ownerThreadId != threadId
                || !ReferenceEquals(batch._queryPlan.Owner, world)
                || !batch._queryPlan.TryGetChunkPlan(chunk.ArchetypeId, chunk.GlobalId, out ChunkPlan chunkPlan)
                || chunkPlan.EntityRefStampState is not { } state
                || !state.TryGetPending(
                    componentIndex,
                    chunkPlan.PlanEntityBase + slotIndex,
                    out int batchPending))
            {
                continue;
            }

            pendingCount = unchecked(pendingCount + batchPending);
            foundPending = true;
        }

        return foundPending;
    }

    private void Flush()
    {
        ReadOnlySpan<ArchetypePlan> plans = _queryPlan.MatchingPlans();
        int routeCount = _queryPlan.EntityRefDataComponentCount;
        for (int planIndex = 0; planIndex < plans.Length; planIndex++)
        {
            ref readonly ArchetypePlan plan = ref plans.RefAt(planIndex);
            EntityRefStampState? state = plan.EntityRefStampState;
            if (state is null)
            {
                continue;
            }

            ReadOnlySpan<int> componentIndices = plan.EntityRefComponentIndices;
            long totalRows = CountRows(plan);
            for (int route = 0; route < routeCount; route++)
            {
                int componentIndex = componentIndices.RefAt(route);
                int writtenRows = state.WrittenRows(route);
                if (componentIndex < 0 || writtenRows == 0)
                {
                    continue;
                }

                int singleWriteRows = state.SingleWriteRows(route);
                long correctionRows = totalRows - singleWriteRows;
                if (writtenRows > correctionRows)
                {
                    _queryPlan.IncrementArchetypeComponentStamp(plan.Archetype.Id, componentIndex);
                    if (correctionRows != 0)
                    {
                        AdjustRowsFromArchetypeStamp(plan, state, route, componentIndex);
                    }

                    continue;
                }

                IncrementWrittenRows(plan, state, route, componentIndex);
            }
        }
    }

    private static long CountRows(in ArchetypePlan plan)
    {
        long count = 0;
        for (int chunkIndex = 0; chunkIndex < plan.ChunkCount; chunkIndex++)
        {
            count += plan.ChunkArray.RefAt(chunkIndex).Chunk.Count;
        }

        return count;
    }

    private static void AdjustRowsFromArchetypeStamp(
        in ArchetypePlan plan,
        EntityRefStampState state,
        int route,
        int componentIndex)
    {
        for (int chunkIndex = 0; chunkIndex < plan.ChunkCount; chunkIndex++)
        {
            ref readonly ChunkPlan chunkPlan = ref plan.ChunkArray.RefAt(chunkIndex);
            Chunk chunk = chunkPlan.Chunk;
            int planEntityBase = chunkPlan.PlanEntityBase;
            for (int slotIndex = 0; slotIndex < chunk.Count; slotIndex++)
            {
                int delta = state.WriteCount(route, planEntityBase + slotIndex) - 1;
                if (delta != 0)
                {
                    chunk.AdjustComponentStamp(componentIndex, slotIndex, delta);
                }
            }
        }
    }

    private static void IncrementWrittenRows(
        in ArchetypePlan plan,
        EntityRefStampState state,
        int route,
        int componentIndex)
        => state.AdjustWrittenRows(plan, route, componentIndex);

    private void ClearPendingWrites()
    {
        ReadOnlySpan<ArchetypePlan> plans = _queryPlan.MatchingPlans();
        for (int planIndex = 0; planIndex < plans.Length; planIndex++)
        {
            plans.RefAt(planIndex).EntityRefStampState?.Clear(_generation);
        }
    }
}

internal sealed class EntityRefStampState
{
    private readonly int[] _componentIndices;
    private ulong[] _writtenBits = Array.Empty<ulong>();
    private readonly int[] _writtenRowsByRoute;
    private readonly int[] _singleWriteRowsByRoute;
    private int[] _writeCounts = Array.Empty<int>();
    private int _entityCapacity;
    private int _wordsPerRoute;
    private int _generation;
    private bool _buffersReadyForCurrentCapacity;
    private bool _active;

    internal EntityRefStampState(int[] componentIndices)
    {
        _componentIndices = componentIndices;
        _writtenRowsByRoute = new int[componentIndices.Length];
        _singleWriteRowsByRoute = new int[componentIndices.Length];
    }

    internal void Begin(int generation, int entityCapacity)
    {
        _generation = generation;
        if (entityCapacity > _entityCapacity)
        {
            _entityCapacity = entityCapacity;
            _buffersReadyForCurrentCapacity = false;
        }

        _active = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Mark(int generation, int planEntityIndex, int route, int componentIndex)
    {
        if (!_active
            || generation != _generation
            || (uint)route >= (uint)_componentIndices.Length
            || _componentIndices.RefAt(route) != componentIndex
            || (uint)planEntityIndex >= (uint)_entityCapacity)
        {
            return false;
        }

        if (!_buffersReadyForCurrentCapacity)
        {
            EnsureWriteCountCapacity();
            EnsureWrittenBitCapacity();
            _buffersReadyForCurrentCapacity = true;
        }

        ref int writeCount = ref _writeCounts.RefAt(GetWriteCountIndex(route, planEntityIndex));
        if (writeCount == 0)
        {
            int bitIndex = GetWrittenBitIndex(route, planEntityIndex);
            _writtenBits.RefAt(bitIndex) |= 1UL << (planEntityIndex & 63);
            _writtenRowsByRoute.RefAt(route)++;
            _singleWriteRowsByRoute.RefAt(route)++;
        }
        else if (writeCount == 1)
        {
            _singleWriteRowsByRoute.RefAt(route)--;
        }

        writeCount++;
        return true;
    }

    internal bool TryGetPending(int componentIndex, int planEntityIndex, out int pendingCount)
    {
        for (int route = 0; route < _componentIndices.Length; route++)
        {
            if (_componentIndices.RefAt(route) != componentIndex
                || (uint)planEntityIndex >= (uint)_entityCapacity
                || _writeCounts.Length == 0
                || !_active)
            {
                continue;
            }

            pendingCount = _writeCounts.RefAt(GetWriteCountIndex(route, planEntityIndex));
            return pendingCount != 0;
        }

        pendingCount = 0;
        return false;
    }

    internal int WrittenRows(int route) => _writtenRowsByRoute.RefAt(route);

    internal int SingleWriteRows(int route) => _singleWriteRowsByRoute.RefAt(route);

    internal int WriteCount(int route, int entityIndex)
        => _writeCounts.Length == 0
            ? 0
            : _writeCounts.RefAt(GetWriteCountIndex(route, entityIndex));

    internal void Clear(int generation)
    {
        if (!_active || generation != _generation)
        {
            return;
        }

        for (int route = 0; route < _writtenRowsByRoute.Length; route++)
        {
            if (_writtenRowsByRoute.RefAt(route) == 0)
            {
                continue;
            }

            int bitBase = route * _wordsPerRoute;
            for (int wordIndex = 0; wordIndex < _wordsPerRoute; wordIndex++)
            {
                ref ulong word = ref _writtenBits.RefAt(bitBase + wordIndex);
                ulong bits = word;
                while (bits != 0)
                {
                    int bitIndex = BitOperationsCompat.TrailingZeroCount(bits);
                    int planEntityIndex = (wordIndex * 64) + bitIndex;
                    _writeCounts.RefAt(GetWriteCountIndex(route, planEntityIndex)) = 0;
                    bits &= bits - 1;
                }

                word = 0;
            }

            _writtenRowsByRoute.RefAt(route) = 0;
            _singleWriteRowsByRoute.RefAt(route) = 0;
        }

        _generation = 0;
        _active = false;
    }

    private void EnsureWriteCountCapacity()
    {
        int required = _componentIndices.Length * _entityCapacity;
        if (required > _writeCounts.Length)
        {
            Array.Resize(ref _writeCounts, required);
        }
    }

    private void EnsureWrittenBitCapacity()
    {
        int requiredWordsPerRoute = (_entityCapacity + 63) / 64;
        if (requiredWordsPerRoute > _wordsPerRoute)
        {
            _wordsPerRoute = requiredWordsPerRoute;
            Array.Resize(ref _writtenBits, _componentIndices.Length * _wordsPerRoute);
        }
    }

    internal void AdjustWrittenRows(in ArchetypePlan plan, int route, int componentIndex)
    {
        int bitBase = route * _wordsPerRoute;
        for (int wordIndex = 0; wordIndex < _wordsPerRoute; wordIndex++)
        {
            ulong bits = _writtenBits.RefAt(bitBase + wordIndex);
            while (bits != 0)
            {
                int bitIndex = BitOperationsCompat.TrailingZeroCount(bits);
                int planEntityIndex = (wordIndex * 64) + bitIndex;
                int chunkIndex = planEntityIndex / Chunk.Capacity;
                int slotIndex = planEntityIndex % Chunk.Capacity;
                Chunk chunk = plan.ChunkArray.RefAt(chunkIndex).Chunk;
                chunk.AdjustComponentStamp(componentIndex, slotIndex, WriteCount(route, planEntityIndex));
                bits &= bits - 1;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetWriteCountIndex(int route, int planEntityIndex)
        => (route * _entityCapacity) + planEntityIndex;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetWrittenBitIndex(int route, int planEntityIndex)
        => (route * _wordsPerRoute) + (planEntityIndex >> 6);
}
