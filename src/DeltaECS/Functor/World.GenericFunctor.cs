namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public sealed partial class World
{
    private readonly Dictionary<GenericFunctorKey, List<GenericFunctorEntry>> _genericFunctors = new(GenericFunctorKeyComparer.Instance);
    private GenericFunctorEntry? _lastGenericFunctorEntry;
    // Copyable operation structs cannot own disposable buffers; World keeps their snapshots until disposal.
    private nint _genericFunctorInputBlocks;

    private struct GenericFunctorInputBlock
    {
        internal nint Next;
    }

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
            int typeHash = (int)handle ^ (int)(handle >> 32);
            return (typeHash * 397) ^ key.ArgumentHash;
        }
    }

    private sealed class GenericFunctorEntry(Type functorType, ComponentId[] arguments, IGeneratedGenericFunctor executor)
    {
        internal readonly Type FunctorType = functorType;
        internal readonly ComponentId[] Arguments = arguments;
        internal readonly IGeneratedGenericFunctor Executor = executor;
    }

    internal readonly struct GenericFunctorInputs
    {
        private readonly nint _entities;
        private readonly nint _arguments;
        private readonly int _entityCount;
        private readonly int _argumentCount;

        internal GenericFunctorInputs(nint entities, int entityCount, nint arguments, int argumentCount)
        {
            _entities = entities;
            _entityCount = entityCount;
            _arguments = arguments;
            _argumentCount = argumentCount;
        }

        internal ReadOnlySpan<Entity> Entities => ArrayAccess.AsReadOnlySpan<Entity>(_entities, _entityCount);
        internal ReadOnlySpan<ComponentId> Arguments => ArrayAccess.AsReadOnlySpan<ComponentId>(_arguments, _argumentCount);
    }

    /// <summary>Direct executor for a runtime-selected generic functor operation.</summary>
    [System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]
    public struct GenericFunctorOperationInvoker : IEcsOperationInvoker
    {
        private readonly World _world;
        private Query _query;
        private readonly GenericFunctorInputs _inputs;
        private bool _hasQuery;
        private readonly GeneratedGenericFunctorMode _mode;
        private readonly Type _functorType;
        private readonly int _workerCount;
        private IGeneratedGenericFunctor? _executor;

        internal GenericFunctorOperationInvoker(
            World world,
            in Query query,
            GenericFunctorInputs inputs,
            bool hasQuery,
            GeneratedGenericFunctorMode mode,
            Type functorType,
            int workerCount)
        {
            _world = world;
            _query = query;
            _inputs = inputs;
            _hasQuery = hasQuery;
            _mode = mode;
            _functorType = functorType;
            _workerCount = workerCount;
            _executor = null;
        }

        /// <summary>Resolves the typed executor once and runs the operation.</summary>
        public void Invoke()
        {
            _world.ValidateGenericFunctorOperation(in _query, _hasQuery, _functorType);
            _executor ??= _world.ResolveGenericFunctor(in _query, _hasQuery, _functorType, _inputs.Arguments);
            if (!_hasQuery)
            {
                _query = _executor.CreateQuery(_world, _inputs.Arguments);
                _hasQuery = true;
            }

            _executor.Execute(_world, in _query, _inputs.Entities, _hasQuery, _mode, _workerCount, _inputs.Arguments);
        }
    }

    /// <summary>Direct executor for a runtime-selected generic functor with mutable context.</summary>
    [System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]
    public struct GenericFunctorContextOperationInvoker<TContext> : IEcsOperationInvoker<TContext>
    {
        private readonly World _world;
        private Query _query;
        private readonly GenericFunctorInputs _inputs;
        private bool _hasQuery;
        private readonly GeneratedGenericFunctorMode _mode;
        private readonly Type _functorType;
        private readonly int _workerCount;
        private IGeneratedGenericFunctor<TContext>? _executor;

        internal GenericFunctorContextOperationInvoker(
            World world,
            in Query query,
            GenericFunctorInputs inputs,
            bool hasQuery,
            GeneratedGenericFunctorMode mode,
            Type functorType,
            int workerCount)
        {
            _world = world;
            _query = query;
            _inputs = inputs;
            _hasQuery = hasQuery;
            _mode = mode;
            _functorType = functorType;
            _workerCount = workerCount;
            _executor = null;
        }

        /// <summary>Resolves the typed executor once and runs the operation.</summary>
        public void Invoke(ref TContext context)
        {
            _world.ValidateGenericFunctorOperation(in _query, _hasQuery, _functorType);
            if (_executor is null)
            {
                IGeneratedGenericFunctor executor = _world.ResolveGenericFunctor(in _query, _hasQuery, _functorType, _inputs.Arguments);
                if (!_hasQuery)
                {
                    _query = executor.CreateQuery(_world, _inputs.Arguments);
                    _hasQuery = true;
                }

                if (executor is not IGeneratedGenericFunctor<TContext> typedExecutor)
                {
                    ThrowHelper.ThrowGenericFunctorContextMismatch(_functorType, typeof(TContext));
                    return;
                }

                _executor = typedExecutor;
            }

            _executor.Execute(_world, in _query, _inputs.Entities, _hasQuery, _mode, _workerCount, ref context, _inputs.Arguments);
        }
    }

    /// <summary>Creates a reusable operation for a runtime-selected generic functor.</summary>
    /// <remarks>The input snapshots are stored in native memory owned and released by this world.</remarks>
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
        GenericFunctorInputs inputs = StoreGenericFunctorInputs(entities, arguments);
        return new EcsOperation<GenericFunctorOperationInvoker>(
            new GenericFunctorOperationInvoker(this, in operationQuery, inputs, hasQuery, mode, functorType, workerCount));
    }

    /// <summary>Creates a reusable operation for a runtime-selected generic functor with mutable caller-owned context.</summary>
    /// <remarks>The input snapshots are stored in native memory owned and released by this world.</remarks>
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
        GenericFunctorInputs inputs = StoreGenericFunctorInputs(entities, arguments);
        return new EcsOperation<TContext, GenericFunctorContextOperationInvoker<TContext>>(
            context,
            new GenericFunctorContextOperationInvoker<TContext>(
                this,
                in operationQuery,
                inputs,
                hasQuery,
                mode,
                functorType,
                workerCount));
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
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNull(functorType, nameof(functorType));
        if (hasQuery && (!query.IsValid || query.Owner != this))
        {
            ThrowHelper.ThrowInvalidEntityQueryHandle();
        }
    }

    private GenericFunctorInputs StoreGenericFunctorInputs(
        ReadOnlySpan<Entity> entities,
        ReadOnlySpan<ComponentId> arguments)
    {
        EnsureExecutionAccess();
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        if (entities.IsEmpty && arguments.IsEmpty)
        {
            return default;
        }

        nuint headerSize = (nuint)Unsafe.SizeOf<GenericFunctorInputBlock>();
        nuint entityByteCount = checked((nuint)entities.Length * (nuint)Unsafe.SizeOf<Entity>());
        nuint argumentByteCount = checked((nuint)arguments.Length * (nuint)Unsafe.SizeOf<ComponentId>());
        nint address = NativeMemoryCompat.Alloc(checked(headerSize + entityByteCount + argumentByteCount));
        ArrayAccess.GetRefAtZero<GenericFunctorInputBlock>(address).Next = _genericFunctorInputBlocks;
        _genericFunctorInputBlocks = address;

        nint entityAddress = ArrayAccess.AddBytes(address, headerSize);
        nint argumentAddress = ArrayAccess.AddBytes(entityAddress, entityByteCount);
        entities.CopyTo(ArrayAccess.AsSpan<Entity>(entityAddress, entities.Length));
        arguments.CopyTo(ArrayAccess.AsSpan<ComponentId>(argumentAddress, arguments.Length));
        return new GenericFunctorInputs(entityAddress, entities.Length, argumentAddress, arguments.Length);
    }

    private void DisposeGenericFunctorInputBlocks()
    {
        while (_genericFunctorInputBlocks != 0)
        {
            nint address = _genericFunctorInputBlocks;
            _genericFunctorInputBlocks = ArrayAccess.GetRefAtZero<GenericFunctorInputBlock>(address).Next;
            NativeMemoryCompat.Free(address);
        }
    }
}
