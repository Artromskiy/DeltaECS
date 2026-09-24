namespace Delta.ECS;

using System;
using System.Runtime.ExceptionServices;
using System.Threading;

internal enum ParallelSchedulingStrategy
{
    RequestedWorkers,
    LimitWorkersToChunkCount,
    SingleThreadWhenUnderfilled,
    SplitChunksWhenUnderfilled,
    SingleThread,
    MinimumGrainWithSingleChunkSplit,
    ChunkBoundedGrain
}

/// <summary>
/// Persistent executor for a generated invoker with static per-worker ranges.
/// </summary>
internal sealed class StaticParallelQueryExecutor<TInvoker> : IDisposable
    where TInvoker : struct, IGeneratedParallelInvoker
{
    private const int DefaultWorkerCount = 2;
    private const int WorkerPollSpinCount = 8;
    private const int CacheLineSize = 64;
    private const int MinimumEntitiesPerWorker = Chunk.Capacity / 2;
    private readonly object _lifecycle = new();
    private WorkerSlot[] _workerSlots = Array.Empty<WorkerSlot>();
    private Worker[] _workers = Array.Empty<Worker>();
    private TInvoker[] _workerInvokers = Array.Empty<TInvoker>();
    private ExceptionDispatchInfo?[] _workerFailures = Array.Empty<ExceptionDispatchInfo?>();
    private ParallelChunk[] _chunks = Array.Empty<ParallelChunk>();
    private int[] _chunkOffsets = Array.Empty<int>();
    private Entity[] _entities = Array.Empty<Entity>();
    private ParallelRange[] _ranges = Array.Empty<ParallelRange>();
    private World? _entityWorld;
    private World? _world;
    private QueryPlan? _entityPlan;
    private int _entityCount;
    private bool _entityMode;
    private QueryPlan? _cachedPlan;
    private int _cachedPlanVersion = -1;
    private QueryPlan? _cachedRangePlan;
    private int _cachedRangeVersion = -1;
    private int _cachedRangeWorkerCount;
    private int _chunkCount;
    private int _totalEntityCount;
    private bool _splitChunkRanges;
    private readonly ParallelSchedulingStrategy _schedulingStrategy;
    private int _cachedRangeWorkCount;
    private bool _cachedRangeSplitChunks;
    private int _runVersion;
    private int _stopping;
    private bool _disposed;

    internal StaticParallelQueryExecutor()
        : this(ParallelSchedulingStrategy.RequestedWorkers)
    {
    }

    internal StaticParallelQueryExecutor(ParallelSchedulingStrategy schedulingStrategy)
    {
        _schedulingStrategy = schedulingStrategy;
    }

    internal void Execute(
        QueryPlan plan,
        ref TInvoker invoker,
        int requestedWorkerCount)
    {
        ThrowHelper.ThrowIfNegative(requestedWorkerCount, nameof(requestedWorkerCount));
        if (Volatile.Read(ref _disposed))
        {
            ThrowHelper.ThrowDisposedWorld();
        }

        BuildChunkList(plan);
        _world = plan.Owner;
        if (_chunkCount == 0)
        {
            return;
        }

        int workerCount = requestedWorkerCount == 0
            ? DefaultWorkerCount
            : requestedWorkerCount;
        workerCount = Math.Min(
            Math.Max(1, Environment.ProcessorCount),
            Math.Max(1, workerCount));

        bool splitChunkRanges = false;
        if (_schedulingStrategy == ParallelSchedulingStrategy.SingleThread)
        {
            ExecuteSingleThread(ref invoker);
            return;
        }

        if (_schedulingStrategy == ParallelSchedulingStrategy.MinimumGrainWithSingleChunkSplit)
        {
            if (_chunkCount == 1)
            {
                workerCount = Math.Min(workerCount, _totalEntityCount / MinimumEntitiesPerWorker);
                splitChunkRanges = workerCount > 1;
            }
            else
            {
                workerCount = Math.Min(
                    Math.Min(workerCount, _chunkCount),
                    _totalEntityCount / MinimumEntitiesPerWorker);
            }

            if (workerCount < 2)
            {
                ExecuteSingleThread(ref invoker);
                return;
            }
        }

        if (_schedulingStrategy == ParallelSchedulingStrategy.ChunkBoundedGrain)
        {
            if (_chunkCount == 1)
            {
                int maximumSplitWorkers = _totalEntityCount / MinimumEntitiesPerWorker;
                if (workerCount > 1 && workerCount <= maximumSplitWorkers)
                {
                    splitChunkRanges = true;
                }
                else
                {
                    ExecuteSingleThread(ref invoker);
                    return;
                }
            }
            else
            {
                if (_chunkCount < workerCount
                    || (workerCount > 1 && _totalEntityCount / workerCount < MinimumEntitiesPerWorker))
                {
                    ExecuteSingleThread(ref invoker);
                    return;
                }
            }
        }

        if (_schedulingStrategy is not (ParallelSchedulingStrategy.MinimumGrainWithSingleChunkSplit or ParallelSchedulingStrategy.ChunkBoundedGrain)
            && _chunkCount < workerCount)
        {
            switch (_schedulingStrategy)
            {
                case ParallelSchedulingStrategy.LimitWorkersToChunkCount:
                    workerCount = _chunkCount;
                    break;
                case ParallelSchedulingStrategy.SingleThreadWhenUnderfilled:
                    ExecuteSingleThread(ref invoker);
                    return;
                case ParallelSchedulingStrategy.SplitChunksWhenUnderfilled when !plan.HasTagFilters:
                    splitChunkRanges = true;
                    workerCount = Math.Min(workerCount, _totalEntityCount);
                    break;
                case ParallelSchedulingStrategy.SplitChunksWhenUnderfilled:
                    workerCount = _chunkCount;
                    break;
            }
        }

        if (invoker.RequiresSingleThread || workerCount == 1)
        {
            ExecuteSingleThread(ref invoker);
            return;
        }

        EnsureWorkerCapacity(workerCount - 1);
        _splitChunkRanges = splitChunkRanges;
        PrepareRanges(plan, workerCount, splitChunkRanges);

        for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            _workerInvokers.RefAt(workerIndex) = invoker;
            _workerFailures.RefAt(workerIndex) = null;
        }

        int run = _runVersion == int.MaxValue ? 1 : _runVersion + 1;
        _runVersion = run;
        for (int workerIndex = 1; workerIndex < workerCount; workerIndex++)
        {
            WorkerSlot slot = _workerSlots.RefAt(workerIndex);
            Volatile.Write(ref slot.PublishedRun, run);
        }

        ExecuteRange(0, run, workerSlot: null);
        for (int workerIndex = 1; workerIndex < workerCount; workerIndex++)
        {
            while (Volatile.Read(ref _workerSlots.RefAt(workerIndex).CompletedRun) != run)
            {
                Thread.SpinWait(WorkerPollSpinCount);
            }
        }

        invoker = _workerInvokers.GetRefAtZero();
        for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            if (_workerFailures.RefAt(workerIndex) is { } failure)
            {
                failure.Throw();
            }
        }
        _splitChunkRanges = false;
    }

    internal void ExecuteEntityList(
        World world,
        QueryPlan plan,
        ReadOnlySpan<Entity> entities,
        ref TInvoker invoker,
        int requestedWorkerCount)
    {
        ThrowHelper.ThrowIfNegative(requestedWorkerCount, nameof(requestedWorkerCount));
        if (Volatile.Read(ref _disposed))
        {
            ThrowHelper.ThrowDisposedWorld();
        }

        EnsureEntityCapacity(entities.Length);
        entities.CopyTo(_entities.AsSpan(0, entities.Length));
        _entityWorld = world;
        _entityPlan = plan;
        _entityCount = entities.Length;
        _entityMode = true;
        try
        {
            if (_entityCount == 0)
            {
                return;
            }

            int workerCount = requestedWorkerCount == 0
                ? DefaultWorkerCount
                : requestedWorkerCount;
            workerCount = Math.Min(
                Math.Max(1, Environment.ProcessorCount),
                Math.Max(1, workerCount));

            if (invoker.RequiresSingleThread || workerCount == 1)
            {
                ExecuteEntityRange(ref invoker, 0, _entityCount);
                return;
            }

            EnsureWorkerCapacity(workerCount - 1);
            PrepareEntityRanges(_entityCount, workerCount);

            for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
            {
                _workerInvokers.RefAt(workerIndex) = invoker;
                _workerFailures.RefAt(workerIndex) = null;
            }

            int run = _runVersion == int.MaxValue ? 1 : _runVersion + 1;
            _runVersion = run;
            for (int workerIndex = 1; workerIndex < workerCount; workerIndex++)
            {
                WorkerSlot slot = _workerSlots.RefAt(workerIndex);
                Volatile.Write(ref slot.PublishedRun, run);
            }

            ExecuteRange(0, run, workerSlot: null);
            for (int workerIndex = 1; workerIndex < workerCount; workerIndex++)
            {
                while (Volatile.Read(ref _workerSlots.RefAt(workerIndex).CompletedRun) != run)
                {
                    Thread.SpinWait(WorkerPollSpinCount);
                }
            }

            invoker = _workerInvokers.GetRefAtZero();
            for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
            {
                if (_workerFailures.RefAt(workerIndex) is { } failure)
                {
                    failure.Throw();
                }
            }
        }
        finally
        {
            _entityMode = false;
            _entityWorld = null;
            _entityPlan = null;
            _entityCount = 0;
        }
    }

    private void ExecuteSingleThread(ref TInvoker invoker)
    {
        TInvoker invocation = invoker;
        try
        {
            for (int chunkIndex = 0; chunkIndex < _chunkCount; chunkIndex++)
            {
                ParallelChunk work = _chunks.RefAt(chunkIndex);
                ChunkPlan chunkPlan = work.Chunk;
                GeneratedQuerySlots slots = new(_world!, in chunkPlan, _cachedPlan);
                invocation.Invoke(ref slots);
            }

            invoker = invocation;
        }
        catch (Exception exception)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    public void Dispose()
    {
        Worker[] workers;
        lock (_lifecycle)
        {
            if (_disposed)
            {
                return;
            }

            Volatile.Write(ref _disposed, true);
            Volatile.Write(ref _stopping, 1);
            workers = _workers;
        }

        for (int workerIndex = 0; workerIndex < workers.Length; workerIndex++)
        {
            workers.RefAt(workerIndex).Thread.Join();
        }

        _workers = Array.Empty<Worker>();
        _workerSlots = Array.Empty<WorkerSlot>();
        _workerInvokers = Array.Empty<TInvoker>();
        _workerFailures = Array.Empty<ExceptionDispatchInfo?>();
        _chunks = Array.Empty<ParallelChunk>();
        _entities = Array.Empty<Entity>();
        _ranges = Array.Empty<ParallelRange>();
        _chunkOffsets = Array.Empty<int>();
    }

    private void BuildChunkList(QueryPlan plan)
    {
        if (ReferenceEquals(_cachedPlan, plan) && _cachedPlanVersion == plan.MatchingVersion)
        {
            return;
        }

        ReadOnlySpan<ChunkPlan> chunks = plan.MatchingChunkPlans();
        EnsureChunkCapacity(chunks.Length);
        EnsureChunkOffsetCapacity(chunks.Length + 1);
        _chunkOffsets.RefAt(0) = 0;
        int totalEntityCount = 0;
        for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
        {
            ref readonly ChunkPlan chunk = ref chunks.RefAt(chunkIndex);
            _chunks.RefAt(chunkIndex) = new ParallelChunk(chunk);
            totalEntityCount += chunk.Chunk.Count;
            _chunkOffsets.RefAt(chunkIndex + 1) = totalEntityCount;
        }

        _chunkCount = chunks.Length;
        _totalEntityCount = totalEntityCount;
        _cachedPlan = plan;
        _cachedPlanVersion = plan.MatchingVersion;
        _cachedRangePlan = null;
        _cachedRangeVersion = -1;
        _cachedRangeWorkerCount = 0;
        _cachedRangeWorkCount = 0;
        _cachedRangeSplitChunks = false;
    }

    private void PrepareRanges(QueryPlan plan, int workerCount, bool splitChunks)
    {
        int workCount = splitChunks ? _totalEntityCount : _chunkCount;
        if (ReferenceEquals(_cachedRangePlan, plan)
            && _cachedRangeVersion == plan.MatchingVersion
            && _cachedRangeWorkerCount == workerCount
            && _cachedRangeWorkCount == workCount
            && _cachedRangeSplitChunks == splitChunks)
        {
            return;
        }

        EnsureRangeCapacity(workerCount);
        for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            _ranges.RefAt(workerIndex) = new ParallelRange(
                (int)((long)workerIndex * workCount / workerCount),
                (int)((long)(workerIndex + 1) * workCount / workerCount));
        }

        _cachedRangePlan = plan;
        _cachedRangeVersion = plan.MatchingVersion;
        _cachedRangeWorkerCount = workerCount;
        _cachedRangeWorkCount = workCount;
        _cachedRangeSplitChunks = splitChunks;
    }

    private void ExecuteRange(int workerIndex, int run, WorkerSlot? workerSlot)
    {
        ParallelRange range = _ranges.RefAt(workerIndex);
        World? world = _world;
        bool schedulerWorker = false;
        try
        {
            if (workerSlot is not null && world is not null)
            {
                schedulerWorker = world.TryEnterSchedulerWorker();
            }

            if (_entityMode)
            {
                ExecuteEntityRange(
                    ref _workerInvokers.RefAt(workerIndex),
                    range.StartChunk,
                    range.EndChunk);
                return;
            }

            TInvoker invocation = _workerInvokers.RefAt(workerIndex);
            if (_splitChunkRanges)
            {
                ExecuteChunkSlotRange(ref invocation, range.StartChunk, range.EndChunk);
            }
            else
            {
                for (int chunkIndex = range.StartChunk; chunkIndex < range.EndChunk; chunkIndex++)
                {
                    ParallelChunk work = _chunks.RefAt(chunkIndex);
                    ChunkPlan chunkPlan = work.Chunk;
                    GeneratedQuerySlots slots = new(_world!, in chunkPlan, _cachedPlan);
                    invocation.Invoke(ref slots);
                }
            }

            _workerInvokers.RefAt(workerIndex) = invocation;
        }
        catch (Exception exception)
        {
            _workerFailures.RefAt(workerIndex) = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            if (schedulerWorker && world is { } schedulerWorld)
            {
                schedulerWorld.ExitSchedulerWorker();
            }

            if (workerSlot is not null)
            {
                Volatile.Write(ref workerSlot.CompletedRun, run);
            }
        }
    }

    private void ExecuteChunkSlotRange(ref TInvoker invocation, int startEntity, int endEntity)
    {
        int chunkIndex = 0;
        while (chunkIndex < _chunkCount && _chunkOffsets.RefAt(chunkIndex + 1) <= startEntity)
        {
            chunkIndex++;
        }

        while (chunkIndex < _chunkCount && _chunkOffsets.RefAt(chunkIndex) < endEntity)
        {
            int chunkStart = _chunkOffsets.RefAt(chunkIndex);
            int chunkEnd = _chunkOffsets.RefAt(chunkIndex + 1);
            int rangeStart = Math.Max(startEntity, chunkStart);
            int rangeEnd = Math.Min(endEntity, chunkEnd);
            int rangeCount = rangeEnd - rangeStart;
            if (rangeCount > 0)
            {
                ChunkPlan chunkPlan = _chunks.RefAt(chunkIndex).Chunk;
                GeneratedQuerySlots slots = new(
                    _world!,
                    in chunkPlan,
                    rangeCount,
                    rangeStart - chunkStart,
                    _cachedPlan);
                invocation.Invoke(ref slots);
            }

            chunkIndex++;
        }
    }

    private void ExecuteEntityRange(ref TInvoker invoker, int start, int end)
    {
        World world = _entityWorld!;
        QueryPlan plan = _entityPlan!;
        TInvoker invocation = invoker;
        for (int index = start; index < end; index++)
        {
            Entity entity = _entities[index];
            if (!world.TryResolveEntityLocation(entity, out Chunk chunk, out int slot)
                || !plan.TryGetChunkPlan(chunk.ArchetypeId, chunk.GlobalId, out ChunkPlan chunkPlan)
                || !plan.MatchesTagSlot(chunk, slot))
            {
                continue;
            }

            var slots = new GeneratedQuerySlots(world, in chunkPlan, 1, slot);
            invocation.Invoke(ref slots);
        }

        invoker = invocation;
    }

    private void WorkerLoop(Worker worker)
    {
        int workerIndex = worker.Index;
        WorkerSlot workerSlot = worker.Slot;
        int observedRun = 0;
        while (Volatile.Read(ref _stopping) == 0)
        {
            int run;
            while ((run = Volatile.Read(ref workerSlot.PublishedRun)) == observedRun)
            {
                if (Volatile.Read(ref _stopping) != 0)
                {
                    return;
                }

                Thread.SpinWait(WorkerPollSpinCount);
            }

            if (Volatile.Read(ref _stopping) != 0)
            {
                return;
            }

            observedRun = run;
            ExecuteRange(workerIndex, run, workerSlot);
        }
    }

    private void EnsureWorkerCapacity(int requiredBackgroundWorkers)
    {
        if (requiredBackgroundWorkers <= _workers.Length
            && requiredBackgroundWorkers + 1 <= _workerSlots.Length)
        {
            return;
        }

        lock (_lifecycle)
        {
            if (_disposed)
            {
                ThrowHelper.ThrowDisposedWorld();
            }

            int previousLength = _workers.Length;
            int totalWorkers = requiredBackgroundWorkers + 1;
            if (_workerSlots.Length < totalWorkers)
            {
                int previousSlotLength = _workerSlots.Length;
                Array.Resize(ref _workerSlots, totalWorkers);
                Array.Resize(ref _workerInvokers, totalWorkers);
                Array.Resize(ref _workerFailures, totalWorkers);
                for (int workerIndex = previousSlotLength; workerIndex < totalWorkers; workerIndex++)
                {
                    _workerSlots.RefAt(workerIndex) = new WorkerSlot();
                }
            }

            if (_workers.Length < requiredBackgroundWorkers)
            {
                Array.Resize(ref _workers, requiredBackgroundWorkers);
            }

            for (int workerIndex = previousLength; workerIndex < requiredBackgroundWorkers; workerIndex++)
            {
                Worker worker = new(this, workerIndex + 1, _workerSlots.RefAt(workerIndex + 1));
                _workers.RefAt(workerIndex) = worker;
                worker.Thread.Start(worker);
            }
        }
    }

    private void EnsureChunkCapacity(int required)
    {
        if (required <= _chunks.Length)
        {
            return;
        }

        int capacity = _chunks.Length == 0 ? 4 : _chunks.Length;
        while (capacity < required)
        {
            capacity = checked(capacity * 2);
        }

        Array.Resize(ref _chunks, capacity);
    }

    private void EnsureChunkOffsetCapacity(int required)
    {
        if (required > _chunkOffsets.Length)
        {
            Array.Resize(ref _chunkOffsets, required);
        }
    }

    private void EnsureEntityCapacity(int required)
    {
        if (required <= _entities.Length)
        {
            return;
        }

        int capacity = _entities.Length == 0 ? 4 : _entities.Length;
        while (capacity < required)
        {
            capacity = checked(capacity * 2);
        }

        Array.Resize(ref _entities, capacity);
    }

    private void EnsureRangeCapacity(int required)
    {
        if (required <= _ranges.Length)
        {
            return;
        }

        Array.Resize(ref _ranges, required);
    }

    private void PrepareEntityRanges(int entityCount, int workerCount)
    {
        EnsureRangeCapacity(workerCount);
        for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            _ranges.RefAt(workerIndex) = new ParallelRange(
                (int)((long)workerIndex * entityCount / workerCount),
                (int)((long)(workerIndex + 1) * entityCount / workerCount));
        }
    }

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential,
        Size = CacheLineSize)]
    private sealed class WorkerSlot
    {
        internal int PublishedRun;

        internal int CompletedRun;
    }

    private readonly struct ParallelRange
    {
        internal ParallelRange(int startChunk, int endChunk)
        {
            StartChunk = startChunk;
            EndChunk = endChunk;
        }

        internal int StartChunk { get; }
        internal int EndChunk { get; }
    }

    private readonly struct ParallelChunk
    {
        internal ParallelChunk(ChunkPlan chunk) => Chunk = chunk;

        internal ChunkPlan Chunk { get; }
    }

    private sealed class Worker
    {
        internal Worker(
            StaticParallelQueryExecutor<TInvoker> owner,
            int index,
            WorkerSlot slot)
        {
            Slot = slot;
            Thread = new Thread(static state => ((Worker)state!).Owner.WorkerLoop((Worker)state!))
            {
                IsBackground = true,
                Name = $"DeltaECS.GeneratedQueryWorker.{index}"
            };
            Owner = owner;
            Index = index;
        }

        internal StaticParallelQueryExecutor<TInvoker> Owner { get; }
        internal int Index { get; }
        internal WorkerSlot Slot { get; }
        internal Thread Thread { get; }
    }
}
