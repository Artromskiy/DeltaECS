namespace Delta.ECS;

using System;
using System.Collections.Generic;

internal sealed class EntityRefStampBatch
{
    [ThreadStatic]
    private static EntityRefStampBatch? _current;

    private readonly QueryPlan _queryPlan;
    private readonly Dictionary<int, ChunkStampMark> _marksByChunkId = new();
    private readonly List<ChunkStampMark> _markPool = new();
    private EntityRefStampBatch? _previous;
    private int _generation;
    private int _markCount;
    private int _ownerThreadId;
    private bool _active;

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

    internal void Begin()
    {
        _generation = _generation == int.MaxValue ? 1 : _generation + 1;
        _ownerThreadId = Environment.CurrentManagedThreadId;
        _markCount = 0;
        _marksByChunkId.Clear();

        ReadOnlySpan<ChunkPlan> chunkPlans = _queryPlan.MatchingChunkPlans();
        for (int index = 0; index < chunkPlans.Length; index++)
        {
            ref readonly ChunkPlan chunkPlan = ref chunkPlans.RefAt(index);
            ChunkStampMark mark;
            if (_markCount == _markPool.Count)
            {
                mark = new ChunkStampMark(_queryPlan.EntityRefDataComponentCount);
                _markPool.Add(mark);
            }
            else
            {
                mark = _markPool[_markCount];
            }

            mark.Reset(chunkPlan.Chunk, chunkPlan.EntityRefComponentIndices);
            _marksByChunkId.Add(chunkPlan.Chunk.GlobalId, mark);
            _markCount++;
        }

        _previous = _current;
        _active = true;
        _current = this;
    }

    internal ChunkStampMark? GetMark(Chunk chunk)
        => _active
            && _ownerThreadId == Environment.CurrentManagedThreadId
            && _marksByChunkId.TryGetValue(chunk.GlobalId, out ChunkStampMark? mark)
            && ReferenceEquals(mark.Chunk, chunk)
                ? mark
                : null;

    internal bool Mark(
        int generation,
        ChunkStampMark? mark,
        Chunk chunk,
        int route,
        int componentIndex,
        int slotIndex)
        => _active
            && generation == _generation
            && _ownerThreadId == Environment.CurrentManagedThreadId
            && mark is not null
            && ReferenceEquals(mark.Chunk, chunk)
            && mark.Mark(route, componentIndex, slotIndex);

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
            for (int index = 0; index < _markCount; index++)
            {
                _markPool[index].Clear();
            }

            _markCount = 0;
            _marksByChunkId.Clear();
            _active = false;
            _ownerThreadId = 0;
            _current = _previous;
            _previous = null;
        }
    }

    internal static bool TryGetPending(World world, Chunk chunk, int componentIndex, int slotIndex, out int pendingCount)
    {
        int threadId = Environment.CurrentManagedThreadId;
        pendingCount = 0;
        bool foundPending = false;
        for (EntityRefStampBatch? batch = _current; batch is not null; batch = batch._previous)
        {
            if (!batch._active
                || batch._ownerThreadId != threadId
                || !ReferenceEquals(batch._queryPlan.Owner, world)
                || !batch._marksByChunkId.TryGetValue(chunk.GlobalId, out ChunkStampMark? mark)
                || !ReferenceEquals(mark.Chunk, chunk))
            {
                continue;
            }

            if (mark.TryGetPending(componentIndex, slotIndex, out int batchPending))
            {
                pendingCount = unchecked(pendingCount + batchPending);
                foundPending = true;
            }
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
            ReadOnlySpan<int> componentIndices = plan.EntityRefComponentIndices;
            for (int route = 0; route < routeCount; route++)
            {
                int componentIndex = componentIndices.RefAt(route);
                if (componentIndex < 0)
                {
                    continue;
                }

                long totalRows = 0;
                long writtenRows = 0;
                long singleWriteRows = 0;
                for (int chunkIndex = 0; chunkIndex < plan.ChunkCount; chunkIndex++)
                {
                    Chunk chunk = plan.ChunkArray.RefAt(chunkIndex).Chunk;
                    ChunkStampMark mark = _marksByChunkId[chunk.GlobalId];
                    totalRows += chunk.Count;
                    writtenRows += mark.WrittenRows(route);
                    singleWriteRows += mark.SingleWriteRows(route);
                }

                if (writtenRows == 0)
                {
                    continue;
                }

                long correctionRows = totalRows - singleWriteRows;
                if (writtenRows > correctionRows)
                {
                    _queryPlan.IncrementArchetypeComponentStamp(plan.Archetype.Id, componentIndex);
                    AdjustRowsFromArchetypeStamp(plan, route, componentIndex);
                    continue;
                }

                IncrementWrittenRows(plan, route, componentIndex);
            }
        }
    }

    private void AdjustRowsFromArchetypeStamp(ArchetypePlan plan, int route, int componentIndex)
    {
        for (int chunkIndex = 0; chunkIndex < plan.ChunkCount; chunkIndex++)
        {
            Chunk chunk = plan.ChunkArray.RefAt(chunkIndex).Chunk;
            ChunkStampMark mark = _marksByChunkId[chunk.GlobalId];
            for (int slotIndex = 0; slotIndex < chunk.Count; slotIndex++)
            {
                int delta = mark.WriteCount(route, slotIndex) - 1;
                if (delta != 0)
                {
                    chunk.AdjustComponentStamp(componentIndex, slotIndex, delta);
                }
            }
        }
    }

    private void IncrementWrittenRows(ArchetypePlan plan, int route, int componentIndex)
    {
        for (int chunkIndex = 0; chunkIndex < plan.ChunkCount; chunkIndex++)
        {
            Chunk chunk = plan.ChunkArray.RefAt(chunkIndex).Chunk;
            ChunkStampMark mark = _marksByChunkId[chunk.GlobalId];
            mark.IncrementWrittenRows(chunk, route, componentIndex);
        }
    }

    internal sealed class ChunkStampMark
    {
        private readonly int[][] _writeCountsByRoute;
        private readonly ulong[][] _writtenSlotsByRoute;
        private readonly int[] _writtenRowsByRoute;
        private readonly int[] _singleWriteRowsByRoute;
        private int[] _componentIndices = Array.Empty<int>();

        internal ChunkStampMark(int routeCount)
        {
            _writeCountsByRoute = new int[routeCount][];
            _writtenSlotsByRoute = new ulong[routeCount][];
            _writtenRowsByRoute = new int[routeCount];
            _singleWriteRowsByRoute = new int[routeCount];
        }

        internal Chunk? Chunk { get; private set; }

        internal void Reset(Chunk chunk, int[] componentIndices)
        {
            Chunk = chunk;
            _componentIndices = componentIndices;
        }

        internal bool Mark(int route, int componentIndex, int slotIndex)
        {
            if ((uint)route >= (uint)_componentIndices.Length
                || _componentIndices.RefAt(route) != componentIndex
                || (uint)slotIndex >= (uint)Chunk!.Count)
            {
                return false;
            }

            int[]? counts = _writeCountsByRoute[route];
            if (counts is null)
            {
                counts = new int[Delta.ECS.Chunk.Capacity];
                _writeCountsByRoute[route] = counts;
                _writtenSlotsByRoute[route] = new ulong[(Delta.ECS.Chunk.Capacity + 63) / 64];
            }

            ref int writeCount = ref counts.RefAt(slotIndex);
            if (writeCount == 0)
            {
                _writtenSlotsByRoute[route][slotIndex >> 6] |= 1UL << (slotIndex & 63);
                _writtenRowsByRoute[route]++;
                _singleWriteRowsByRoute[route]++;
            }
            else if (writeCount == 1)
            {
                _singleWriteRowsByRoute[route]--;
            }

            writeCount++;
            return true;
        }

        internal int WrittenRows(int route) => _writtenRowsByRoute.RefAt(route);

        internal int SingleWriteRows(int route) => _singleWriteRowsByRoute.RefAt(route);

        internal int WriteCount(int route, int slotIndex)
            => _writeCountsByRoute.RefAt(route) is { } counts
                ? counts.RefAt(slotIndex)
                : 0;

        internal bool TryGetPending(int componentIndex, int slotIndex, out int pendingCount)
        {
            if (Chunk is not null && (uint)slotIndex < (uint)Chunk.Count)
            {
                for (int route = 0; route < _componentIndices.Length; route++)
                {
                    if (_componentIndices.RefAt(route) != componentIndex
                        || _writeCountsByRoute.RefAt(route) is not { } counts)
                    {
                        continue;
                    }

                    pendingCount = counts.RefAt(slotIndex);
                    return pendingCount != 0;
                }
            }

            pendingCount = 0;
            return false;
        }

        internal void IncrementWrittenRows(Chunk chunk, int route, int componentIndex)
        {
            if (_writeCountsByRoute.RefAt(route) is not { } counts
                || _writtenSlotsByRoute.RefAt(route) is not { } writtenSlots)
            {
                return;
            }

            for (int wordIndex = 0; wordIndex < writtenSlots.Length; wordIndex++)
            {
                ulong bits = writtenSlots.RefAt(wordIndex);
                while (bits != 0)
                {
                    int bitIndex = BitOperationsCompat.TrailingZeroCount(bits);
                    int slotIndex = (wordIndex * 64) + bitIndex;
                    chunk.AdjustComponentStamp(componentIndex, slotIndex, counts.RefAt(slotIndex));
                    bits &= bits - 1;
                }
            }
        }

        internal void Clear()
        {
            for (int route = 0; route < _writeCountsByRoute.Length; route++)
            {
                if (_writeCountsByRoute.RefAt(route) is not { } counts
                    || _writtenSlotsByRoute.RefAt(route) is not { } writtenSlots)
                {
                    continue;
                }

                for (int wordIndex = 0; wordIndex < writtenSlots.Length; wordIndex++)
                {
                    ulong bits = writtenSlots.RefAt(wordIndex);
                    while (bits != 0)
                    {
                        int bitIndex = BitOperationsCompat.TrailingZeroCount(bits);
                        int slotIndex = (wordIndex * 64) + bitIndex;
                        counts.RefAt(slotIndex) = 0;
                        bits &= bits - 1;
                    }

                    writtenSlots.RefAt(wordIndex) = 0;
                }

                _writtenRowsByRoute.RefAt(route) = 0;
                _singleWriteRowsByRoute.RefAt(route) = 0;
            }

            Chunk = null;
            _componentIndices = Array.Empty<int>();
        }
    }

}
