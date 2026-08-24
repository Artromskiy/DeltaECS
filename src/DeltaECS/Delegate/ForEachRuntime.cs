namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

internal interface IForEachInvoker
{
    void Invoke(ref QueryChunkCursor cursor);
}

internal static class ForEachRuntime
{
    internal static WriteAccess AccessWrite<T>(World world, in Query query, ComponentId component)
    {
        if (!ReferenceEquals(query.Owner, world) || !query.IsValid)
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(query));
        }

        if (!world.Layouts.TryGet(component, out var layout)
            || layout.RuntimeType != typeof(T))
        {
            throw new ArgumentException($"Component {component} is not registered as {typeof(T)}.", nameof(component));
        }

        return query.AccessWrite(component);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Execute<TInvoker>(World world, in Query query, ref TInvoker invoker)
        where TInvoker : struct, IForEachInvoker
    {
        world.Query(in query, ref invoker, static (ref TInvoker state, ref QueryChunkCursor cursor) =>
            state.Invoke(ref cursor));
    }

    internal static void ResolveComponentIds(in Query query, Span<ComponentId> destination)
    {
        if (query.Description.AllMask.Count != destination.Length)
        {
            throw new ArgumentException("A query-only ForEach overload requires exactly the callback components in its All mask.", nameof(query));
        }

        query.Description.AllMask.CopyComponentIds(destination);
    }
}
