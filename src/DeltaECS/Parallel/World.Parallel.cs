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
    /// same or smaller query topology. A non-empty query always uses the worker protocol
    /// regardless of its entity count; <paramref name="workerCount"/> equal to one explicitly
    /// selects a single worker. Requested worker counts are clamped to the available processor
    /// count. The callback must not retain <paramref name="action"/> data or the supplied chunk
    /// after it returns.
    /// Generated typed overloads are named <c>ForEachParallel</c> and
    /// <c>ForEachEntityParallel</c>. Their optional state parameter is
    /// <c>in</c>, <c>ref readonly</c>, or by value; a parallel <c>ref</c> state
    /// overload is intentionally not provided. Functor calls use the same
    /// generated names and bypass interception.
    /// </remarks>
    public void ForEachParallel(
        in Query query,
        QueryChunkAction action,
        int workerCount = 0)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
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

    /// <summary>Executes a generated callback without component parameters in parallel.</summary>
    public void ForEachParallel(in Query query, ForEachAction action, int workerCount = 0)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        var invoker = new ParallelActionInvoker(action);
        GeneratedForEachRuntime.ExecuteParallelDense(this, in query, ref invoker, ReadOnlySpan<int>.Empty, workerCount);
    }

    /// <summary>Executes an entity callback without component parameters in parallel.</summary>
    public void ForEachEntityParallel(in Query query, ForEachEntityAction action, int workerCount = 0)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        var invoker = new ParallelEntityActionInvoker(action);
        GeneratedForEachRuntime.ExecuteParallelDense(this, in query, ref invoker, ReadOnlySpan<int>.Empty, workerCount);
    }

    /// <summary>Executes a generated callback with read-only context and no component parameters.</summary>
    public void ForEachParallel<TContext>(
        in Query query,
        in TContext context,
        ForEachContextAction_In<TContext> action,
        int workerCount = 0)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        var invoker = new ParallelContextActionInvoker<TContext>(context, action);
        GeneratedForEachRuntime.ExecuteParallelDense(this, in query, ref invoker, ReadOnlySpan<int>.Empty, workerCount);
    }

    /// <summary>Executes a generated callback with value context and no component parameters.</summary>
    public void ForEachParallel<TContext>(
        in Query query,
        TContext context,
        ForEachContextAction_Value<TContext> action,
        int workerCount = 0)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        var invoker = new ParallelValueContextActionInvoker<TContext>(context, action);
        GeneratedForEachRuntime.ExecuteParallelDense(this, in query, ref invoker, ReadOnlySpan<int>.Empty, workerCount);
    }

    /// <summary>Executes an entity callback with read-only context and no component parameters.</summary>
    public void ForEachEntityParallel<TContext>(
        in Query query,
        in TContext context,
        ForEachContextEntityAction_In<TContext> action,
        int workerCount = 0)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        var invoker = new ParallelContextEntityActionInvoker<TContext>(context, action);
        GeneratedForEachRuntime.ExecuteParallelDense(this, in query, ref invoker, ReadOnlySpan<int>.Empty, workerCount);
    }

    /// <summary>Executes an entity callback with value context and no component parameters.</summary>
    public void ForEachEntityParallel<TContext>(
        in Query query,
        TContext context,
        ForEachContextEntityAction_Value<TContext> action,
        int workerCount = 0)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        var invoker = new ParallelValueContextEntityActionInvoker<TContext>(context, action);
        GeneratedForEachRuntime.ExecuteParallelDense(this, in query, ref invoker, ReadOnlySpan<int>.Empty, workerCount);
    }

    /// <summary>Compatibility alias for the historical entity-suffix spelling.</summary>
    public void ForEachParallelEntity(in Query query, ForEachEntityAction action, int workerCount = 0) =>
        ForEachEntityParallel(in query, action, workerCount);

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
            ThrowHelper.ThrowIfDisposed(_disposed, this);
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
            ThrowHelper.ThrowIfDisposed(_disposed, this);
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
        ThrowHelper.ThrowIfDisposed(Volatile.Read(ref _disposed), this);
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
            generatedExecutors.RefAt(index).Dispose();
        }
    }

    private readonly struct ParallelActionInvoker : IGeneratedParallelInvoker
    {
        private readonly ForEachAction _action;

        internal ParallelActionInvoker(ForEachAction action) => _action = action;

        public bool RequiresSingleThread => false;

        public void Invoke(ref GeneratedQuerySlots slots)
        {
            int count = slots.Count;
            for (int index = 0; index < count; index++)
            {
                _action();
            }
        }
    }

    private readonly struct ParallelEntityActionInvoker : IGeneratedParallelInvoker
    {
        private readonly ForEachEntityAction _action;

        internal ParallelEntityActionInvoker(ForEachEntityAction action) => _action = action;

        public bool RequiresSingleThread => false;

        public void Invoke(ref GeneratedQuerySlots slots)
        {
            int count = slots.Count;
            for (int index = 0; index < count; index++)
            {
                _action(slots.EntityAt(index));
            }
        }
    }

    private readonly struct ParallelContextActionInvoker<TContext> : IGeneratedParallelInvoker
    {
        private readonly TContext _context;
        private readonly ForEachContextAction_In<TContext> _action;

        internal ParallelContextActionInvoker(TContext context, ForEachContextAction_In<TContext> action)
        {
            _context = context;
            _action = action;
        }

        public bool RequiresSingleThread => false;

        public void Invoke(ref GeneratedQuerySlots slots)
        {
            int count = slots.Count;
            for (int index = 0; index < count; index++)
            {
                _action(in _context);
            }
        }
    }

    private readonly struct ParallelValueContextActionInvoker<TContext> : IGeneratedParallelInvoker
    {
        private readonly TContext _context;
        private readonly ForEachContextAction_Value<TContext> _action;

        internal ParallelValueContextActionInvoker(TContext context, ForEachContextAction_Value<TContext> action)
        {
            _context = context;
            _action = action;
        }

        public bool RequiresSingleThread => false;

        public void Invoke(ref GeneratedQuerySlots slots)
        {
            int count = slots.Count;
            for (int index = 0; index < count; index++)
            {
                _action(_context);
            }
        }
    }

    private readonly struct ParallelContextEntityActionInvoker<TContext> : IGeneratedParallelInvoker
    {
        private readonly TContext _context;
        private readonly ForEachContextEntityAction_In<TContext> _action;

        internal ParallelContextEntityActionInvoker(TContext context, ForEachContextEntityAction_In<TContext> action)
        {
            _context = context;
            _action = action;
        }

        public bool RequiresSingleThread => false;

        public void Invoke(ref GeneratedQuerySlots slots)
        {
            int count = slots.Count;
            for (int index = 0; index < count; index++)
            {
                _action(in _context, slots.EntityAt(index));
            }
        }
    }

    private readonly struct ParallelValueContextEntityActionInvoker<TContext> : IGeneratedParallelInvoker
    {
        private readonly TContext _context;
        private readonly ForEachContextEntityAction_Value<TContext> _action;

        internal ParallelValueContextEntityActionInvoker(TContext context, ForEachContextEntityAction_Value<TContext> action)
        {
            _context = context;
            _action = action;
        }

        public bool RequiresSingleThread => false;

        public void Invoke(ref GeneratedQuerySlots slots)
        {
            int count = slots.Count;
            for (int index = 0; index < count; index++)
            {
                _action(_context, slots.EntityAt(index));
            }
        }
    }
}
