#pragma warning disable CS1591 // Public members are compiler-support API hidden with EditorBrowsable(Never).

namespace Delta.ECS;

using System;
using System.ComponentModel;

/// <summary>Generated execution shape for a runtime-selected generic functor.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum GeneratedGenericFunctorMode
{
    Components,
    Entity,
    EntityList,
    EntityListEntity,
    ParallelComponents,
    ParallelEntity,
    EntityListParallel,
    EntityListParallelEntity,
}

/// <summary>Compiler-support contract for a closed generic functor execution.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedGenericFunctor
{
    /// <summary>Creates the query implied by this closed functor's component rows.</summary>
    Query CreateQuery(World world, ReadOnlySpan<ComponentId> genericArguments);

    /// <summary>Executes one default functor across the selected query.</summary>
    void Execute(
        World world,
        in Query query,
        ReadOnlySpan<Entity> entities,
        bool hasQuery,
        GeneratedGenericFunctorMode mode,
        int workerCount,
        ReadOnlySpan<ComponentId> genericArguments);
}

/// <summary>Compiler-support contract for a generic functor with caller-owned context.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedGenericFunctor<TContext> : IGeneratedGenericFunctor
{
    /// <summary>Executes one default functor across the selected query with a strongly typed context.</summary>
    void Execute(
        World world,
        in Query query,
        ReadOnlySpan<Entity> entities,
        bool hasQuery,
        GeneratedGenericFunctorMode mode,
        int workerCount,
        ref TContext context,
        ReadOnlySpan<ComponentId> genericArguments);
}

public static partial class GeneratedForEachRuntime
{
    /// <summary>Resolves a derived registration used by a generic functor component row.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ComponentId GetGenericFunctorComponent<T>(World world, ReadOnlySpan<ComponentId> genericArguments)
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        return world.Layouts.GetGenericComponent(typeof(T), genericArguments);
    }

    /// <summary>Throws when a generic functor requires a context but none was passed.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void ThrowGenericFunctorRequiresContext(Type functorType)
        => ThrowHelper.ThrowGenericFunctorRequiresContext(functorType);

    /// <summary>Throws when a mutable-context generic functor is requested in parallel mode.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void ThrowGenericFunctorParallelRefContext(Type functorType)
        => ThrowHelper.ThrowGenericFunctorParallelRefContext(functorType);

    /// <summary>Throws when a generic functor does not match the requested execution mode.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void ThrowGenericFunctorModeMismatch(Type functorType, GeneratedGenericFunctorMode mode)
        => ThrowHelper.ThrowGenericFunctorModeMismatch(functorType, mode);

    /// <summary>Throws when a generated generic-functor mode value is invalid.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void ThrowInvalidGenericFunctorMode(GeneratedGenericFunctorMode mode)
        => ThrowHelper.ThrowInvalidGenericFunctorMode(mode);
}
