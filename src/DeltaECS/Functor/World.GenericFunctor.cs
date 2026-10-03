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
}
