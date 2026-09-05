namespace Delta.ECS;

using System.Runtime.ExceptionServices;
using System.Threading;

/// <summary>Reusable per-world executor for chunk callbacks with static worker ranges.</summary>
internal sealed class ParallelQueryExecutor : IDisposable
{
    private const int MinimumParallelEntityCount = 250_000;
    private readonly object _lifecycle = new();
    private Worker[] _workers = Array.Empty<Worker>();
    private ExceptionDispatchInfo?[] _workerFailures = Array.Empty<ExceptionDispatchInfo?>();
    private ParallelChunk[] _chunks = Array.Empty<ParallelChunk>();
    private ParallelRange[] _ranges = Array.Empty<ParallelRange>();
    private QueryPlan? _cachedPlan;
    private int _cachedPlanVersion = -1;
    private QueryPlan? _cachedRangePlan;
    private int _cachedRangeVersion = -1;
    private int _cachedRangeWorkerCount;
    private QueryPlan? _activePlan;
    private QueryWriteSession? _activeSession;
    private QueryChunkAction? _activeAction;
    private int _activeGeneration;
    private int _chunkCount;
    private int _entityCount;
    private int _runVersion;
    private int _publishedRun;
    private int _remainingWorkers;
    private int _completedRun;
    private int _stopping;
    private bool _disposed;

    internal void Execute(
        World owner,
        in Query query,
        QueryChunkAction action,
        int requestedWorkerCount)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfNegative(requestedWorkerCount);
        if (Volatile.Read(ref _disposed))
        {
            ThrowHelper.ThrowDisposedWorld();
        }

        QueryPlan plan = owner.ValidateParallelQuery(in query);
        QueryWriteSession session = owner.RentQueryWriteSession(plan, out int generation);
        owner.BeginQueryLease();
        try
        {
            BuildChunkList(plan);
            if (_chunkCount == 0)
            {
                return;
            }

            int workerCount = requestedWorkerCount == 0
                ? Environment.ProcessorCount
                : requestedWorkerCount;
            workerCount = Math.Max(1, Math.Min(workerCount, _chunkCount));
            if (workerCount == 1 || _entityCount < MinimumParallelEntityCount)
            {
                ExecuteSingleThread(plan, session, generation, action);
                return;
            }

            EnsureWorkerCapacity(workerCount);
            PrepareRanges(plan, workerCount);
            ExecuteMultiThread(plan, session, generation, action, workerCount);
        }
        finally
        {
            owner.ReturnQueryWriteSession(session, generation);
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
        _workerFailures = Array.Empty<ExceptionDispatchInfo?>();
        _chunks = Array.Empty<ParallelChunk>();
        _ranges = Array.Empty<ParallelRange>();
    }

    private void ExecuteSingleThread(
        QueryPlan plan,
        QueryWriteSession session,
        int generation,
        QueryChunkAction action)
    {
        for (int chunkIndex = 0; chunkIndex < _chunkCount; chunkIndex++)
        {
            ParallelChunk work = _chunks[chunkIndex];
            action(new QueryChunk(work.Plan, work.Chunk, plan, session, generation));
        }
    }

    private void ExecuteMultiThread(
        QueryPlan plan,
        QueryWriteSession session,
        int generation,
        QueryChunkAction action,
        int workerCount)
    {
        int run = _runVersion == int.MaxValue ? 1 : _runVersion + 1;
        _runVersion = run;
        _activePlan = plan;
        _activeSession = session;
        _activeGeneration = generation;
        _activeAction = action;
        Volatile.Write(ref _remainingWorkers, workerCount);
        for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            _workerFailures[workerIndex] = null;
        }

        Volatile.Write(ref _publishedRun, run);

        while (Volatile.Read(ref _completedRun) != run)
        {
            Thread.SpinWait(8);
        }

        _activePlan = null;
        _activeSession = null;
        _activeAction = null;
        for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            if (_workerFailures[workerIndex] is { } failure)
            {
                failure.Throw();
            }
        }
    }

    private void WorkerLoop(Worker worker)
    {
        int workerIndex = worker.Index;
        int observedRun = 0;
        while (Volatile.Read(ref _stopping) == 0)
        {
            int run;
            while ((run = Volatile.Read(ref _publishedRun)) == observedRun)
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
            ExecuteRange(workerIndex, run);
        }
    }

    private void ExecuteRange(int workerIndex, int run)
    {
        ParallelRange range = _ranges[workerIndex];
        try
        {
            QueryPlan plan = _activePlan!;
            QueryWriteSession session = _activeSession!;
            QueryChunkAction action = _activeAction!;
            for (int chunkIndex = range.StartChunk; chunkIndex < range.EndChunk; chunkIndex++)
            {
                ParallelChunk work = _chunks[chunkIndex];
                action(new QueryChunk(work.Plan, work.Chunk, plan, session, _activeGeneration));
            }
        }
        catch (Exception exception)
        {
            _workerFailures[workerIndex] = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            if (Interlocked.Decrement(ref _remainingWorkers) == 0)
            {
                Volatile.Write(ref _completedRun, run);
            }
        }
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
        int count = 0;
        int entityCount = 0;
        for (int planIndex = 0; planIndex < plans.Length; planIndex++)
        {
            ArchetypePlan archetypePlan = plans[planIndex];
            ReadOnlySpan<ChunkPlan> chunks = archetypePlan.Chunks;
            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                _chunks[count++] = new ParallelChunk(archetypePlan, chunks[chunkIndex]);
                entityCount += chunks[chunkIndex].Chunk.Count;
            }
        }

        _chunkCount = count;
        _entityCount = entityCount;
        _cachedPlan = plan;
        _cachedPlanVersion = plan.MatchingVersion;
        _cachedRangePlan = null;
        _cachedRangeVersion = -1;
        _cachedRangeWorkerCount = 0;
    }

    private void EnsureWorkerCapacity(int requiredBackgroundWorkers)
    {
        if (requiredBackgroundWorkers <= _workers.Length)
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
            if (_workers.Length < requiredBackgroundWorkers)
            {
                Array.Resize(ref _workers, requiredBackgroundWorkers);
            }

            if (_workerFailures.Length < requiredBackgroundWorkers)
            {
                Array.Resize(ref _workerFailures, requiredBackgroundWorkers);
            }

            for (int workerIndex = previousLength; workerIndex < requiredBackgroundWorkers; workerIndex++)
            {
                Worker worker = new(this, workerIndex);
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
            ParallelQueryExecutor owner,
            int index)
        {
            Thread = new Thread(static state => ((Worker)state!).Owner.WorkerLoop((Worker)state!))
            {
                IsBackground = true,
                Name = $"DeltaECS.QueryWorker.{index}"
            };
            Owner = owner;
            Index = index;
        }

        internal ParallelQueryExecutor Owner { get; }
        internal int Index { get; }
        internal Thread Thread { get; }
    }
}
