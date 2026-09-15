namespace Delta.ECS;

using System;
using System.Runtime.ExceptionServices;
using System.Threading;

/// <summary>
/// Persistent executor for a generated invoker with static per-worker ranges.
/// </summary>
internal sealed class StaticParallelQueryExecutor<TInvoker> : IDisposable
    where TInvoker : struct, IGeneratedParallelInvoker
{
    private const int DefaultWorkerCount = 2;
    private const int WorkerPollSpinCount = 8;
    private const int CacheLineSize = 64;
    private readonly object _lifecycle = new();
    private WorkerSlot[] _workerSlots = Array.Empty<WorkerSlot>();
    private Worker[] _workers = Array.Empty<Worker>();
    private TInvoker[] _workerInvokers = Array.Empty<TInvoker>();
    private ExceptionDispatchInfo?[] _workerFailures = Array.Empty<ExceptionDispatchInfo?>();
    private ParallelChunk[] _chunks = Array.Empty<ParallelChunk>();
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
    private int _runVersion;
    private int _stopping;
    private bool _disposed;

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

        if (invoker.RequiresSingleThread || workerCount == 1)
        {
            ExecuteSingleThread(ref invoker);
            return;
        }

        EnsureWorkerCapacity(workerCount - 1);
        PrepareRanges(plan, workerCount);

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
        try
        {
            for (int chunkIndex = 0; chunkIndex < _chunkCount; chunkIndex++)
            {
                ParallelChunk work = _chunks.RefAt(chunkIndex);
                ChunkPlan chunkPlan = work.Chunk;
                GeneratedQuerySlots slots = new(_world!, in chunkPlan);
                invoker.Invoke(ref slots);
            }
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
    }

    private void BuildChunkList(QueryPlan plan)
    {
        if (ReferenceEquals(_cachedPlan, plan) && _cachedPlanVersion == plan.MatchingVersion)
        {
            return;
        }

        ReadOnlySpan<ChunkPlan> chunks = plan.MatchingChunkPlans();
        EnsureChunkCapacity(chunks.Length);
        for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
        {
            _chunks.RefAt(chunkIndex) = new ParallelChunk(chunks.RefAt(chunkIndex));
        }

        _chunkCount = chunks.Length;
        _cachedPlan = plan;
        _cachedPlanVersion = plan.MatchingVersion;
        _cachedRangePlan = null;
        _cachedRangeVersion = -1;
        _cachedRangeWorkerCount = 0;
    }

    private void PrepareRanges(QueryPlan plan, int workerCount)
    {
        if (ReferenceEquals(_cachedRangePlan, plan)
            && _cachedRangeVersion == plan.MatchingVersion
            && _cachedRangeWorkerCount == workerCount)
        {
            return;
        }

        EnsureRangeCapacity(workerCount);
        for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            _ranges.RefAt(workerIndex) = new ParallelRange(
                (int)((long)workerIndex * _chunkCount / workerCount),
                (int)((long)(workerIndex + 1) * _chunkCount / workerCount));
        }

        _cachedRangePlan = plan;
        _cachedRangeVersion = plan.MatchingVersion;
        _cachedRangeWorkerCount = workerCount;
    }

    private void ExecuteRange(int workerIndex, int run, WorkerSlot? workerSlot)
    {
        ParallelRange range = _ranges.RefAt(workerIndex);
        try
        {
            if (_entityMode)
            {
                ExecuteEntityRange(
                    ref _workerInvokers.RefAt(workerIndex),
                    range.StartChunk,
                    range.EndChunk);
                return;
            }

            for (int chunkIndex = range.StartChunk; chunkIndex < range.EndChunk; chunkIndex++)
            {
                ParallelChunk work = _chunks.RefAt(chunkIndex);
                ChunkPlan chunkPlan = work.Chunk;
                GeneratedQuerySlots slots = new(_world!, in chunkPlan);
                _workerInvokers.RefAt(workerIndex).Invoke(ref slots);
            }
        }
        catch (Exception exception)
        {
            _workerFailures.RefAt(workerIndex) = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            if (workerSlot is not null)
            {
                Volatile.Write(ref workerSlot.CompletedRun, run);
            }
        }
    }

    private void ExecuteEntityRange(ref TInvoker invoker, int start, int end)
    {
        World world = _entityWorld!;
        QueryPlan plan = _entityPlan!;
        for (int index = start; index < end; index++)
        {
            Entity entity = _entities[index];
            if (!world.TryResolveEntityLocation(entity, out Chunk chunk, out int slot)
                || !plan.TryGetChunkPlan(chunk.ArchetypeId, chunk.GlobalId, out ChunkPlan chunkPlan))
            {
                continue;
            }

            var slots = new GeneratedQuerySlots(world, in chunkPlan, 1, slot);
            invoker.Invoke(ref slots);
        }
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
