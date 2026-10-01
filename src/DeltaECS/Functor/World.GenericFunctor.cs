namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel;

public sealed partial class World
{
    private readonly Dictionary<(Type Functor, int Hash), List<GenericFunctorEntry>> _genericFunctors = new();

    private sealed class GenericFunctorEntry(ComponentId[] arguments, IGeneratedGenericFunctor executor)
    {
        internal readonly ComponentId[] Arguments = arguments;
        internal readonly IGeneratedGenericFunctor Executor = executor;
    }

    /// <summary>Executes a generated runtime-selected generic functor over a query or entity list.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void ExecuteGenericFunctor(
        in Query query,
        ReadOnlySpan<Entity> entities,
        bool hasQuery,
        GeneratedGenericFunctorMode mode,
        Type functorType,
        int workerCount,
        ReadOnlySpan<ComponentId> arguments)
    {
        IGeneratedGenericFunctor executor = ResolveGenericFunctor(in query, hasQuery, functorType, arguments);
        executor.Execute(this, in query, entities, hasQuery, mode, workerCount, arguments);
    }

    /// <summary>Executes a generated runtime-selected generic functor with a caller-owned typed context.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void ExecuteGenericFunctor<TContext>(
        in Query query,
        ReadOnlySpan<Entity> entities,
        bool hasQuery,
        GeneratedGenericFunctorMode mode,
        Type functorType,
        int workerCount,
        ref TContext context,
        ReadOnlySpan<ComponentId> arguments)
    {
        IGeneratedGenericFunctor executor = ResolveGenericFunctor(in query, hasQuery, functorType, arguments);
        IGeneratedGenericFunctor<TContext>? contextExecutor = executor as IGeneratedGenericFunctor<TContext>;
        if (contextExecutor is null)
        {
            ThrowHelper.ThrowGenericFunctorContextMismatch(functorType, typeof(TContext));
        }

        contextExecutor.Execute(this, in query, entities, hasQuery, mode, workerCount, ref context, arguments);
    }

    private IGeneratedGenericFunctor ResolveGenericFunctor(
        in Query query,
        bool hasQuery,
        Type functorType,
        ReadOnlySpan<ComponentId> arguments)
    {
        EnsureExecutionAccess();
        ThrowHelper.ThrowIfNull(functorType, nameof(functorType));
        if (hasQuery && (!query.IsValid || query.Owner != this))
        {
            ThrowHelper.ThrowInvalidEntityQueryHandle();
        }

        var key = (functorType, ComponentSet.ComputeHash(arguments));
        if (_genericFunctors.TryGetValue(key, out var entries))
        {
            foreach (var entry in entries)
            {
                if (arguments.SequenceEqual(entry.Arguments))
                {
                    return entry.Executor;
                }
            }
        }

        Type closedFunctor = _layouts.CloseGenericType(functorType, arguments);
        var executor = CreateGenericFunctorExecutor(functorType, closedFunctor);
        if (entries is null)
        {
            entries = new List<GenericFunctorEntry>();
            _genericFunctors.Add(key, entries);
        }

        entries.Add(new GenericFunctorEntry(arguments.ToArray(), executor));
        return executor;
    }

    private static IGeneratedGenericFunctor CreateGenericFunctorExecutor(Type functorType, Type closedFunctor)
    {
        foreach (GeneratedGenericFunctorAttribute factory in functorType.Assembly.GetCustomAttributes<GeneratedGenericFunctorAttribute>())
        {
            if (factory.FunctorType == functorType)
            {
                Type executorType = factory.ExecutorType.MakeGenericType(closedFunctor.GetGenericArguments());
                return (IGeneratedGenericFunctor)Activator.CreateInstance(executorType)!; // Generated executors have a public parameterless constructor.
            }
        }

        return ThrowHelper.ThrowMissingGenericFunctor(functorType);
    }
}
