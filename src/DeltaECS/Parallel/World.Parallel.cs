namespace Delta.ECS;

using System.Threading;

public sealed partial class World
{
    private readonly object _parallelExecutorGate = new();
    private Dictionary<Type, IDisposable>? _generatedParallelExecutors;
    private int _parallelExecutionActive;

    private readonly struct EntityParallelInvoker : IGeneratedParallelInvoker
    {
        private readonly ForEachEntityAction _action;

        internal EntityParallelInvoker(ForEachEntityAction action) => _action = action;

        public bool RequiresSingleThread => false;

        public void Invoke(ref GeneratedQuerySlots slots)
        {
            int count = slots.Count;
            ref Entity firstEntity = ref slots.GetGeneratedEntityReference();
            for (int index = 0; index < count; index++)
            {
                _action(global::System.Runtime.CompilerServices.Unsafe.Add(ref firstEntity, index));
            }
        }
    }

    private readonly struct EntityParallelContextInvoker<TContext> : IGeneratedParallelInvoker
    {
        private readonly TContext _context;
        private readonly ForEachContextEntityActionIn<TContext> _action;

        internal EntityParallelContextInvoker(in TContext context, ForEachContextEntityActionIn<TContext> action)
        {
            _context = context;
            _action = action;
        }

        public bool RequiresSingleThread => false;

        public void Invoke(ref GeneratedQuerySlots slots)
        {
            int count = slots.Count;
            ref Entity firstEntity = ref slots.GetGeneratedEntityReference();
            for (int index = 0; index < count; index++)
            {
                _action(in _context, global::System.Runtime.CompilerServices.Unsafe.Add(ref firstEntity, index));
            }
        }
    }

    private readonly struct EntityParallelValueContextInvoker<TContext> : IGeneratedParallelInvoker
    {
        private readonly TContext _context;
        private readonly ForEachContextEntityActionValue<TContext> _action;

        internal EntityParallelValueContextInvoker(TContext context, ForEachContextEntityActionValue<TContext> action)
        {
            _context = context;
            _action = action;
        }

        public bool RequiresSingleThread => false;

        public void Invoke(ref GeneratedQuerySlots slots)
        {
            int count = slots.Count;
            ref Entity firstEntity = ref slots.GetGeneratedEntityReference();
            for (int index = 0; index < count; index++)
            {
                _action(_context, global::System.Runtime.CompilerServices.Unsafe.Add(ref firstEntity, index));
            }
        }
    }

    /// <summary>
    /// Zero-component parallel callback overload.
    /// Use a component-bearing generated <c>ForEachParallel</c> callback with
    /// one or more component parameters. Generated forms can target the
    /// query or an explicit entity span, with optional <c>ComponentId</c>
    /// selectors, context and worker count.
    /// For example: <c>world.ForEachParallel(in query,
    /// static (ref Position position) =&gt; position.X++, workerCount: 4)</c>.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    /// <exception cref="System.InvalidOperationException">The generated component-bearing overload was not selected.</exception>
    public void ForEachParallel(in Query query, ForEachAction action, int workerCount = 0)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>
    /// Iterates every entity selected by <paramref name="query"/> in parallel
    /// without requesting component rows. Generated forms may also include
    /// component rows, explicit <c>ComponentId</c> selectors, context, or an
    /// explicit entity-span target. For example:
    /// <c>world.ForEachEntityParallel(in query, static entity =&gt; Log(entity), workerCount: 4)</c>.
    /// </summary>
    public void ForEachEntityParallel(in Query query, ForEachEntityAction action, int workerCount = 0)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        var invoker = new EntityParallelInvoker(action);
        GeneratedForEachRuntime.ExecuteParallelDense(
            this,
            in query,
            ref invoker,
            ReadOnlySpan<int>.Empty,
            workerCount);
    }

    /// <summary>
    /// Zero-component parallel context callback overload.
    /// Use a component-bearing generated <c>ForEachParallel</c> callback with
    /// <c>in</c>, <c>ref readonly</c>, or value context, one or more component
    /// parameters, and a worker count. The target can be the query or an explicit
    /// entity span, with optional <c>ComponentId</c> selectors.
    /// For example: <c>world.ForEachParallel(in query, in settings,
    /// static (in Settings value, ref Position position) =&gt; position.X += value.Step,
    /// workerCount: 4)</c>.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    /// <exception cref="System.InvalidOperationException">The generated component-bearing overload was not selected.</exception>
    public void ForEachParallel<TContext>(
        in Query query,
        in TContext context,
        ForEachContextActionIn<TContext> action,
        int workerCount = 0)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>
    /// Zero-component parallel value-context callback overload.
    /// Use a component-bearing generated <c>ForEachParallel</c> callback with one
    /// or more component parameters and a worker count. The target can be the
    /// query or an explicit entity span, with optional <c>ComponentId</c> selectors.
    /// For example: <c>world.ForEachParallel(in query, settings,
    /// static (Settings value, ref Position position) =&gt; position.X += value.Step,
    /// workerCount: 4)</c>.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    /// <exception cref="System.InvalidOperationException">The generated component-bearing overload was not selected.</exception>
    public void ForEachParallel<TContext>(
        in Query query,
        TContext context,
        ForEachContextActionValue<TContext> action,
        int workerCount = 0)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>
    /// Iterates every entity selected by <paramref name="query"/> in parallel
    /// with read-only caller context and without requesting component rows.
    /// For example: <c>world.ForEachEntityParallel(in query, in state,
    /// static (in State value, Entity entity) =&gt; Log(value, entity), workerCount: 4)</c>.
    /// </summary>
    public void ForEachEntityParallel<TContext>(
        in Query query,
        in TContext context,
        ForEachContextEntityActionIn<TContext> action,
        int workerCount = 0)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        var invoker = new EntityParallelContextInvoker<TContext>(in context, action);
        GeneratedForEachRuntime.ExecuteParallelDense(
            this,
            in query,
            ref invoker,
            ReadOnlySpan<int>.Empty,
            workerCount);
    }

    /// <summary>
    /// Iterates every entity selected by <paramref name="query"/> in parallel
    /// with value context and without requesting component rows. For example:
    /// <c>world.ForEachEntityParallel(in query, state,
    /// static (State value, Entity entity) =&gt; Log(value, entity), workerCount: 4)</c>.
    /// </summary>
    public void ForEachEntityParallel<TContext>(
        in Query query,
        TContext context,
        ForEachContextEntityActionValue<TContext> action,
        int workerCount = 0)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        var invoker = new EntityParallelValueContextInvoker<TContext>(context, action);
        GeneratedForEachRuntime.ExecuteParallelDense(
            this,
            in query,
            ref invoker,
            ReadOnlySpan<int>.Empty,
            workerCount);
    }

    /// <summary>
    /// Zero-component parallel stamp callback anchor. Use a generated
    /// <c>ForEachStampParallel</c> form with one or more <c>in Stamp</c>
    /// parameters and an explicit worker count.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    public void ForEachStampParallel(in Query query, ForEachAction action, int workerCount = 0)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>
    /// Zero-component parallel entity stamp callback anchor. Generated
    /// <c>ForEachEntityStampParallel</c> callbacks receive <c>Entity</c>
    /// followed by one or more <c>in Stamp</c> parameters.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    public void ForEachEntityStampParallel(in Query query, ForEachEntityAction action, int workerCount = 0)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>
    /// Zero-component parallel stamp context anchor. Generated forms support
    /// read-only or value context and one or more <c>in Stamp</c> parameters.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    public void ForEachStampParallel<TContext>(
        in Query query,
        in TContext context,
        ForEachContextActionIn<TContext> action,
        int workerCount = 0)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>Zero-component parallel value-context stamp anchor.</summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    public void ForEachStampParallel<TContext>(
        in Query query,
        TContext context,
        ForEachContextActionValue<TContext> action,
        int workerCount = 0)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>Zero-component parallel entity stamp context anchor.</summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    public void ForEachEntityStampParallel<TContext>(
        in Query query,
        in TContext context,
        ForEachContextEntityActionIn<TContext> action,
        int workerCount = 0)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>Zero-component parallel entity value-context stamp anchor.</summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    public void ForEachEntityStampParallel<TContext>(
        in Query query,
        TContext context,
        ForEachContextEntityActionValue<TContext> action,
        int workerCount = 0)
        => ThrowHelper.ThrowGeneratedIterationRequired();

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

    private void DisposeGeneratedParallelExecutors()
    {
        IDisposable[] generatedExecutors;
        lock (_parallelExecutorGate)
        {
            generatedExecutors = _generatedParallelExecutors is { } executors
                ? executors.Values.ToArray()
                : Array.Empty<IDisposable>();
            _generatedParallelExecutors?.Clear();
        }

        for (int index = 0; index < generatedExecutors.Length; index++)
        {
            generatedExecutors.RefAt(index).Dispose();
        }
    }

}
