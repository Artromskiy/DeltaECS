namespace DVG.ECS;

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
    private int _nextChunkId;
    private int _activeChunkLeases;
    private int _chunkAccessorId;
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
        if (!Canonicalize(componentIds, out var canonical, out var mask))
        {
            throw new InvalidOperationException("Component list is empty or invalid.");
        }

        return new ArchetypeHandle(this, GetOrCreateArchetype(mask, canonical).Id);
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

    [Obsolete("Unsafe API: returned reference can escape active archetype/slot ownership and must not be used outside a live lease scope.")]
    public ref T GetComponentRefUnsafe<T>(Entity entity, ComponentId componentId)
    {
        if (!TryResolve(entity, out var recordIndex))
        {
            throw new InvalidOperationException("Unable to get component reference.");
        }

        ref readonly var record = ref RecordAt(recordIndex);
        var archetype = _archetypes[record.Archetype];
        if (!archetype.TryGetComponentIndex(componentId, out var componentIndex)
            || !_layouts.TryGet(componentId, out var layout)
            || !IsCompatibleComponentType<T>(layout))
        {
            throw new InvalidOperationException("Unable to get component reference.");
        }

        return ref archetype.GetChunk(record.Chunk).GetComponentRow<T>(componentIndex)[record.SlotIndex];
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

    public void Query(in QueryDescription query, QueryAccess access, Action<DenseChunkScope> body)
    {
        var cached = GetOrCreateQuery(query);
        var archetypes = cached.MatchingArchetypes(this);
        var hasTags = !query.AllTags.IsEmpty || !query.AnyTags.IsEmpty || !query.NoneTags.IsEmpty;
        var writeTick = access == QueryAccess.Write ? AdvanceWorldTick() : 0;
        for (var i = 0; i < archetypes.Length; i++)
        {
            var archetype = _archetypes[archetypes[i]];
            var rowIndices = cached.ComponentRowIndices(i);
            for (var chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
            {
                var chunk = archetype.GetChunk(chunkIndex);
                if (chunk.IsEmpty)
                {
                    continue;
                }

                var overlay = BuildOverlayMask(query, hasTags, chunk.GlobalId, chunk.Size, out var fullMask);
                if (hasTags && overlay is null && !fullMask)
                {
                    continue;
                }

                if (writeTick != 0)
                {
                    MarkQueryRows(chunk, rowIndices, writeTick);
                }

                _activeChunkLeases++;
                using var scope = new DenseChunkScope(this, archetype, chunk, chunk.GlobalId, overlay, fullMask);
                body(scope);
            }
        }
    }

    public void Query<TState>(in QueryHandle handle, QueryAccess access, ref TState state, ChunkAction<TState> body)
    {
        if (!ReferenceEquals(handle.Owner, this) || !handle.IsValid)
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(handle));
        }

        var query = handle.Description;
        var cached = handle.Cached;
        var archetypes = cached.MatchingArchetypes(this);
        var hasTags = !query.AllTags.IsEmpty || !query.AnyTags.IsEmpty || !query.NoneTags.IsEmpty;
        var writeTick = access == QueryAccess.Write ? AdvanceWorldTick() : 0;
        _activeChunkLeases++;
        try
        {
            for (var i = 0; i < archetypes.Length; i++)
            {
                var archetype = _archetypes[archetypes[i]];
                var rowIndices = cached.ComponentRowIndices(i);
                for (var chunkIndex = 0; chunkIndex < archetype.ChunkCount; chunkIndex++)
                {
                    var chunk = archetype.GetChunk(chunkIndex);
                    if (chunk.IsEmpty)
                    {
                        continue;
                    }

                    var chunkId = chunk.GlobalId;
                    var overlay = BuildOverlayMask(query, hasTags, chunkId, chunk.Size, out var fullMask);
                    if (hasTags && overlay is null && !fullMask)
                    {
                        continue;
                    }

                    if (writeTick != 0)
                    {
                        MarkQueryRows(chunk, rowIndices, writeTick);
                    }

                    var accessor = new DenseChunkAccessor(this, archetype, chunk, chunkId, rowIndices, overlay, fullMask, RentChunkAccessor());
                    try
                    {
                        body(ref state, ref accessor);
                    }
                    finally
                    {
                        accessor.Dispose();
                    }
                }
            }
        }
        finally
        {
            _activeChunkLeases--;
            InvalidateChunkAccessors();
        }
    }

    public CachedChunkEnumerator QueryChunks(in QueryHandle handle, QueryAccess access = QueryAccess.Read)
    {
        if (!ReferenceEquals(handle.Owner, this) || !handle.IsValid)
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(handle));
        }

        return new CachedChunkEnumerator(this, handle.Cached, handle.Description, access == QueryAccess.Write ? AdvanceWorldTick() : 0);
    }

    public ref struct CachedChunkEnumerator
    {
        private readonly World _owner;
        private readonly QueryDescription _query;
        private readonly CachedQuery _cached;
        private readonly int[] _archetypeIds;
        private readonly bool _hasTags;
        private readonly uint _writeTick;
        private int _archetypePosition;
        private int _chunkPosition;
        private DenseChunkAccessor _current;
        private bool _hasCurrent;
        private bool _disposed;

        internal CachedChunkEnumerator(World owner, CachedQuery cached, QueryDescription query, uint writeTick)
        {
            _owner = owner;
            _cached = cached;
            _query = query;
            _archetypeIds = cached.MatchingArchetypes(owner);
            _hasTags = !query.AllTags.IsEmpty
                || !query.AnyTags.IsEmpty
                || !query.NoneTags.IsEmpty;
            _writeTick = writeTick;
            _archetypePosition = 0;
            _chunkPosition = 0;
            _current = default;
            _hasCurrent = false;
            _disposed = false;
            _owner._activeChunkLeases++;
        }

        public DenseChunkAccessor Current
        {
            get
            {
                if (!_hasCurrent || _disposed)
                {
                    throw new InvalidOperationException("The cached chunk enumerator is not positioned on a chunk.");
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

            if (_hasCurrent)
            {
                _current.Dispose();
                _hasCurrent = false;
            }

            while (_archetypePosition < _archetypeIds.Length)
            {
                var archetype = _owner._archetypes[_archetypeIds[_archetypePosition]];
                var rowIndices = _cached.ComponentRowIndices(_archetypePosition);
                while (_chunkPosition < archetype.ChunkCount)
                {
                    var chunkIndex = _chunkPosition++;
                    var chunk = archetype.GetChunk(chunkIndex);
                    if (chunk.IsEmpty)
                    {
                        continue;
                    }

                    var chunkId = chunk.GlobalId;
                    var overlay = _owner.BuildOverlayMask(_query, _hasTags, chunkId, chunk.Size, out var fullMask);
                    if (_hasTags && overlay is null && !fullMask)
                    {
                        continue;
                    }

                    if (_writeTick != 0)
                    {
                        MarkQueryRows(chunk, rowIndices, _writeTick);
                    }

                    _current = new DenseChunkAccessor(_owner, archetype, chunk, chunkId, rowIndices, overlay, fullMask, _owner.RentChunkAccessor());
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

            if (_hasCurrent)
            {
                _current.Dispose();
                _hasCurrent = false;
            }

            _owner._activeChunkLeases--;
            _owner.InvalidateChunkAccessors();
            _disposed = true;
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

    internal void CompleteChunkScope() => _activeChunkLeases--;
    internal int RentChunkAccessor() => ++_chunkAccessorId;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsChunkAccessorIdValid(int accessorId) => accessorId == _chunkAccessorId;
    internal void InvalidateChunkAccessors() => _chunkAccessorId++;
    internal void ReturnChunkLeaseOverlay(ulong[]? overlayMask)
    {
        if (overlayMask is not null)
        {
            ArrayPool<ulong>.Shared.Return(overlayMask, clearArray: true);
        }
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

    private static void MarkQueryRows(Chunk chunk, int[] rowIndices, uint writeTick)
    {
        for (var rowIndex = 0; rowIndex < rowIndices.Length; rowIndex++)
        {
            chunk.MarkComponentWritten(rowIndices[rowIndex], writeTick);
        }
    }

    private int ApplyComponents(bool isAdd, ComponentId[] componentIds, ReadOnlySpan<Entity> entities)
    {
        EnsureNoActiveLease(isAdd ? "add components" : "remove components");
        if (componentIds.Length == 0 || entities.Length == 0)
        {
            return 0;
        }

        var normalizedComponents = new ComponentId[componentIds.Length];
        componentIds.AsSpan().CopyTo(normalizedComponents);
        var componentCount = NormalizeInPlace(normalizedComponents);
        if (componentCount == 0)
        {
            return 0;
        }

        var changeMask = ComponentMask.From(normalizedComponents.AsSpan(0, componentCount));
        var edgesBySource = new Dictionary<int, TransitionEdge>();
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
            if (!edgesBySource.TryGetValue(sourceArchetypeId, out var edge))
            {
                edge = GetTransitionEdge(sourceArchetypeId, changeMask, isAdd);
                edgesBySource.Add(sourceArchetypeId, edge);
            }

            if (edge.TargetArchetypeId == sourceArchetypeId)
            {
                continue;
            }

            MoveEntity(recordIndex, edge);
            changed++;
        }

        return changed;
    }

    private void DestroyResolved(int recordIndex)
    {
        ref var record = ref RecordAt(recordIndex);
        var archetype = _archetypes[record.Archetype];
        var chunk = archetype.GetChunk(record.Chunk);
        var chunkId = chunk.GlobalId;
        var lastSlotIndex = chunk.Size - 1;
        var moved = archetype.RemoveEntity(record.Chunk, record.SlotIndex);
        if (record.SlotIndex != lastSlotIndex)
        {
            _overlayTags.MoveSlotBits(chunkId, lastSlotIndex, record.SlotIndex);
        }

        _overlayTags.ClearSlot(chunkId, lastSlotIndex);
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

        _overlayTags.CopySlotTags(sourceChunkId, sourceSlotIndex, targetChunk.GlobalId, targetSlotIndex);
        var lastSlotIndex = sourceChunk.Size - 1;
        var moved = sourceArchetype.RemoveEntity(sourceChunkIndex, sourceSlotIndex);
        if (sourceSlotIndex != lastSlotIndex)
        {
            _overlayTags.MoveSlotBits(sourceChunkId, lastSlotIndex, sourceSlotIndex);
        }

        _overlayTags.ClearSlot(sourceChunkId, lastSlotIndex);
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

        var targetIds = new ComponentId[targetMask.Count];
        targetMask.CopyComponentIds(targetIds);
        var target = GetOrCreateArchetype(targetMask, targetIds);
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

    private Archetype GetOrCreateArchetype(ComponentMask mask, ComponentId[] componentIds)
    {
        if (_archetypeByMask.TryGetValue(mask, out var existing))
        {
            return _archetypes[existing];
        }

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

    private static bool Canonicalize(ComponentId[] components, out ComponentId[] canonical, out ComponentMask mask)
    {
        canonical = new ComponentId[components.Length];
        var count = 0;
        for (var i = 0; i < components.Length; i++)
        {
            if (components[i].IsValid)
            {
                canonical[count++] = components[i];
            }
        }

        if (count == 0)
        {
            canonical = Array.Empty<ComponentId>();
            mask = default;
            return false;
        }

        Array.Sort(canonical, 0, count, ComponentIdComparer.Instance);
        var uniqueCount = 1;
        for (var i = 1; i < count; i++)
        {
            if (canonical[i] != canonical[uniqueCount - 1])
            {
                canonical[uniqueCount++] = canonical[i];
            }
        }

        if (uniqueCount != canonical.Length)
        {
            Array.Resize(ref canonical, uniqueCount);
        }

        mask = ComponentMask.From(canonical);
        return true;
    }

    private static int NormalizeInPlace(Span<ComponentId> components)
    {
        var count = 0;
        for (var i = 0; i < components.Length; i++)
        {
            if (components[i].IsValid)
            {
                components[count++] = components[i];
            }
        }

        components[..count].Sort(ComponentIdComparer.Instance);
        var uniqueCount = count == 0 ? 0 : 1;
        for (var i = 1; i < count; i++)
        {
            if (components[i] != components[uniqueCount - 1])
            {
                components[uniqueCount++] = components[i];
            }
        }

        return uniqueCount;
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

    private ulong[]? BuildOverlayMask(QueryDescription query, bool hasTags, int chunkId, int chunkSize, out bool fullMask)
    {
        fullMask = true;
        if (!hasTags)
        {
            return null;
        }

        var candidate = ArrayPool<ulong>.Shared.Rent(_overlayTags.WordsPerChunk);
        if (!_overlayTags.TryBuildMask(query, chunkId, chunkSize, candidate))
        {
            ArrayPool<ulong>.Shared.Return(candidate, clearArray: true);
            fullMask = false;
            return null;
        }

        if (IsAllOnes(candidate, chunkSize))
        {
            ArrayPool<ulong>.Shared.Return(candidate, clearArray: true);
            return null;
        }

        fullMask = false;
        return candidate;
    }

    private void EnsureNoActiveLease(string operation)
    {
        if (_activeChunkLeases > 0)
        {
            throw new InvalidOperationException($"Cannot {operation} while chunk leases are active.");
        }
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

    private static bool IsAllOnes(ulong[] words, int chunkSize)
    {
        var fullWords = chunkSize >> 6;
        for (var i = 0; i < fullWords; i++)
        {
            if (words[i] != ulong.MaxValue)
            {
                return false;
            }
        }

        var remaining = chunkSize & 63;
        return remaining == 0 || words[fullWords] == (1UL << remaining) - 1UL;
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

    private sealed class ComponentIdComparer : IComparer<ComponentId>
    {
        public static readonly ComponentIdComparer Instance = new();

        public int Compare(ComponentId x, ComponentId y) => x.Value.CompareTo(y.Value);
    }
}
