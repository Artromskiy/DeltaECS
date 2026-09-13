namespace Delta.ECS;

using System.Threading;

public sealed partial class World
{
    private readonly object _parallelExecutorGate = new();
    private Dictionary<Type, IDisposable>? _generatedParallelExecutors;
    private int _parallelExecutionActive;

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
    /// Zero-component parallel entity callback overload.
    /// Use a component-bearing generated <c>ForEachEntityParallel</c> callback;
    /// it puts <c>Entity</c> first and supports one or more component parameters.
    /// Generated forms can target the query or an explicit entity span, with
    /// optional <c>ComponentId</c> selectors, context and worker count.
    /// For example: <c>world.ForEachEntityParallel(in query,
    /// static (Entity entity, ref Position position) =&gt; Log(entity), workerCount: 4)</c>.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    /// <exception cref="System.InvalidOperationException">The generated component-bearing overload was not selected.</exception>
    public void ForEachEntityParallel(in Query query, ForEachEntityAction action, int workerCount = 0)
        => ThrowHelper.ThrowGeneratedIterationRequired();

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
        ForEachContextAction_In<TContext> action,
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
        ForEachContextAction_Value<TContext> action,
        int workerCount = 0)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>
    /// Zero-component parallel entity context callback overload.
    /// Use a component-bearing generated <c>ForEachEntityParallel</c> callback;
    /// it places <c>Entity</c> before component parameters, after any context,
    /// and accepts <c>in</c>, <c>ref readonly</c>, or value context plus a worker count.
    /// The target can be the query or an explicit entity span, with optional
    /// <c>ComponentId</c> selectors.
    /// For example: <c>world.ForEachEntityParallel(in query, in settings,
    /// static (in Settings value, Entity entity, ref Position position) =&gt;
    /// position.X += value.Step + entity.Index, workerCount: 4)</c>.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    /// <exception cref="System.InvalidOperationException">The generated component-bearing overload was not selected.</exception>
    public void ForEachEntityParallel<TContext>(
        in Query query,
        in TContext context,
        ForEachContextEntityAction_In<TContext> action,
        int workerCount = 0)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>
    /// Zero-component parallel entity value-context callback overload.
    /// Use a component-bearing generated <c>ForEachEntityParallel</c> callback;
    /// it places <c>Entity</c> before component parameters, after the context,
    /// and accepts value context plus a worker count. The target can be the query
    /// or an explicit entity span, with optional <c>ComponentId</c> selectors.
    /// For example: <c>world.ForEachEntityParallel(in query, settings,
    /// static (Settings value, Entity entity, ref Position position) =&gt;
    /// position.X += value.Step + entity.Index, workerCount: 4)</c>.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    /// <exception cref="System.InvalidOperationException">The generated component-bearing overload was not selected.</exception>
    public void ForEachEntityParallel<TContext>(
        in Query query,
        TContext context,
        ForEachContextEntityAction_Value<TContext> action,
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
