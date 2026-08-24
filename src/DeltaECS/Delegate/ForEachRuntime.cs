namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

internal interface IForEachInvoker
{
    void Invoke(ref QueryChunkCursor cursor);
}

internal static class ForEachRuntime
{
    internal static ReadAccess AccessRead<T>(World world, in Query query, ComponentId component)
    {
        int queryRow = ValidateComponent<T>(world, in query, component);
        return new ReadAccess(query.Cached, queryRow);
    }

    internal static WriteAccess AccessWrite<T>(World world, in Query query, ComponentId component)
    {
        int queryRow = ValidateComponent<T>(world, in query, component);
        return new WriteAccess(query.Cached, queryRow);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Execute<TInvoker>(World world, in Query query, ref TInvoker invoker, bool hasWrites)
        where TInvoker : struct, IForEachInvoker
    {
        world.ExecuteForEach(in query, ref invoker, hasWrites);
    }

    internal static void ResolveComponentIds(in Query query, Span<ComponentId> destination)
    {
        if (!query.IsValid)
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(query));
        }

        if (query.Description.AllMask.Count != destination.Length)
        {
            throw new ArgumentException("A query-only ForEach overload requires exactly the callback components in its All mask.", nameof(query));
        }

        query.Description.AllMask.CopyComponentIds(destination);
    }

    private static int ValidateComponent<T>(World world, in Query query, ComponentId component)
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

        if (!query.Description.AllMask.Contains(component))
        {
            throw new ArgumentException("A ForEach component must be guaranteed by the query All mask.", nameof(component));
        }

        return query.Description.AllMask.Rank(component);
    }
}
