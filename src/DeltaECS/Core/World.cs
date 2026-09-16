namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

public sealed partial class World : IDisposable
{
    private const int DefaultInitialCapacity = 1024;
    private const int InitialFreeRecordCapacity = 16;
    private const int QueryCacheSweepInterval = 64;
    private const int InitialDestroyScratchCapacity = 32;
    private const int InitialDeferredQueryPlanCapacity = 16;
    private const int InitialBatchEdgeCapacity = 4;
    private const int TransitionHashMultiplier = 397;

    private readonly ComponentLayoutRegistry _layouts;
    private readonly List<Archetype> _archetypes = new();
    private readonly ComponentRowArrayPool _componentRowArrayPool = new();
    private readonly List<Chunk> _freeRecordChunks = new();
    private readonly Dictionary<ComponentMask, int> _archetypeByMask = new();
    private readonly EntityRecordStorage _records = new();
    private NativeMemory<int> _freeRecords = new(InitialFreeRecordCapacity);
    private int _freeCount;
    private readonly Dictionary<TransitionKey, TransitionEdge> _transitionCache = new();
    private readonly ComponentSetCache _componentSetCache = new();
    private readonly Dictionary<QuerySpec, WeakReference<QueryPlan>> _queryCache = new();
    private int _queryCacheSweepCountdown = QueryCacheSweepInterval;
    private NativeMemory<DestroyEntry> _destroyScratch = new(InitialDestroyScratchCapacity);
    private readonly List<Archetype> _deferredQueryPlanArchetypes = new(InitialDeferredQueryPlanCapacity);
    private readonly List<QueryPlan> _deferredQueryPlans = new(InitialDeferredQueryPlanCapacity);
    private GeneratedWhereStructuralPlan[] _generatedWhereArchetypePlans = Array.Empty<GeneratedWhereStructuralPlan>();
    private GeneratedWhereTargetCursor?[] _generatedWhereTargetCursors = Array.Empty<GeneratedWhereTargetCursor?>();
    private NativeMemory<int> _generatedWhereTargetCursorStamps = new(0);
    private NativeMemory<int> _deferredQueryPlanArchetypeStamps = new(0);
    private Archetype[] _generatedWhereSourceArchetypes = Array.Empty<Archetype>();
    private NativeMemory<int> _generatedWhereSourceCounts = new(0);
    private int _generatedWhereSourceArchetypeCount;
    private int _generatedWherePlanStamp;
    private int _deferredQueryPlanArchetypeStamp;
    private int _generatedWhereDestroyCapacity;
    private bool _generatedWhereDestroyCapacityReserved;
    private bool _generatedWhereStructuralActive;
    private bool _queryPlanBatchActive;
    private TransitionEdge[] _batchEdgeSlots = Array.Empty<TransitionEdge>();
    private NativeMemory<int> _batchEdgeStamps = new(0);
    private int _batchEdgeStamp;
    private int _nextChunkId;
    private Chunk?[] _chunksById = Array.Empty<Chunk?>();
    private int _activeChunkLeases;
    private Stamp[][] _archetypeComponentWriteStamps = Array.Empty<Stamp[]>();
    private bool _disposed;
    private int _schedulerExecutionActive;

    [ThreadStatic]
    private static World? _schedulerExecutionWorld;

    public World(
        ComponentLayoutRegistry? layouts = null,
        int initialEntityCapacity = DefaultInitialCapacity)
    {
        ThrowHelper.ThrowIfNegative(initialEntityCapacity, nameof(initialEntityCapacity));

        _layouts = layouts ?? new ComponentLayoutRegistry();
        _records.Capacity = initialEntityCapacity;
    }

    private int _aliveEntityCount;

    public int AliveEntityCount
    {
        get
        {
            EnsureExecutionAccess();
            return _aliveEntityCount;
        }
        private set => _aliveEntityCount = value;
    }

    public ComponentLayoutRegistry Layouts
    {
        get
        {
            EnsureExecutionAccess();
            return _layouts;
        }
    }

    internal bool IsDisposed => _disposed;

    internal List<Archetype> Archetypes => _archetypes;

    /// <summary>
    /// Releases native storage owned by this world and all of its archetypes.
    /// A world is the sole owner of these buffers; callers must dispose the
    /// world rather than copying or disposing individual storage fields.
    /// </summary>
    public void Dispose()
    {
        EnsureExecutionAccess();
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
        _componentSetCache.Clear();

        foreach (var archetype in _archetypes)
        {
            archetype.Dispose();
        }

        DisposeGeneratedParallelExecutors();
        _freeRecords.Dispose();
        _destroyScratch.Dispose();
        _generatedWhereSourceCounts.Dispose();
        _generatedWhereTargetCursorStamps.Dispose();
        _deferredQueryPlanArchetypeStamps.Dispose();
        _deferredQueryPlanArchetypes.Clear();
        _deferredQueryPlans.Clear();
        _batchEdgeStamps.Dispose();
        _archetypeComponentWriteStamps = Array.Empty<Stamp[]>();
        _componentRowArrayPool.Clear();
        _chunksById = Array.Empty<Chunk?>();
        _freeRecordChunks.Clear();
        GC.SuppressFinalize(this);
    }

    public Query CreateQuery(in QuerySpec spec)
    {
        EnsureExecutionAccess();
        return new Query(this, GetOrCreateQuery(spec), spec);
    }

    /// <summary>Creates a query requiring the supplied component registrations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query WhereAll(params ReadOnlySpan<ComponentId> components)
        => CreateQuery(QuerySpec.WhereAll(components));

    /// <summary>Creates a query matching at least one supplied component registration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query WhereAny(params ReadOnlySpan<ComponentId> components)
        => CreateQuery(QuerySpec.WhereAny(components));

    /// <summary>Creates a query excluding the supplied component registrations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query WhereNone(params ReadOnlySpan<ComponentId> components)
        => CreateQuery(QuerySpec.WhereNone(components));

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

        if (!TryGetComponentSet(componentIds, out ComponentSet? componentSet))
        {
            ThrowHelper.ThrowInvalidComponentList();
        }

        var archetype = GetOrCreateArchetype(componentSet.Mask);
        return CreateBatch(archetype, output);
    }

    /// <summary>Creates a requested number of entities into caller-owned storage.</summary>
    /// <remarks>When <paramref name="output"/> is empty, handles are not retained.</remarks>
    public int Create(ReadOnlySpan<ComponentId> componentIds, int count, Span<Entity> output)
    {
        ThrowHelper.ThrowIfNegative(count, nameof(count));
        if (output.Length != 0 && output.Length < count)
        {
            ThrowHelper.ThrowEntityDestinationTooSmall(nameof(output));
        }
        if (output.Length == 0)
        {
            EnsureNoActiveLease("create entities");
            if (count == 0)
            {
                return 0;
            }

            if (!TryGetComponentSet(componentIds, out ComponentSet? componentSet))
            {
                ThrowHelper.ThrowInvalidComponentList();
            }

            return CreateBatch(GetOrCreateArchetype(componentSet.Mask), count);
        }

        return Create(componentIds, output[..count]);
    }

    /// <summary>Creates entities with the supplied component set without retaining entity handles.</summary>
    public int Create(ReadOnlySpan<ComponentId> componentIds, int count)
        => Create(componentIds, count, Span<Entity>.Empty);

    private int CreateBatch(Archetype archetype, Span<Entity> output)
        => CreateBatch(archetype, output.Length, output);

    private int CreateBatch(Archetype archetype, int count)
        => CreateBatchNoOutput(archetype, count);

    private int CreateBatch(Archetype archetype, int count, Span<Entity> output)
    {
        if (output.IsEmpty)
        {
            return CreateBatchNoOutput(archetype, count);
        }

        if (count == 0)
        {
            return 0;
        }

        BeginQueryPlanBatch();
        DeferQueryPlanUpdates(archetype);
        try
        {
            _records.EnsureCapacity(checked(_records.Count + count));
            bool appendOnly = _freeCount == 0 && _freeRecordChunks.Count == 0;
            int appendedRecordStart = _records.Count;
            Span<EntityRecord> appendedRecords = appendOnly ? _records.Append(count) : default;
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
                RegisterChunk(chunk);
                int slotIndex = chunk.Count - reserved;
                if (reusedCount != 0)
                {
                    chunk.InitializeSlotRange(slotIndex, reusedCount);
                }

                chunk.StampAllRange(slotIndex, reserved, new Stamp(1));
                Span<Entity> chunkEntities = chunk.RawEntities;
                if (appendOnly)
                {
                    int recordOffset = outputIndex;
                    for (int reservedIndex = 0; reservedIndex < reserved; reservedIndex++)
                    {
                        int reservedSlot = slotIndex + reservedIndex;
                        int recordIndex = appendedRecordStart + recordOffset + reservedIndex;
                        appendedRecords[recordOffset + reservedIndex] = new EntityRecord
                        {
                            Generation = 1,
                            ChunkId = chunk.GlobalId,
                            SlotIndex = reservedSlot
                        };
                        var entity = new Entity(recordIndex, 1);
                        chunkEntities[reservedSlot] = entity;
                        output.RefAt(outputIndex++) = entity;
                    }
                }
                else
                {
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
                        chunkEntities[reservedSlot] = entity;
                        output.RefAt(outputIndex++) = entity;
                    }
                }
            }

            AliveEntityCount += count;
            return count;
        }
        finally
        {
            EndQueryPlanBatch();
        }
    }

    private int CreateBatchNoOutput(Archetype archetype, int count)
    {
        if (count == 0)
        {
            return 0;
        }

        BeginQueryPlanBatch();
        DeferQueryPlanUpdates(archetype);
        try
        {
            _records.EnsureCapacity(checked(_records.Count + count));
            bool appendOnly = _freeCount == 0 && _freeRecordChunks.Count == 0;
            int appendedRecordStart = _records.Count;
            Span<EntityRecord> appendedRecords = appendOnly ? _records.Append(count) : default;
            int created = 0;
            while (created < count)
            {
                int remaining = count - created;
                int chunkId = archetype.HasAvailableChunk(remaining) ? -1 : AllocateChunkId();
                int reserved = archetype.ReserveRange(
                    remaining,
                    chunkId,
                    out _,
                    out var chunk,
                    out int reusedCount);
                RegisterChunk(chunk);
                int slotIndex = chunk.Count - reserved;
                if (reusedCount != 0)
                {
                    chunk.InitializeSlotRange(slotIndex, reusedCount);
                }

                chunk.StampAllRange(slotIndex, reserved, new Stamp(1));
                Span<Entity> chunkEntities = chunk.RawEntities;
                if (appendOnly)
                {
                    for (int reservedIndex = 0; reservedIndex < reserved; reservedIndex++)
                    {
                        int reservedSlot = slotIndex + reservedIndex;
                        int recordIndex = appendedRecordStart + created + reservedIndex;
                        appendedRecords[created + reservedIndex] = new EntityRecord
                        {
                            Generation = 1,
                            ChunkId = chunk.GlobalId,
                            SlotIndex = reservedSlot
                        };
                        chunkEntities[reservedSlot] = new Entity(recordIndex, 1);
                    }
                }
                else
                {
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
                        chunkEntities[reservedSlot] = entity;
                    }
                }

                created += reserved;
            }

            AliveEntityCount += count;
            return count;
        }
        finally
        {
            EndQueryPlanBatch();
        }
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

        BeginQueryPlanBatch();
        try
        {
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
        finally
        {
            EndQueryPlanBatch();
        }
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

    public bool IsAlive(Entity entity)
    {
        EnsureExecutionAccess();
        return TryResolve(entity, out _);
    }

    /// <summary>Reports whether an alive entity owns the specified component.</summary>
    public bool Has(Entity entity, ComponentId componentId)
    {
        if (!TryResolve(entity, out int recordIndex))
        {
            return false;
        }

        return GetRecordArchetype(RecordAt(recordIndex)).Contains(componentId);
    }

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
    public bool Add(Entity entity, params ReadOnlySpan<ComponentId> componentIds)
    {
        Span<Entity> entities = stackalloc Entity[1];
        entities.GetRefAtZero() = entity;
        return ApplyComponents(true, componentIds, entities) == 1;
    }

    /// <summary>Adds the component set to one entity and reports whether it changed.</summary>
    public bool Add(Entity entity, ComponentId[] componentIds)
        => Add(entity, (ReadOnlySpan<ComponentId>)componentIds);

    /// <summary>Adds the component set to every entity in a caller-owned batch.</summary>
    public int Add(ReadOnlySpan<Entity> entities, params ReadOnlySpan<ComponentId> componentIds)
        => ApplyComponents(true, componentIds, entities);

    /// <summary>Adds the component set to every entity in a caller-owned batch.</summary>
    public int Add(ReadOnlySpan<Entity> entities, ComponentId[] componentIds)
        => Add(entities, (ReadOnlySpan<ComponentId>)componentIds);

    /// <summary>Removes the component set from one entity and reports whether it changed.</summary>
    public bool Remove(Entity entity, params ReadOnlySpan<ComponentId> componentIds)
    {
        Span<Entity> entities = stackalloc Entity[1];
        entities.GetRefAtZero() = entity;
        return ApplyComponents(false, componentIds, entities) == 1;
    }

    /// <summary>Removes the component set from one entity and reports whether it changed.</summary>
    public bool Remove(Entity entity, ComponentId[] componentIds)
        => Remove(entity, (ReadOnlySpan<ComponentId>)componentIds);

    /// <summary>Removes the component set from every entity in a caller-owned batch.</summary>
    public int Remove(ReadOnlySpan<Entity> entities, params ReadOnlySpan<ComponentId> componentIds)
        => ApplyComponents(false, componentIds, entities);

    /// <summary>Removes the component set from every entity in a caller-owned batch.</summary>
    public int Remove(ReadOnlySpan<Entity> entities, ComponentId[] componentIds)
        => Remove(entities, (ReadOnlySpan<ComponentId>)componentIds);

    public int Add(in Query query, ComponentId[] componentIds) => ApplyQueryComponents(query, true, componentIds);

    /// <summary>Adds a component set to every entity matched by a query.</summary>
    public int Add(in Query query, params ReadOnlySpan<ComponentId> componentIds)
        => ApplyQueryComponents(query, true, componentIds);

    public int Remove(in Query query, ComponentId[] componentIds) => ApplyQueryComponents(query, false, componentIds);

    /// <summary>Removes a component set from every entity matched by a query.</summary>
    public int Remove(in Query query, params ReadOnlySpan<ComponentId> componentIds)
        => ApplyQueryComponents(query, false, componentIds);

    public int Destroy(in Query query)
    {
        EnsureExecutionAccess();
        ValidateQuery(query);
        EnsureNoActiveLease("destroy entities");

        var cached = query.Cached;
        ReadOnlySpan<int> archetypes = cached.MatchingArchetypes();
        int destroyed = 0;
        BeginQueryPlanBatch();
        try
        {
            for (int archetypeIndex = 0; archetypeIndex < archetypes.Length; archetypeIndex++)
            {
                var archetype = _archetypes[archetypes.RefAt(archetypeIndex)];
                DeferQueryPlanUpdates(archetype);
                for (int chunkIndex = archetype.ChunkCount - 1; chunkIndex >= 0; chunkIndex--)
                {
                    if (archetype.GetChunk(chunkIndex).Count == 0)
                    {
                        continue;
                    }

                    destroyed += DestroyChunk(archetype, chunkIndex);
                }
            }

            return destroyed;
        }
        finally
        {
            EndQueryPlanBatch();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp GetComponentStamp(
        int archetypeId,
        Chunk chunk,
        int componentIndex,
        int slotIndex)
        => new(unchecked(
            chunk.GetComponentStampTrusted(componentIndex, slotIndex).Value
            + _archetypeComponentWriteStamps.RefAt(archetypeId).RefAt(componentIndex).Value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal EntityComponentStampWriter CreateEntityComponentStampWriter(
        Chunk chunk,
        int componentIndex,
        int slotIndex,
        Stamp stamp)
        => new(chunk, componentIndex, slotIndex, stamp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp[] GetArchetypeComponentStamps(int archetypeId)
        => _archetypeComponentWriteStamps.RefAt(archetypeId);

    internal void BeginQueryLease() => _activeChunkLeases++;

    internal void EndQueryLease() => _activeChunkLeases--;

    private int ApplyComponents(bool isAdd, ReadOnlySpan<ComponentId> componentIds, ReadOnlySpan<Entity> entities)
    {
        EnsureNoActiveLease(isAdd ? "add components" : "remove components");
        if (componentIds.Length == 0 || entities.Length == 0)
        {
            return 0;
        }

        if (!TryGetComponentSet(componentIds, out ComponentSet? changeSet))
        {
            return 0;
        }

        int edgeStamp = entities.Length == 1 ? 0 : BeginBatchEdgeCache();
        int changed = 0;
        bool batch = entities.Length > 1;
        if (batch)
        {
            BeginQueryPlanBatch();
        }

        try
        {
            for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
            {
                var entity = entities.RefAt(entityIndex);
                if (!TryResolve(entity, out int recordIndex))
                {
                    continue;
                }

                ref readonly var record = ref RecordAt(recordIndex);
                int sourceArchetypeId = GetRecordChunk(record).ArchetypeId;
                var edge = edgeStamp == 0
                    ? GetTransitionEdge(sourceArchetypeId, changeSet, isAdd)
                    : GetBatchTransitionEdge(sourceArchetypeId, changeSet, isAdd, edgeStamp);
                if (edge.IsNoOp)
                {
                    continue;
                }

                MoveEntity(recordIndex, edge);
                changed++;
            }

            return changed;
        }
        finally
        {
            if (batch)
            {
                EndQueryPlanBatch();
            }
        }
    }

    private int ApplyQueryComponents(in Query query, bool isAdd, ReadOnlySpan<ComponentId> componentIds)
    {
        EnsureExecutionAccess();
        ValidateQuery(query);
        EnsureNoActiveLease(isAdd ? "add components" : "remove components");
        if (componentIds.Length == 0)
        {
            return 0;
        }

        if (!TryGetComponentSet(componentIds, out ComponentSet? changeSet))
        {
            return 0;
        }

        var cached = query.Cached;
        ReadOnlySpan<int> matchingArchetypes = cached.MatchingArchetypes();
        int edgeStamp = BeginBatchEdgeCache();
        int changed = 0;
        BeginQueryPlanBatch();

        try
        {
            for (int matchingIndex = 0; matchingIndex < matchingArchetypes.Length; matchingIndex++)
            {
                var sourceArchetype = _archetypes[matchingArchetypes.RefAt(matchingIndex)];
                if (sourceArchetype.ActiveChunkCount == 0)
                {
                    continue;
                }

                var edge = GetBatchTransitionEdge(sourceArchetype.Id, changeSet, isAdd, edgeStamp);
                if (edge.IsNoOp)
                {
                    continue;
                }

                changed += MoveArchetypeBlocks(sourceArchetype, edge);
            }

            return changed;
        }
        finally
        {
            EndQueryPlanBatch();
        }
    }

    internal bool BeginGeneratedWhereStructural(
        in Query query,
        bool isDestroy,
        bool isAdd,
        ReadOnlySpan<ComponentId> componentIds,
        out GeneratedDenseExecution execution)
    {
        EnsureExecutionAccess();
        EnsureNoActiveLease(isDestroy
            ? "destroy entities"
            : isAdd ? "add components" : "remove components");
        ValidateQuery(in query);

        ComponentSet changeSet;
        if (isDestroy)
        {
            changeSet = ComponentSet.Empty;
        }
        else if (!TryGetComponentSet(componentIds, out ComponentSet? resolvedChangeSet))
        {
            execution = default;
            return false;
        }
        else
        {
            changeSet = resolvedChangeSet;
        }

        QueryPlan plan = query.Cached;
        ReadOnlySpan<ArchetypePlan> matchingPlans = plan.MatchingPlans();
        EnsureGeneratedWhereSourceArchetypeCapacity(matchingPlans.Length);
        _generatedWhereSourceArchetypeCount = matchingPlans.Length;
        for (int index = 0; index < matchingPlans.Length; index++)
        {
            _generatedWhereSourceArchetypes[index] = matchingPlans.RefAt(index).Archetype;
        }

        BeginQueryPlanBatch();
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
                DeferQueryPlanUpdates(sourceArchetype);
            }
            else
            {
                TransitionEdge edge = GetBatchTransitionEdge(
                    sourceArchetype.Id,
                    changeSet,
                    isAdd,
                    edgeStamp);
                if (!edge.IsNoOp)
                {
                    targetArchetype = _archetypes[edge.TargetArchetypeId];
                    sourceToTargetRows = edge.SourceToTargetRowIndices;
                    addedTargetRows = edge.AddedTargetRowIndices;
                    targetCursor = GetGeneratedWhereTargetCursor(targetArchetype.Id);
                    DeferQueryPlanUpdates(sourceArchetype);
                    DeferQueryPlanUpdates(targetArchetype);
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
            EndQueryPlanBatch();
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
    private void DeferQueryPlanUpdates(Archetype archetype)
    {
        archetype.DeferQueryPlanUpdates();
        MarkDeferredQueryPlanArchetype(archetype);
    }

    private void BeginQueryPlanBatch()
    {
        _deferredQueryPlanArchetypes.Clear();
        _deferredQueryPlans.Clear();
        _deferredQueryPlanArchetypeStamp = NextGeneratedWhereStamp(_deferredQueryPlanArchetypeStamp);
        EnsureGeneratedWhereArchetypeCapacity(_archetypes.Count);
        _queryPlanBatchActive = true;
    }

    private void EndQueryPlanBatch()
    {
        try
        {
            for (int index = 0; index < _deferredQueryPlanArchetypes.Count; index++)
            {
                _deferredQueryPlanArchetypes[index].RefreshQueryPlans(_deferredQueryPlans);
            }

            for (int index = 0; index < _deferredQueryPlans.Count; index++)
            {
                _deferredQueryPlans[index].CompleteChunkPlanRefresh();
            }
        }
        finally
        {
            _deferredQueryPlanArchetypes.Clear();
            _deferredQueryPlans.Clear();
            _queryPlanBatchActive = false;
        }
    }

    private void MarkDeferredQueryPlanArchetype(Archetype archetype)
    {
        int archetypeId = archetype.Id;
        EnsureGeneratedWhereArchetypeCapacity(archetypeId + 1);
        if (_deferredQueryPlanArchetypeStamps.RefAt(archetypeId) == _deferredQueryPlanArchetypeStamp)
        {
            return;
        }

        _deferredQueryPlanArchetypeStamps.RefAt(archetypeId) = _deferredQueryPlanArchetypeStamp;
        _deferredQueryPlanArchetypes.Add(archetype);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkGeneratedWhereAffected(Archetype archetype)
        => MarkDeferredQueryPlanArchetype(archetype);

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
        _deferredQueryPlanArchetypeStamps.Resize(capacity);
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
            _deferredQueryPlanArchetypeStamps.Span.Clear();
            return 1;
        }

        return stamp + 1;
    }

    internal void CopyGeneratedWhereRun<TInitializer>(
        Chunk sourceChunk,
        int sourceSlot,
        int count,
        Archetype targetArchetype,
        int[] sourceToTargetRows,
        int[] addedTargetRows,
        GeneratedWhereTargetCursor targetCursor,
        ref TInitializer initializer)
        where TInitializer : struct, IGeneratedComponentValueInitializer
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
                RegisterChunk(targetChunk);
                targetCursor.Chunk = targetChunk;
                targetCursor.Slot = targetChunk.Count - reserved;
                targetCursor.Remaining = reserved;
            }

            int copied = Math.Min(count, targetCursor.Remaining);
            Chunk target = targetCursor.Chunk!;
            int targetSlot = targetCursor.Slot;
            CopyChunkRange(
                sourceChunk,
                target,
                sourceSlot,
                targetSlot,
                copied,
                edge);
            var writer = new GeneratedComponentValueWriter(
                target,
                targetArchetype,
                targetSlot,
                edge.AddedTargetRowIndices,
                copied);
            initializer.Initialize(ref writer);
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
        DeferQueryPlanUpdates(sourceArchetype);
        DeferQueryPlanUpdates(targetArchetype);
        int initialTargetChunkCount = targetArchetype.ChunkCount;
        targetArchetype.PrepareBlockMoveCandidates(initialTargetChunkCount);
        while (sourceArchetype.TryGetLastActiveChunk(out int sourceChunkIndex, out Chunk sourceChunk))
        {
            while (sourceChunk.Count != 0
                && targetArchetype.TryTakeBlockPartialChunk(out Chunk targetChunk))
            {
                int copied = Math.Min(sourceChunk.Count, Chunk.Capacity - targetChunk.Count);
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
                Chunk replacedDonor = targetArchetype.ReplaceEmptyChunk(donorChunkIndex, adopted);
                DisposeDetachedChunk(replacedDonor);
            }

            if (!hasDonor)
            {
                targetArchetype.AttachAdoptedChunk(adopted);
            }
            movedCount += adopted.Count;
        }

        return movedCount;
    }

    private void DisposeDetachedChunk(Chunk chunk)
    {
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

        if (_queryPlanBatchActive)
        {
            DeferQueryPlanUpdates(archetype);
        }

        chunk.BeginFreeRecordBlock(count);
        chunk.SetFreeRecordListIndex(_freeRecordChunks.Count);
        _freeRecordChunks.Add(chunk);
        chunk.ClearAll();
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
        if (_queryPlanBatchActive)
        {
            DeferQueryPlanUpdates(archetype);
        }

        int chunkIndex = chunk.ArchetypeIndex;
        var moved = archetype.RemoveEntity(chunkIndex, record.SlotIndex);
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
        if (_queryPlanBatchActive)
        {
            DeferQueryPlanUpdates(sourceArchetype);
            DeferQueryPlanUpdates(targetArchetype);
        }

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
        RegisterChunk(targetChunk);

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
        ComponentSet changeSet,
        bool isAdd)
    {
        var key = new TransitionKey(sourceArchetypeId, changeSet.Id, isAdd);
        if (_transitionCache.TryGetValue(key, out var edge))
        {
            return edge;
        }

        var source = _archetypes[sourceArchetypeId];
        bool noOp = isAdd
            ? source.Mask.ContainsAll(changeSet.Mask)
            : !source.Mask.Intersects(changeSet.Mask);
        if (noOp)
        {
            edge = TransitionEdge.NoOp(sourceArchetypeId);
            _transitionCache.Add(key, edge);
            return edge;
        }

        ComponentMask targetMask = isAdd
            ? source.Mask.Or(changeSet.Mask)
            : source.Mask.Except(changeSet.Mask);
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
            _componentRowArrayPool);
        _archetypeByMask.Add(mask, archetype.Id);
        _archetypes.Add(archetype);
        RegisterArchetypeStampStorage(archetype.Id, archetype.ComponentCount);
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
        if (archetypeId >= _archetypeComponentWriteStamps.Length)
        {
            int capacity = Math.Max(
                archetypeId + 1,
                _archetypeComponentWriteStamps.Length == 0
                    ? 4
                    : _archetypeComponentWriteStamps.Length * 2);
            Array.Resize(ref _archetypeComponentWriteStamps, capacity);
        }

        _archetypeComponentWriteStamps.RefAt(archetypeId) = componentCount == 0
            ? Array.Empty<Stamp>()
            : new Stamp[componentCount];
    }

    private void RegisterChunk(Chunk chunk)
    {
        EnsureChunkReferenceCapacity(chunk.GlobalId + 1);
        _chunksById[chunk.GlobalId] = chunk;
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

    private bool TryGetComponentSet(
        ReadOnlySpan<ComponentId> componentIds,
        [NotNullWhen(true)] out ComponentSet? componentSet)
    {
        if (componentIds.Length == 0)
        {
            componentSet = null;
            return false;
        }

        componentSet = GetOrCreateComponentSet(componentIds);
        return true;
    }

    internal ComponentSet GetOrCreateComponentSet(ReadOnlySpan<ComponentId> componentIds)
    {
        EnsureExecutionAccess();
        if (componentIds.Length == 0)
        {
            return ComponentSet.Empty;
        }

        if (_componentSetCache.TryGet(componentIds, out ComponentSet? cached))
        {
            return cached;
        }

        ComponentSet set = CreateComponentSet(componentIds);
        _componentSetCache.Add(set);
        return set;
    }

    internal bool TryGetComponentSet(ComponentSetId id, out ComponentSet? set)
        => _componentSetCache.TryGet(id, out set);

    internal ComponentSet GetOrCreateComponentSet(
        RuntimeTypeHandle key,
        Func<World, ComponentId[]> resolver)
    {
        if (_componentSetCache.TryGet(key, out ComponentSet? cached))
        {
            return cached;
        }

        ComponentId[] componentIds = resolver(this);
        if (componentIds.Length == 0)
        {
            return ComponentSet.Empty;
        }

        ComponentSet set = GetOrCreateComponentSet(componentIds);
        _componentSetCache.Add(key, set);
        return set;
    }

    private ComponentSet CreateComponentSet(ReadOnlySpan<ComponentId> componentIds)
    {
        var ownedIds = new ComponentId[componentIds.Length];
        componentIds.CopyTo(ownedIds);
        return CreateComponentSet(ownedIds);
    }

    private ComponentSet CreateComponentSet(ComponentId[] componentIds)
    {
        ValidateComponentIds(componentIds);
        return new ComponentSet(componentIds, ComponentMask.From(componentIds));
    }

    private void ValidateComponentIds(ReadOnlySpan<ComponentId> componentIds)
    {
        for (int index = 0; index < componentIds.Length; index++)
        {
            ComponentId componentId = componentIds[index];
            if (!componentId.IsValid)
            {
                ThrowHelper.ThrowComponentIdOutOfRange();
            }

            if (!_layouts.TryGet(componentId, out _))
            {
                ThrowHelper.ThrowWorldComponentNotRegistered(componentId.Value, nameof(componentIds));
            }
        }
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
        if ((uint)recordIndex >= (uint)_records.Count)
        {
            return false;
        }

        ref readonly var record = ref RecordAt(recordIndex);
        if (record.Generation != entity.Generation
            || record.ChunkId < 0
            || !TryGetChunkById(record.ChunkId, out Chunk chunk)
            || (uint)record.SlotIndex >= (uint)chunk.Count)
        {
            return false;
        }

        return chunk.RawEntities.RefAt(record.SlotIndex) == entity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryResolveEntityLocation(Entity entity, out Chunk chunk, out int slotIndex)
    {
        if (!TryResolve(entity, out int recordIndex))
        {
            chunk = null!;
            slotIndex = -1;
            return false;
        }

        ref readonly EntityRecord record = ref RecordAt(recordIndex);
        chunk = GetRecordChunk(record);
        slotIndex = record.SlotIndex;
        return true;
    }

    private QueryPlan GetOrCreateQuery(QuerySpec spec)
    {
        EnsureExecutionAccess();
        if (--_queryCacheSweepCountdown == 0)
        {
            SweepDeadQueryPlans();
            _queryCacheSweepCountdown = QueryCacheSweepInterval;
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

    internal IDisposable EnterSchedulerExecution()
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        if (_schedulerExecutionWorld is not null
            || Interlocked.CompareExchange(ref _schedulerExecutionActive, 1, 0) != 0)
        {
            ThrowHelper.ThrowWorldSchedulerAlreadyExecuting();
        }

        _schedulerExecutionWorld = this;
        return new SchedulerExecutionLease(this);
    }

    internal void EnterSchedulerWorker()
    {
        if (Volatile.Read(ref _schedulerExecutionActive) == 0
            || _schedulerExecutionWorld is not null)
        {
            ThrowHelper.ThrowUnauthorizedSchedulerWorker();
        }

        _schedulerExecutionWorld = this;
    }

    internal bool TryEnterSchedulerWorker()
    {
        if (Volatile.Read(ref _schedulerExecutionActive) == 0)
        {
            return false;
        }

        EnterSchedulerWorker();
        return true;
    }

    internal void ExitSchedulerWorker()
    {
        if (ReferenceEquals(_schedulerExecutionWorld, this))
        {
            _schedulerExecutionWorld = null;
        }
    }

    internal void EnsureExecutionAccess()
    {
        if (Volatile.Read(ref _schedulerExecutionActive) != 0
            && !ReferenceEquals(_schedulerExecutionWorld, this))
        {
            ThrowHelper.ThrowWorldSchedulerExecutionActive();
        }
    }

    private void ExitSchedulerExecution()
    {
        if (ReferenceEquals(_schedulerExecutionWorld, this))
        {
            _schedulerExecutionWorld = null;
        }

        Volatile.Write(ref _schedulerExecutionActive, 0);
    }

    private sealed class SchedulerExecutionLease : IDisposable
    {
        private World? _world;

        internal SchedulerExecutionLease(World world) => _world = world;

        public void Dispose()
        {
            if (_world is { } world)
            {
                _world = null;
                world.ExitSchedulerExecution();
            }
        }
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
        EnsureExecutionAccess();
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
        ComponentSet changeSet,
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

        var edge = GetTransitionEdge(sourceArchetypeId, changeSet, isAdd);
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

        int capacity = Math.Max(required, _batchEdgeSlots.Length == 0 ? InitialBatchEdgeCapacity : _batchEdgeSlots.Length * 2);
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
        public TransitionKey(int sourceArchetypeId, ComponentSetId changeSetId, bool isAdd)
        {
            SourceArchetypeId = sourceArchetypeId;
            ChangeSetId = changeSetId;
            IsAdd = isAdd;
        }

        public int SourceArchetypeId { get; }
        public ComponentSetId ChangeSetId { get; }
        public bool IsAdd { get; }

        public bool Equals(TransitionKey other) => SourceArchetypeId == other.SourceArchetypeId
            && ChangeSetId == other.ChangeSetId && IsAdd == other.IsAdd;

        public override bool Equals(object? obj) => obj is TransitionKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (SourceArchetypeId * TransitionHashMultiplier) ^ ChangeSetId.Value;
                return (hash * TransitionHashMultiplier) ^ (IsAdd ? 1 : 0);
            }
        }
    }

    private readonly struct TransitionEdge
    {
        public TransitionEdge(
            int targetArchetypeId,
            int[] sourceToTargetRowIndices,
            int[] addedTargetRowIndices,
            bool isNoOp = false)
        {
            TargetArchetypeId = targetArchetypeId;
            SourceToTargetRowIndices = sourceToTargetRowIndices;
            AddedTargetRowIndices = addedTargetRowIndices;
            IsNoOp = isNoOp;
        }

        public int TargetArchetypeId { get; }
        public int[] SourceToTargetRowIndices { get; }
        public int[] AddedTargetRowIndices { get; }
        public bool IsNoOp { get; }

        public static TransitionEdge NoOp(int sourceArchetypeId)
            => new(sourceArchetypeId, Array.Empty<int>(), Array.Empty<int>(), isNoOp: true);
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
