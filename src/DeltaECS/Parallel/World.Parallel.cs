namespace Delta.ECS;

using System.Threading;

public sealed partial class World
{
    private readonly object _parallelExecutorGate = new();
    private ParallelQueryExecutor? _parallelQueryExecutor;
    private Dictionary<Type, IDisposable>? _generatedParallelExecutors;
    private int _parallelExecutionActive;

    /// <summary>
    /// Executes a callback once for every active matching chunk using reusable workers.
    /// </summary>
    /// <remarks>
    /// The query must have registered every write access before this call. Each matching
    /// chunk is owned by one worker for the duration of the callback, so component rows may
    /// be read and written without locks when the callback does not share mutable state.
    /// Structural changes remain forbidden until the call returns. The first call may create
    /// worker threads and grow reusable buffers; subsequent calls do not allocate for the
    /// same or smaller query topology. The callback must not retain <paramref name="action"/>
    /// data or the supplied chunk after it returns.
    /// </remarks>
    public void ForEachParallel(
        in Query query,
        QueryChunkAction action,
        int workerCount = 0)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnterParallelExecution();
        try
        {
            ParallelQueryExecutor executor = GetParallelQueryExecutor();
            executor.Execute(this, in query, action, workerCount);
        }
        finally
        {
            ExitParallelExecution();
        }
    }

    internal QueryPlan ValidateParallelQuery(in Query query)
    {
        ValidateQuery(in query);
        return query.Cached;
    }

    internal StaticParallelQueryExecutor<TInvoker> GetParallelQueryExecutor<TInvoker>()
        where TInvoker : struct, IGeneratedParallelInvoker
    {
        Type invokerType = typeof(TInvoker);
        Dictionary<Type, IDisposable>? executors = Volatile.Read(ref _generatedParallelExecutors);
        if (executors is not null
            && executors.TryGetValue(invokerType, out IDisposable? cached))
        {
            return (StaticParallelQueryExecutor<TInvoker>)cached;
        }

        lock (_parallelExecutorGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            executors = _generatedParallelExecutors;
            if (executors is not null
                && executors.TryGetValue(invokerType, out IDisposable? existing))
            {
                return (StaticParallelQueryExecutor<TInvoker>)existing;
            }

            var executor = new StaticParallelQueryExecutor<TInvoker>();
            (executors ??= new Dictionary<Type, IDisposable>()).Add(invokerType, executor);
            Volatile.Write(ref _generatedParallelExecutors, executors);
            return executor;
        }
    }

    private ParallelQueryExecutor GetParallelQueryExecutor()
    {
        ParallelQueryExecutor? executor = Volatile.Read(ref _parallelQueryExecutor);
        if (executor is not null)
        {
            return executor;
        }

        lock (_parallelExecutorGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            executor = _parallelQueryExecutor;
            if (executor is not null)
            {
                return executor;
            }

            executor = new ParallelQueryExecutor();
            Volatile.Write(ref _parallelQueryExecutor, executor);
            return executor;
        }
    }

    internal void EnterParallelExecution()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed), this);
        if (Interlocked.CompareExchange(ref _parallelExecutionActive, 1, 0) != 0)
        {
            ThrowHelper.ThrowParallelExecutionActive();
        }
    }

    internal void ExitParallelExecution()
    {
        Volatile.Write(ref _parallelExecutionActive, 0);
    }

    private void DisposeParallelQueryExecutor()
    {
        ParallelQueryExecutor? executor;
        IDisposable[] generatedExecutors;
        lock (_parallelExecutorGate)
        {
            executor = _parallelQueryExecutor;
            _parallelQueryExecutor = null;
            generatedExecutors = _generatedParallelExecutors is { } executors
                ? executors.Values.ToArray()
                : Array.Empty<IDisposable>();
            _generatedParallelExecutors?.Clear();
        }

        executor?.Dispose();
        for (int index = 0; index < generatedExecutors.Length; index++)
        {
            generatedExecutors[index].Dispose();
        }
    }
}
