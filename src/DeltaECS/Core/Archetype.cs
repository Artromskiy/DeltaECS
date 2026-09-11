namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

internal sealed class Archetype
{
    private readonly int _id;
    private readonly int _chunkCapacity;
    private readonly ComponentRowArrayPool _componentRowArrayPool;
    private NativeMemory<ComponentId> _componentIds;
    private readonly ComponentLayout[] _layouts;
    private readonly ComponentRowOperations[] _rowOperations;
    private readonly List<Chunk> _chunks = new();
    private readonly List<int> _availableChunkStack = new();
    private readonly List<int> _blockPartialChunkCandidates = new();
    private readonly List<int> _blockEmptyChunkCandidates = new();
    private NativeMemory<bool> _availableChunkFlags = new(0);
    private NativeMemory<int> _availableChunkPositions = new(0);
    private NativeMemory<int> _activeChunkIndices = new(0);
    private Chunk[] _activeChunks = Array.Empty<Chunk>();
    private NativeMemory<int> _activeChunkPositions = new(0);
    private readonly List<QueryPlanLink> _queryPlans = new();
    private int _activeChunkCount;
    private bool _deferQueryPlanUpdates;
    private int _blockPartialCandidateCount;
    private int _blockEmptyCandidateCount;

    internal Archetype(
        int id,
        ComponentMask mask,
        ComponentLayout[] layouts,
        ComponentRowOperations[] rowOperations,
        ComponentId[] componentIds,
        int chunkCapacity,
        ComponentRowArrayPool componentRowArrayPool)
    {
        if (layouts.Length != componentIds.Length
            || rowOperations.Length != componentIds.Length)
        {
            ThrowHelper.ThrowArchetypeLayoutMismatch();
        }

        _id = id;
        Mask = mask;
        _chunkCapacity = chunkCapacity;
        _componentRowArrayPool = componentRowArrayPool;
        _componentIds = new NativeMemory<ComponentId>(componentIds);
        _layouts = layouts;
        _rowOperations = rowOperations;
    }

    internal int Id => _id;

    internal ComponentMask Mask { get; }

    internal ReadOnlySpan<ComponentId> ComponentIds => _componentIds.ReadOnlySpan;

    internal int ComponentCount => _componentIds.Length;

    internal int ChunkCount => _chunks.Count;

    internal int ActiveChunkCount => _activeChunkCount;

    internal ReadOnlySpan<Chunk> ActiveChunks => _activeChunks.AsSpan(0, _activeChunkCount);

    internal void Attach(QueryPlan query, int planIndex)
    {
        CompactDeadQueryPlanLinks();
        _queryPlans.Add(new QueryPlanLink(query.WeakReference, planIndex));
    }

    internal void DeferQueryPlanUpdates() => _deferQueryPlanUpdates = true;

    internal void RefreshQueryPlans()
    {
        if (!_deferQueryPlanUpdates)
        {
            return;
        }

        _deferQueryPlanUpdates = false;
        for (int index = 0; index < _queryPlans.Count;)
        {
            QueryPlanLink link = _queryPlans[index];
            if (link.Query.TryGetTarget(out QueryPlan? query))
            {
                query.RefreshArchetype(link.PlanIndex, this);
                index++;
            }
            else
            {
                RemoveQueryPlanLink(index);
            }
        }
    }

    internal int EntityCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _chunks.Count; i++)
            {
                count += _chunks[i].Count;
            }

            return count;
        }
    }

    internal bool Contains(ComponentId componentId) => Mask.Contains(componentId);

    internal bool TryGetComponentIndex(ComponentId componentId, out int index)
    {
        index = Mask.Rank(componentId);
        return index >= 0;
    }

    internal ref readonly ComponentLayout GetLayout(int index) => ref _layouts.RefAt(index);

    internal ComponentLayout[] Layouts => _layouts;

    internal ComponentRowOperations[] RowOperations => _rowOperations;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool HasAvailableChunk() => _availableChunkStack.Count != 0;

    internal bool TryPeekAvailableChunk(out Chunk chunk)
    {
        for (int stackIndex = _availableChunkStack.Count - 1; stackIndex >= 0; stackIndex--)
        {
            Chunk candidate = _chunks[_availableChunkStack[stackIndex]];
            if (!candidate.IsFull)
            {
                chunk = candidate;
                return true;
            }
        }

        chunk = null!;
        return false;
    }

    internal void AddEntity(
        Entity entity,
        int chunkId,
        out int chunkIndex,
        out int slotIndex,
        out bool reusedSlot)
    {
        if (TryTakeAvailableChunk(out int availableIndex, out var available))
        {
            chunkIndex = availableIndex;
            bool wasEmpty = available.IsEmpty;
            slotIndex = available.Add(entity, out reusedSlot);
            if (wasEmpty)
            {
                ActivateChunk(chunkIndex);
            }

            if (!available.IsFull)
            {
                PushAvailableChunk(chunkIndex);
            }

            return;
        }

        chunkIndex = _chunks.Count;
        _chunks.Add(new Chunk(
            _chunkCapacity,
            _layouts,
            _rowOperations,
            chunkId,
            _id,
            chunkIndex,
            _componentRowArrayPool));
        EnsureAvailableChunkCapacity(chunkIndex);
        _activeChunkPositions.RefAt(chunkIndex) = -1;
        _availableChunkPositions.RefAt(chunkIndex) = -1;
        slotIndex = _chunks[chunkIndex].Add(entity, out reusedSlot);
        ActivateChunk(chunkIndex);
        if (!_chunks[chunkIndex].IsFull)
        {
            PushAvailableChunk(chunkIndex);
        }
    }

    internal int ReserveRange(int count, int chunkId, out int chunkIndex, out Chunk chunk, out int reusedCount)
    {
        ThrowHelper.ThrowIfNegativeOrZero(count, nameof(count));

        if (TryTakeAvailableChunk(out int availableIndex, out var available))
        {
            chunkIndex = availableIndex;
            chunk = available;
        }
        else
        {
            chunkIndex = _chunks.Count;
            chunk = new Chunk(
                _chunkCapacity,
                _layouts,
                _rowOperations,
                chunkId,
                _id,
                chunkIndex,
                _componentRowArrayPool);
            _chunks.Add(chunk);
            EnsureAvailableChunkCapacity(chunkIndex);
            _activeChunkPositions.RefAt(chunkIndex) = -1;
            _availableChunkPositions.RefAt(chunkIndex) = -1;
        }

        bool wasEmpty = chunk.IsEmpty;
        int reserved = Math.Min(count, chunk.Capacity - chunk.Count);
        chunk.ReserveRange(reserved, out reusedCount);
        if (wasEmpty && reserved > 0)
        {
            ActivateChunk(chunkIndex);
        }

        if (!chunk.IsFull)
        {
            PushAvailableChunk(chunkIndex);
        }

        return reserved;
    }

    internal Entity RemoveEntity(int chunkIndex, int slotIndex)
    {
        var chunk = _chunks[chunkIndex];
        var moved = chunk.RemoveSwapBack(slotIndex);
        if (chunk.IsEmpty)
        {
            DeactivateChunk(chunkIndex);
        }

        PushAvailableChunk(chunkIndex);
        return moved;
    }

    internal bool TryGetLastActiveChunk(out int chunkIndex, out Chunk chunk)
    {
        if (_activeChunkCount == 0)
        {
            chunkIndex = -1;
            chunk = null!;
            return false;
        }

        int activePosition = _activeChunkCount - 1;
        chunkIndex = _activeChunkIndices.RefAt(activePosition);
        chunk = _activeChunks.RefAt(activePosition);
        return true;
    }

    internal void PrepareBlockMoveCandidates(int maximumChunkIndex)
    {
        _blockPartialChunkCandidates.Clear();
        _blockEmptyChunkCandidates.Clear();
        for (int stackIndex = _availableChunkStack.Count - 1; stackIndex >= 0; stackIndex--)
        {
            int candidateIndex = _availableChunkStack[stackIndex];
            if (candidateIndex >= maximumChunkIndex)
            {
                continue;
            }

            var candidate = _chunks[candidateIndex];
            if (candidate.IsEmpty)
            {
                _blockEmptyChunkCandidates.Add(candidateIndex);
                continue;
            }

            if (!candidate.IsFull)
            {
                _blockPartialChunkCandidates.Add(candidateIndex);
            }
        }

        _blockPartialCandidateCount = _blockPartialChunkCandidates.Count;
        _blockEmptyCandidateCount = _blockEmptyChunkCandidates.Count;
    }

    internal bool TryTakeBlockPartialChunk(out Chunk chunk)
    {
        while (_blockPartialCandidateCount > 0)
        {
            int candidateIndex = _blockPartialChunkCandidates[--_blockPartialCandidateCount];
            var candidate = _chunks[candidateIndex];
            if (candidate.IsEmpty || candidate.IsFull)
            {
                continue;
            }

            chunk = candidate;
            return true;
        }

        chunk = null!;
        return false;
    }

    internal void RequeueBlockPartialChunk(Chunk chunk)
    {
        if (_blockPartialCandidateCount == _blockPartialChunkCandidates.Count)
        {
            _blockPartialChunkCandidates.Add(chunk.ArchetypeIndex);
        }
        else
        {
            _blockPartialChunkCandidates[_blockPartialCandidateCount] = chunk.ArchetypeIndex;
        }

        _blockPartialCandidateCount++;
    }

    internal bool TryTakeBlockEmptyChunk(out int chunkIndex, out Chunk chunk)
    {
        while (_blockEmptyCandidateCount > 0)
        {
            chunkIndex = _blockEmptyChunkCandidates[--_blockEmptyCandidateCount];
            chunk = _chunks[chunkIndex];
            if (chunk.IsEmpty)
            {
                return true;
            }
        }

        chunkIndex = -1;
        chunk = null!;
        return false;
    }

    internal int ReserveExistingChunk(Chunk chunk, int count, out int slotIndex)
    {
        int chunkIndex = chunk.ArchetypeIndex;
        if ((uint)chunkIndex >= (uint)_chunks.Count || !ReferenceEquals(_chunks[chunkIndex], chunk))
        {
            ThrowHelper.ThrowInvalidChunkLocation();
        }

        bool wasEmpty = chunk.IsEmpty;
        slotIndex = chunk.ReserveRange(count, out _);
        if (wasEmpty && count > 0)
        {
            ActivateChunk(chunkIndex);
        }

        if (chunk.IsFull)
        {
            RemoveAvailableChunk(chunkIndex);
        }

        return count;
    }

    internal int AttachAdoptedChunk(Chunk chunk)
    {
        int chunkIndex = _chunks.Count;
        _chunks.Add(chunk);
        EnsureAvailableChunkCapacity(chunkIndex);
        _activeChunkPositions.RefAt(chunkIndex) = -1;
        _availableChunkPositions.RefAt(chunkIndex) = -1;
        _availableChunkFlags.RefAt(chunkIndex) = false;
        chunk.SetArchetypeLocation(_id, chunkIndex);
        if (!chunk.IsEmpty)
        {
            ActivateChunk(chunkIndex);
        }

        if (!chunk.IsFull)
        {
            PushAvailableChunk(chunkIndex);
        }

        return chunkIndex;
    }

    internal Chunk ReplaceEmptyChunk(int chunkIndex, Chunk adopted)
    {
        if ((uint)chunkIndex >= (uint)_chunks.Count)
        {
            ThrowHelper.ThrowInvalidChunkLocation();
        }

        Chunk donor = _chunks[chunkIndex];
        if (!donor.IsEmpty || adopted.IsEmpty)
        {
            ThrowHelper.ThrowInvalidChunkLocation();
        }

        RemoveAvailableChunk(chunkIndex);
        _activeChunkPositions.RefAt(chunkIndex) = -1;
        _chunks[chunkIndex] = adopted;
        adopted.SetArchetypeLocation(_id, chunkIndex);
        ActivateChunk(chunkIndex);
        if (!adopted.IsFull)
        {
            PushAvailableChunk(chunkIndex);
        }

        donor.SetArchetypeLocation(-1, -1);
        return donor;
    }

    internal Chunk DetachChunk(int chunkIndex)
    {
        if ((uint)chunkIndex >= (uint)_chunks.Count)
        {
            ThrowHelper.ThrowInvalidChunkLocation();
        }

        Chunk detached = _chunks[chunkIndex];
        RemoveAvailableChunk(chunkIndex);
        if (_activeChunkPositions.RefAt(chunkIndex) >= 0)
        {
            DeactivateChunk(chunkIndex);
        }

        int lastIndex = _chunks.Count - 1;
        if (chunkIndex != lastIndex)
        {
            Chunk replacement = _chunks[lastIndex];
            bool replacementAvailable = _availableChunkFlags.RefAt(lastIndex);
            int replacementActivePosition = _activeChunkPositions.RefAt(lastIndex);
            RemoveAvailableChunk(lastIndex);

            _chunks[chunkIndex] = replacement;
            replacement.SetArchetypeLocation(_id, chunkIndex);
            _availableChunkFlags.RefAt(lastIndex) = false;
            _availableChunkPositions.RefAt(lastIndex) = -1;
            _activeChunkPositions.RefAt(lastIndex) = -1;
            _availableChunkFlags.RefAt(chunkIndex) = false;
            _availableChunkPositions.RefAt(chunkIndex) = -1;
            _activeChunkPositions.RefAt(chunkIndex) = replacementActivePosition;
            if (replacementActivePosition >= 0)
            {
                _activeChunkIndices.RefAt(replacementActivePosition) = chunkIndex;
            }

            if (replacementAvailable)
            {
                PushAvailableChunk(chunkIndex);
            }
        }
        else
        {
            _availableChunkFlags.RefAt(chunkIndex) = false;
            _availableChunkPositions.RefAt(chunkIndex) = -1;
            _activeChunkPositions.RefAt(chunkIndex) = -1;
        }

        _chunks.RemoveAt(lastIndex);
        detached.SetArchetypeLocation(-1, -1);
        return detached;
    }

    internal void ReleaseChunk(int chunkIndex)
    {
        if (_chunks[chunkIndex].IsEmpty)
        {
            DeactivateChunk(chunkIndex);
        }

        PushAvailableChunk(chunkIndex);
    }

    private bool TryTakeAvailableChunk(out int chunkIndex, out Chunk chunk)
    {
        while (_availableChunkStack.Count != 0)
        {
            int stackIndex = _availableChunkStack.Count - 1;
            chunkIndex = _availableChunkStack[stackIndex];
            _availableChunkStack.RemoveAt(stackIndex);
            _availableChunkFlags.RefAt(chunkIndex) = false;
            _availableChunkPositions.RefAt(chunkIndex) = -1;
            chunk = _chunks[chunkIndex];
            if (!chunk.IsFull)
            {
                return true;
            }
        }

        chunkIndex = -1;
        chunk = null!;
        return false;
    }

    private void PushAvailableChunk(int chunkIndex)
    {
        if (_availableChunkFlags.RefAt(chunkIndex) || _chunks[chunkIndex].IsFull)
        {
            return;
        }

        _availableChunkFlags.RefAt(chunkIndex) = true;
        _availableChunkPositions.RefAt(chunkIndex) = _availableChunkStack.Count;
        _availableChunkStack.Add(chunkIndex);
    }

    private void RemoveAvailableChunk(int chunkIndex)
    {
        if (!_availableChunkFlags.RefAt(chunkIndex))
        {
            return;
        }

        int stackIndex = _availableChunkPositions.RefAt(chunkIndex);
        if ((uint)stackIndex >= (uint)_availableChunkStack.Count)
        {
            _availableChunkFlags.RefAt(chunkIndex) = false;
            _availableChunkPositions.RefAt(chunkIndex) = -1;
            return;
        }

        int lastStackIndex = _availableChunkStack.Count - 1;
        if (stackIndex != lastStackIndex)
        {
            int movedChunkIndex = _availableChunkStack[lastStackIndex];
            _availableChunkStack[stackIndex] = movedChunkIndex;
            _availableChunkPositions.RefAt(movedChunkIndex) = stackIndex;
        }

        _availableChunkStack.RemoveAt(lastStackIndex);
        _availableChunkFlags.RefAt(chunkIndex) = false;
        _availableChunkPositions.RefAt(chunkIndex) = -1;
    }

    private void EnsureAvailableChunkCapacity(int chunkIndex)
    {
        if (chunkIndex >= _availableChunkFlags.Length)
        {
            int capacity = Math.Max(chunkIndex + 1, _availableChunkFlags.Length == 0 ? 4 : _availableChunkFlags.Length * 2);
            _availableChunkFlags.Resize(capacity);
            _availableChunkPositions.Resize(capacity);
            _activeChunkPositions.Resize(capacity);
        }
    }

    private void ActivateChunk(int chunkIndex)
    {
        if (_activeChunkPositions.RefAt(chunkIndex) >= 0)
        {
            return;
        }

        if (_activeChunkCount == _activeChunkIndices.Length)
        {
            int capacity = Math.Max(4, _activeChunkIndices.Length * 2);
            _activeChunkIndices.Resize(capacity);
            Array.Resize(ref _activeChunks, capacity);
        }

        _activeChunkPositions.RefAt(chunkIndex) = _activeChunkCount;
        _activeChunkIndices.RefAt(_activeChunkCount) = chunkIndex;
        int activePosition = _activeChunkCount;
        var chunk = _chunks[chunkIndex];
        _activeChunks.RefAt(_activeChunkCount++) = chunk;
        if (_deferQueryPlanUpdates)
        {
            return;
        }

        for (int index = 0; index < _queryPlans.Count;)
        {
            QueryPlanLink link = _queryPlans[index];
            if (link.Query.TryGetTarget(out QueryPlan? query))
            {
                query.OnChunkActivated(link.PlanIndex, chunk, activePosition);
                index++;
            }
            else
            {
                RemoveQueryPlanLink(index);
            }
        }
    }

    private void DeactivateChunk(int chunkIndex)
    {
        int position = _activeChunkPositions.RefAt(chunkIndex);
        if (position < 0)
        {
            return;
        }

        int lastPosition = --_activeChunkCount;
        int movedChunkIndex = _activeChunkIndices.RefAt(lastPosition);
        if (position != lastPosition)
        {
            _activeChunkIndices.RefAt(position) = movedChunkIndex;
            _activeChunks.RefAt(position) = _activeChunks.RefAt(lastPosition);
            _activeChunkPositions.RefAt(movedChunkIndex) = position;
        }

        _activeChunkIndices.RefAt(lastPosition) = -1;
        _activeChunks.RefAt(lastPosition) = null!;
        _activeChunkPositions.RefAt(chunkIndex) = -1;
        if (_deferQueryPlanUpdates)
        {
            return;
        }

        for (int index = 0; index < _queryPlans.Count;)
        {
            QueryPlanLink link = _queryPlans[index];
            if (link.Query.TryGetTarget(out QueryPlan? query))
            {
                query.OnChunkDeactivated(link.PlanIndex, position, lastPosition);
                index++;
            }
            else
            {
                RemoveQueryPlanLink(index);
            }
        }
    }

    private void RemoveQueryPlanLink(int index)
    {
        int lastIndex = _queryPlans.Count - 1;
        if (index != lastIndex)
        {
            _queryPlans[index] = _queryPlans[lastIndex];
        }

        _queryPlans.RemoveAt(lastIndex);
    }

    private void CompactDeadQueryPlanLinks()
    {
        for (int index = 0; index < _queryPlans.Count;)
        {
            if (_queryPlans[index].Query.TryGetTarget(out _))
            {
                index++;
            }
            else
            {
                RemoveQueryPlanLink(index);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetChunkGlobalId(int chunkIndex) => _chunks[chunkIndex].GlobalId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Chunk GetChunk(int chunkIndex) => _chunks[chunkIndex];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Chunk GetActiveChunk(int activeIndex) => _activeChunks.RefAt(activeIndex);

    internal void Dispose()
    {
        for (int index = 0; index < _chunks.Count; index++)
        {
            _chunks[index].Dispose();
        }

        _componentIds.Dispose();
        _availableChunkFlags.Dispose();
        _availableChunkPositions.Dispose();
        _activeChunkIndices.Dispose();
        _activeChunkPositions.Dispose();
        _queryPlans.Clear();
    }
}
