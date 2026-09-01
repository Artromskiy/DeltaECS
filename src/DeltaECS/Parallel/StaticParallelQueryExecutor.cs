namespace Delta.ECS;

using System.Runtime.ExceptionServices;
using System.Threading;

/// <summary>
/// Persistent executor for a generated invoker with static per-worker ranges.
/// </summary>
internal sealed class StaticParallelQueryExecutor<TInvoker> : IDisposable
    where TInvoker : struct, IGeneratedParallelInvoker
{
    private const int MinimumParallelEntityCount = 32_768;
    private readonly object _lifecycle = new();
    private WorkerSlot[] _workerSlots = Array.Empty<WorkerSlot>();
    private Worker[] _workers = Array.Empty<Worker>();
    private TInvoker[] _workerInvokers = Array.Empty<TInvoker>();
    private ExceptionDispatchInfo?[] _workerFailures = Array.Empty<ExceptionDispatchInfo?>();
    private ParallelChunk[] _chunks = Array.Empty<ParallelChunk>();
    private ParallelRange[] _ranges = Array.Empty<ParallelRange>();
    private QueryPlan? _cachedPlan;
    private int _cachedPlanVersion = -1;
    private QueryPlan? _cachedRangePlan;
    private int _cachedRangeVersion = -1;
    private int _cachedRangeWorkerCount;
    private int _chunkCount;
    private int _entityCount;
    private int _runVersion;
    private int _stopping;
    private bool _disposed;

    internal void Execute(
        QueryPlan plan,
        ref TInvoker invoker,
        int requestedWorkerCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requestedWorkerCount);
        if (Volatile.Read(ref _disposed))
        {
            ThrowHelper.ThrowDisposedWorld();
        }

        BuildChunkList(plan);
        if (_chunkCount == 0)
        {
            return;
        }

        int workerCount = requestedWorkerCount == 0
            ? Environment.ProcessorCount
            : requestedWorkerCount;
        workerCount = Math.Max(1, Math.Min(workerCount, _chunkCount));

        if (invoker.RequiresSingleThread
            || workerCount == 1
            || requestedWorkerCount == 0 && _entityCount < MinimumParallelEntityCount)
        {
            ExecuteSingleThread(ref invoker);
            return;
        }

        EnsureWorkerCapacity(workerCount - 1);
        PrepareRanges(plan, workerCount);

        for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            _workerInvokers[workerIndex] = invoker;
            _workerFailures[workerIndex] = null;
        }

        int run = _runVersion == int.MaxValue ? 1 : _runVersion + 1;
        _runVersion = run;
        for (int workerIndex = 1; workerIndex < workerCount; workerIndex++)
        {
            ParallelRange range = _ranges[workerIndex];
            WorkerSlot slot = _workerSlots[workerIndex];
            slot.StartChunk = range.StartChunk;
            slot.EndChunk = range.EndChunk;
            Volatile.Write(ref slot.PublishedRun, run);
        }

        ExecuteRange(0, run, workerSlot: null);
        for (int workerIndex = 1; workerIndex < workerCount; workerIndex++)
        {
            while (Volatile.Read(ref _workerSlots[workerIndex].CompletedRun) != run)
            {
                Thread.SpinWait(8);
            }
        }

        invoker = _workerInvokers[0];
        for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            if (_workerFailures[workerIndex] is { } failure)
            {
                failure.Throw();
            }
        }
    }

    private void ExecuteSingleThread(ref TInvoker invoker)
    {
        try
        {
            for (int chunkIndex = 0; chunkIndex < _chunkCount; chunkIndex++)
            {
                ParallelChunk work = _chunks[chunkIndex];
                GeneratedQuerySlots slots = new(work.Plan, work.Chunk);
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
            workers[workerIndex].Thread.Join();
        }

        _workers = Array.Empty<Worker>();
        _workerSlots = Array.Empty<WorkerSlot>();
        _workerInvokers = Array.Empty<TInvoker>();
        _workerFailures = Array.Empty<ExceptionDispatchInfo?>();
        _chunks = Array.Empty<ParallelChunk>();
        _ranges = Array.Empty<ParallelRange>();
    }

    private void BuildChunkList(QueryPlan plan)
    {
        if (ReferenceEquals(_cachedPlan, plan) && _cachedPlanVersion == plan.MatchingVersion)
        {
            return;
        }

        ReadOnlySpan<ArchetypePlan> plans = plan.MatchingPlans();
        int required = 0;
        for (int planIndex = 0; planIndex < plans.Length; planIndex++)
        {
            required = checked(required + plans[planIndex].ChunkCount);
        }

        EnsureChunkCapacity(required);
        int chunkCount = 0;
        int entityCount = 0;
        for (int planIndex = 0; planIndex < plans.Length; planIndex++)
        {
            ArchetypePlan archetypePlan = plans[planIndex];
            ReadOnlySpan<ChunkPlan> chunks = archetypePlan.Chunks;
            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                _chunks[chunkCount++] = new ParallelChunk(archetypePlan, chunks[chunkIndex]);
                entityCount += chunks[chunkIndex].Chunk.Count;
            }
        }

        _chunkCount = chunkCount;
        _entityCount = entityCount;
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
            _ranges[workerIndex] = new ParallelRange(
                (int)((long)workerIndex * _chunkCount / workerCount),
                (int)((long)(workerIndex + 1) * _chunkCount / workerCount));
        }

        _cachedRangePlan = plan;
        _cachedRangeVersion = plan.MatchingVersion;
        _cachedRangeWorkerCount = workerCount;
    }

    private void ExecuteRange(int workerIndex, int run, WorkerSlot? workerSlot)
    {
        ParallelRange range = _ranges[workerIndex];
        try
        {
            for (int chunkIndex = range.StartChunk; chunkIndex < range.EndChunk; chunkIndex++)
            {
                ParallelChunk work = _chunks[chunkIndex];
                GeneratedQuerySlots slots = new(work.Plan, work.Chunk);
                _workerInvokers[workerIndex].Invoke(ref slots);
            }
        }
        catch (Exception exception)
        {
            _workerFailures[workerIndex] = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            if (workerSlot is not null)
            {
                Volatile.Write(ref workerSlot.CompletedRun, run);
            }
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

                Thread.SpinWait(8);
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
                    _workerSlots[workerIndex] = new WorkerSlot();
                }
            }

            if (_workers.Length < requiredBackgroundWorkers)
            {
                Array.Resize(ref _workers, requiredBackgroundWorkers);
            }

            for (int workerIndex = previousLength; workerIndex < requiredBackgroundWorkers; workerIndex++)
            {
                Worker worker = new(this, workerIndex + 1, _workerSlots[workerIndex + 1]);
                _workers[workerIndex] = worker;
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

    private void EnsureRangeCapacity(int required)
    {
        if (required <= _ranges.Length)
        {
            return;
        }

        Array.Resize(ref _ranges, required);
    }

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential,
        Size = 64)]
    private sealed class WorkerSlot
    {
        internal int PublishedRun;

        internal int CompletedRun;

        internal int StartChunk;

        internal int EndChunk;
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
        internal ParallelChunk(ArchetypePlan plan, ChunkPlan chunk)
        {
            Plan = plan;
            Chunk = chunk;
        }

        internal ArchetypePlan Plan { get; }
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
