namespace DVG.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

internal sealed class Archetype
{
    private readonly int _id;
    private readonly int _chunkCapacity;
    private readonly ComponentId[] _componentIds;
    private readonly ComponentLayout[] _layouts;
    private readonly List<Chunk> _chunks = new();
    private readonly List<int> _availableChunkStack = new();
    private bool[] _availableChunkFlags = Array.Empty<bool>();

    public Archetype(int id, ComponentMask mask, ComponentLayout[] layouts, ComponentId[] componentIds, int chunkCapacity)
    {
        if (componentIds.Length == 0 || layouts.Length != componentIds.Length)
        {
            throw new ArgumentException("Archetype must have matching non-empty component and layout arrays.");
        }

        _id = id;
        Mask = mask;
        _chunkCapacity = chunkCapacity;
        _componentIds = componentIds;
        _layouts = layouts;
    }

    public int Id => _id;

    public ComponentMask Mask { get; }

    public ReadOnlySpan<ComponentId> ComponentIds => _componentIds;

    public int ComponentCount => _componentIds.Length;

    public int ChunkCount => _chunks.Count;

    public int EntityCount
    {
        get
        {
            var count = 0;
            for (var i = 0; i < _chunks.Count; i++)
            {
                count += _chunks[i].Size;
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

    public void AddEntity(Entity entity, int chunkId, out int chunkIndex, out int slotIndex)
    {
        if (TryTakeAvailableChunk(out var availableIndex, out var available))
        {
            chunkIndex = availableIndex;
            slotIndex = available.Add(entity);
            if (!available.IsFull)
            {
                PushAvailableChunk(chunkIndex);
            }

            return;
        }

        chunkIndex = _chunks.Count;
        _chunks.Add(new Chunk(_chunkCapacity, _layouts, chunkId));
        EnsureAvailableChunkCapacity(chunkIndex);
        slotIndex = _chunks[chunkIndex].Add(entity);
        if (!_chunks[chunkIndex].IsFull)
        {
            PushAvailableChunk(chunkIndex);
        }
    }

    public Entity RemoveEntity(int chunkIndex, int slotIndex)
    {
        var moved = _chunks[chunkIndex].RemoveSwapBack(slotIndex);
        PushAvailableChunk(chunkIndex);
        return moved;
    }

    private bool TryTakeAvailableChunk(out int chunkIndex, out Chunk chunk)
    {
        while (_availableChunkStack.Count != 0)
        {
            var stackIndex = _availableChunkStack.Count - 1;
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
            Array.Resize(ref _availableChunkFlags, Math.Max(chunkIndex + 1, _availableChunkFlags.Length == 0 ? 4 : _availableChunkFlags.Length * 2));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetChunkGlobalId(int chunkIndex) => _chunks[chunkIndex].GlobalId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk GetChunk(int chunkIndex) => _chunks[chunkIndex];
}
