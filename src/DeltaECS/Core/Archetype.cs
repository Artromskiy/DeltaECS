namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

internal sealed class Archetype
{
    private readonly int _id;
    private readonly int _chunkCapacity;
    private NativeMemory<ComponentId> _componentIds;
    private readonly ComponentLayout[] _layouts;
    private readonly ComponentRowOperations[] _rowOperations;
    private readonly List<Chunk> _chunks = new();
    private readonly List<int> _availableChunkStack = new();
    private NativeMemory<bool> _availableChunkFlags = new(0);
    private NativeMemory<int> _activeChunkIndices = new(0);
    private Chunk[] _activeChunks = Array.Empty<Chunk>();
    private NativeMemory<int> _activeChunkPositions = new(0);
    private readonly List<QueryPlanLink> _queryPlans = new();
    private int _activeChunkCount;

    internal Archetype(
        int id,
        ComponentMask mask,
        ComponentLayout[] layouts,
        ComponentRowOperations[] rowOperations,
        ComponentId[] componentIds,
        int chunkCapacity)
    {
        if (layouts.Length != componentIds.Length
            || rowOperations.Length != componentIds.Length)
        {
            ThrowHelper.ThrowArchetypeLayoutMismatch();
        }

        _id = id;
        Mask = mask;
        _chunkCapacity = chunkCapacity;
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

    internal ref readonly ComponentLayout GetLayout(int index) => ref _layouts[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool HasAvailableChunk() => _availableChunkStack.Count != 0;

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
        _chunks.Add(new Chunk(_chunkCapacity, _layouts, _rowOperations, chunkId));
        EnsureAvailableChunkCapacity(chunkIndex);
        _activeChunkPositions[chunkIndex] = -1;
        slotIndex = _chunks[chunkIndex].Add(entity, out reusedSlot);
        ActivateChunk(chunkIndex);
        if (!_chunks[chunkIndex].IsFull)
        {
            PushAvailableChunk(chunkIndex);
        }
    }

    internal int ReserveRange(int count, int chunkId, out int chunkIndex, out Chunk chunk, out int reusedCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        if (TryTakeAvailableChunk(out int availableIndex, out var available))
        {
            chunkIndex = availableIndex;
            chunk = available;
        }
        else
        {
            chunkIndex = _chunks.Count;
            chunk = new Chunk(_chunkCapacity, _layouts, _rowOperations, chunkId);
            _chunks.Add(chunk);
            EnsureAvailableChunkCapacity(chunkIndex);
            _activeChunkPositions[chunkIndex] = -1;
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
            _availableChunkFlags[chunkIndex] = false;
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
        if (_availableChunkFlags[chunkIndex] || _chunks[chunkIndex].IsFull)
        {
            return;
        }

        _availableChunkFlags[chunkIndex] = true;
        _availableChunkStack.Add(chunkIndex);
    }

    private void EnsureAvailableChunkCapacity(int chunkIndex)
    {
        if (chunkIndex >= _availableChunkFlags.Length)
        {
            int capacity = Math.Max(chunkIndex + 1, _availableChunkFlags.Length == 0 ? 4 : _availableChunkFlags.Length * 2);
            _availableChunkFlags.Resize(capacity);
            _activeChunkPositions.Resize(capacity);
        }
    }

    private void ActivateChunk(int chunkIndex)
    {
        if (_activeChunkPositions[chunkIndex] >= 0)
        {
            return;
        }

        if (_activeChunkCount == _activeChunkIndices.Length)
        {
            int capacity = Math.Max(4, _activeChunkIndices.Length * 2);
            _activeChunkIndices.Resize(capacity);
            Array.Resize(ref _activeChunks, capacity);
        }

        _activeChunkPositions[chunkIndex] = _activeChunkCount;
        _activeChunkIndices[_activeChunkCount] = chunkIndex;
        int activePosition = _activeChunkCount;
        var chunk = _chunks[chunkIndex];
        _activeChunks[_activeChunkCount++] = chunk;
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
        int position = _activeChunkPositions[chunkIndex];
        if (position < 0)
        {
            return;
        }

        int lastPosition = --_activeChunkCount;
        int movedChunkIndex = _activeChunkIndices[lastPosition];
        if (position != lastPosition)
        {
            _activeChunkIndices[position] = movedChunkIndex;
            _activeChunks[position] = _activeChunks[lastPosition];
            _activeChunkPositions[movedChunkIndex] = position;
        }

        _activeChunkIndices[lastPosition] = -1;
        _activeChunks[lastPosition] = null!;
        _activeChunkPositions[chunkIndex] = -1;
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
    internal Chunk GetActiveChunk(int activeIndex) => _activeChunks[activeIndex];

    internal void Dispose()
    {
        for (int index = 0; index < _chunks.Count; index++)
        {
            _chunks[index].Dispose();
        }

        _componentIds.Dispose();
        _availableChunkFlags.Dispose();
        _activeChunkIndices.Dispose();
        _activeChunkPositions.Dispose();
        _queryPlans.Clear();
    }
}
