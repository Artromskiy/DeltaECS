namespace Delta.ECS;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>Compiler-support contract for generated dense-query functor invokers.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedForEachInvoker
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Invoke(ref GeneratedQuerySlots slots);
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
    private readonly Chunk _chunk;
    private readonly ReadOnlySpan<int> _componentRows;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;

    internal GeneratedSequenceCursor(
        ArchetypePlan plan,
        Chunk chunk,
        int slot,
        Entity entity,
        QueryWriteSession writeSession,
        int sessionGeneration)
    {
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
    public ReadRow GetReadRow(int queryComponentIndex)
    {
        _writeSession.EnsureActive(_sessionGeneration);
        int physicalRow = _componentRows.Ref(queryComponentIndex);
        return new ReadRow(_chunk.GetRawComponentRowTrusted(physicalRow));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WriteRow GetWriteRow(int queryComponentIndex)
    {
        int physicalRow = _componentRows.Ref(queryComponentIndex);
        _writeSession.Acquire(_sessionGeneration, out uint writeTick, out Stamp writeStamp);
        _chunk.MarkComponentWritten(physicalRow, Slot, writeTick, writeStamp);
        return new WriteRow(_chunk.GetRawComponentRowTrusted(physicalRow));
    }
}

/// <summary>Non-generic runtime services consumed by generated ForEach code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedForEachRuntime
{
    public static int AccessRead(
        World world,
        in Query query,
        Type runtimeType)
    {
        ComponentId component = ResolvePrimaryComponent(world, in query, runtimeType);
        return ValidateComponent(world, in query, component, runtimeType);
    }

    public static int AccessWrite(
        World world,
        in Query query,
        Type runtimeType)
    {
        ComponentId component = ResolvePrimaryComponent(world, in query, runtimeType);
        return ValidateComponent(world, in query, component, runtimeType);
    }

    public static int AccessRead(
        World world,
        in Query query,
        ComponentId component,
        Type runtimeType)
    {
        return ValidateComponent(world, in query, component, runtimeType);
    }

    private static ComponentId ResolvePrimaryComponent(World world, in Query query, Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(runtimeType);
        if (!ReferenceEquals(query.Owner, world) || !query.IsValid)
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(query));
        }

        return query.Cached.ResolvePrimaryComponent(world, runtimeType);
    }

    public static int AccessWrite(
        World world,
        in Query query,
        ComponentId component,
        Type runtimeType)
    {
        return ValidateComponent(world, in query, component, runtimeType);
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
