namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.ComponentModel;

public sealed partial class World
{
    private readonly Dictionary<GenericFunctorKey, List<GenericFunctorEntry>> _genericFunctors = new(GenericFunctorKeyComparer.Instance);
    private GenericFunctorEntry? _lastGenericFunctorEntry;

    private readonly struct GenericFunctorKey(RuntimeTypeHandle functorType, int argumentHash)
    {
        internal readonly RuntimeTypeHandle FunctorType = functorType;
        internal readonly int ArgumentHash = argumentHash;
    }

    private sealed class GenericFunctorKeyComparer : IEqualityComparer<GenericFunctorKey>
    {
        internal static readonly GenericFunctorKeyComparer Instance = new();

        bool IEqualityComparer<GenericFunctorKey>.Equals(GenericFunctorKey left, GenericFunctorKey right)
            => left.FunctorType.Value == right.FunctorType.Value && left.ArgumentHash == right.ArgumentHash;

        int IEqualityComparer<GenericFunctorKey>.GetHashCode(GenericFunctorKey key)
        {
            long handle = key.FunctorType.Value.ToInt64();
            int typeHash = unchecked((int)handle ^ (int)(handle >> 32));
            return unchecked((typeHash * 397) ^ key.ArgumentHash);
        }
    }

    private sealed class GenericFunctorEntry(Type functorType, ComponentId[] arguments, IGeneratedGenericFunctor executor)
    {
        internal readonly Type FunctorType = functorType;
        internal readonly ComponentId[] Arguments = arguments;
        internal readonly IGeneratedGenericFunctor Executor = executor;
    }

    /// <summary>Creates a reusable operation for a runtime-selected generic functor.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EcsOperation CreateGenericFunctorOperation(
        in Query query,
        ReadOnlySpan<Entity> entities,
        bool hasQuery,
        GeneratedGenericFunctorMode mode,
        Type functorType,
        int workerCount,
        ReadOnlySpan<ComponentId> arguments)
    {
        Query operationQuery = query;
        Entity[] operationEntities = entities.ToArray();
        ComponentId[] operationArguments = arguments.ToArray();
        IGeneratedGenericFunctor? executor = null;
        bool operationHasQuery = hasQuery;

        return new EcsOperation(() =>
        {
            ValidateGenericFunctorOperation(in operationQuery, operationHasQuery, functorType);
            executor ??= ResolveGenericFunctor(in operationQuery, operationHasQuery, functorType, operationArguments);
            if (!operationHasQuery)
            {
                operationQuery = executor.CreateQuery(this, operationArguments);
                operationHasQuery = true;
            }

            executor.Execute(this, in operationQuery, operationEntities, operationHasQuery, mode, workerCount, operationArguments);
        });
    }

    /// <summary>Creates a reusable operation for a runtime-selected generic functor with mutable caller-owned context.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EcsOperation<TContext> CreateGenericFunctorOperation<TContext>(
        in Query query,
        ReadOnlySpan<Entity> entities,
        bool hasQuery,
        GeneratedGenericFunctorMode mode,
        Type functorType,
        int workerCount,
        TContext context,
        ReadOnlySpan<ComponentId> arguments)
    {
        Query operationQuery = query;
        Entity[] operationEntities = entities.ToArray();
        ComponentId[] operationArguments = arguments.ToArray();
        IGeneratedGenericFunctor<TContext>? executor = null;
        bool operationHasQuery = hasQuery;

        return new EcsOperation<TContext>(context, (ref TContext operationContext) =>
        {
            ValidateGenericFunctorOperation(in operationQuery, operationHasQuery, functorType);
            if (executor is null)
            {
                IGeneratedGenericFunctor genericExecutor = ResolveGenericFunctor(in operationQuery, operationHasQuery, functorType, operationArguments);
                if (!operationHasQuery)
                {
                    operationQuery = genericExecutor.CreateQuery(this, operationArguments);
                    operationHasQuery = true;
                }

                if (genericExecutor is not IGeneratedGenericFunctor<TContext> typedExecutor)
                {
                    ThrowHelper.ThrowGenericFunctorContextMismatch(functorType, typeof(TContext));
                    return;
                }

                executor = typedExecutor;
            }

            executor!.Execute(
                this,
                in operationQuery,
                operationEntities,
                operationHasQuery,
                mode,
                workerCount,
                ref operationContext,
                operationArguments);
        });
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

        GenericFunctorEntry? lastEntry = _lastGenericFunctorEntry;
        if (lastEntry is not null && lastEntry.FunctorType == functorType && arguments.SequenceEqual(lastEntry.Arguments))
        {
            return lastEntry.Executor;
        }

        var key = new GenericFunctorKey(functorType.TypeHandle, ComponentSet.ComputeHash(arguments));
        if (_genericFunctors.TryGetValue(key, out var entries))
        {
            foreach (var entry in entries)
            {
                if (arguments.SequenceEqual(entry.Arguments))
                {
                    _lastGenericFunctorEntry = entry;
                    return entry.Executor;
                }
            }
        }

        if (!functorType.IsGenericTypeDefinition)
        {
            ThrowHelper.ThrowNotGenericTypeDefinition(functorType);
        }

        IGeneratedGenericFunctor executor;
        if (GeneratedGenericBindingRegistry.TryGetFunctorDispatcher(
            functorType,
            out int dispatcherArity,
            out GeneratedGenericFunctorDispatcher dispatcher))
        {
            if (dispatcherArity != arguments.Length)
            {
                ThrowHelper.ThrowGenericTypeArgumentCountMismatch(functorType, dispatcherArity, arguments.Length);
            }

            IGeneratedComponentTypeToken[] typeTokens = _layouts.GetComponentTypeTokens(arguments);
            executor = dispatcher(typeTokens);
        }
        else
        {
            return ThrowHelper.ThrowMissingGenericFunctor(functorType);
        }
        if (entries is null)
        {
            entries = new List<GenericFunctorEntry>();
            _genericFunctors.Add(key, entries);
        }

        var newEntry = new GenericFunctorEntry(functorType, arguments.ToArray(), executor);
        entries.Add(newEntry);
        _lastGenericFunctorEntry = newEntry;
        return executor;
    }

    private void ValidateGenericFunctorOperation(in Query query, bool hasQuery, Type functorType)
    {
        EnsureExecutionAccess();
        ThrowHelper.ThrowIfNull(functorType, nameof(functorType));
        if (hasQuery && (!query.IsValid || query.Owner != this))
        {
            ThrowHelper.ThrowInvalidEntityQueryHandle();
        }
    }
}
