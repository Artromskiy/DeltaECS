using System.Runtime.CompilerServices;

namespace Delta.ECS;

using System;
using System.ComponentModel;

/// <summary>A deferred operation that can be executed without boxing its concrete value.</summary>
public interface IOperation
{
    /// <summary>Executes this operation.</summary>
    void Invoke();
}

/// <summary>A deferred operation that can also use caller-owned mutable state.</summary>
public interface IOperation<TState> : IOperation
{
    /// <summary>Executes this operation using and updating caller-owned state.</summary>
    void Invoke(ref TState state);
}

/// <summary>A deferred operation that can use caller-owned context and functor state.</summary>
public interface IOperation<TContext, TFunctor> : IOperation
{
    /// <summary>Executes the operation using caller-owned functor state and its owned context.</summary>
    void Invoke(ref TFunctor functor);

    /// <summary>Executes the operation using its owned functor and caller-owned context.</summary>
    void InvokeWithContext(ref TContext context);

    /// <summary>Executes the operation using and updating caller-owned context and functor state.</summary>
    void Invoke(ref TContext context, ref TFunctor functor);
}

/// <summary>Execution contract for an operation with no mutable caller-owned state.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEcsOperationInvoker
{
    /// <summary>Executes the operation.</summary>
    void Invoke();
}

/// <summary>Execution contract for an operation with one mutable state value.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEcsOperationInvoker<TState>
{
    /// <summary>Executes the operation using and updating caller-owned state.</summary>
    void Invoke(ref TState state);
}

/// <summary>Execution contract for an operation with mutable context and functor state.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEcsOperationInvoker<TContext, TFunctor>
{
    /// <summary>Executes the operation using caller-owned context and functor state.</summary>
    void Invoke(ref TContext context, ref TFunctor functor);
}

/// <summary>A reusable deferred operation with a concrete value-type executor.</summary>
/// <remarks>Use <see cref="IOperation"/> to store different operation shapes behind one reference.</remarks>
public struct EcsOperation<TInvoker> : IOperation
    where TInvoker : struct, IEcsOperationInvoker
{
    private TInvoker _invoker;

    /// <summary>Creates a deferred operation from its concrete executor.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EcsOperation(TInvoker invoker) => _invoker = invoker;

    /// <summary>Executes this operation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke() => _invoker.Invoke();
}

/// <summary>A reusable deferred operation with concrete value-type executor and mutable state.</summary>
/// <remarks><see cref="Invoke()"/> updates the operation-owned state. <see cref="Invoke(ref TState)"/> uses caller-owned state.</remarks>
public struct EcsOperation<TState, TInvoker> : IOperation<TState>
    where TInvoker : struct, IEcsOperationInvoker<TState>
{
    private TState _state;
    private TInvoker _invoker;

    /// <summary>Creates a deferred operation with its initial state and concrete executor.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EcsOperation(TState state, TInvoker invoker)
    {
        _state = state;
        _invoker = invoker;
    }

    /// <summary>Executes this operation using and updating its operation-owned state.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke() => _invoker.Invoke(ref _state);

    /// <summary>Executes this operation using and updating caller-owned state.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke(ref TState state) => _invoker.Invoke(ref state);
}

/// <summary>A reusable deferred operation with concrete value-type executor, context and functor state.</summary>
/// <remarks>Overloads use either operation-owned or caller-owned context and functor values.</remarks>
public struct EcsOperation<TContext, TFunctor, TInvoker> : IOperation<TContext, TFunctor>
    where TInvoker : struct, IEcsOperationInvoker<TContext, TFunctor>
{
    private TContext _context;
    private TFunctor _functor;
    private TInvoker _invoker;

    /// <summary>Creates a deferred operation with its initial state and concrete executor.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EcsOperation(TContext context, TFunctor functor, TInvoker invoker)
    {
        _context = context;
        _functor = functor;
        _invoker = invoker;
    }

    /// <summary>Executes the operation using and updating its operation-owned context and functor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke() => _invoker.Invoke(ref _context, ref _functor);

    /// <summary>Executes the operation using its owned context and caller-owned functor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke(ref TFunctor functor) => _invoker.Invoke(ref _context, ref functor);

    /// <summary>Executes the operation using caller-owned context and its owned functor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InvokeWithContext(ref TContext context) => _invoker.Invoke(ref context, ref _functor);

    /// <summary>Executes the operation using and updating caller-owned context and functor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke(ref TContext context, ref TFunctor functor) => _invoker.Invoke(ref context, ref functor);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Invoke() => _invoke();
}
