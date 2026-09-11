namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public sealed partial class World : IDisposable
{
    private const int DefaultChunkCapacity = 512;
    private const int DefaultInitialCapacity = 1024;

    private readonly ComponentLayoutRegistry _layouts;
    private readonly int _chunkCapacity;
    private readonly List<Archetype> _archetypes = new();
    private readonly ComponentRowArrayPool _componentRowArrayPool = new();
    private readonly List<Chunk> _freeRecordChunks = new();
    private readonly Dictionary<ComponentMask, int> _archetypeByMask = new();
    private readonly EntityRecordStorage _records = new();
    private NativeMemory<int> _freeRecords = new(16);
    private int _freeCount;
    private readonly Dictionary<TransitionKey, TransitionEdge> _transitionCache = new();
    private readonly Dictionary<QuerySpec, WeakReference<QueryPlan>> _queryCache = new(QuerySpec.Comparer);
    private int _queryCacheSweepCountdown = 64;
    private NativeMemory<DestroyEntry> _destroyScratch = new(32);
    private NativeMemory<Entity> _sequenceScratch = new(0);
    private readonly List<Archetype> _generatedWhereAffectedArchetypes = new(16);
    private GeneratedWhereStructuralPlan[] _generatedWhereArchetypePlans = Array.Empty<GeneratedWhereStructuralPlan>();
    private GeneratedWhereTargetCursor?[] _generatedWhereTargetCursors = Array.Empty<GeneratedWhereTargetCursor?>();
    private NativeMemory<int> _generatedWhereTargetCursorStamps = new(0);
    private NativeMemory<int> _generatedWhereAffectedArchetypeStamps = new(0);
    private Archetype[] _generatedWhereSourceArchetypes = Array.Empty<Archetype>();
    private NativeMemory<int> _generatedWhereSourceCounts = new(0);
    private int _generatedWhereSourceArchetypeCount;
    private int _generatedWherePlanStamp;
    private int _generatedWhereAffectedArchetypeStamp;
    private int _generatedWhereDestroyCapacity;
    private bool _generatedWhereDestroyCapacityReserved;
    private bool _generatedWhereStructuralActive;
    private TransitionEdge[] _batchEdgeSlots = Array.Empty<TransitionEdge>();
    private NativeMemory<int> _batchEdgeStamps = new(0);
    private int _batchEdgeStamp;
    private int _nextChunkId;
    private Chunk?[] _chunksById = Array.Empty<Chunk?>();
    private int _activeChunkLeases;
    private QueryWriteSession? _queryWriteSessionPool;
    private int _archetypeVersion;
    private Stamp[][] _archetypeComponentWriteStamps = Array.Empty<Stamp[]>();
    private NativeMemory<Stamp>[] _chunkComponentWriteStamps = Array.Empty<NativeMemory<Stamp>>();
    private int[] _archetypeStampComponentCounts = Array.Empty<int>();
    private int[] _chunkStampComponentCounts = Array.Empty<int>();
    private bool _disposed;

    public World(
        ComponentLayoutRegistry? layouts = null,
        int initialEntityCapacity = DefaultInitialCapacity,
        int chunkCapacity = DefaultChunkCapacity)
    {
        ThrowHelper.ThrowIfNegative(initialEntityCapacity, nameof(initialEntityCapacity));

        ThrowHelper.ThrowIfNegativeOrZero(chunkCapacity, nameof(chunkCapacity));

        _layouts = layouts ?? new ComponentLayoutRegistry();
        _chunkCapacity = chunkCapacity;
        _records.Capacity = initialEntityCapacity;
    }

    public int ArchetypeVersion => _archetypeVersion;

    public int AliveEntityCount { get; private set; }

    public ComponentLayoutRegistry Layouts => _layouts;

    internal bool IsDisposed => _disposed;

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
        foreach (var weakQueryPlan in _queryCache.Values)
        {
            if (weakQueryPlan.TryGetTarget(out QueryPlan? queryPlan))
            {
                queryPlan.Dispose();
            }
        }

        _queryCache.Clear();

        foreach (var archetype in _archetypes)
        {
            archetype.Dispose();
        }

        DisposeParallelQueryExecutor();
        _freeRecords.Dispose();
        _destroyScratch.Dispose();
        _sequenceScratch.Dispose();
        _generatedWhereSourceCounts.Dispose();
        _generatedWhereTargetCursorStamps.Dispose();
        _generatedWhereAffectedArchetypeStamps.Dispose();
        _batchEdgeStamps.Dispose();
        DisposeStampLayers();
        _componentRowArrayPool.Clear();
        _chunksById = Array.Empty<Chunk?>();
        _freeRecordChunks.Clear();
        GC.SuppressFinalize(this);
    }

    public ArchetypeHandle GetOrCreateArchetype(params ReadOnlySpan<ComponentId> componentIds)
    {
        EnsureNoActiveLease("create an archetype");
        if (!TryBuildComponentMask(componentIds, out var mask))
        {
            ThrowHelper.ThrowInvalidComponentList();
        }

        return new ArchetypeHandle(this, GetOrCreateArchetype(mask).Id);
    }

    public ArchetypeHandle GetOrCreateArchetype(ComponentId first, ComponentId second)
        => GetOrCreateArchetype(stackalloc[] { first, second });

    public Query CreateQuery(in QuerySpec spec) => new Query(this, GetOrCreateQuery(spec), spec);

    /// <summary>Begins a validated query execution scope with independent iterators.</summary>
    public QueryScope BeginScope(in Query handle) => new QueryScope(this, handle);

    public Entity Create(params ReadOnlySpan<ComponentId> componentIds)
    {
        Span<Entity> entities = stackalloc Entity[1];
        return Create(componentIds, entities) == 0 ? default : entities.GetRefAtZero();
    }

    public int Create(ReadOnlySpan<ComponentId> componentIds, Span<Entity> output)
    {
        EnsureNoActiveLease("create entities");
        if (output.Length == 0)
        {
            return 0;
        }

        if (!TryBuildComponentMask(componentIds, out var mask))
        {
            ThrowHelper.ThrowInvalidComponentList();
        }

        var archetype = GetOrCreateArchetype(mask);
        return CreateBatch(archetype, output);
    }

    /// <summary>Creates entities into the supplied archetype without allocating entity output storage.</summary>
    public int Create(ArchetypeHandle handle, int count)
    {
        ThrowHelper.ThrowIfNegative(count, nameof(count));
        EnsureNoActiveLease("create entities");
        if (count == 0)
        {
            return 0;
        }

        return CreateBatch(ResolveArchetype(handle), count);
    }

    /// <summary>Creates <paramref name="count"/> entities with the supplied component set.</summary>
    /// <remarks>The returned array owns the entity handles and is allocated once for the batch.</remarks>
    public Entity[] Create(ReadOnlySpan<ComponentId> componentIds, int count)
    {
        ThrowHelper.ThrowIfNegative(count, nameof(count));
        var output = new Entity[count];
        Create(componentIds, output);
        return output;
    }

    /// <summary>Creates a requested number of entities into caller-owned storage.</summary>
    public int Create(ReadOnlySpan<ComponentId> componentIds, int count, Span<Entity> output)
    {
        ThrowHelper.ThrowIfNegative(count, nameof(count));
        if (output.Length < count)
        {
            ThrowHelper.ThrowEntityDestinationTooSmall(nameof(output));
        }

        return Create(componentIds, output[..count]);
    }

    public Entity Create(ArchetypeHandle handle)
    {
        Span<Entity> entities = stackalloc Entity[1];
        return Create(handle, entities) == 0 ? default : entities.GetRefAtZero();
    }

    public int Create(ArchetypeHandle handle, Span<Entity> output)
    {
        EnsureNoActiveLease("create entities");
        if (output.Length == 0)
        {
            return 0;
        }

        var archetype = ResolveArchetype(handle);
        return CreateBatch(archetype, output);
    }

    private int CreateBatch(Archetype archetype, Span<Entity> output)
        => CreateBatch(archetype, output.Length, output);

    private int CreateBatch(Archetype archetype, int count)
        => CreateBatch(archetype, count, Span<Entity>.Empty);

    private int CreateBatch(Archetype archetype, int count, Span<Entity> output)
    {
        if (count == 0)
        {
            return 0;
        }

        _records.EnsureCapacity(checked(_records.Count + count));
        int outputIndex = 0;
        while (outputIndex < count)
        {
            int remaining = count - outputIndex;
            int chunkId = archetype.HasAvailableChunk(remaining) ? -1 : AllocateChunkId();
            int reserved = archetype.ReserveRange(
                remaining,
                chunkId,
                out _,
                out var chunk,
                out int reusedCount);
            RegisterChunkStampStorage(chunk);
            int slotIndex = chunk.Count - reserved;
            if (reusedCount != 0)
            {
                chunk.InitializeSlotRange(slotIndex, reusedCount);
            }

            chunk.StampAllRange(slotIndex, reserved, new Stamp(1));
            for (int reservedIndex = 0; reservedIndex < reserved; reservedIndex++)
            {
                int reservedSlot = slotIndex + reservedIndex;
                int recordIndex;
                if (chunk.TryTakeFreeRecordForSlot(reservedSlot, out int freeRecordIndex))
                {
                    if (!chunk.HasFreeRecordBlock)
                    {
                        CompleteFreeRecordBlock(chunk);
                    }

                    recordIndex = RecycleRecord(freeRecordIndex);
                }
                else
                {
                    recordIndex = AllocateRecord();
                }

                ref var record = ref RecordAt(recordIndex);
                var entity = new Entity(recordIndex, record.Generation);
                record.ChunkId = chunk.GlobalId;
                record.SlotIndex = reservedSlot;
                chunk.RawEntities.RefAt(reservedSlot) = entity;
                if (!output.IsEmpty)
                {
                    output.RefAt(outputIndex) = entity;
                }

                outputIndex++;
                AliveEntityCount++;
            }
        }

        return count;
    }

    private Archetype ResolveArchetype(ArchetypeHandle handle)
    {
        if (!handle.IsValid
            || !ReferenceEquals(handle.Owner, this)
            || (uint)handle.ArchetypeId >= (uint)_archetypes.Count)
        {
            ThrowHelper.ThrowArchetypeHandleInvalid(nameof(handle));
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
        return true;
    }

    public int Destroy(ReadOnlySpan<Entity> entities)
    {
        EnsureNoActiveLease("destroy entities");
        EnsureDestroyScratch(entities.Length);
        int count = 0;
        for (int i = 0; i < entities.Length; i++)
        {
            if (TryResolve(entities.RefAt(i), out int recordIndex))
            {
                ref readonly var record = ref RecordAt(recordIndex);
                Chunk chunk = GetRecordChunk(record);
                _destroyScratch.RefAt(count++) = new DestroyEntry(
                    entities.RefAt(i),
                    recordIndex,
                    chunk.ArchetypeId,
                    chunk.GlobalId,
                    record.SlotIndex);
            }
        }

        if (count == 0)
        {
            return 0;
        }

        if (count == 1)
        {
            // A single valid handle needs neither sorting nor a second
            // record/lifetime validation: no mutation has happened yet.
            DestroyResolved(_destroyScratch.GetRefAtZero().RecordIndex);
            return 1;
        }

        if (IsAscendingDestroyOrder(count))
        {
            EnsureFreeRecordCapacity(_freeCount + count);
            return DestroyAscendingBatch(count);
        }

        SpanSortCompat.Sort(_destroyScratch.Span[..count], DestroyEntryComparer.Instance);
        EnsureFreeRecordCapacity(_freeCount + count);
        int destroyed = 0;
        int groupStart = 0;
        while (groupStart < count)
        {
            var first = _destroyScratch.RefAt(groupStart);
            int groupEnd = groupStart + 1;
            while (groupEnd < count)
            {
                var next = _destroyScratch.RefAt(groupEnd);
                if (next.Archetype != first.Archetype || next.ChunkId != first.ChunkId)
                {
                    break;
                }

                groupEnd++;
            }

            int groupCount = groupEnd - groupStart;
            var archetype = _archetypes[first.Archetype];
            var chunk = GetChunkById(first.ChunkId);
            if (groupCount == chunk.Count && IsCompleteDestroyChunk(groupStart, groupCount))
            {
                destroyed += DestroyChunk(archetype, chunk.ArchetypeIndex);
            }
            else
            {
                for (int index = groupStart; index < groupEnd; index++)
                {
                    var entry = _destroyScratch.RefAt(index);
                    if (IsCurrentDestroyEntry(entry))
                    {
                        DestroyResolved(entry.RecordIndex);
                        destroyed++;
                    }
                }
            }

            groupStart = groupEnd;
        }

        return destroyed;
    }

    private int DestroyAscendingBatch(int count)
    {
        int destroyed = 0;
        int groupEnd = count;
        while (groupEnd > 0)
        {
            var last = _destroyScratch.RefAt(groupEnd - 1);
            int groupStart = groupEnd - 1;
            while (groupStart > 0)
            {
                var previous = _destroyScratch.RefAt(groupStart - 1);
                if (previous.Archetype != last.Archetype || previous.ChunkId != last.ChunkId)
                {
                    break;
                }

                groupStart--;
            }

            int groupCount = groupEnd - groupStart;
            var archetype = _archetypes[last.Archetype];
            var chunk = GetChunkById(last.ChunkId);
            if (groupCount == chunk.Count && IsCompleteAscendingDestroyChunk(groupStart, groupCount))
            {
                destroyed += DestroyChunk(archetype, chunk.ArchetypeIndex);
            }
            else
            {
                for (int index = groupEnd - 1; index >= groupStart; index--)
                {
                    var entry = _destroyScratch.RefAt(index);
                    if (IsCurrentDestroyEntry(entry))
                    {
                        DestroyResolved(entry.RecordIndex);
                        destroyed++;
                    }
                }
            }

            groupEnd = groupStart;
        }

        return destroyed;
    }

    public bool IsAlive(Entity entity) => TryResolve(entity, out _);

    private bool SetCore<T>(Entity entity, ComponentId componentId, in T value)
    {
        if (!TryResolve(entity, out int recordIndex))
        {
            ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
        }

        ref readonly var record = ref RecordAt(recordIndex);
        if (!GetRecordArchetype(record).Contains(componentId))
        {
            ThrowHelper.ThrowMissingComponent<T>(entity, componentId);
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
        var chunk = GetRecordChunk(record);
        var archetype = _archetypes[chunk.ArchetypeId];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex))
        {
            return false;
        }

        stamp = GetComponentStamp(
            archetype.Id,
            chunk,
            componentIndex,
            record.SlotIndex);
        return true;
    }

    private bool TryGetCore<T>(Entity entity, ComponentId componentId, out T value)
    {
        if (!TryResolve(entity, out int recordIndex))
        {
            value = default!;
            return false;
        }

        ref readonly var record = ref RecordAt(recordIndex);
        var chunk = GetRecordChunk(record);
        var archetype = _archetypes[chunk.ArchetypeId];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex)
            || !_layouts.TryGet(componentId, out var layout)
            || !IsCompatibleComponentType<T>(layout))
        {
            value = default!;
            return false;
        }

        value = chunk.GetComponentRow<T>(componentIndex).RefAt(record.SlotIndex);
        return true;
    }

    /// <summary>Adds the component set to one entity and reports whether it changed.</summary>
    public bool Add(ComponentId[] componentIds, Entity entity)
    {
        Span<Entity> entities = stackalloc Entity[1];
        entities.GetRefAtZero() = entity;
        return ApplyComponents(true, componentIds, entities) == 1;
    }

    public int Add(ComponentId[] componentIds, ReadOnlySpan<Entity> entities) => ApplyComponents(true, componentIds, entities);

    /// <summary>Adds a component set to every eligible entity in a caller-owned batch.</summary>
    public int Add(ReadOnlySpan<ComponentId> componentIds, ReadOnlySpan<Entity> entities)
        => ApplyComponents(true, componentIds, entities);

    /// <summary>Removes the component set from one entity and reports whether it changed.</summary>
    public bool Remove(ComponentId[] componentIds, Entity entity)
    {
        Span<Entity> entities = stackalloc Entity[1];
        entities.GetRefAtZero() = entity;
        return ApplyComponents(false, componentIds, entities) == 1;
    }

    public int Remove(ComponentId[] componentIds, ReadOnlySpan<Entity> entities) => ApplyComponents(false, componentIds, entities);

    /// <summary>Removes a component set from every eligible entity in a caller-owned batch.</summary>
    public int Remove(ReadOnlySpan<ComponentId> componentIds, ReadOnlySpan<Entity> entities)
        => ApplyComponents(false, componentIds, entities);

    public int Add(in Query query, ComponentId[] componentIds) => ApplyQueryComponents(query, true, componentIds);

    /// <summary>Adds a component set to every entity matched by a query.</summary>
    public int Add(in Query query, ReadOnlySpan<ComponentId> componentIds)
        => ApplyQueryComponents(query, true, componentIds);

    public int Remove(in Query query, ComponentId[] componentIds) => ApplyQueryComponents(query, false, componentIds);

    /// <summary>Removes a component set from every entity matched by a query.</summary>
    public int Remove(in Query query, ReadOnlySpan<ComponentId> componentIds)
        => ApplyQueryComponents(query, false, componentIds);

    public int Destroy(in Query query)
    {
        ValidateQuery(query);
        EnsureNoActiveLease("destroy entities");

        var cached = query.Cached;
        ReadOnlySpan<int> archetypes = cached.MatchingArchetypes();
        int destroyed = 0;
        for (int archetypeIndex = 0; archetypeIndex < archetypes.Length; archetypeIndex++)
        {
            var archetype = _archetypes[archetypes.RefAt(archetypeIndex)];
            archetype.DeferQueryPlanUpdates();
            for (int chunkIndex = archetype.ChunkCount - 1; chunkIndex >= 0; chunkIndex--)
            {
                if (archetype.GetChunk(chunkIndex).Count == 0)
                {
                    continue;
                }

                destroyed += DestroyChunk(archetype, chunkIndex);
            }

            archetype.RefreshQueryPlans();
        }

        return destroyed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp GetComponentStamp(
        int archetypeId,
        Chunk chunk,
        int componentIndex,
        int slotIndex)
        => StampMath.Sum(
            chunk.GetComponentStampTrusted(componentIndex, slotIndex),
            _chunkComponentWriteStamps.RefAt(chunk.GlobalId).RefAt(componentIndex),
            _archetypeComponentWriteStamps.RefAt(archetypeId).RefAt(componentIndex));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref Stamp GetChunkComponentStampReference(Chunk chunk, int componentIndex)
        => ref _chunkComponentWriteStamps.RefAt(chunk.GlobalId).RefAt(componentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NativeMemory<Stamp> GetChunkComponentStamps(Chunk chunk)
        => _chunkComponentWriteStamps.RefAt(chunk.GlobalId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref Stamp GetArchetypeComponentStampReference(int archetypeId, int componentIndex)
        => ref _archetypeComponentWriteStamps.RefAt(archetypeId).RefAt(componentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkChunkComponentWritten(Chunk chunk, int componentIndex, Stamp stamp)
        => CreateChunkComponentStampWriter(chunk, componentIndex, stamp).Mark();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkArchetypeComponentWritten(int archetypeId, int componentIndex, Stamp stamp)
        => CreateArchetypeComponentStampWriter(archetypeId, componentIndex, stamp).Mark();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp IncrementChunkComponentStamp(Chunk chunk, int componentIndex)
    {
        NativeMemory<Stamp> stamps = _chunkComponentWriteStamps.RefAt(chunk.GlobalId);
        ref Stamp value = ref stamps.RefAt(componentIndex);
        Stamp stamp = value.Next();
        value = stamp;
        return stamp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp IncrementArchetypeComponentStamp(int archetypeId, int componentIndex)
    {
        Stamp[] stamps = _archetypeComponentWriteStamps.RefAt(archetypeId);
        ref Stamp value = ref stamps.RefAt(componentIndex);
        Stamp stamp = value.Next();
        value = stamp;
        return stamp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal EntityComponentStampWriter CreateEntityComponentStampWriter(
        Chunk chunk,
        int componentIndex,
        int slotIndex,
        Stamp stamp)
        => new(chunk, componentIndex, slotIndex, stamp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ChunkComponentStampWriter CreateChunkComponentStampWriter(
        Chunk chunk,
        int componentIndex,
        Stamp stamp)
        => new(
            _chunkComponentWriteStamps.RefAt(chunk.GlobalId),
            componentIndex,
            stamp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ArchetypeComponentStampWriter CreateArchetypeComponentStampWriter(
        int archetypeId,
        int componentIndex,
        Stamp stamp)
        => new(_archetypeComponentWriteStamps.RefAt(archetypeId), componentIndex, stamp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp[] GetArchetypeComponentStamps(int archetypeId)
        => _archetypeComponentWriteStamps.RefAt(archetypeId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ClearChunkComponentStamps(Chunk chunk)
        => _chunkComponentWriteStamps.RefAt(chunk.GlobalId).Clear();

    public int CollectAliveEntities(Span<Entity> destination)
    {
        int count = 0;
        for (int i = 0; i < _records.Count; i++)
        {
            ref readonly var record = ref RecordAt(i);
            if (!TryResolve(new Entity(i, record.Generation), out _))
            {
                continue;
            }

            if (count >= destination.Length)
            {
                ThrowHelper.ThrowWorldDestinationOutOfRange(nameof(destination));
            }

            destination.RefAt(count++) = new Entity(i, record.Generation);
        }

        return count;
    }

    internal void BeginQueryLease() => _activeChunkLeases++;

    internal void EndQueryLease() => _activeChunkLeases--;

    internal QueryWriteSession RentQueryWriteSession(QueryPlan query, out int generation)
        => RentQueryWriteSession(query.HasWriteAccess, out generation);

    internal QueryWriteSession RentQueryWriteSession(bool writeEnabled, out int generation)
    {
        QueryWriteSession session;
        if (_queryWriteSessionPool is { } pooled)
        {
            session = pooled;
            _queryWriteSessionPool = pooled.Next;
        }
        else
        {
            session = new QueryWriteSession();
        }

        generation = session.Reset(writeEnabled);
        return session;
    }

    internal void ReturnQueryWriteSession(QueryWriteSession session, int generation)
    {
        if (!session.TryRelease(generation))
        {
            return;
        }

        _activeChunkLeases--;
        session.Next = _queryWriteSessionPool;
        _queryWriteSessionPool = session;
    }

    private int ApplyComponents(bool isAdd, ReadOnlySpan<ComponentId> componentIds, ReadOnlySpan<Entity> entities)
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
        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            var entity = entities.RefAt(entityIndex);
            if (!TryResolve(entity, out int recordIndex))
            {
                continue;
            }

            ref readonly var record = ref RecordAt(recordIndex);
            int sourceArchetypeId = GetRecordChunk(record).ArchetypeId;
            var sourceArchetype = _archetypes[sourceArchetypeId];
            ComponentMask targetMask = isAdd
                ? sourceArchetype.Mask.Or(changeMask)
                : sourceArchetype.Mask.Except(changeMask);
            if (targetMask == sourceArchetype.Mask)
            {
                continue;
            }

            var edge = edgeStamp == 0
                ? GetTransitionEdge(sourceArchetypeId, changeMask, isAdd, targetMask)
                : GetBatchTransitionEdge(sourceArchetypeId, changeMask, isAdd, targetMask, edgeStamp);

            MoveEntity(recordIndex, edge);
            changed++;
        }

        return changed;
    }

    private int ApplyQueryComponents(in Query query, bool isAdd, ReadOnlySpan<ComponentId> componentIds)
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
        ReadOnlySpan<int> matchingArchetypes = cached.MatchingArchetypes();
        int edgeStamp = BeginBatchEdgeCache();
        int changed = 0;
        for (int matchingIndex = 0; matchingIndex < matchingArchetypes.Length; matchingIndex++)
        {
            var sourceArchetype = _archetypes[matchingArchetypes.RefAt(matchingIndex)];
            ComponentMask targetMask = isAdd
                ? sourceArchetype.Mask.Or(changeMask)
                : sourceArchetype.Mask.Except(changeMask);
            if (targetMask == sourceArchetype.Mask)
            {
                continue;
            }

            if (sourceArchetype.ActiveChunkCount == 0)
            {
                continue;
            }

            var edge = GetBatchTransitionEdge(sourceArchetype.Id, changeMask, isAdd, targetMask, edgeStamp);
            changed += MoveArchetypeBlocks(sourceArchetype, edge);
        }

        return changed;
    }

    internal bool BeginGeneratedWhereStructural(
        in Query query,
        bool isDestroy,
        bool isAdd,
        ReadOnlySpan<ComponentId> componentIds,
        out GeneratedDenseExecution execution)
    {
        EnsureNoActiveLease(isDestroy
            ? "destroy entities"
            : isAdd ? "add components" : "remove components");
        ValidateQuery(in query);

        ComponentMask changeMask = default;
        if (!isDestroy && (componentIds.Length == 0 || !TryBuildComponentMask(componentIds, out changeMask)))
        {
            execution = default;
            return false;
        }

        QueryPlan plan = query.Cached;
        ReadOnlySpan<ArchetypePlan> matchingPlans = plan.MatchingPlans();
        EnsureGeneratedWhereSourceArchetypeCapacity(matchingPlans.Length);
        _generatedWhereSourceArchetypeCount = matchingPlans.Length;
        for (int index = 0; index < matchingPlans.Length; index++)
        {
            _generatedWhereSourceArchetypes[index] = matchingPlans.RefAt(index).Archetype;
        }

        _generatedWhereAffectedArchetypes.Clear();
        _generatedWhereAffectedArchetypeStamp = NextGeneratedWhereStamp(_generatedWhereAffectedArchetypeStamp);
        BeginGeneratedWherePlanCache();
        int edgeStamp = isDestroy ? 0 : BeginBatchEdgeCache();
        for (int index = 0; index < _generatedWhereSourceArchetypeCount; index++)
        {
            Archetype sourceArchetype = _generatedWhereSourceArchetypes[index];
            Archetype? targetArchetype = null;
            int[] sourceToTargetRows = Array.Empty<int>();
            int[] addedTargetRows = Array.Empty<int>();
            GeneratedWhereTargetCursor? targetCursor = null;

            if (sourceArchetype.ActiveChunkCount == 0)
            {
                _generatedWhereArchetypePlans.RefAt(sourceArchetype.Id) =
                    new GeneratedWhereStructuralPlan(
                        sourceArchetype,
                        targetArchetype,
                        sourceToTargetRows,
                        addedTargetRows,
                        targetCursor,
                        isDestroy);
                continue;
            }

            if (isDestroy)
            {
                sourceArchetype.DeferQueryPlanUpdates();
                MarkGeneratedWhereAffected(sourceArchetype);
            }
            else
            {
                bool noOp = isAdd
                    ? sourceArchetype.Mask.ContainsAll(changeMask)
                    : !sourceArchetype.Mask.Intersects(changeMask);
                if (!noOp)
                {
                    TransitionEdge edge = GetBatchTransitionEdge(
                        sourceArchetype.Id,
                        changeMask,
                        isAdd,
                        edgeStamp);
                    targetArchetype = _archetypes[edge.TargetArchetypeId];
                    sourceToTargetRows = edge.SourceToTargetRowIndices;
                    addedTargetRows = edge.AddedTargetRowIndices;
                    targetCursor = GetGeneratedWhereTargetCursor(targetArchetype.Id);
                    sourceArchetype.DeferQueryPlanUpdates();
                    targetArchetype.DeferQueryPlanUpdates();
                    MarkGeneratedWhereAffected(sourceArchetype);
                    MarkGeneratedWhereAffected(targetArchetype);
                }
            }

            _generatedWhereArchetypePlans.RefAt(sourceArchetype.Id) =
                new GeneratedWhereStructuralPlan(
                    sourceArchetype,
                    targetArchetype,
                    sourceToTargetRows,
                    addedTargetRows,
                    targetCursor,
                    isDestroy);
        }

        ReadOnlySpan<ChunkPlan> matchingChunks = plan.MatchingChunkPlans();
        EnsureGeneratedWhereSourceCountCapacity(matchingChunks.Length);
        _generatedWhereDestroyCapacity = 0;
        _generatedWhereDestroyCapacityReserved = false;
        for (int index = 0; index < matchingChunks.Length; index++)
        {
            int count = matchingChunks.RefAt(index).Chunk.Count;
            _generatedWhereSourceCounts.RefAt(index) = count;
            if (isDestroy)
            {
                _generatedWhereDestroyCapacity = checked(_generatedWhereDestroyCapacity + count);
            }
        }

        _generatedWhereStructuralActive = true;
        execution = new GeneratedDenseExecution(
            this,
            plan.MatchingPlans(),
            matchingChunks,
            _generatedWhereSourceCounts.ReadOnlySpan[..matchingChunks.Length],
            ownsLease: false);
        return true;
    }

    internal GeneratedWhereStructuralContext BeginGeneratedWhereChunk(int chunkId, int sourceCount)
    {
        Chunk sourceChunk = GetChunkById(chunkId);
        ref readonly GeneratedWhereStructuralPlan plan =
            ref _generatedWhereArchetypePlans.RefAt(sourceChunk.ArchetypeId);
        return new GeneratedWhereStructuralContext(
            this,
            sourceChunk,
            in plan,
            sourceCount);
    }

    internal void EndGeneratedWhereStructural()
    {
        try
        {
            RefreshGeneratedWhereAffectedArchetypes();
        }
        finally
        {
            _generatedWhereStructuralActive = false;
            _generatedWhereSourceArchetypeCount = 0;
            _generatedWhereDestroyCapacity = 0;
            _generatedWhereDestroyCapacityReserved = false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkGeneratedWhereAffected(Archetype archetype)
    {
        int archetypeId = archetype.Id;
        EnsureGeneratedWhereArchetypeCapacity(archetypeId + 1);
        if (_generatedWhereAffectedArchetypeStamps.RefAt(archetypeId) == _generatedWhereAffectedArchetypeStamp)
        {
            return;
        }

        _generatedWhereAffectedArchetypeStamps.RefAt(archetypeId) = _generatedWhereAffectedArchetypeStamp;
        _generatedWhereAffectedArchetypes.Add(archetype);
    }

    private void RefreshGeneratedWhereAffectedArchetypes()
    {
        for (int index = 0; index < _generatedWhereAffectedArchetypes.Count; index++)
        {
            _generatedWhereAffectedArchetypes[index].RefreshQueryPlans();
        }
    }

    private void BeginGeneratedWherePlanCache()
    {
        _generatedWherePlanStamp = NextGeneratedWhereStamp(_generatedWherePlanStamp);
        EnsureGeneratedWhereArchetypeCapacity(_archetypes.Count);
    }

    private GeneratedWhereTargetCursor GetGeneratedWhereTargetCursor(int archetypeId)
    {
        EnsureGeneratedWhereArchetypeCapacity(archetypeId + 1);
        if (_generatedWhereTargetCursorStamps.RefAt(archetypeId) != _generatedWherePlanStamp)
        {
            GeneratedWhereTargetCursor? cursor = _generatedWhereTargetCursors[archetypeId];
            if (cursor is null)
            {
                cursor = new GeneratedWhereTargetCursor();
                _generatedWhereTargetCursors[archetypeId] = cursor;
            }

            cursor.Reset();
            _generatedWhereTargetCursorStamps.RefAt(archetypeId) = _generatedWherePlanStamp;
        }

        return _generatedWhereTargetCursors[archetypeId]!;
    }

    private void EnsureGeneratedWhereArchetypeCapacity(int required)
    {
        if (required <= _generatedWhereArchetypePlans.Length)
        {
            return;
        }

        int previousLength = _generatedWhereArchetypePlans.Length;
        int capacity = Math.Max(required, previousLength == 0 ? 4 : previousLength * 2);
        Array.Resize(ref _generatedWhereArchetypePlans, capacity);
        Array.Resize(ref _generatedWhereTargetCursors, capacity);
        _generatedWhereTargetCursorStamps.Resize(capacity);
        _generatedWhereAffectedArchetypeStamps.Resize(capacity);
    }

    private void EnsureGeneratedWhereSourceArchetypeCapacity(int required)
    {
        if (required <= _generatedWhereSourceArchetypes.Length)
        {
            return;
        }

        int capacity = Math.Max(required, _generatedWhereSourceArchetypes.Length == 0 ? 4 : _generatedWhereSourceArchetypes.Length * 2);
        Array.Resize(ref _generatedWhereSourceArchetypes, capacity);
    }

    private void EnsureGeneratedWhereSourceCountCapacity(int required)
    {
        if (required <= _generatedWhereSourceCounts.Length)
        {
            return;
        }

        _generatedWhereSourceCounts.Resize(Math.Max(required, _generatedWhereSourceCounts.Length == 0 ? 4 : _generatedWhereSourceCounts.Length * 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int NextGeneratedWhereStamp(int stamp)
    {
        if (stamp == int.MaxValue)
        {
            _generatedWhereTargetCursorStamps.Span.Clear();
            _generatedWhereAffectedArchetypeStamps.Span.Clear();
            return 1;
        }

        return stamp + 1;
    }

    internal void CopyGeneratedWhereRun(
        Chunk sourceChunk,
        int sourceSlot,
        int count,
        Archetype targetArchetype,
        int[] sourceToTargetRows,
        int[] addedTargetRows,
        GeneratedWhereTargetCursor targetCursor)
    {
        TransitionEdge edge = new(targetArchetype.Id, sourceToTargetRows, addedTargetRows);
        while (count != 0)
        {
            if (targetCursor.Remaining == 0)
            {
                int chunkId = targetArchetype.HasAvailableChunk(count, allowFreeRecordBlocks: false)
                    ? -1
                    : AllocateChunkId();
                int reserved = targetArchetype.ReserveRange(
                    count,
                    chunkId,
                    out _,
                    out Chunk targetChunk,
                    out _,
                    allowFreeRecordBlocks: false);
                RegisterChunkStampStorage(targetChunk);
                targetCursor.Chunk = targetChunk;
                targetCursor.Slot = targetChunk.Count - reserved;
                targetCursor.Remaining = reserved;
            }

            int copied = Math.Min(count, targetCursor.Remaining);
            CopyChunkRange(
                sourceChunk,
                targetCursor.Chunk!,
                sourceSlot,
                targetCursor.Slot,
                copied,
                edge);
            sourceSlot += copied;
            targetCursor.Slot += copied;
            targetCursor.Remaining -= copied;
            count -= copied;
        }
    }

    internal void FreeGeneratedWhereRun(Chunk sourceChunk, int sourceSlot, int count)
    {
        if (!_generatedWhereDestroyCapacityReserved)
        {
            EnsureFreeRecordCapacity(_freeCount + _generatedWhereDestroyCapacity);
            _generatedWhereDestroyCapacityReserved = true;
        }

        Span<int> freeRecords = _freeRecords.Span.Slice(_freeCount, count);
        Span<Entity> entities = sourceChunk.RawEntities.Slice(sourceSlot, count);
        for (int index = 0; index < count; index++)
        {
            Entity entity = entities.RefAt(index);
            ref var record = ref RecordAt(entity.Index);
            record.ChunkId = -1;
            record.SlotIndex = -1;
            freeRecords.RefAt(index) = entity.Index;
        }

        _freeCount += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CommitGeneratedWhereDestroy(int count) => AliveEntityCount -= count;

    internal void CopyChunkRangeWithin(Chunk chunk, int sourceSlot, int targetSlot, int count)
    {
        if (sourceSlot == targetSlot || count == 0)
        {
            return;
        }

        chunk.RawEntities.Slice(sourceSlot, count).CopyTo(chunk.RawEntities.Slice(targetSlot, count));
        for (int componentIndex = 0; componentIndex < chunk.ComponentCount; componentIndex++)
        {
            Array.Copy(
                chunk.GetRawComponentRowTrusted(componentIndex),
                sourceSlot,
                chunk.GetRawComponentRowTrusted(componentIndex),
                targetSlot,
                count);
            chunk.CopyStampRangeTo(
                chunk,
                sourceSlot,
                targetSlot,
                count,
                componentIndex,
                componentIndex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UpdateChunkRecordSlotsSameChunk(Chunk chunk, int slotIndex, int count)
    {
        Span<Entity> entities = chunk.RawEntities.Slice(slotIndex, count);
        for (int index = 0; index < count; index++)
        {
            ref var record = ref RecordAt(entities.RefAt(index).Index);
            record.SlotIndex = slotIndex + index;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateChunkRecordLocations(Chunk chunk, int slotIndex, int count)
    {
        int chunkId = chunk.GlobalId;
        Span<Entity> entities = chunk.RawEntities.Slice(slotIndex, count);
        for (int index = 0; index < count; index++)
        {
            ref var record = ref RecordAt(entities.RefAt(index).Index);
            record.ChunkId = chunkId;
            record.SlotIndex = slotIndex + index;
        }
    }

    private int MoveArchetypeBlocks(Archetype sourceArchetype, TransitionEdge edge)
    {
        int movedCount = 0;
        var targetArchetype = _archetypes[edge.TargetArchetypeId];
        sourceArchetype.DeferQueryPlanUpdates();
        targetArchetype.DeferQueryPlanUpdates();
        int initialTargetChunkCount = targetArchetype.ChunkCount;
        targetArchetype.PrepareBlockMoveCandidates(initialTargetChunkCount);
        while (sourceArchetype.TryGetLastActiveChunk(out int sourceChunkIndex, out Chunk sourceChunk))
        {
            while (sourceChunk.Count != 0
                && targetArchetype.TryTakeBlockPartialChunk(out Chunk targetChunk))
            {
                int copied = Math.Min(sourceChunk.Count, targetChunk.Capacity - targetChunk.Count);
                int sourceSlot = sourceChunk.Count - copied;
                int targetSlot = targetChunk.Count;
                targetArchetype.ReserveExistingChunk(targetChunk, copied, out _);
                CopyChunkRange(sourceChunk, targetChunk, sourceSlot, targetSlot, copied, edge);
                sourceChunk.TrimTail(copied);
                if (!targetChunk.IsFull)
                {
                    targetArchetype.RequeueBlockPartialChunk(targetChunk);
                }

                movedCount += copied;
            }

            if (sourceChunk.Count == 0)
            {
                Chunk detached = sourceArchetype.DetachChunk(sourceChunkIndex);
                DisposeDetachedChunk(detached);
                continue;
            }

            bool hasDonor = targetArchetype.TryTakeBlockEmptyChunk(
                out int donorChunkIndex,
                out Chunk donor);
            Chunk adopted = sourceArchetype.DetachChunk(sourceChunkIndex);
            if (!hasDonor)
            {
                adopted.AdoptLayout(
                    targetArchetype.Layouts,
                    targetArchetype.RowOperations,
                    edge.SourceToTargetRowIndices,
                    edge.AddedTargetRowIndices);
            }
            else
            {
                adopted.AdoptLayoutFromEmptyChunk(
                    donor,
                    targetArchetype.Layouts,
                    targetArchetype.RowOperations,
                    edge.SourceToTargetRowIndices,
                    edge.AddedTargetRowIndices);
                RemapChunkStampStorageFromEmptyChunk(
                    adopted,
                    donor,
                    edge.SourceToTargetRowIndices,
                    targetArchetype.ComponentCount);
                Chunk replacedDonor = targetArchetype.ReplaceEmptyChunk(donorChunkIndex, adopted);
                DisposeDetachedChunk(replacedDonor);
            }

            if (!hasDonor)
            {
                RemapChunkStampStorage(adopted, edge.SourceToTargetRowIndices, targetArchetype.ComponentCount);
            }

            if (!hasDonor)
            {
                targetArchetype.AttachAdoptedChunk(adopted);
            }
            movedCount += adopted.Count;
        }

        sourceArchetype.RefreshQueryPlans();
        targetArchetype.RefreshQueryPlans();

        return movedCount;
    }

    private void DisposeDetachedChunk(Chunk chunk)
    {
        ClearChunkComponentStamps(chunk);
        if ((uint)chunk.GlobalId < (uint)_chunksById.Length
            && ReferenceEquals(_chunksById[chunk.GlobalId], chunk))
        {
            _chunksById[chunk.GlobalId] = null;
        }

        chunk.Dispose();
    }

    private void CopyChunkRange(
        Chunk source,
        Chunk target,
        int sourceSlot,
        int targetSlot,
        int count,
        TransitionEdge edge)
    {
        source.RawEntities.Slice(sourceSlot, count).CopyTo(target.RawEntities.Slice(targetSlot, count));
        for (int sourceComponentIndex = 0; sourceComponentIndex < edge.SourceToTargetRowIndices.Length; sourceComponentIndex++)
        {
            int targetComponentIndex = edge.SourceToTargetRowIndices.RefAt(sourceComponentIndex);
            if (targetComponentIndex < 0)
            {
                continue;
            }

            Array.Copy(
                source.GetRawComponentRowTrusted(sourceComponentIndex),
                sourceSlot,
                target.GetRawComponentRowTrusted(targetComponentIndex),
                targetSlot,
                count);
            source.CopyStampRangeTo(
                target,
                sourceSlot,
                targetSlot,
                count,
                sourceComponentIndex,
                targetComponentIndex);
        }

        target.InitializeRowsRange(targetSlot, count, edge.AddedTargetRowIndices);
        target.StampRowsRange(targetSlot, count, edge.AddedTargetRowIndices, new Stamp(1));
        UpdateChunkRecordLocations(target, targetSlot, count);
    }

    private int DestroyChunk(Archetype archetype, int chunkIndex)
    {
        var chunk = archetype.GetChunk(chunkIndex);
        int count = chunk.Count;
        if (count == 0)
        {
            return 0;
        }

        chunk.BeginFreeRecordBlock(count);
        chunk.SetFreeRecordListIndex(_freeRecordChunks.Count);
        _freeRecordChunks.Add(chunk);
        chunk.ClearAll();
        ClearChunkComponentStamps(chunk);
        archetype.ReleaseChunk(chunkIndex);
        AliveEntityCount -= count;
        return count;
    }

    private bool IsCompleteDestroyChunk(int start, int count)
    {
        for (int index = 0; index < count; index++)
        {
            var entry = _destroyScratch.RefAt(start + index);
            if (entry.SlotIndex != count - index - 1 || !IsCurrentDestroyEntry(entry))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsCompleteAscendingDestroyChunk(int start, int count)
    {
        for (int index = 0; index < count; index++)
        {
            var entry = _destroyScratch.RefAt(start + index);
            if (entry.SlotIndex != index || !IsCurrentDestroyEntry(entry))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsAscendingDestroyOrder(int count)
    {
        for (int index = 1; index < count; index++)
        {
            var previous = _destroyScratch.RefAt(index - 1);
            var current = _destroyScratch.RefAt(index);
            if (current.Archetype < previous.Archetype
                || (current.Archetype == previous.Archetype && current.ChunkId < previous.ChunkId)
                || (current.Archetype == previous.Archetype
                    && current.ChunkId == previous.ChunkId
                    && current.SlotIndex < previous.SlotIndex))
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsCurrentDestroyEntry(DestroyEntry entry)
    {
        ref readonly var record = ref RecordAt(entry.RecordIndex);
        return record.Generation == entry.Entity.Generation
            && record.ChunkId == entry.ChunkId
            && record.SlotIndex == entry.SlotIndex;
    }

    private void DestroyResolved(int recordIndex)
    {
        ref var record = ref RecordAt(recordIndex);
        Chunk chunk = GetRecordChunk(record);
        var archetype = _archetypes[chunk.ArchetypeId];
        int chunkIndex = chunk.ArchetypeIndex;
        var moved = archetype.RemoveEntity(chunkIndex, record.SlotIndex);
        if (chunk.IsEmpty)
        {
            ClearChunkComponentStamps(chunk);
        }

        if (moved.IsValid)
        {
            ref var movedRecord = ref RecordAt(moved.Index);
            movedRecord.ChunkId = chunk.GlobalId;
            movedRecord.SlotIndex = record.SlotIndex;
        }

        record.ChunkId = -1;
        PushFree(recordIndex);
        AliveEntityCount--;
    }

    private void MoveEntity(int recordIndex, TransitionEdge edge)
    {
        MoveEntity(recordIndex, edge, out _, out _);
    }

    private void MoveEntity(
        int recordIndex,
        TransitionEdge edge,
        out int targetChunkIndex,
        out int targetSlotIndex)
    {
        ref var sourceRecord = ref RecordAt(recordIndex);
        Chunk sourceChunk = GetRecordChunk(sourceRecord);
        var sourceArchetype = _archetypes[sourceChunk.ArchetypeId];
        var targetArchetype = _archetypes[edge.TargetArchetypeId];

        int sourceSlotIndex = sourceRecord.SlotIndex;
        int sourceChunkIndex = sourceChunk.ArchetypeIndex;
        int targetChunkId = targetArchetype.HasAvailableChunk(0) ? -1 : AllocateChunkId();
        targetArchetype.AddEntity(
            new Entity(recordIndex, sourceRecord.Generation),
            targetChunkId,
            out targetChunkIndex,
            out targetSlotIndex,
            out bool reusedTargetSlot);
        var targetChunk = targetArchetype.GetChunk(targetChunkIndex);
        RegisterChunkStampStorage(targetChunk);

        for (int sourceIndex = 0; sourceIndex < edge.SourceToTargetRowIndices.Length; sourceIndex++)
        {
            int targetIndex = edge.SourceToTargetRowIndices.RefAt(sourceIndex);
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
            targetChunk.MarkComponentStamped(edge.AddedTargetRowIndices.RefAt(i), targetSlotIndex, new Stamp(1));
        }

        var moved = sourceArchetype.RemoveEntity(sourceChunkIndex, sourceSlotIndex);
        if (sourceChunk.IsEmpty)
        {
            ClearChunkComponentStamps(sourceChunk);
        }
        sourceRecord.ChunkId = targetChunk.GlobalId;
        sourceRecord.SlotIndex = targetSlotIndex;
        if (moved.IsValid)
        {
            ref var movedRecord = ref RecordAt(moved.Index);
            movedRecord.ChunkId = sourceChunk.GlobalId;
            movedRecord.SlotIndex = sourceSlotIndex;
        }
    }

    private TransitionEdge GetTransitionEdge(
        int sourceArchetypeId,
        ComponentMask changeMask,
        bool isAdd,
        ComponentMask targetMask)
    {
        var key = new TransitionKey(sourceArchetypeId, changeMask, isAdd);
        if (_transitionCache.TryGetValue(key, out var edge))
        {
            return edge;
        }

        var source = _archetypes[sourceArchetypeId];
        var target = GetOrCreateArchetype(targetMask);
        int[] mapping = new int[source.ComponentCount];
        bool[] copiedTargetRows = new bool[target.ComponentCount];
        for (int i = 0; i < mapping.Length; i++)
        {
            mapping.RefAt(i) = target.Mask.Rank(source.ComponentIds.RefAt(i));
            if (mapping.RefAt(i) >= 0)
            {
                copiedTargetRows.RefAt(mapping.RefAt(i)) = true;
            }
        }

        int[] addedTargetRows = new int[target.ComponentCount];
        int addedCount = 0;
        for (int targetIndex = 0; targetIndex < copiedTargetRows.Length; targetIndex++)
        {
            if (!copiedTargetRows.RefAt(targetIndex))
            {
                addedTargetRows.RefAt(addedCount++) = targetIndex;
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
        Chunk chunk = GetRecordChunk(record);
        var archetype = _archetypes[chunk.ArchetypeId];
        if (!archetype.TryGetComponentIndex(componentId, out int componentIndex)
            || !_layouts.TryGet(componentId, out var layout)
            || !IsCompatibleComponentType<T>(layout))
        {
            return false;
        }

        chunk.GetComponentRow<T>(componentIndex).RefAt(record.SlotIndex) = value;
        Stamp stamp = chunk.IncrementComponentStamp(componentIndex, record.SlotIndex);
        CreateEntityComponentStampWriter(
            chunk,
            componentIndex,
            record.SlotIndex,
            stamp).MarkPoint();
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
            if (!_layouts.TryGet(componentIds.RefAt(i), out var layout))
            {
                ThrowHelper.ThrowMissingComponentLayout(componentIds.RefAt(i).Value);
            }

            layouts.RefAt(i) = layout;
            rowOperations.RefAt(i) = _layouts.GetRowOperations(componentIds.RefAt(i));
        }

        var archetype = new Archetype(
            _archetypes.Count,
            mask,
            layouts,
            rowOperations,
            componentIds,
            _chunkCapacity,
            _componentRowArrayPool);
        _archetypeByMask.Add(mask, archetype.Id);
        _archetypes.Add(archetype);
        RegisterArchetypeStampStorage(archetype.Id, archetype.ComponentCount);
        _archetypeVersion++;
        List<QuerySpec>? deadQueries = null;
        foreach (var entry in _queryCache)
        {
            if (entry.Value.TryGetTarget(out QueryPlan? queryPlan))
            {
                queryPlan.OnArchetypeCreated(archetype);
            }
            else
            {
                (deadQueries ??= new List<QuerySpec>()).Add(entry.Key);
            }
        }

        if (deadQueries is not null)
        {
            for (int index = 0; index < deadQueries.Count; index++)
            {
                _queryCache.Remove(deadQueries[index]);
            }
        }

        return archetype;
    }

    private void RegisterArchetypeStampStorage(int archetypeId, int componentCount)
    {
        EnsureStampStorageCapacity(
            ref _archetypeComponentWriteStamps,
            ref _archetypeStampComponentCounts,
            archetypeId + 1);
        if (componentCount == 0)
        {
            return;
        }

        _archetypeComponentWriteStamps.RefAt(archetypeId) = new Stamp[componentCount];
        _archetypeStampComponentCounts.RefAt(archetypeId) = componentCount;
    }

    internal void RegisterChunkStampStorage(Chunk chunk)
    {
        EnsureChunkReferenceCapacity(chunk.GlobalId + 1);
        _chunksById[chunk.GlobalId] = chunk;
        if (chunk.ComponentCount == 0)
        {
            return;
        }

        int chunkId = chunk.GlobalId;
        EnsureStampStorageCapacity(
            ref _chunkComponentWriteStamps,
            ref _chunkStampComponentCounts,
            chunkId + 1);
        if (_chunkStampComponentCounts.RefAt(chunkId) == 0)
        {
            _chunkComponentWriteStamps.RefAt(chunkId) = new NativeMemory<Stamp>(chunk.ComponentCount);
            _chunkStampComponentCounts.RefAt(chunkId) = chunk.ComponentCount;
        }
    }

    private void EnsureChunkReferenceCapacity(int required)
    {
        if (required <= _chunksById.Length)
        {
            return;
        }

        int capacity = Math.Max(required, _chunksById.Length == 0 ? 4 : _chunksById.Length * 2);
        Array.Resize(ref _chunksById, capacity);
    }

    private void RemapChunkStampStorage(Chunk chunk, ReadOnlySpan<int> sourceToTarget, int targetComponentCount)
    {
        int chunkId = chunk.GlobalId;
        EnsureChunkReferenceCapacity(chunkId + 1);
        _chunksById[chunkId] = chunk;
        EnsureStampStorageCapacity(
            ref _chunkComponentWriteStamps,
            ref _chunkStampComponentCounts,
            chunkId + 1);
        if (targetComponentCount == 0)
        {
            if (_chunkStampComponentCounts.RefAt(chunkId) != 0)
            {
                _chunkComponentWriteStamps.RefAt(chunkId).Dispose();
                _chunkStampComponentCounts.RefAt(chunkId) = 0;
            }

            return;
        }

        NativeMemory<Stamp> replacement = new(targetComponentCount);
        if (_chunkStampComponentCounts.RefAt(chunkId) != 0)
        {
            NativeMemory<Stamp> source = _chunkComponentWriteStamps.RefAt(chunkId);
            for (int sourceIndex = 0; sourceIndex < sourceToTarget.Length; sourceIndex++)
            {
                int targetIndex = sourceToTarget.RefAt(sourceIndex);
                if (targetIndex >= 0)
                {
                    replacement.RefAt(targetIndex) = source.RefAt(sourceIndex);
                }
            }

            source.Dispose();
        }

        _chunkComponentWriteStamps.RefAt(chunkId) = replacement;
        _chunkStampComponentCounts.RefAt(chunkId) = targetComponentCount;
    }

    private void RemapChunkStampStorageFromEmptyChunk(
        Chunk sourceChunk,
        Chunk donorChunk,
        ReadOnlySpan<int> sourceToTarget,
        int targetComponentCount)
    {
        int sourceChunkId = sourceChunk.GlobalId;
        int donorChunkId = donorChunk.GlobalId;
        EnsureChunkReferenceCapacity(Math.Max(sourceChunkId, donorChunkId) + 1);
        EnsureStampStorageCapacity(
            ref _chunkComponentWriteStamps,
            ref _chunkStampComponentCounts,
            Math.Max(sourceChunkId, donorChunkId) + 1);

        ref NativeMemory<Stamp> sourceStamps = ref _chunkComponentWriteStamps.RefAt(sourceChunkId);
        NativeMemory<Stamp> donorStamps = _chunkComponentWriteStamps.RefAt(donorChunkId);
        int sourceComponentCount = _chunkStampComponentCounts.RefAt(sourceChunkId);
        for (int sourceIndex = 0; sourceIndex < sourceToTarget.Length; sourceIndex++)
        {
            int targetIndex = sourceToTarget.RefAt(sourceIndex);
            if (targetIndex >= 0 && sourceIndex < sourceComponentCount)
            {
                donorStamps.RefAt(targetIndex) = sourceStamps.RefAt(sourceIndex);
            }
        }

        sourceStamps.Dispose();
        _chunkComponentWriteStamps.RefAt(sourceChunkId) = donorStamps;
        _chunkStampComponentCounts.RefAt(sourceChunkId) = targetComponentCount;
        _chunkComponentWriteStamps.RefAt(donorChunkId) = default;
        _chunkStampComponentCounts.RefAt(donorChunkId) = 0;
        if ((uint)donorChunkId < (uint)_chunksById.Length
            && ReferenceEquals(_chunksById[donorChunkId], donorChunk))
        {
            _chunksById[donorChunkId] = null;
        }
    }

    private static void EnsureStampStorageCapacity<T>(
        ref T[] storage,
        ref int[] componentCounts,
        int required)
    {
        if (required <= storage.Length)
        {
            return;
        }

        int capacity = Math.Max(required, storage.Length == 0 ? 4 : storage.Length * 2);
        Array.Resize(ref storage, capacity);
        Array.Resize(ref componentCounts, capacity);
    }

    private void DisposeStampLayers()
    {
        for (int index = 0; index < _chunkStampComponentCounts.Length; index++)
        {
            if (_chunkStampComponentCounts.RefAt(index) != 0)
            {
                _chunkComponentWriteStamps.RefAt(index).Dispose();
            }
        }

        _archetypeComponentWriteStamps = Array.Empty<Stamp[]>();
        _chunkComponentWriteStamps = Array.Empty<NativeMemory<Stamp>>();
        _archetypeStampComponentCounts = Array.Empty<int>();
        _chunkStampComponentCounts = Array.Empty<int>();
    }

    private bool TryBuildComponentMask(ReadOnlySpan<ComponentId> componentIds, out ComponentMask mask)
    {
        for (int i = 0; i < componentIds.Length; i++)
        {
            if (!componentIds.RefAt(i).IsValid)
            {
                continue;
            }

            if (!_layouts.TryGet(componentIds.RefAt(i), out _))
            {
                ThrowHelper.ThrowWorldComponentNotRegistered(componentIds.RefAt(i).Value, nameof(componentIds));
            }
        }

        mask = ComponentMask.FromValidated(componentIds);
        return !mask.IsEmpty;
    }

    private int AllocateRecord()
    {
        if (_freeCount > 0)
        {
            return RecycleRecord(_freeRecords.RefAt(--_freeCount));
        }

        if (_freeRecordChunks.Count != 0)
        {
            Chunk chunk = _freeRecordChunks[^1];
            int recycledIndex = chunk.TakeFreeRecordIndex();
            if (!chunk.HasFreeRecordBlock)
            {
                CompleteFreeRecordBlock(chunk);
            }

            return RecycleRecord(recycledIndex);
        }

        int index = _records.Count;
        _records.Add(new EntityRecord { Generation = 1, ChunkId = -1, SlotIndex = -1 });
        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RecycleRecord(int recordIndex)
    {
        ref var record = ref RecordAt(recordIndex);
        record.Generation = NextGeneration(record.Generation);
        return recordIndex;
    }

    private void CompleteFreeRecordBlock(Chunk chunk)
    {
        int listIndex = chunk.FreeRecordListIndex;
        if ((uint)listIndex < (uint)_freeRecordChunks.Count
            && ReferenceEquals(_freeRecordChunks[listIndex], chunk))
        {
            int lastIndex = _freeRecordChunks.Count - 1;
            if (listIndex != lastIndex)
            {
                Chunk moved = _freeRecordChunks[lastIndex];
                _freeRecordChunks[listIndex] = moved;
                moved.SetFreeRecordListIndex(listIndex);
            }

            _freeRecordChunks.RemoveAt(lastIndex);
        }

        chunk.SetFreeRecordListIndex(-1);
        if (chunk.ArchetypeId >= 0)
        {
            _archetypes[chunk.ArchetypeId].RequeueFreedRecordChunk(chunk);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NextGeneration(int generation) => generation == int.MaxValue ? 1 : generation + 1;

    private void PushFree(int recordIndex)
    {
        EnsureFreeRecordCapacity(_freeCount + 1);
        _freeRecords.RefAt(_freeCount++) = recordIndex;
    }

    private void EnsureFreeRecordCapacity(int required)
    {
        if (required <= _freeRecords.Length)
        {
            return;
        }

        _freeRecords.Resize(Math.Max(required, _freeRecords.Length == 0 ? 4 : _freeRecords.Length * 2));
    }

    private int AllocateChunkId() => _nextChunkId++;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Chunk GetChunkById(int chunkId)
    {
        if ((uint)chunkId >= (uint)_chunksById.Length || _chunksById[chunkId] is null)
        {
            ThrowHelper.ThrowInvalidChunkLocation();
        }

        return _chunksById[chunkId]!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetChunkById(int chunkId, out Chunk chunk)
    {
        if ((uint)chunkId < (uint)_chunksById.Length && _chunksById[chunkId] is { } found)
        {
            chunk = found;
            return true;
        }

        chunk = null!;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Chunk GetRecordChunk(in EntityRecord record) => GetChunkById(record.ChunkId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Archetype GetRecordArchetype(in EntityRecord record) => _archetypes[GetRecordChunk(record).ArchetypeId];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref EntityRecord RecordAt(int recordIndex) => ref _records.RefAt(recordIndex);

    private bool TryResolve(Entity entity, out int recordIndex)
    {
        recordIndex = entity.Index;
        if (recordIndex < 0 || recordIndex >= _records.Count)
        {
            return false;
        }

        ref readonly var record = ref RecordAt(recordIndex);
        if (record.ChunkId < 0 || record.Generation != entity.Generation)
        {
            return false;
        }

        if (!TryGetChunkById(record.ChunkId, out Chunk? chunk)
            || (uint)record.SlotIndex >= (uint)chunk.Count)
        {
            return false;
        }

        return chunk.RawEntities.RefAt(record.SlotIndex) == entity;
    }

    private QueryPlan GetOrCreateQuery(QuerySpec spec)
    {
        if (--_queryCacheSweepCountdown == 0)
        {
            SweepDeadQueryPlans();
            _queryCacheSweepCountdown = 64;
        }

        if (_queryCache.TryGetValue(spec, out WeakReference<QueryPlan>? weakQueryPlan)
            && weakQueryPlan.TryGetTarget(out QueryPlan? cached))
        {
            return cached;
        }

        cached = new QueryPlan(this, spec);
        _queryCache[spec] = cached.WeakReference;
        return cached;
    }

    private void SweepDeadQueryPlans()
    {
        List<QuerySpec>? deadQueries = null;
        foreach (var entry in _queryCache)
        {
            if (!entry.Value.TryGetTarget(out _))
            {
                (deadQueries ??= new List<QuerySpec>()).Add(entry.Key);
            }
        }

        if (deadQueries is null)
        {
            return;
        }

        for (int index = 0; index < deadQueries.Count; index++)
        {
            _queryCache.Remove(deadQueries[index]);
        }
    }

    private void EnsureNoActiveLease(string operation)
    {
        if (_activeChunkLeases > 0 || _generatedWhereStructuralActive)
        {
            ThrowHelper.ThrowStructuralChangeWhileLeased(operation);
        }
    }

    private void ValidateQuery(in Query query)
    {
        if (!query.IsValid || !ReferenceEquals(query.Owner, this))
        {
            ThrowHelper.ThrowInvalidQuery(nameof(query));
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

        if (_batchEdgeStamps.RefAt(sourceArchetypeId) == stamp)
        {
            return _batchEdgeSlots.RefAt(sourceArchetypeId);
        }

        Archetype source = _archetypes[sourceArchetypeId];
        ComponentMask targetMask = isAdd
            ? source.Mask.Or(changeMask)
            : source.Mask.Except(changeMask);
        TransitionEdge edge = GetTransitionEdge(sourceArchetypeId, changeMask, isAdd, targetMask);
        _batchEdgeSlots.RefAt(sourceArchetypeId) = edge;
        _batchEdgeStamps.RefAt(sourceArchetypeId) = stamp;
        return edge;
    }

    private TransitionEdge GetBatchTransitionEdge(
        int sourceArchetypeId,
        ComponentMask changeMask,
        bool isAdd,
        ComponentMask targetMask,
        int stamp)
    {
        if ((uint)sourceArchetypeId >= (uint)_batchEdgeStamps.Length)
        {
            EnsureBatchEdgeCapacity(sourceArchetypeId + 1);
        }

        if (_batchEdgeStamps.RefAt(sourceArchetypeId) == stamp)
        {
            return _batchEdgeSlots.RefAt(sourceArchetypeId);
        }

        var edge = GetTransitionEdge(sourceArchetypeId, changeMask, isAdd, targetMask);
        _batchEdgeSlots.RefAt(sourceArchetypeId) = edge;
        _batchEdgeStamps.RefAt(sourceArchetypeId) = stamp;
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
        public DestroyEntry(Entity entity, int recordIndex, int archetype, int chunkId, int slotIndex)
        {
            Entity = entity;
            RecordIndex = recordIndex;
            Archetype = archetype;
            ChunkId = chunkId;
            SlotIndex = slotIndex;
        }

        public Entity Entity { get; }
        public int RecordIndex { get; }
        public int Archetype { get; }
        public int ChunkId { get; }
        public int SlotIndex { get; }
    }

    private sealed class DestroyEntryComparer : IComparer<DestroyEntry>
    {
        public static readonly DestroyEntryComparer Instance = new();

        public int Compare(DestroyEntry x, DestroyEntry y)
        {
            int result = x.Archetype.CompareTo(y.Archetype);
            if (result != 0) return result;
            result = x.ChunkId.CompareTo(y.ChunkId);
            return result != 0 ? result : y.SlotIndex.CompareTo(x.SlotIndex);
        }
    }

}
