namespace Delta.ECS;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>Compiler-support contract for generated dense-query functor invokers.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedForEachInvoker
{
    void Invoke(ref QueryChunkCursor cursor);
}

/// <summary>Compiler-support contract for generated entity-sequence functor invokers.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedSequenceInvoker
{
    void Invoke(ref GeneratedSequenceCursor cursor);
}

/// <summary>Compiler-support cursor used by generated entity-sequence code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedSequenceCursor
{
    private readonly QueryPlan _query;
    private readonly Chunk _chunk;
    private readonly ReadOnlySpan<int> _componentRows;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;

    internal GeneratedSequenceCursor(
        QueryPlan query,
        ArchetypePlan plan,
        Chunk chunk,
        int slot,
        Entity entity,
        QueryWriteSession writeSession,
        int sessionGeneration)
    {
        _query = query;
        _chunk = chunk;
        _componentRows = plan.ComponentRows;
        _writeSession = writeSession;
        _sessionGeneration = sessionGeneration;
        Slot = slot;
        Entity = entity;
    }

    public int Slot { get; }

    public Entity Entity { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadRow GetRow(ReadAccess access)
    {
        _writeSession.EnsureActive(_sessionGeneration);
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        return new ReadRow(_chunk.GetRawComponentRow(physicalRow));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WriteRow GetRow(WriteAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        _writeSession.Acquire(_sessionGeneration, out uint writeTick, out Stamp writeStamp);
        _chunk.MarkComponentWritten(physicalRow, Slot, writeTick, writeStamp);
        return new WriteRow(_chunk.GetRawComponentRow(physicalRow));
    }
}

/// <summary>Non-generic runtime services consumed by generated ForEach code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedForEachRuntime
{
    public static ReadAccess AccessRead(
        World world,
        in Query query,
        ComponentId component,
        Type runtimeType)
    {
        int queryRow = ValidateComponent(world, in query, component, runtimeType);
        return new ReadAccess(query.Cached, queryRow);
    }

    public static WriteAccess AccessWrite(
        World world,
        in Query query,
        ComponentId component,
        Type runtimeType)
    {
        int queryRow = ValidateComponent(world, in query, component, runtimeType);
        return new WriteAccess(query.Cached, queryRow);
    }

    public static Query CreateSequenceQuery(World world, ReadOnlySpan<ComponentId> components)
    {
        ArgumentNullException.ThrowIfNull(world);
        var spec = QuerySpec.ForComponents(components);
        return world.CreateQuery(in spec);
    }

    private static int ValidateComponent(
        World world,
        in Query query,
        ComponentId component,
        Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(runtimeType);
        if (!ReferenceEquals(query.Owner, world) || !query.IsValid)
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(query));
        }

        if (!world.Layouts.TryGet(component, out ComponentLayout layout)
            || layout.RuntimeType != runtimeType)
        {
            throw new ArgumentException(
                $"Component {component} is not registered as {runtimeType}.",
                nameof(component));
        }

        if (!query.Description.AllMask.Contains(component))
        {
            throw new ArgumentException(
                "A ForEach component must be guaranteed by the query All mask.",
                nameof(component));
        }

        return query.Description.AllMask.Rank(component);
    }
}
