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
    private int _activeChunkCount;

    public Archetype(
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
            throw new ArgumentException("Archetype must have matching component and layout arrays.");
        }

        _id = id;
        Mask = mask;
        _chunkCapacity = chunkCapacity;
        _componentIds = new NativeMemory<ComponentId>(componentIds);
        _layouts = layouts;
        _rowOperations = rowOperations;
    }

    public int Id => _id;

    public ComponentMask Mask { get; }

    public ReadOnlySpan<ComponentId> ComponentIds => _componentIds.ReadOnlySpan;

    public int ComponentCount => _componentIds.Length;

    public int ChunkCount => _chunks.Count;

    public int ActiveChunkCount => _activeChunkCount;

    internal ReadOnlySpan<Chunk> ActiveChunks => _activeChunks.AsSpan(0, _activeChunkCount);

    public int EntityCount
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

    public bool Contains(ComponentId componentId) => Mask.Contains(componentId);

    public bool TryGetComponentIndex(ComponentId componentId, out int index)
    {
        index = Mask.Rank(componentId);
        return index >= 0;
    }

    public ref readonly ComponentLayout GetLayout(int index) => ref _layouts[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAvailableChunk() => _availableChunkStack.Count != 0;

    public void AddEntity(
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

    public int ReserveRange(int count, int chunkId, out int chunkIndex, out Chunk chunk, out int reusedCount)
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

    public Entity RemoveEntity(int chunkIndex, int slotIndex)
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

    public void ReleaseChunk(int chunkIndex)
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
        _activeChunks[_activeChunkCount++] = _chunks[chunkIndex];
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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetChunkGlobalId(int chunkIndex) => _chunks[chunkIndex].GlobalId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk GetChunk(int chunkIndex) => _chunks[chunkIndex];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk GetActiveChunk(int activeIndex) => _activeChunks[activeIndex];

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
    }
}
