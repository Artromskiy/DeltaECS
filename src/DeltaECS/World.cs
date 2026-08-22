namespace Delta.ECS;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public sealed class World
{
    private const int DefaultChunkCapacity = 1024;
    private const int DefaultInitialCapacity = 1024;

    private readonly ComponentLayoutRegistry _layouts;
    private readonly int _chunkCapacity;
    private readonly OverlayTagManager _overlayTags;
    private readonly List<Archetype> _archetypes = new();
    private readonly Dictionary<ComponentMask, int> _archetypeByMask = new();
    private readonly List<EntityRecord> _records = new();
    private int[] _freeRecords = new int[16];
    private int _freeCount;
    private readonly Dictionary<TransitionKey, TransitionEdge> _transitionCache = new();
    private readonly Dictionary<QueryDescription, CachedQuery> _queryCache = new(QueryDescription.Comparer);
    private DestroyEntry[] _destroyScratch = new DestroyEntry[32];
    private TransitionEdge[] _batchEdgeSlots = Array.Empty<TransitionEdge>();
    private int[] _batchEdgeStamps = Array.Empty<int>();
    private int _batchEdgeStamp;
    private int _nextChunkId;
    private int _activeChunkLeases;
    private int _archetypeVersion;

    public World(
        ComponentLayoutRegistry? layouts = null,
        int initialEntityCapacity = DefaultInitialCapacity,
        int chunkCapacity = DefaultChunkCapacity)
    {
        if (initialEntityCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialEntityCapacity));
        }

        if (chunkCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkCapacity));
        }

        _layouts = layouts ?? new ComponentLayoutRegistry();
        _chunkCapacity = chunkCapacity;
        _records.Capacity = initialEntityCapacity;
        _overlayTags = new OverlayTagManager(chunkCapacity);
    }

    public int ArchetypeVersion => _archetypeVersion;

    public uint WorldTick { get; private set; }

    public int AliveEntityCount { get; private set; }

    public ComponentLayoutRegistry Layouts => _layouts;

    internal List<Archetype> Archetypes => _archetypes;

    public ArchetypeHandle GetArchetype(params ComponentId[] componentIds) => ResolveArchetype(componentIds);

    public ArchetypeHandle ResolveArchetype(params ComponentId[] componentIds)
    {
        ArgumentNullException.ThrowIfNull(componentIds);
        if (!TryBuildComponentMask(componentIds, out var mask))
        {
            throw new InvalidOperationException("Component list is empty or invalid.");
        }

        return new ArchetypeHandle(this, GetOrCreateArchetype(mask).Id);
    }

    public QueryHandle CreateQuery(in QueryDescription description)
    {
        return new QueryHandle(this, GetOrCreateQuery(description), description);
    }

    public Entity Create(ComponentId[] componentIds)
    {
        Span<Entity> entities = stackalloc Entity[1];
        return CreateBatch(componentIds, entities) == 0 ? Entity.Null : entities[0];
    }

    public int CreateBatch(ComponentId[] componentIds, Span<Entity> output)
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
        for (var i = 0; i < output.Length; i++)
        {
            var recordIndex = AllocateRecord();
            ref var record = ref RecordAt(recordIndex);
            var entity = new Entity(recordIndex, record.Generation);
            var chunkId = archetype.HasAvailableChunk() ? -1 : AllocateChunkId();
            archetype.AddEntity(
                entity,
                chunkId,
                out var chunkIndex,
                out var slotIndex,
                out var reusedSlot);
            if (reusedSlot)
            {
                archetype.GetChunk(chunkIndex).InitializeSlot(slotIndex);
            }
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
        if (!TryResolve(entity, out var recordIndex))
        {
            return false;
        }

        DestroyResolved(recordIndex);
        return true;
    }

    public int DestroyBatch(ReadOnlySpan<Entity> entities)
    {
        EnsureNoActiveLease("destroy entities");
        EnsureDestroyScratch(entities.Length);
        var count = 0;
        for (var i = 0; i < entities.Length; i++)
        {
            if (TryResolve(entities[i], out var recordIndex))
            {
                ref readonly var record = ref RecordAt(recordIndex);
                _destroyScratch[count++] = new DestroyEntry(entities[i], recordIndex, record.Archetype, record.Chunk, record.SlotIndex);
            }
        }

        Array.Sort(_destroyScratch, 0, count, DestroyEntryComparer.Instance);
        var destroyed = 0;
        for (var i = 0; i < count; i++)
        {
            var entry = _destroyScratch[i];
            if (!TryResolve(entry.Entity, out var recordIndex))
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

        return destroyed;
    }

    public bool IsAlive(Entity entity) => TryResolve(entity, out _);

    public bool HasChangedSince(int globalChunkId, ComponentId componentId, uint sinceTick)
    {
        for (var archetypeIndex = 0; archetypeIndex < _archetypes.Count; archetypeIndex++)
        {
            var archetype = _archetypes[archetypeIndex];
            if (!archetype.TryGetComponentIndex(componentId, out var componentIndex))
            {
                continue;
            }

            for (var chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
            {
                var chunk = archetype.GetChunk(chunkIndex);
                if (chunk.GlobalId != globalChunkId)
                {
                    continue;
                }

                var version = chunk.GetComponentVersion(componentIndex);
                return version != sinceTick
                    && unchecked(version - sinceTick) < 0x8000_0000u;
            }
        }

        return false;
    }

    public bool SetComponent<T>(Entity entity, ComponentId componentId, in T value)
    {
        return TryResolve(entity, out var recordIndex)
            && SetComponentUnchecked(recordIndex, componentId, value);
    }

    public bool TryGetComponent<T>(Entity entity, ComponentId componentId, out T value)
    {
        if (!TryResolve(entity, out var recordIndex))
        {
            value = default!;
            return false;
        }

        ref readonly var record = ref RecordAt(recordIndex);
        var archetype = _archetypes[record.Archetype];
        if (!archetype.TryGetComponentIndex(componentId, out var componentIndex)
            || !_layouts.TryGet(componentId, out var layout)
            || !IsCompatibleComponentType<T>(layout))
        {
            value = default!;
            return false;
        }

        value = archetype.GetChunk(record.Chunk).GetComponentRow<T>(componentIndex)[record.SlotIndex];
        return true;
    }

    public void AddTag(Entity entity, TagId tag)
    {
        ValidateTag(tag);
        if (TryResolve(entity, out var recordIndex))
        {
            ref readonly var record = ref RecordAt(recordIndex);
            var archetype = _archetypes[record.Archetype];
            _overlayTags.AddTag(archetype.GetChunkGlobalId(record.Chunk), record.SlotIndex, tag);
        }
    }

    public void RemoveTag(Entity entity, TagId tag)
    {
        ValidateTag(tag);
        if (TryResolve(entity, out var recordIndex))
        {
            ref readonly var record = ref RecordAt(recordIndex);
            var archetype = _archetypes[record.Archetype];
            _overlayTags.RemoveTag(archetype.GetChunkGlobalId(record.Chunk), record.SlotIndex, tag);
        }
    }

    public bool HasTag(Entity entity, TagId tag)
    {
        ValidateTag(tag);
        if (!TryResolve(entity, out var recordIndex))
        {
            return false;
        }

        ref readonly var record = ref RecordAt(recordIndex);
        var archetype = _archetypes[record.Archetype];
        return _overlayTags.HasTag(archetype.GetChunkGlobalId(record.Chunk), record.SlotIndex, tag);
    }

    private static void ValidateTag(TagId tag)
    {
        if (!tag.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(tag), "TagId must be non-negative.");
        }
    }

    public void AddComponents(ComponentId[] componentIds, Entity entity)
    {
        Span<Entity> entities = stackalloc Entity[1];
        entities[0] = entity;
        _ = ApplyComponents(true, componentIds, entities);
    }

    public int AddComponents(ComponentId[] componentIds, ReadOnlySpan<Entity> entities)
    {
        return ApplyComponents(true, componentIds, entities);
    }

    public void RemoveComponents(ComponentId[] componentIds, Entity entity)
    {
        Span<Entity> entities = stackalloc Entity[1];
        entities[0] = entity;
        _ = ApplyComponents(false, componentIds, entities);
    }

    public int RemoveComponents(ComponentId[] componentIds, ReadOnlySpan<Entity> entities)
    {
        return ApplyComponents(false, componentIds, entities);
    }

    public int AddComponents(in QueryHandle query, ComponentId[] componentIds)
    {
        return ApplyQueryComponents(query, true, componentIds);
    }

    public int RemoveComponents(in QueryHandle query, ComponentId[] componentIds)
    {
        return ApplyQueryComponents(query, false, componentIds);
    }

    public int Destroy(in QueryHandle query)
    {
        ValidateQueryHandle(query);
        EnsureNoActiveLease("destroy entities");

        var cached = query.Cached;
        var archetypes = cached.MatchingArchetypes(this);
        if (cached.HasTags)
        {
            var matches = CollectTaggedQueryEntities(query.Description, archetypes);
            return DestroyBatch(CollectionsMarshal.AsSpan(matches));
        }

        var destroyed = 0;
        for (var archetypeIndex = 0; archetypeIndex < archetypes.Length; archetypeIndex++)
        {
            var archetype = _archetypes[archetypes[archetypeIndex]];
            for (var chunkIndex = archetype.ChunkCount - 1; chunkIndex >= 0; chunkIndex--)
            {
                destroyed += DestroyChunk(archetype, chunkIndex);
            }
        }

        return destroyed;
    }

    /// <summary>
    /// Executes a dense query through the experimental Version 1 cursor path.
    /// The cursor is valid only for the callback invocation and must not be retained.
    /// </summary>
    public void QueryCursor<TContext>(in QueryHandle handle, ref TContext context, QueryCursorAction<TContext> action)
    {
        if (!ReferenceEquals(handle.Owner, this) || !handle.IsValid)
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(handle));
        }

        var query = handle.Description;
        var cached = handle.Cached;
        var plans = cached.MatchingPlans(this);
        var writeTick = QueryWriteTick(cached);
        ulong[]? scratch = cached.HasTags ? RentChunkOverlayScratch() : null;
        _activeChunkLeases++;
        try
        {
            for (var planIndex = 0; planIndex < plans.Length; planIndex++)
            {
                var plan = plans[planIndex];
                var archetype = plan.Archetype;
                for (var chunkIndex = 0; chunkIndex < archetype.ActiveChunkCount; chunkIndex++)
                {
                    var chunk = archetype.GetActiveChunk(chunkIndex);
                    var overlayResult = cached.HasTags
                        ? _overlayTags.BuildMask(query, chunk.GlobalId, chunk.Count, scratch!)
                        : OverlayMaskResult.Full;
                    if (overlayResult == OverlayMaskResult.None)
                    {
                        continue;
                    }

                    var cursor = new DenseChunkCursor(cached, archetype.Id, chunk, plan.ComponentRows, writeTick, scratch, overlayResult);
                    action(ref context, ref cursor);
                }
            }
        }
        finally
        {
            _activeChunkLeases--;
            if (scratch is not null)
            {
                ReturnChunkOverlayScratch(scratch);
            }
        }
    }

    /// <summary>Enumerates query chunks through the cursor API. Dispose the enumerator when finished.</summary>
    public CursorChunkEnumerator QueryCursorChunks(in QueryHandle handle)
    {
        if (!ReferenceEquals(handle.Owner, this) || !handle.IsValid)
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(handle));
        }

        return new CursorChunkEnumerator(this, handle.Cached, handle.Description, QueryWriteTick(handle.Cached));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint QueryWriteTick(CachedQuery cached) =>
        cached.HasWriteBindings ? AdvanceWorldTick() : 0;

    public ref struct CursorChunkEnumerator
    {
        private readonly World _owner;
        private readonly QueryDescription _query;
        private readonly CachedQuery _cached;
        private readonly DenseArchetypePlan[] _plans;
        private readonly bool _hasTags;
        private ulong[]? _overlayScratch;
        private readonly uint _writeTick;
        private int _archetypePosition;
        private int _chunkPosition;
        private DenseChunkCursor _current;
        private bool _hasCurrent;
        private bool _disposed;

        internal CursorChunkEnumerator(World owner, CachedQuery cached, QueryDescription query, uint writeTick)
        {
            _owner = owner;
            _query = query;
            _cached = cached;
            _plans = cached.MatchingPlans(owner);
            _hasTags = cached.HasTags;
            _overlayScratch = _hasTags ? owner.RentChunkOverlayScratch() : null;
            _writeTick = writeTick;
            _archetypePosition = 0;
            _chunkPosition = 0;
            _current = default;
            _hasCurrent = false;
            _disposed = false;
            _owner._activeChunkLeases++;
        }

        public DenseChunkCursor Current
        {
            get
            {
                if (!_hasCurrent || _disposed)
                {
                    throw new InvalidOperationException("The cursor chunk enumerator is not positioned on a chunk.");
                }

                return _current;
            }
        }

        public bool MoveNext()
        {
            if (_disposed)
            {
                return false;
            }

            while (_archetypePosition < _plans.Length)
            {
                var plan = _plans[_archetypePosition];
                var archetype = plan.Archetype;
                while (_chunkPosition < archetype.ActiveChunkCount)
                {
                    var chunk = archetype.GetActiveChunk(_chunkPosition++);
                    var overlayResult = _hasTags
                        ? _owner._overlayTags.BuildMask(_query, chunk.GlobalId, chunk.Count, _overlayScratch!)
                        : OverlayMaskResult.Full;
                    if (overlayResult == OverlayMaskResult.None)
                    {
                        continue;
                    }

                    _current = new DenseChunkCursor(_cached, archetype.Id, chunk, plan.ComponentRows, _writeTick, _overlayScratch, overlayResult);
                    _hasCurrent = true;
                    return true;
                }

                _archetypePosition++;
                _chunkPosition = 0;
            }

            Dispose();
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _hasCurrent = false;
            _owner._activeChunkLeases--;
            if (_overlayScratch is not null)
            {
                _owner.ReturnChunkOverlayScratch(_overlayScratch);
                _overlayScratch = null;
            }
        }
    }

    public int CollectAliveEntities(Span<Entity> destination)
    {
        var count = 0;
        for (var i = 0; i < _records.Count; i++)
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

    internal ulong[] RentChunkOverlayScratch() => ArrayPool<ulong>.Shared.Rent(_overlayTags.WordsPerChunk);
    internal void ReturnChunkOverlayScratch(ulong[] scratch)
    {
        ArrayPool<ulong>.Shared.Return(scratch, clearArray: true);
    }

    private uint AdvanceWorldTick()
    {
        if (WorldTick == uint.MaxValue)
        {
            for (var archetypeIndex = 0; archetypeIndex < _archetypes.Count; archetypeIndex++)
            {
                var archetype = _archetypes[archetypeIndex];
                for (var chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
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

        var edgeStamp = entities.Length == 1 ? 0 : BeginBatchEdgeCache();
        var changed = 0;
        for (var entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            var entity = entities[entityIndex];
            if (!TryResolve(entity, out var recordIndex))
            {
                continue;
            }

            ref readonly var record = ref RecordAt(recordIndex);
            var sourceArchetypeId = record.Archetype;
            var edge = edgeStamp == 0
                ? GetTransitionEdge(sourceArchetypeId, changeMask, isAdd)
                : GetBatchTransitionEdge(sourceArchetypeId, changeMask, isAdd, edgeStamp);

            if (edge.TargetArchetypeId == sourceArchetypeId)
            {
                continue;
            }

            MoveEntity(recordIndex, edge);
            changed++;
        }

        return changed;
    }

    private int ApplyQueryComponents(in QueryHandle query, bool isAdd, ComponentId[] componentIds)
    {
        ValidateQueryHandle(query);
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
        var matchingArchetypes = cached.MatchingArchetypes(this);
        if (cached.HasTags)
        {
            var matches = CollectTaggedQueryEntities(query.Description, matchingArchetypes);
            return ApplyComponents(isAdd, componentIds, CollectionsMarshal.AsSpan(matches));
        }

        var edgeStamp = BeginBatchEdgeCache();
        var changed = 0;
        for (var matchingIndex = 0; matchingIndex < matchingArchetypes.Length; matchingIndex++)
        {
            var sourceArchetype = _archetypes[matchingArchetypes[matchingIndex]];
            var edge = GetBatchTransitionEdge(sourceArchetype.Id, changeMask, isAdd, edgeStamp);
            if (edge.TargetArchetypeId == sourceArchetype.Id)
            {
                continue;
            }

            changed += MoveArchetypeBlocks(sourceArchetype, edge);
        }

        return changed;
    }

    private int MoveArchetypeBlocks(Archetype sourceArchetype, TransitionEdge edge)
    {
        return _overlayTags.HasAnyTags
            ? MoveArchetypeBlocksTagged(sourceArchetype, edge)
            : MoveArchetypeBlocksDense(sourceArchetype, edge);
    }

    private int MoveArchetypeBlocksDense(Archetype sourceArchetype, TransitionEdge edge)
    {
        var movedCount = 0;
        var targetArchetype = _archetypes[edge.TargetArchetypeId];
        for (var sourceChunkIndex = sourceArchetype.ChunkCount - 1; sourceChunkIndex >= 0; sourceChunkIndex--)
        {
            var sourceChunk = sourceArchetype.GetChunk(sourceChunkIndex);
            var sourceCount = sourceChunk.Count;
            if (sourceCount == 0)
            {
                continue;
            }

            var sourceEntities = sourceChunk.RawEntities;
            var sourceEnd = sourceCount;
            while (sourceEnd > 0)
            {
                var targetChunkId = targetArchetype.HasAvailableChunk() ? -1 : AllocateChunkId();
                var reserved = targetArchetype.ReserveRange(
                    sourceEnd,
                    targetChunkId,
                    out var targetChunkIndex,
                    out var targetChunk);
                var targetSlot = targetChunk.Count - reserved;
                var sourceSlot = sourceEnd - reserved;

                Array.Copy(sourceEntities, sourceSlot, targetChunk.RawEntities, targetSlot, reserved);
                for (var sourceComponentIndex = 0; sourceComponentIndex < edge.SourceToTargetRowIndices.Length; sourceComponentIndex++)
                {
                    var targetComponentIndex = edge.SourceToTargetRowIndices[sourceComponentIndex];
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
                }

                targetChunk.InitializeRowsRange(targetSlot, reserved, edge.AddedTargetRowIndices);
                for (var slot = 0; slot < reserved; slot++)
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

    private int MoveArchetypeBlocksTagged(Archetype sourceArchetype, TransitionEdge edge)
    {
        var movedCount = 0;
        var targetArchetype = _archetypes[edge.TargetArchetypeId];
        for (var sourceChunkIndex = sourceArchetype.ChunkCount - 1; sourceChunkIndex >= 0; sourceChunkIndex--)
        {
            var sourceChunk = sourceArchetype.GetChunk(sourceChunkIndex);
            var sourceCount = sourceChunk.Count;
            if (sourceCount == 0)
            {
                continue;
            }

            var sourceEntities = sourceChunk.RawEntities;
            var sourceChunkId = sourceChunk.GlobalId;
            var sourceEnd = sourceCount;
            while (sourceEnd > 0)
            {
                var targetChunkId = targetArchetype.HasAvailableChunk() ? -1 : AllocateChunkId();
                var reserved = targetArchetype.ReserveRange(
                    sourceEnd,
                    targetChunkId,
                    out var targetChunkIndex,
                    out var targetChunk);
                var targetSlot = targetChunk.Count - reserved;
                var sourceSlot = sourceEnd - reserved;

                Array.Copy(sourceEntities, sourceSlot, targetChunk.RawEntities, targetSlot, reserved);
                for (var sourceComponentIndex = 0; sourceComponentIndex < edge.SourceToTargetRowIndices.Length; sourceComponentIndex++)
                {
                    var targetComponentIndex = edge.SourceToTargetRowIndices[sourceComponentIndex];
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
                }

                targetChunk.InitializeRowsRange(targetSlot, reserved, edge.AddedTargetRowIndices);
                for (var slot = 0; slot < reserved; slot++)
                {
                    var entity = sourceEntities[sourceSlot + slot];
                    _overlayTags.CopySlotTags(sourceChunkId, sourceSlot + slot, targetChunk.GlobalId, targetSlot + slot);
                    ref var record = ref RecordAt(entity.Index);
                    record.Archetype = targetArchetype.Id;
                    record.Chunk = targetChunkIndex;
                    record.SlotIndex = targetSlot + slot;
                }

                sourceEnd = sourceSlot;
                movedCount += reserved;
            }

            for (var slot = sourceCount - 1; slot >= 0; slot--)
            {
                _overlayTags.ClearSlot(sourceChunkId, slot);
            }

            sourceChunk.ClearAll();
            sourceArchetype.ReleaseChunk(sourceChunkIndex);
        }

        return movedCount;
    }

    private int DestroyChunk(Archetype archetype, int chunkIndex)
    {
        var chunk = archetype.GetChunk(chunkIndex);
        var count = chunk.Count;
        if (count == 0)
        {
            return 0;
        }

        var entities = chunk.RawEntities;
        var preserveOverlayTags = _overlayTags.HasAnyTags;
        for (var slot = count - 1; slot >= 0; slot--)
        {
            var entity = entities[slot];
            ref var record = ref RecordAt(entity.Index);
            record.Archetype = -1;
            record.Chunk = -1;
            record.SlotIndex = -1;
            record.Generation++;
            PushFree(entity.Index);
            if (preserveOverlayTags)
            {
                _overlayTags.ClearSlot(chunk.GlobalId, slot);
            }
        }

        chunk.ClearAll();
        archetype.ReleaseChunk(chunkIndex);
        AliveEntityCount -= count;
        return count;
    }

    private List<Entity> CollectTaggedQueryEntities(
        QueryDescription query,
        int[] matchingArchetypes)
    {
        var matches = new List<Entity>();
        var scratch = RentChunkOverlayScratch();
        try
        {
            for (var matchingIndex = 0; matchingIndex < matchingArchetypes.Length; matchingIndex++)
            {
                var archetype = _archetypes[matchingArchetypes[matchingIndex]];
                for (var activeChunkIndex = 0; activeChunkIndex < archetype.ActiveChunkCount; activeChunkIndex++)
                {
                    var chunk = archetype.GetActiveChunk(activeChunkIndex);

                    var result = _overlayTags.BuildMask(query, chunk.GlobalId, chunk.Count, scratch);
                    if (result == OverlayMaskResult.None)
                    {
                        continue;
                    }

                    var entities = chunk.Entities;
                    if (result == OverlayMaskResult.Full)
                    {
                        for (var slot = 0; slot < entities.Length; slot++)
                        {
                            matches.Add(entities[slot]);
                        }
                    }
                    else
                    {
                        for (var slot = 0; slot < entities.Length; slot++)
                        {
                            if ((scratch[slot >> 6] & (1UL << (slot & 63))) != 0)
                            {
                                matches.Add(entities[slot]);
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            ReturnChunkOverlayScratch(scratch);
        }

        return matches;
    }

    private void DestroyResolved(int recordIndex)
    {
        ref var record = ref RecordAt(recordIndex);
        var archetype = _archetypes[record.Archetype];
        var chunk = archetype.GetChunk(record.Chunk);
        var chunkId = chunk.GlobalId;
        var preserveOverlayTags = _overlayTags.HasAnyTags;
        var lastSlotIndex = chunk.Count - 1;
        var moved = archetype.RemoveEntity(record.Chunk, record.SlotIndex);
        if (preserveOverlayTags && record.SlotIndex != lastSlotIndex)
        {
            _overlayTags.MoveSlotBits(chunkId, lastSlotIndex, record.SlotIndex);
        }

        if (preserveOverlayTags)
        {
            _overlayTags.ClearSlot(chunkId, lastSlotIndex);
        }
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

    private void MoveEntity(int recordIndex, TransitionEdge edge)
    {
        ref var sourceRecord = ref RecordAt(recordIndex);
        var sourceArchetype = _archetypes[sourceRecord.Archetype];
        var targetArchetype = _archetypes[edge.TargetArchetypeId];
        var sourceChunk = sourceArchetype.GetChunk(sourceRecord.Chunk);
        var sourceChunkId = sourceChunk.GlobalId;
        var preserveOverlayTags = _overlayTags.HasAnyTags;
        var sourceSlotIndex = sourceRecord.SlotIndex;
        var sourceChunkIndex = sourceRecord.Chunk;
        var targetChunkId = targetArchetype.HasAvailableChunk() ? -1 : AllocateChunkId();
        targetArchetype.AddEntity(
            new Entity(recordIndex, sourceRecord.Generation),
            targetChunkId,
            out var targetChunkIndex,
            out var targetSlotIndex,
            out var reusedTargetSlot);
        var targetChunk = targetArchetype.GetChunk(targetChunkIndex);

        for (var sourceIndex = 0; sourceIndex < edge.SourceToTargetRowIndices.Length; sourceIndex++)
        {
            var targetIndex = edge.SourceToTargetRowIndices[sourceIndex];
            if (targetIndex >= 0)
            {
                sourceChunk.CopySlotTo(targetChunk, sourceSlotIndex, targetSlotIndex, sourceIndex, targetIndex);
            }
        }

        if (reusedTargetSlot)
        {
            targetChunk.InitializeRows(targetSlotIndex, edge.AddedTargetRowIndices);
        }

        if (preserveOverlayTags)
        {
            _overlayTags.CopySlotTags(sourceChunkId, sourceSlotIndex, targetChunk.GlobalId, targetSlotIndex);
        }
        var lastSlotIndex = sourceChunk.Count - 1;
        var moved = sourceArchetype.RemoveEntity(sourceChunkIndex, sourceSlotIndex);
        if (preserveOverlayTags && sourceSlotIndex != lastSlotIndex)
        {
            _overlayTags.MoveSlotBits(sourceChunkId, lastSlotIndex, sourceSlotIndex);
        }

        if (preserveOverlayTags)
        {
            _overlayTags.ClearSlot(sourceChunkId, lastSlotIndex);
        }
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
        if (targetMask.IsEmpty)
        {
            throw new InvalidOperationException("An archetype cannot have zero dense components.");
        }

        var target = GetOrCreateArchetype(targetMask);
        var mapping = new int[source.ComponentCount];
        var copiedTargetRows = new bool[target.ComponentCount];
        for (var i = 0; i < mapping.Length; i++)
        {
            mapping[i] = target.Mask.Rank(source.ComponentIds[i]);
            if (mapping[i] >= 0)
            {
                copiedTargetRows[mapping[i]] = true;
            }
        }

        var addedTargetRows = new int[target.ComponentCount];
        var addedCount = 0;
        for (var targetIndex = 0; targetIndex < copiedTargetRows.Length; targetIndex++)
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
        if (!archetype.TryGetComponentIndex(componentId, out var componentIndex)
            || !_layouts.TryGet(componentId, out var layout)
            || !IsCompatibleComponentType<T>(layout))
        {
            return false;
        }

        archetype.GetChunk(record.Chunk).GetComponentRow<T>(componentIndex)[record.SlotIndex] = value;
        return true;
    }

    private Archetype GetOrCreateArchetype(ComponentMask mask)
    {
        if (_archetypeByMask.TryGetValue(mask, out var existing))
        {
            return _archetypes[existing];
        }

        var componentIds = new ComponentId[mask.Count];
        mask.CopyComponentIds(componentIds);
        var layouts = new ComponentLayout[componentIds.Length];
        var rowOperations = new ComponentRowOperations[componentIds.Length];
        for (var i = 0; i < componentIds.Length; i++)
        {
            if (!_layouts.TryGet(componentIds[i], out var layout))
            {
                throw new InvalidOperationException($"Missing component layout for {componentIds[i].Value}.");
            }

            if (layout.StorageClass != ComponentStorageClass.Dense)
            {
                throw new InvalidOperationException("Non-dense component classes are not supported yet in this delivery.");
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
        for (var i = 0; i < componentIds.Length; i++)
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

        var index = _records.Count;
        _records.Add(new EntityRecord { Generation = 0, Archetype = -1, Chunk = -1, SlotIndex = -1 });
        return index;
    }

    private void PushFree(int recordIndex)
    {
        if (_freeCount == _freeRecords.Length)
        {
            Array.Resize(ref _freeRecords, Math.Max(4, _freeRecords.Length * 2));
        }

        _freeRecords[_freeCount++] = recordIndex;
    }

    private int AllocateChunkId() => _nextChunkId++;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref EntityRecord RecordAt(int recordIndex)
    {
        return ref CollectionsMarshal.AsSpan(_records)[recordIndex];
    }

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

    private CachedQuery GetOrCreateQuery(QueryDescription description)
    {
        if (_queryCache.TryGetValue(description, out var cached))
        {
            return cached;
        }

        cached = new CachedQuery(description);
        _queryCache.Add(description, cached);
        return cached;
    }

    private void EnsureNoActiveLease(string operation)
    {
        if (_activeChunkLeases > 0)
        {
            throw new InvalidOperationException($"Cannot {operation} while chunk leases are active.");
        }
    }

    private void ValidateQueryHandle(in QueryHandle query)
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
            Array.Clear(_batchEdgeStamps, 0, _batchEdgeStamps.Length);
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

        var capacity = Math.Max(required, _batchEdgeSlots.Length == 0 ? 4 : _batchEdgeSlots.Length * 2);
        Array.Resize(ref _batchEdgeSlots, capacity);
        Array.Resize(ref _batchEdgeStamps, capacity);
    }

    private void EnsureDestroyScratch(int required)
    {
        if (required > _destroyScratch.Length)
        {
            Array.Resize(ref _destroyScratch, Math.Max(required, _destroyScratch.Length * 2));
        }
    }

    private static bool IsCompatibleComponentType<T>(ComponentLayout layout)
    {
        return layout.RuntimeType == typeof(T);
    }

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
            var result = x.Archetype.CompareTo(y.Archetype);
            if (result != 0) return result;
            result = x.Chunk.CompareTo(y.Chunk);
            return result != 0 ? result : y.SlotIndex.CompareTo(x.SlotIndex);
        }
    }

}
