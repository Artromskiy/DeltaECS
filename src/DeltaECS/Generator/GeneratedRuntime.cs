namespace Delta.ECS;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>Compiler-support contract for generated entity-sequence functor invokers.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedSequenceInvoker
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Invoke(ref GeneratedSequenceCursor cursor);
}

/// <summary>Trusted compiler-support execution state for generated dense queries.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedDenseExecution
{
    private World? _owner;
    private readonly ArchetypePlan[] _plans;
    private readonly int _planCount;
    private readonly uint _writeTick;
    private readonly Stamp _writeStamp;
    private ChunkPlan[] _chunks;
    private int _chunkCount;
    private int _planIndex;
    private int _chunkIndex;

    internal GeneratedDenseExecution(
        World owner,
        ArchetypePlan[] plans,
        int planCount,
        uint writeTick,
        Stamp writeStamp)
    {
        _owner = owner;
        _plans = plans;
        _planCount = planCount;
        _writeTick = writeTick;
        _writeStamp = writeStamp;
        _chunks = Array.Empty<ChunkPlan>();
        _chunkCount = 0;
        _planIndex = -1;
        _chunkIndex = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext(out GeneratedQuerySlots slots)
    {
        if (_owner is null)
        {
            QueryThrowHelper.ThrowDisposedQueryExecution();
        }

        return MoveNextTrusted(out slots);
    }

    /// <summary>Advances a validated generated execution without repeating the lifetime guard.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNextTrusted(out GeneratedQuerySlots slots)
    {
        int nextChunk = _chunkIndex + 1;
        if ((uint)nextChunk < (uint)_chunkCount)
        {
            _chunkIndex = nextChunk;
            slots = new GeneratedQuerySlots(
                _plans.Ref(_planIndex),
                _chunks.Ref(_chunkIndex),
                _writeTick,
                _writeStamp);
            return true;
        }

        while ((uint)++_planIndex < (uint)_planCount)
        {
            ref readonly ArchetypePlan plan = ref _plans.Ref(_planIndex);
            _chunks = plan.ChunkArray;
            _chunkCount = plan.ChunkCount;
            if (_chunkCount == 0)
            {
                continue;
            }

            _chunkIndex = 0;
            slots = new GeneratedQuerySlots(
                plan,
                _chunks.Ref(_chunkIndex),
                _writeTick,
                _writeStamp);
            return true;
        }

        _planIndex = _planCount;
        _chunkCount = 0;
        _chunkIndex = -1;
        slots = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        World? owner = _owner;
        if (owner is null)
        {
            return;
        }

        owner.EndQueryLease();
        _owner = null;
        _chunks = Array.Empty<ChunkPlan>();
    }
}

/// <summary>Compiler-support cursor used by generated entity-sequence code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedSequenceCursor
{
    private readonly Chunk _chunk;
    private readonly ReadOnlySpan<int> _componentRows;
    private readonly Array[] _resolvedRowsByQuery;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;

    internal GeneratedSequenceCursor(
        ArchetypePlan plan,
        ChunkPlan chunkPlan,
        int slot,
        Entity entity,
        QueryWriteSession writeSession,
        int sessionGeneration)
    {
        Chunk chunk = chunkPlan.Chunk;
        _chunk = chunk;
        _componentRows = plan.ComponentRows;
        _resolvedRowsByQuery = chunkPlan.ComponentRows;
        _writeSession = writeSession;
        _sessionGeneration = sessionGeneration;
        Slot = slot;
        Entity = entity;
    }

    public int Slot { get; }

    public Entity Entity { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T GetGeneratedReadReference<T>(int queryComponentIndex)
    {
        _writeSession.EnsureActive(_sessionGeneration);
        return ref Unsafe.As<byte, T>(ref ArrayAccess.DataReference(_resolvedRowsByQuery.Ref(queryComponentIndex)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetGeneratedWriteReference<T>(int queryComponentIndex)
    {
        _writeSession.Acquire(_sessionGeneration, out uint writeTick, out Stamp writeStamp);
        int physicalRow = _componentRows.Ref(queryComponentIndex);
        _chunk.MarkComponentWritten(physicalRow, Slot, writeTick, writeStamp);
        return ref Unsafe.As<byte, T>(ref ArrayAccess.DataReference(_resolvedRowsByQuery.Ref(queryComponentIndex)));
    }
}

/// <summary>Non-generic runtime services consumed by generated ForEach code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedForEachRuntime
{
    /// <summary>Opens the trusted dense execution used by generated callbacks.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static GeneratedDenseExecution OpenDense(World world, in Query query, bool hasWrites)
    {
        QueryPlan plan = ValidateQuery(world, in query);
        ArchetypePlan[] plans = plan.MatchingPlanArray;
        int planCount = plan.MatchingPlanCount;
        uint writeTick = 0;
        Stamp writeStamp = default;
        if (hasWrites)
        {
            for (int planIndex = 0; planIndex < planCount; planIndex++)
            {
                if (plans.Ref(planIndex).Chunks.IsEmpty)
                {
                    continue;
                }

                writeTick = world.ReserveQueryWrite(out writeStamp);
                break;
            }
        }

        world.BeginQueryLease();
        return new GeneratedDenseExecution(
            world,
            plans,
            planCount,
            writeTick,
            writeStamp);
    }

    /// <summary>Creates a validated read access token for a closed generated dense path.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ReadAccess CreateReadAccess(
        World world,
        in Query query,
        Type runtimeType)
    {
        QueryPlan plan = ValidateQuery(world, in query, runtimeType);
        return new ReadAccess(plan, plan.ResolvePrimaryReadRoute(runtimeType));
    }

    /// <summary>Creates a validated write access token for a closed generated dense path.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static WriteAccess CreateWriteAccess(
        World world,
        in Query query,
        Type runtimeType)
    {
        QueryPlan plan = ValidateQuery(world, in query, runtimeType);
        int route = plan.ResolvePrimaryReadRoute(runtimeType);
        return new WriteAccess(plan, plan.UpgradeReadRouteToWrite(route));
    }

    /// <summary>Creates a validated explicit-component read access token for a closed generated dense path.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ReadAccess CreateReadAccess(
        World world,
        in Query query,
        ComponentId component,
        Type runtimeType)
    {
        QueryPlan plan = ValidateQuery(world, in query, runtimeType);
        return new ReadAccess(plan, plan.ResolveReadRoute(component, runtimeType));
    }

    /// <summary>Creates a validated explicit-component write access token for a closed generated dense path.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static WriteAccess CreateWriteAccess(
        World world,
        in Query query,
        ComponentId component,
        Type runtimeType)
    {
        QueryPlan plan = ValidateQuery(world, in query, runtimeType);
        int route = plan.ResolveReadRoute(component, runtimeType);
        return new WriteAccess(plan, plan.UpgradeReadRouteToWrite(route));
    }

    /// <summary>
    /// Returns a cached primary read access after the generated dense scope has
    /// validated the query. This is compiler support and must not be called
    /// without the preceding <see cref="OpenDense"/> validation.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ReadAccess GetPreparedReadAccess(in Query query, Type runtimeType)
        => query.Cached.GetPreparedPrimaryReadAccess(runtimeType);

    /// <summary>
    /// Returns a cached primary write access after the generated dense scope has
    /// validated the query. This is compiler support and must not be called
    /// without the preceding <see cref="OpenDense"/> validation.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static WriteAccess GetPreparedWriteAccess(in Query query, Type runtimeType)
        => query.Cached.GetPreparedPrimaryWriteAccess(runtimeType);

    /// <summary>
    /// Returns a cached explicit-component read access after dense scope
    /// validation. The component/type contract remains checked by the plan.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ReadAccess GetPreparedReadAccess(
        in Query query,
        ComponentId component,
        Type runtimeType)
        => query.Cached.GetPreparedReadAccess(component, runtimeType);

    /// <summary>
    /// Returns a cached explicit-component write access after dense scope
    /// validation. The component/type contract remains checked by the plan.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static WriteAccess GetPreparedWriteAccess(
        in Query query,
        ComponentId component,
        Type runtimeType)
        => query.Cached.GetPreparedWriteAccess(component, runtimeType);

    public static int AccessRead(
        World world,
        in Query query,
        Type runtimeType)
    {
        QueryPlan plan = ValidateQuery(world, in query, runtimeType);
        return plan.ResolvePrimaryReadRoute(runtimeType);
    }

    public static int AccessWrite(
        World world,
        in Query query,
        Type runtimeType)
    {
        QueryPlan plan = ValidateQuery(world, in query, runtimeType);
        return plan.UpgradeReadRouteToWrite(plan.ResolvePrimaryReadRoute(runtimeType));
    }

    public static int AccessRead(
        World world,
        in Query query,
        ComponentId component,
        Type runtimeType)
    {
        QueryPlan plan = ValidateQuery(world, in query, runtimeType);
        return plan.ResolveReadRoute(component, runtimeType);
    }

    private static QueryPlan ValidateQuery(World world, in Query query, Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(runtimeType);
        return ValidateQuery(world, in query);
    }

    private static QueryPlan ValidateQuery(World world, in Query query)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!ReferenceEquals(query.Owner, world) || !query.IsValid)
        {
            throw new ArgumentException("Query handle does not belong to this world.", nameof(query));
        }

        return query.Cached;
    }

    public static int AccessWrite(
        World world,
        in Query query,
        ComponentId component,
        Type runtimeType)
    {
        QueryPlan plan = ValidateQuery(world, in query, runtimeType);
        int readRoute = plan.ResolveReadRoute(component, runtimeType);
        return plan.UpgradeReadRouteToWrite(readRoute);
    }
}
