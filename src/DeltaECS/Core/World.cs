namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public sealed partial class World : IDisposable
{
    private const int DefaultChunkCapacity = 1024;
    private const int DefaultInitialCapacity = 1024;

    private readonly ComponentLayoutRegistry _layouts;
    private readonly int _chunkCapacity;
    private readonly List<Archetype> _archetypes = new();
    private readonly Dictionary<ComponentMask, int> _archetypeByMask = new();
    private readonly List<EntityRecord> _records = new();
    private NativeMemory<int> _freeRecords = new(16);
    private int _freeCount;
    private readonly Dictionary<TransitionKey, TransitionEdge> _transitionCache = new();
    private readonly Dictionary<QuerySpec, QueryPlan> _queryCache = new(QuerySpec.Comparer);
    private NativeMemory<DestroyEntry> _destroyScratch = new(32);
    private TransitionEdge[] _batchEdgeSlots = Array.Empty<TransitionEdge>();
    private NativeMemory<int> _batchEdgeStamps = new(0);
    private int _batchEdgeStamp;
    private int _nextChunkId;
    private int _activeChunkLeases;
    private int _archetypeVersion;
    private MutationStampSource _mutationStamps;
    private bool _disposed;

    public World(
        ComponentLayoutRegistry? layouts = null,
        int initialEntityCapacity = DefaultInitialCapacity,
        int chunkCapacity = DefaultChunkCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialEntityCapacity);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkCapacity);

        _layouts = layouts ?? new ComponentLayoutRegistry();
        _chunkCapacity = chunkCapacity;
        _records.Capacity = initialEntityCapacity;
    }

    public int ArchetypeVersion => _archetypeVersion;

    public uint WorldTick { get; private set; }

    public Stamp Stamp => _mutationStamps.Current;

    public int AliveEntityCount { get; private set; }

    public ComponentLayoutRegistry Layouts => _layouts;

    internal List<Archetype> Archetypes => _archetypes;

    /// <summary>
    /// Releases native storage owned by this world and all of its archetypes.
    /// A world is the sole owner of these buffers; callers must dispose the
    /// world rather than copying or disposing individual storage fields.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        EnsureNoActiveLease("dispose the world");

        _disposed = true;
        foreach (var queryPlan in _queryCache.Values)
        {
            queryPlan.Dispose();
        }

        foreach (var archetype in _archetypes)
        {
            archetype.Dispose();
        }

        _freeRecords.Dispose();
        _destroyScratch.Dispose();
        _batchEdgeStamps.Dispose();
        GC.SuppressFinalize(this);
    }

    public ArchetypeHandle GetArchetype(params ReadOnlySpan<ComponentId> componentIds) => ResolveArchetype(componentIds);

    public ArchetypeHandle GetArchetype(ComponentId first, ComponentId second)
        => GetArchetype(stackalloc[] { first, second });

    public ArchetypeHandle ResolveArchetype(params ReadOnlySpan<ComponentId> componentIds)
    {
        if (!TryBuildComponentMask(componentIds, out var mask))
        {
            throw new InvalidOperationException("Component list is empty or invalid.");
        }

        return new ArchetypeHandle(this, GetOrCreateArchetype(mask).Id);
    }

    public Query CreateQuery(in QuerySpec spec) => new Query(this, GetOrCreateQuery(spec), spec);

    /// <summary>Creates a validated query scope with independent iterators.</summary>
    public QueryScope OpenQuery(in Query handle) => new QueryScope(this, handle);

    public Entity Create(params ReadOnlySpan<ComponentId> componentIds)
    {
        Span<Entity> entities = stackalloc Entity[1];
        return CreateBatch(componentIds, entities) == 0 ? Entity.Null : entities[0];
    }

    public int CreateBatch(ReadOnlySpan<ComponentId> componentIds, Span<Entity> output)
    {
        if (output.Length == 0)
        {
            return 0;
        }

        var handle = ResolveArchetype(componentIds);
        return CreateBatch(handle, output);
    }

    public Entity Create(ArchetypeHandle handle)
    {
        Span<Entity> entities = stackalloc Entity[1];
        return CreateBatch(handle, entities) == 0 ? Entity.Null : entities[0];
    }

    public int CreateBatch(ArchetypeHandle handle, Span<Entity> output)
    {
        var archetype = ResolveArchetype(handle);
        return CreateBatch(archetype, output);
    }

    private int CreateBatch(Archetype archetype, Span<Entity> output)
    {
        if (output.Length == 0)
        {
            return 0;
        }

        Stamp stamp = _mutationStamps.Next();
        for (int i = 0; i < output.Length; i++)
        {
            int recordIndex = AllocateRecord();
            ref var record = ref RecordAt(recordIndex);
            var entity = new Entity(recordIndex, record.Generation);
            int chunkId = archetype.HasAvailableChunk() ? -1 : AllocateChunkId();
            archetype.AddEntity(
                entity,
                chunkId,
                out int chunkIndex,
                out int slotIndex,
                out bool reusedSlot);
            if (reusedSlot)
            {
                archetype.GetChunk(chunkIndex).InitializeSlot(slotIndex);
            }
            archetype.GetChunk(chunkIndex).StampAll(slotIndex, stamp);
            record.Archetype = archetype.Id;
            record.Chunk = chunkIndex;
            record.SlotIndex = slotIndex;
            output[i] = entity;
            AliveEntityCount++;
        }

        return output.Length;
    }

    private Archetype ResolveArchetype(ArchetypeHandle handle)
    {
        if (!handle.IsValid
            || !ReferenceEquals(handle.Owner, this)
            || (uint)handle.ArchetypeId >= (uint)_archetypes.Count)
        {
            throw new ArgumentException("Archetype handle does not belong to this world.", nameof(handle));
        }

        return _archetypes[handle.ArchetypeId];
    }

    public bool Destroy(Entity entity)
    {
        EnsureNoActiveLease("destroy entities");
        if (!TryResolve(entity, out int recordIndex))
        {
            return false;
        }

        DestroyResolved(recordIndex);
        _ = _mutationStamps.Next();
        return true;
    }

    public int DestroyBatch(ReadOnlySpan<Entity> entities)
    {
        EnsureNoActiveLease("destroy entities");
        EnsureDestroyScratch(entities.Length);
        int count = 0;
        for (int i = 0; i < entities.Length; i++)
        {
            if (TryResolve(entities[i], out int recordIndex))
            {
                ref readonly var record = ref RecordAt(recordIndex);
                _destroyScratch[count++] = new DestroyEntry(entities[i], recordIndex, record.Archetype, record.Chunk, record.SlotIndex);
            }
        }

        _destroyScratch.Span[..count].Sort(DestroyEntryComparer.Instance);
        int destroyed = 0;
        for (int i = 0; i < count; i++)
        {
            var entry = _destroyScratch[i];
            if (!TryResolve(entry.Entity, out int recordIndex))
            {
                continue;
            }

            ref readonly var record = ref RecordAt(recordIndex);
            if (recordIndex != entry.RecordIndex
                || record.Archetype != entry.Archetype
                || record.Chunk != entry.Chunk
                || record.SlotIndex != entry.SlotIndex)
            {
                continue;
            }

            DestroyResolved(recordIndex);
            destroyed++;
        }

        if (destroyed != 0)
        {
            _ = _mutationStamps.Next();
        }

        return destroyed;
    }

    public bool IsAlive(Entity entity) => TryResolve(entity, out _);

    public bool HasChangedSince(int globalChunkId, ComponentId componentId, uint sinceTick)
    {
        for (int archetypeIndex = 0; archetypeIndex < _archetypes.Count; archetypeIndex++)
        {
            var archetype = _archetypes[archetypeIndex];
            if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
            {
                continue;
            }

            for (int chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
            {
                var chunk = archetype.GetChunk(chunkIndex);
                if (chunk.GlobalId != globalChunkId)
                {
                    continue;
                }

                uint version = chunk.GetComponentVersion(componentIndex);
                return version != sinceTick
                    && unchecked(version - sinceTick) < 0x8000_0000u;
            }
        }

        return false;
    }

    public bool SetComponent<T>(Entity entity, ComponentId componentId, in T value)
    {
        if (!TryResolve(entity, out int recordIndex))
        {
            return false;
        }

        return SetComponentUnchecked(recordIndex, componentId, value);
    }

    public bool TryGetComponentStamp(Entity entity, ComponentId componentId, out Stamp stamp)
    {
        stamp = default;
        if (!TryResolve(entity, out int recordIndex))
        {
            return false;
        }

        ref readonly var record = ref RecordAt(recordIndex);
        var archetype = _archetypes[record.Archetype];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            return false;
        }

        stamp = archetype.GetChunk(record.Chunk).GetComponentStamp(componentIndex, record.SlotIndex);
        return true;
    }

    public bool TryGetComponent<T>(Entity entity, ComponentId componentId, out T value)
    {
        if (!TryResolve(entity, out int recordIndex))
        {
            value = default!;
            return false;
        }

        ref readonly var record = ref RecordAt(recordIndex);
        var archetype = _archetypes[record.Archetype];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex)
            || !_layouts.TryGet(componentId, out var layout)
            || !IsCompatibleComponentType<T>(layout))
        {
            value = default!;
            return false;
        }

        value = archetype.GetChunk(record.Chunk).GetComponentRow<T>(componentIndex)[record.SlotIndex];
        return true;
    }

    public void AddComponents(ComponentId[] componentIds, Entity entity)
    {
        Span<Entity> entities = stackalloc Entity[1];
        entities[0] = entity;
        _ = ApplyComponents(true, componentIds, entities);
    }

    public int AddComponents(ComponentId[] componentIds, ReadOnlySpan<Entity> entities) => ApplyComponents(true, componentIds, entities);

    public void RemoveComponents(ComponentId[] componentIds, Entity entity)
    {
        Span<Entity> entities = stackalloc Entity[1];
        entities[0] = entity;
        _ = ApplyComponents(false, componentIds, entities);
    }

    public int RemoveComponents(ComponentId[] componentIds, ReadOnlySpan<Entity> entities) => ApplyComponents(false, componentIds, entities);

    public int AddComponents(in Query query, ComponentId[] componentIds) => ApplyQueryComponents(query, true, componentIds);

    public int RemoveComponents(in Query query, ComponentId[] componentIds) => ApplyQueryComponents(query, false, componentIds);

    public int Destroy(in Query query)
    {
        ValidateQuery(query);
        EnsureNoActiveLease("destroy entities");

        var cached = query.Cached;
        ReadOnlySpan<int> archetypes = cached.MatchingArchetypes(this);
        int destroyed = 0;
        for (int archetypeIndex = 0; archetypeIndex < archetypes.Length; archetypeIndex++)
        {
            var archetype = _archetypes[archetypes[archetypeIndex]];
            for (int chunkIndex = archetype.ChunkCount - 1; chunkIndex >= 0; chunkIndex--)
            {
                destroyed += DestroyChunk(archetype, chunkIndex);
            }
        }

        if (destroyed != 0)
        {
            _ = _mutationStamps.Next();
        }

        return destroyed;
    }

    /// <summary>
    /// Executes a query through the experimental Version 1 cursor path.
    /// The cursor is valid only for the callback invocation and must not be retained.
    /// </summary>
    public void Query<TContext>(in Query handle, ref TContext context, QueryAction<TContext> action)
    {
        if (!ReferenceEquals(handle.Owner, this) || !handle.IsValid)
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(handle));
        }

        var cached = handle.Cached;
        var plans = cached.MatchingPlans(this);
        uint writeTick = QueryWriteTick(cached, out Stamp writeStamp);
        _activeChunkLeases++;
        try
        {
            for (int planIndex = 0; planIndex < plans.Length; planIndex++)
            {
                var plan = plans[planIndex];
                var archetype = plan.Archetype;
                for (int chunkIndex = 0; chunkIndex < archetype.ActiveChunkCount; chunkIndex++)
                {
                    var chunk = archetype.GetActiveChunk(chunkIndex);
                    var cursor = new QueryChunkCursor(cached, archetype.Id, chunk, plan.ComponentRows, writeTick, writeStamp);
                    action(ref context, ref cursor);
                }
            }
        }
        finally
        {
            _activeChunkLeases--;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint QueryWriteTick(QueryPlan cached, out Stamp writeStamp)
    {
        if (!cached.HasWriteAccess)
        {
            writeStamp = default;
            return 0;
        }

        writeStamp = _mutationStamps.Next();
        return AdvanceWorldTick();
    }

    public int CollectAliveEntities(Span<Entity> destination)
    {
        int count = 0;
        for (int i = 0; i < _records.Count; i++)
        {
            ref readonly var record = ref RecordAt(i);
            if (record.Archetype < 0)
            {
                continue;
            }

            if (count >= destination.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(destination));
            }

            destination[count++] = new Entity(i, record.Generation);
        }

        return count;
    }

    internal void BeginQueryLease() => _activeChunkLeases++;

    internal void EndQueryLease() => _activeChunkLeases--;

    internal uint GetQueryWriteTick(QueryPlan cached, out Stamp writeStamp)
        => QueryWriteTick(cached, out writeStamp);

    private uint AdvanceWorldTick()
    {
        if (WorldTick == uint.MaxValue)
        {
            for (int archetypeIndex = 0; archetypeIndex < _archetypes.Count; archetypeIndex++)
            {
                var archetype = _archetypes[archetypeIndex];
                for (int chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
                {
                    archetype.GetChunk(chunkIndex).ClearComponentVersions();
                }
            }

            WorldTick = 1;
        }
        else
        {
            WorldTick++;
        }

        return WorldTick;
    }

    private int ApplyComponents(bool isAdd, ComponentId[] componentIds, ReadOnlySpan<Entity> entities)
    {
        EnsureNoActiveLease(isAdd ? "add components" : "remove components");
        if (componentIds.Length == 0 || entities.Length == 0)
        {
            return 0;
        }

        if (!TryBuildComponentMask(componentIds, out var changeMask))
        {
            return 0;
        }

        int edgeStamp = entities.Length == 1 ? 0 : BeginBatchEdgeCache();
        int changed = 0;
        Stamp operationStamp = default;
        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            var entity = entities[entityIndex];
            if (!TryResolve(entity, out int recordIndex))
            {
                continue;
            }

            ref readonly var record = ref RecordAt(recordIndex);
            int sourceArchetypeId = record.Archetype;
            var edge = edgeStamp == 0
                ? GetTransitionEdge(sourceArchetypeId, changeMask, isAdd)
                : GetBatchTransitionEdge(sourceArchetypeId, changeMask, isAdd, edgeStamp);

            if (edge.TargetArchetypeId == sourceArchetypeId)
            {
                continue;
            }

            if (operationStamp == default)
            {
                operationStamp = _mutationStamps.Next();
            }

            MoveEntity(recordIndex, edge, operationStamp);
            changed++;
        }

        return changed;
    }

    private int ApplyQueryComponents(in Query query, bool isAdd, ComponentId[] componentIds)
    {
        ValidateQuery(query);
        EnsureNoActiveLease(isAdd ? "add components" : "remove components");
        if (componentIds.Length == 0)
        {
            return 0;
        }

        if (!TryBuildComponentMask(componentIds, out var changeMask))
        {
            return 0;
        }

        var cached = query.Cached;
        ReadOnlySpan<int> matchingArchetypes = cached.MatchingArchetypes(this);
        int edgeStamp = BeginBatchEdgeCache();
        int changed = 0;
        Stamp operationStamp = default;
        for (int matchingIndex = 0; matchingIndex < matchingArchetypes.Length; matchingIndex++)
        {
            var sourceArchetype = _archetypes[matchingArchetypes[matchingIndex]];
            var edge = GetBatchTransitionEdge(sourceArchetype.Id, changeMask, isAdd, edgeStamp);
            if (edge.TargetArchetypeId == sourceArchetype.Id)
            {
                continue;
            }

            if (sourceArchetype.ActiveChunkCount == 0)
            {
                continue;
            }

            if (operationStamp == default)
            {
                operationStamp = _mutationStamps.Next();
            }

            changed += MoveArchetypeBlocks(sourceArchetype, edge, operationStamp);
        }

        return changed;
    }

    private int MoveArchetypeBlocks(Archetype sourceArchetype, TransitionEdge edge, Stamp operationStamp)
    {
        int movedCount = 0;
        var targetArchetype = _archetypes[edge.TargetArchetypeId];
        for (int sourceChunkIndex = sourceArchetype.ChunkCount - 1; sourceChunkIndex >= 0; sourceChunkIndex--)
        {
            var sourceChunk = sourceArchetype.GetChunk(sourceChunkIndex);
            int sourceCount = sourceChunk.Count;
            if (sourceCount == 0)
            {
                continue;
            }

            var sourceEntities = sourceChunk.RawEntities;
            int sourceEnd = sourceCount;
            while (sourceEnd > 0)
            {
                int targetChunkId = targetArchetype.HasAvailableChunk() ? -1 : AllocateChunkId();
                int reserved = targetArchetype.ReserveRange(
                    sourceEnd,
                    targetChunkId,
                    out int targetChunkIndex,
                    out var targetChunk);
                int targetSlot = targetChunk.Count - reserved;
                int sourceSlot = sourceEnd - reserved;

                sourceEntities.Slice(sourceSlot, reserved).CopyTo(targetChunk.RawEntities.Slice(targetSlot, reserved));
                for (int sourceComponentIndex = 0; sourceComponentIndex < edge.SourceToTargetRowIndices.Length; sourceComponentIndex++)
                {
                    int targetComponentIndex = edge.SourceToTargetRowIndices[sourceComponentIndex];
                    if (targetComponentIndex < 0)
                    {
                        continue;
                    }

                    Array.Copy(
                        sourceChunk.GetRawComponentRow(sourceComponentIndex),
                        sourceSlot,
                        targetChunk.GetRawComponentRow(targetComponentIndex),
                        targetSlot,
                        reserved);
                    sourceChunk.CopyStampRangeTo(
                        targetChunk,
                        sourceSlot,
                        targetSlot,
                        reserved,
                        sourceComponentIndex,
                        targetComponentIndex);
                }

                targetChunk.InitializeRowsRange(targetSlot, reserved, edge.AddedTargetRowIndices);
                targetChunk.StampRowsRange(targetSlot, reserved, edge.AddedTargetRowIndices, operationStamp);
                for (int slot = 0; slot < reserved; slot++)
                {
                    var entity = sourceEntities[sourceSlot + slot];
                    ref var record = ref RecordAt(entity.Index);
                    record.Archetype = targetArchetype.Id;
                    record.Chunk = targetChunkIndex;
                    record.SlotIndex = targetSlot + slot;
                }

                sourceEnd = sourceSlot;
                movedCount += reserved;
            }

            sourceChunk.ClearAll();
            sourceArchetype.ReleaseChunk(sourceChunkIndex);
        }

        return movedCount;
    }

    private int DestroyChunk(Archetype archetype, int chunkIndex)
    {
        var chunk = archetype.GetChunk(chunkIndex);
        int count = chunk.Count;
        if (count == 0)
        {
            return 0;
        }

        var entities = chunk.RawEntities;
        for (int slot = count - 1; slot >= 0; slot--)
        {
            var entity = entities[slot];
            ref var record = ref RecordAt(entity.Index);
            record.Archetype = -1;
            record.Chunk = -1;
            record.SlotIndex = -1;
            record.Generation++;
            PushFree(entity.Index);
        }

        chunk.ClearAll();
        archetype.ReleaseChunk(chunkIndex);
        AliveEntityCount -= count;
        return count;
    }

    private void DestroyResolved(int recordIndex)
    {
        ref var record = ref RecordAt(recordIndex);
        var archetype = _archetypes[record.Archetype];
        var chunk = archetype.GetChunk(record.Chunk);
        var moved = archetype.RemoveEntity(record.Chunk, record.SlotIndex);
        if (moved.IsAlive)
        {
            ref var movedRecord = ref RecordAt(moved.Index);
            movedRecord.Chunk = record.Chunk;
            movedRecord.SlotIndex = record.SlotIndex;
        }

        record.Archetype = -1;
        record.Chunk = -1;
        record.SlotIndex = -1;
        record.Generation++;
        PushFree(recordIndex);
        AliveEntityCount--;
    }

    private void MoveEntity(int recordIndex, TransitionEdge edge, Stamp operationStamp)
    {
        ref var sourceRecord = ref RecordAt(recordIndex);
        var sourceArchetype = _archetypes[sourceRecord.Archetype];
        var targetArchetype = _archetypes[edge.TargetArchetypeId];
        var sourceChunk = sourceArchetype.GetChunk(sourceRecord.Chunk);
        int sourceSlotIndex = sourceRecord.SlotIndex;
        int sourceChunkIndex = sourceRecord.Chunk;
        int targetChunkId = targetArchetype.HasAvailableChunk() ? -1 : AllocateChunkId();
        targetArchetype.AddEntity(
            new Entity(recordIndex, sourceRecord.Generation),
            targetChunkId,
            out int targetChunkIndex,
            out int targetSlotIndex,
            out bool reusedTargetSlot);
        var targetChunk = targetArchetype.GetChunk(targetChunkIndex);

        for (int sourceIndex = 0; sourceIndex < edge.SourceToTargetRowIndices.Length; sourceIndex++)
        {
            int targetIndex = edge.SourceToTargetRowIndices[sourceIndex];
            if (targetIndex >= 0)
            {
                sourceChunk.CopySlotTo(targetChunk, sourceSlotIndex, targetSlotIndex, sourceIndex, targetIndex);
            }
        }

        if (reusedTargetSlot)
        {
            targetChunk.InitializeRows(targetSlotIndex, edge.AddedTargetRowIndices);
        }
        for (int i = 0; i < edge.AddedTargetRowIndices.Length; i++)
        {
            targetChunk.MarkComponentStamped(edge.AddedTargetRowIndices[i], targetSlotIndex, operationStamp);
        }

        var moved = sourceArchetype.RemoveEntity(sourceChunkIndex, sourceSlotIndex);
        sourceRecord.Archetype = edge.TargetArchetypeId;
        sourceRecord.Chunk = targetChunkIndex;
        sourceRecord.SlotIndex = targetSlotIndex;
        if (moved.IsAlive)
        {
            ref var movedRecord = ref RecordAt(moved.Index);
            movedRecord.Chunk = sourceChunkIndex;
            movedRecord.SlotIndex = sourceSlotIndex;
        }
    }

    private TransitionEdge GetTransitionEdge(int sourceArchetypeId, ComponentMask changeMask, bool isAdd)
    {
        var key = new TransitionKey(sourceArchetypeId, changeMask, isAdd);
        if (_transitionCache.TryGetValue(key, out var edge))
        {
            return edge;
        }

        var source = _archetypes[sourceArchetypeId];
        var targetMask = isAdd ? source.Mask.Or(changeMask) : source.Mask.Except(changeMask);
        var target = GetOrCreateArchetype(targetMask);
        int[] mapping = new int[source.ComponentCount];
        bool[] copiedTargetRows = new bool[target.ComponentCount];
        for (int i = 0; i < mapping.Length; i++)
        {
            mapping[i] = target.Mask.Rank(source.ComponentIds[i]);
            if (mapping[i] >= 0)
            {
                copiedTargetRows[mapping[i]] = true;
            }
        }

        int[] addedTargetRows = new int[target.ComponentCount];
        int addedCount = 0;
        for (int targetIndex = 0; targetIndex < copiedTargetRows.Length; targetIndex++)
        {
            if (!copiedTargetRows[targetIndex])
            {
                addedTargetRows[addedCount++] = targetIndex;
            }
        }

        if (addedCount != addedTargetRows.Length)
        {
            Array.Resize(ref addedTargetRows, addedCount);
        }

        edge = new TransitionEdge(target.Id, mapping, addedTargetRows);
        _transitionCache.Add(key, edge);
        return edge;
    }

    private bool SetComponentUnchecked<T>(int recordIndex, ComponentId componentId, T value)
    {
        ref readonly var record = ref RecordAt(recordIndex);
        var archetype = _archetypes[record.Archetype];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex)
            || !_layouts.TryGet(componentId, out var layout)
            || !IsCompatibleComponentType<T>(layout))
        {
            return false;
        }

        var chunk = archetype.GetChunk(record.Chunk);
        chunk.GetComponentRow<T>(componentIndex)[record.SlotIndex] = value;
        chunk.MarkComponentStamped(componentIndex, record.SlotIndex, _mutationStamps.Next());
        return true;
    }

    private Archetype GetOrCreateArchetype(ComponentMask mask)
    {
        if (_archetypeByMask.TryGetValue(mask, out int existing))
        {
            return _archetypes[existing];
        }

        var componentIds = new ComponentId[mask.Count];
        mask.CopyComponentIds(componentIds);
        var layouts = new ComponentLayout[componentIds.Length];
        var rowOperations = new ComponentRowOperations[componentIds.Length];
        for (int i = 0; i < componentIds.Length; i++)
        {
            if (!_layouts.TryGet(componentIds[i], out var layout))
            {
                throw new InvalidOperationException($"Missing component layout for {componentIds[i].Value}.");
            }

            layouts[i] = layout;
            rowOperations[i] = _layouts.GetRowOperations(componentIds[i]);
        }

        var archetype = new Archetype(
            _archetypes.Count,
            mask,
            layouts,
            rowOperations,
            componentIds,
            _chunkCapacity);
        _archetypeByMask.Add(mask, archetype.Id);
        _archetypes.Add(archetype);
        _archetypeVersion++;
        return archetype;
    }

    private static bool TryBuildComponentMask(ReadOnlySpan<ComponentId> componentIds, out ComponentMask mask)
    {
        mask = default;
        for (int i = 0; i < componentIds.Length; i++)
        {
            if (!componentIds[i].IsValid)
            {
                continue;
            }

            mask = mask.Set(componentIds[i]);
        }

        return !mask.IsEmpty;
    }

    private int AllocateRecord()
    {
        if (_freeCount > 0)
        {
            return _freeRecords[--_freeCount];
        }

        int index = _records.Count;
        _records.Add(new EntityRecord { Generation = 0, Archetype = -1, Chunk = -1, SlotIndex = -1 });
        return index;
    }

    private void PushFree(int recordIndex)
    {
        if (_freeCount == _freeRecords.Length)
        {
            _freeRecords.Resize(Math.Max(4, _freeRecords.Length * 2));
        }

        _freeRecords[_freeCount++] = recordIndex;
    }

    private int AllocateChunkId() => _nextChunkId++;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref EntityRecord RecordAt(int recordIndex) => ref CollectionsMarshal.AsSpan(_records)[recordIndex];

    private bool TryResolve(Entity entity, out int recordIndex)
    {
        recordIndex = entity.Index;
        if (recordIndex < 0 || recordIndex >= _records.Count)
        {
            return false;
        }

        ref readonly var record = ref RecordAt(recordIndex);
        return record.Archetype >= 0 && record.Generation == entity.Generation;
    }

    private QueryPlan GetOrCreateQuery(QuerySpec spec)
    {
        if (_queryCache.TryGetValue(spec, out var cached))
        {
            return cached;
        }

        cached = new QueryPlan(spec);
        _queryCache.Add(spec, cached);
        return cached;
    }

    private void EnsureNoActiveLease(string operation)
    {
        if (_activeChunkLeases > 0)
        {
            throw new InvalidOperationException($"Cannot {operation} while chunk leases are active.");
        }
    }

    private void ValidateQuery(in Query query)
    {
        if (!query.IsValid || !ReferenceEquals(query.Owner, this))
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(query));
        }
    }

    private int BeginBatchEdgeCache()
    {
        EnsureBatchEdgeCapacity(_archetypes.Count);
        if (_batchEdgeStamp == int.MaxValue)
        {
            _batchEdgeStamps.Clear();
            _batchEdgeStamp = 1;
        }
        else
        {
            _batchEdgeStamp++;
        }

        return _batchEdgeStamp;
    }

    private TransitionEdge GetBatchTransitionEdge(
        int sourceArchetypeId,
        ComponentMask changeMask,
        bool isAdd,
        int stamp)
    {
        if ((uint)sourceArchetypeId >= (uint)_batchEdgeStamps.Length)
        {
            EnsureBatchEdgeCapacity(sourceArchetypeId + 1);
        }

        if (_batchEdgeStamps[sourceArchetypeId] == stamp)
        {
            return _batchEdgeSlots[sourceArchetypeId];
        }

        var edge = GetTransitionEdge(sourceArchetypeId, changeMask, isAdd);
        _batchEdgeSlots[sourceArchetypeId] = edge;
        _batchEdgeStamps[sourceArchetypeId] = stamp;
        return edge;
    }

    private void EnsureBatchEdgeCapacity(int required)
    {
        if (required <= _batchEdgeSlots.Length)
        {
            return;
        }

        int capacity = Math.Max(required, _batchEdgeSlots.Length == 0 ? 4 : _batchEdgeSlots.Length * 2);
        Array.Resize(ref _batchEdgeSlots, capacity);
        _batchEdgeStamps.Resize(capacity);
    }

    private void EnsureDestroyScratch(int required)
    {
        if (required > _destroyScratch.Length)
        {
            _destroyScratch.Resize(Math.Max(required, _destroyScratch.Length * 2));
        }
    }

    private static bool IsCompatibleComponentType<T>(ComponentLayout layout) => layout.RuntimeType == typeof(T);

    private readonly struct TransitionKey : IEquatable<TransitionKey>
    {
        public TransitionKey(int sourceArchetypeId, ComponentMask changeMask, bool isAdd)
        {
            SourceArchetypeId = sourceArchetypeId;
            ChangeMask = changeMask;
            IsAdd = isAdd;
        }

        public int SourceArchetypeId { get; }
        public ComponentMask ChangeMask { get; }
        public bool IsAdd { get; }

        public bool Equals(TransitionKey other) => SourceArchetypeId == other.SourceArchetypeId
            && ChangeMask == other.ChangeMask && IsAdd == other.IsAdd;

        public override bool Equals(object? obj) => obj is TransitionKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SourceArchetypeId, ChangeMask, IsAdd);
    }

    private readonly struct TransitionEdge
    {
        public TransitionEdge(
            int targetArchetypeId,
            int[] sourceToTargetRowIndices,
            int[] addedTargetRowIndices)
        {
            TargetArchetypeId = targetArchetypeId;
            SourceToTargetRowIndices = sourceToTargetRowIndices;
            AddedTargetRowIndices = addedTargetRowIndices;
        }

        public int TargetArchetypeId { get; }
        public int[] SourceToTargetRowIndices { get; }
        public int[] AddedTargetRowIndices { get; }
    }

    private readonly struct DestroyEntry
    {
        public DestroyEntry(Entity entity, int recordIndex, int archetype, int chunk, int slotIndex)
        {
            Entity = entity;
            RecordIndex = recordIndex;
            Archetype = archetype;
            Chunk = chunk;
            SlotIndex = slotIndex;
        }

        public Entity Entity { get; }
        public int RecordIndex { get; }
        public int Archetype { get; }
        public int Chunk { get; }
        public int SlotIndex { get; }
    }

    private sealed class DestroyEntryComparer : IComparer<DestroyEntry>
    {
        public static readonly DestroyEntryComparer Instance = new();

        public int Compare(DestroyEntry x, DestroyEntry y)
        {
            int result = x.Archetype.CompareTo(y.Archetype);
            if (result != 0) return result;
            result = x.Chunk.CompareTo(y.Chunk);
            return result != 0 ? result : y.SlotIndex.CompareTo(x.SlotIndex);
        }
    }

}
