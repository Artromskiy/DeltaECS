namespace Delta.ECS;

using System;
using System.ComponentModel;

/// <summary>Runs a deferred ECS operation using caller-owned mutable state.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate void EcsOperationAction<TState>(ref TState state);

/// <summary>Runs a deferred ECS operation using caller-owned context and functor state.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate void EcsOperationAction<TContext, TFunctor>(ref TContext context, ref TFunctor functor);

/// <summary>A reusable, deferred ECS operation with no mutable caller-owned state.</summary>
/// <remarks>The operation holds its inputs until collected. Each invocation performs normal world and query validation.</remarks>
public sealed class EcsOperation
{
    private readonly Action _invoke;

    /// <summary>Creates a deferred operation from its execution action.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EcsOperation(Action invoke)
    {
        ThrowHelper.ThrowIfNull(invoke, nameof(invoke));
        _invoke = invoke;
    }

    /// <summary>Executes this operation.</summary>
    public void Invoke() => _invoke();
}

/// <summary>A reusable, deferred ECS operation that owns a copy of mutable state.</summary>
/// <remarks><see cref="Invoke()"/> updates the operation-owned value. <see cref="Invoke(ref TState)"/> uses caller-owned state instead.</remarks>
public sealed class EcsOperation<TState>
{
    private TState _state;
    private readonly EcsOperationAction<TState> _invoke;

    /// <summary>Creates a deferred operation with its initial state and execution action.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EcsOperation(TState state, EcsOperationAction<TState> invoke)
    {
        ThrowHelper.ThrowIfNull(invoke, nameof(invoke));
        _state = state;
        _invoke = invoke;
    }

    /// <summary>Executes this operation using and updating its owned state.</summary>
    public void Invoke() => _invoke(ref _state);

    /// <summary>Executes this operation using and updating caller-owned state.</summary>
    public void Invoke(ref TState state) => _invoke(ref state);
}

/// <summary>A reusable, deferred ECS operation that owns copies of mutable context and functor state.</summary>
/// <remarks>Each overload can use the operation-owned values or caller-owned values without retaining references between invocations.</remarks>
public sealed class EcsOperation<TContext, TFunctor>
{
    private TContext _context;
    private TFunctor _functor;
    private readonly EcsOperationAction<TContext, TFunctor> _invoke;

    /// <summary>Creates a deferred operation with its initial state and execution action.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EcsOperation(TContext context, TFunctor functor, EcsOperationAction<TContext, TFunctor> invoke)
    {
        ThrowHelper.ThrowIfNull(invoke, nameof(invoke));
        _context = context;
        _functor = functor;
        _invoke = invoke;
    }

    /// <summary>Executes the operation using and updating its owned context and functor.</summary>
    public void Invoke() => _invoke(ref _context, ref _functor);

    /// <summary>Executes the operation using its owned context and caller-owned functor.</summary>
    public void Invoke(ref TFunctor functor) => _invoke(ref _context, ref functor);

    /// <summary>Executes the operation using caller-owned context and its owned functor.</summary>
    public void InvokeWithContext(ref TContext context) => _invoke(ref context, ref _functor);

    /// <summary>Executes the operation using and updating caller-owned context and functor.</summary>
    public void Invoke(ref TContext context, ref TFunctor functor) => _invoke(ref context, ref functor);
}

/// <summary>A reusable, deferred ECS operation that produces a result when invoked.</summary>
public sealed class EcsResultOperation<TResult>
{
    private readonly Func<TResult> _invoke;

    /// <summary>Creates a deferred result operation from its execution function.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EcsResultOperation(Func<TResult> invoke)
    {
        ThrowHelper.ThrowIfNull(invoke, nameof(invoke));
        _invoke = invoke;
    }

    /// <summary>Executes this operation and returns its result.</summary>
    public TResult Invoke() => _invoke();
}
