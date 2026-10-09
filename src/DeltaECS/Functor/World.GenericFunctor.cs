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

    /// <summary>Direct executor for a runtime-selected generic functor operation.</summary>
    [System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]
    public struct GenericFunctorOperationInvoker : IEcsOperationInvoker
    {
        private readonly World _world;
        private Query _query;
        private readonly Entity[] _entities;
        private bool _hasQuery;
        private readonly GeneratedGenericFunctorMode _mode;
        private readonly Type _functorType;
        private readonly int _workerCount;
        private readonly ComponentId[] _arguments;
        private IGeneratedGenericFunctor? _executor;

        internal GenericFunctorOperationInvoker(
            World world,
            in Query query,
            Entity[] entities,
            bool hasQuery,
            GeneratedGenericFunctorMode mode,
            Type functorType,
            int workerCount,
            ComponentId[] arguments)
        {
            _world = world;
            _query = query;
            _entities = entities;
            _hasQuery = hasQuery;
            _mode = mode;
            _functorType = functorType;
            _workerCount = workerCount;
            _arguments = arguments;
            _executor = null;
        }

        /// <summary>Resolves the typed executor once and runs the operation.</summary>
        public void Invoke()
        {
            _world.ValidateGenericFunctorOperation(in _query, _hasQuery, _functorType);
            _executor ??= _world.ResolveGenericFunctor(in _query, _hasQuery, _functorType, _arguments);
            if (!_hasQuery)
            {
                _query = _executor.CreateQuery(_world, _arguments);
                _hasQuery = true;
            }

            _executor.Execute(_world, in _query, _entities, _hasQuery, _mode, _workerCount, _arguments);
        }
    }

    /// <summary>Direct executor for a runtime-selected generic functor with mutable context.</summary>
    [System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]
    public struct GenericFunctorContextOperationInvoker<TContext> : IEcsOperationInvoker<TContext>
    {
        private readonly World _world;
        private Query _query;
        private readonly Entity[] _entities;
        private bool _hasQuery;
        private readonly GeneratedGenericFunctorMode _mode;
        private readonly Type _functorType;
        private readonly int _workerCount;
        private readonly ComponentId[] _arguments;
        private IGeneratedGenericFunctor<TContext>? _executor;

        internal GenericFunctorContextOperationInvoker(
            World world,
            in Query query,
            Entity[] entities,
            bool hasQuery,
            GeneratedGenericFunctorMode mode,
            Type functorType,
            int workerCount,
            ComponentId[] arguments)
        {
            _world = world;
            _query = query;
            _entities = entities;
            _hasQuery = hasQuery;
            _mode = mode;
            _functorType = functorType;
            _workerCount = workerCount;
            _arguments = arguments;
            _executor = null;
        }

        /// <summary>Resolves the typed executor once and runs the operation.</summary>
        public void Invoke(ref TContext context)
        {
            _world.ValidateGenericFunctorOperation(in _query, _hasQuery, _functorType);
            if (_executor is null)
            {
                IGeneratedGenericFunctor executor = _world.ResolveGenericFunctor(in _query, _hasQuery, _functorType, _arguments);
                if (!_hasQuery)
                {
                    _query = executor.CreateQuery(_world, _arguments);
                    _hasQuery = true;
                }

                if (executor is not IGeneratedGenericFunctor<TContext> typedExecutor)
                {
                    ThrowHelper.ThrowGenericFunctorContextMismatch(_functorType, typeof(TContext));
                    return;
                }

                _executor = typedExecutor;
            }

            _executor.Execute(_world, in _query, _entities, _hasQuery, _mode, _workerCount, ref context, _arguments);
        }
    }

    /// <summary>Creates a reusable operation for a runtime-selected generic functor.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EcsOperation<GenericFunctorOperationInvoker> CreateGenericFunctorOperation(
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
        return new EcsOperation<GenericFunctorOperationInvoker>(
            new GenericFunctorOperationInvoker(this, in operationQuery, operationEntities, hasQuery, mode, functorType, workerCount, operationArguments));
    }

    /// <summary>Creates a reusable operation for a runtime-selected generic functor with mutable caller-owned context.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EcsOperation<TContext, GenericFunctorContextOperationInvoker<TContext>> CreateGenericFunctorOperation<TContext>(
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
        return new EcsOperation<TContext, GenericFunctorContextOperationInvoker<TContext>>(
            context,
            new GenericFunctorContextOperationInvoker<TContext>(
                this,
                in operationQuery,
                operationEntities,
                hasQuery,
                mode,
                functorType,
                workerCount,
                operationArguments));
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
