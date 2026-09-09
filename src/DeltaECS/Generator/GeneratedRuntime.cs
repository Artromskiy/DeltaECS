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

/// <summary>Compiler-support contract for one direct generated parallel chunk invocation.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedParallelInvoker
{
    bool RequiresSingleThread { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Invoke(ref GeneratedQuerySlots slots);
}

/// <summary>Compiler-support contract for generated archetype stamp writers.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedArchetypeStampWriter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Write(Stamp[] stamps);
}

/// <summary>Trusted compiler-support execution state for generated write queries.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedDenseExecution
{
    private World? _owner;
    private readonly ReadOnlySpan<ArchetypePlan> _plans;
    private readonly ReadOnlySpan<ChunkPlan> _chunkPlans;
    private int _chunkIndex;

    internal GeneratedDenseExecution(
        World owner,
        ReadOnlySpan<ArchetypePlan> plans,
        ReadOnlySpan<ChunkPlan> chunkPlans)
    {
        _owner = owner;
        _plans = plans;
        _chunkPlans = chunkPlans;
        _chunkIndex = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext(out GeneratedQuerySlots slots)
    {
        if (_owner is null)
        {
            ThrowHelper.ThrowDisposedQueryExecution();
        }

        return MoveNextTrusted(out slots);
    }

    /// <summary>
    /// Marks one write component for every non-empty matching archetype once
    /// before the generated chunk loop starts.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkArchetypeWrite(int queryComponentIndex)
    {
        for (int planIndex = 0; planIndex < _plans.Length; planIndex++)
        {
            ref readonly ArchetypePlan plan = ref _plans.RefAt(planIndex);
            if (plan.ChunkCount == 0)
            {
                continue;
            }

            GeneratedForEachRuntime.IncrementArchetypeStamp(
                plan.ArchetypeStamps,
                queryComponentIndex);
        }
    }

    /// <summary>
    /// Marks several write components for every non-empty matching archetype
    /// in one plan traversal before the generated chunk loop starts.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkArchetypeWrites(scoped ReadOnlySpan<int> queryComponentIndices)
    {
        for (int planIndex = 0; planIndex < _plans.Length; planIndex++)
        {
            ref readonly ArchetypePlan plan = ref _plans.RefAt(planIndex);
            if (plan.ChunkCount == 0)
            {
                continue;
            }

            Stamp[] stamps = plan.ArchetypeStamps;
            for (int accessIndex = 0; accessIndex < queryComponentIndices.Length; accessIndex++)
            {
                GeneratedForEachRuntime.IncrementArchetypeStamp(
                    stamps,
                    queryComponentIndices.RefAt(accessIndex));
            }
        }
    }

    /// <summary>Runs the generated, arity-specific writer once for every matching archetype.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkArchetypeWrites<TWriter>(ref TWriter writer)
        where TWriter : struct, IGeneratedArchetypeStampWriter
    {
        for (int planIndex = 0; planIndex < _plans.Length; planIndex++)
        {
            ref readonly ArchetypePlan plan = ref _plans.RefAt(planIndex);
            if (plan.ChunkCount == 0)
            {
                continue;
            }

            writer.Write(plan.ArchetypeStamps);
        }
    }

    /// <summary>Returns one validated chunk's rows for an indexed generated traversal.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int ChunkCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunkPlans.Length;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetChunkRowsTrusted(int chunkIndex, out Array[] componentRows, out int count)
    {
        ref readonly ChunkPlan chunkPlan = ref _chunkPlans.RefAt(chunkIndex);
        componentRows = chunkPlan.ComponentRows;
        count = chunkPlan.Chunk.Count;
    }

    /// <summary>Advances a validated generated execution without repeating the lifetime guard.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNextTrusted(out GeneratedQuerySlots slots)
    {
        int nextChunk = _chunkIndex + 1;
        if ((uint)nextChunk < (uint)_chunkPlans.Length)
        {
            _chunkIndex = nextChunk;
            slots = new GeneratedQuerySlots(in _chunkPlans.RefAt(_chunkIndex));
            return true;
        }
        _chunkIndex = _chunkPlans.Length;
        slots = default;
        return false;
    }

    /// <summary>Advances a validated execution while exposing only component rows and count.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNextTrusted(out Array[] componentRows, out int count)
    {
        int nextChunk = _chunkIndex + 1;
        if ((uint)nextChunk < (uint)_chunkPlans.Length)
        {
            _chunkIndex = nextChunk;
            ref readonly ChunkPlan chunkPlan = ref _chunkPlans.RefAt(_chunkIndex);
            componentRows = chunkPlan.ComponentRows;
            count = chunkPlan.Chunk.Count;
            return true;
        }

        _chunkIndex = _chunkPlans.Length;
        componentRows = null!;
        count = 0;
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
    }
}

/// <summary>Trusted compiler-support execution state for generated read queries.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedReadDenseExecution
{
    private World? _owner;
    private readonly ReadOnlySpan<ChunkPlan> _chunkPlans;
    private int _chunkIndex;

    internal GeneratedReadDenseExecution(World owner, ReadOnlySpan<ChunkPlan> chunkPlans)
    {
        _owner = owner;
        _chunkPlans = chunkPlans;
        _chunkIndex = -1;
    }

    /// <summary>Returns the number of validated chunks available to generated code.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int ChunkCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunkPlans.Length;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetChunkRowsTrusted(int chunkIndex, out Array[] componentRows, out int count)
    {
        ref readonly ChunkPlan chunkPlan = ref _chunkPlans.RefAt(chunkIndex);
        componentRows = chunkPlan.ComponentRows;
        count = chunkPlan.Chunk.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNextTrusted(out GeneratedReadQuerySlots slots)
    {
        int nextChunk = _chunkIndex + 1;
        if ((uint)nextChunk < (uint)_chunkPlans.Length)
        {
            _chunkIndex = nextChunk;
            slots = new GeneratedReadQuerySlots(in _chunkPlans.RefAt(_chunkIndex));
            return true;
        }
        _chunkIndex = _chunkPlans.Length;
        slots = default;
        return false;
    }

    /// <summary>Advances a validated read execution while exposing only component rows and count.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNextTrusted(out Array[] componentRows, out int count)
    {
        int nextChunk = _chunkIndex + 1;
        if ((uint)nextChunk < (uint)_chunkPlans.Length)
        {
            _chunkIndex = nextChunk;
            ref readonly ChunkPlan chunkPlan = ref _chunkPlans.RefAt(_chunkIndex);
            componentRows = chunkPlan.ComponentRows;
            count = chunkPlan.Chunk.Count;
            return true;
        }

        _chunkIndex = _chunkPlans.Length;
        componentRows = null!;
        count = 0;
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
    }
}

/// <summary>Compiler-support cursor used by generated entity-sequence code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedSequenceCursor
{
    private readonly Chunk _chunk;
    private readonly ReadOnlySpan<int> _componentRows;
    private readonly Array[] _resolvedRowsByQuery;
    private readonly bool _writeEnabled;

    internal GeneratedSequenceCursor(
        in ArchetypePlan plan,
        in ChunkPlan chunkPlan,
        int slot,
        Entity entity,
        bool writeEnabled)
    {
        Chunk chunk = chunkPlan.Chunk;
        _chunk = chunk;
        _componentRows = plan.ComponentRows;
        _resolvedRowsByQuery = chunkPlan.ComponentRows;
        _writeEnabled = writeEnabled;
        Slot = slot;
        Entity = entity;
    }

    public int Slot { get; }

    public Entity Entity { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T GetGeneratedReadReference<T>(int queryComponentIndex)
        => ref GetGeneratedReadReferenceTrusted<T>(queryComponentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetGeneratedWriteReference<T>(int queryComponentIndex)
    {
        if (!_writeEnabled)
        {
            ThrowHelper.ThrowMissingWriteIntent();
        }

        return ref GetGeneratedWriteReferenceTrusted<T>(queryComponentIndex);
    }

    /// <summary>Gets a generated read reference after the sequence execution boundary was validated.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T GetGeneratedReadReferenceTrusted<T>(int queryComponentIndex)
        => ref Unsafe.Add(
            ref Unsafe.As<byte, T>(ref ArrayAccess.DataReference(_resolvedRowsByQuery.RefAt(queryComponentIndex))),
            Slot);

    /// <summary>Gets and stamps a generated write reference after validation.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetGeneratedWriteReferenceTrusted<T>(int queryComponentIndex)
    {
        int physicalRow = _componentRows.RefAt(queryComponentIndex);
        Stamp stamp = _chunk.IncrementComponentStamp(physicalRow, Slot);
        new EntityComponentStampWriter(
            _chunk,
            physicalRow,
            Slot,
            stamp).Mark();
        return ref Unsafe.Add(
            ref Unsafe.As<byte, T>(ref ArrayAccess.DataReference(_resolvedRowsByQuery.RefAt(queryComponentIndex))),
            Slot);
    }
}

/// <summary>Non-generic runtime services consumed by generated ForEach code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedForEachRuntime
{
    /// <summary>Validates a generated callback without requiring a modern BCL.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull(object? value, string parameterName)
        => ThrowHelper.ThrowIfNull(value, parameterName);

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IncrementArchetypeStamp(Stamp[] stamps, int componentIndex)
    {
        ref Stamp stamp = ref stamps.RefAt(componentIndex);
        stamp = stamp.Next();
    }

    /// <summary>Gets the first element of a validated generated component row.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetGeneratedRow<T>(Array[] componentRows, int queryComponentIndex)
        => ref Unsafe.As<T[]>(componentRows.RefAt(queryComponentIndex)).GetRefAtZero();

    /// <summary>
    /// Executes a generated invoker over disjoint chunks. The query and access
    /// routes have already been resolved by the generated call site.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ExecuteParallelDense<TInvoker>(
        World world,
        in Query query,
        ref TInvoker invoker,
        scoped ReadOnlySpan<int> writeComponentIndices,
        int requestedWorkerCount = 0)
        where TInvoker : struct, IGeneratedParallelInvoker
    {
        QueryPlan plan = ValidateQuery(world, in query);
        ReadOnlySpan<ArchetypePlan> plans = plan.MatchingPlans();
        MarkArchetypeWrites(plans, writeComponentIndices);
        QueryWriteSession session = world.RentQueryWriteSession(plan, out int generation);
        world.BeginQueryLease();
        bool entered = false;
        try
        {
            world.EnterParallelExecution();
            entered = true;
            world.GetParallelQueryExecutor<TInvoker>().Execute(
                plan,
                ref invoker,
                requestedWorkerCount);
        }
        finally
        {
            if (entered)
            {
                world.ExitParallelExecution();
            }

            world.ReturnQueryWriteSession(session, generation);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MarkArchetypeWrites(
        ReadOnlySpan<ArchetypePlan> plans,
        scoped ReadOnlySpan<int> componentIndices)
    {
        for (int planIndex = 0; planIndex < plans.Length; planIndex++)
        {
            ref readonly ArchetypePlan plan = ref plans.RefAt(planIndex);
            if (plan.ChunkCount == 0)
            {
                continue;
            }

            Stamp[] stamps = plan.ArchetypeStamps;
            for (int componentIndex = 0; componentIndex < componentIndices.Length; componentIndex++)
            {
                int queryComponentIndex = componentIndices.RefAt(componentIndex);
                bool duplicate = false;
                for (int previous = 0; previous < componentIndex; previous++)
                {
                    duplicate |= componentIndices.RefAt(previous) == queryComponentIndex;
                }

                if (!duplicate)
                {
                    IncrementArchetypeStamp(stamps, queryComponentIndex);
                }
            }
        }
    }

    /// <summary>Opens the trusted dense execution used by generated callbacks.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GeneratedDenseExecution OpenDense(World world, in Query query, bool hasWrites)
    {
        QueryPlan plan = ValidateQuery(world, in query);
        ReadOnlySpan<ArchetypePlan> plans = plan.MatchingPlans();
        ReadOnlySpan<ChunkPlan> chunks = plan.MatchingChunkPlans();
        world.BeginQueryLease();
        return new GeneratedDenseExecution(world, plans, chunks);
    }

    /// <summary>Opens a validated read-only dense execution without write state.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GeneratedReadDenseExecution OpenReadDense(World world, in Query query)
    {
        QueryPlan plan = ValidateQuery(world, in query);
        ReadOnlySpan<ChunkPlan> chunks = plan.MatchingChunkPlans();
        world.BeginQueryLease();
        return new GeneratedReadDenseExecution(world, chunks);
    }

    /// <summary>Opens a validated write dense execution.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GeneratedDenseExecution OpenWriteDense(World world, in Query query)
    {
        QueryPlan plan = ValidateQuery(world, in query);
        ReadOnlySpan<ArchetypePlan> plans = plan.MatchingPlans();
        ReadOnlySpan<ChunkPlan> chunks = plan.MatchingChunkPlans();
        world.BeginQueryLease();
        return new GeneratedDenseExecution(world, plans, chunks);
    }

    /// <summary>Creates a validated read access token for a closed generated dense path.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadAccess GetPreparedReadAccess(in Query query, Type runtimeType)
        => query.Cached.GetPreparedPrimaryReadAccess(runtimeType);

    /// <summary>Returns a cached primary read access using the generated component type.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadAccess GetPreparedReadAccess<T>(in Query query)
        => query.Cached.GetPreparedPrimaryReadAccess<T>();

    /// <summary>Returns a cached primary read route without materializing an access token.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPreparedReadRoute<T>(in Query query)
        => query.Cached.GetPreparedPrimaryReadRoute<T>();

    /// <summary>Returns a cached explicit-component read route after sequence validation.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPreparedReadRoute<T>(in Query query, ComponentId component)
        => query.Cached.GetPreparedReadAccess(component, typeof(T)).QueryComponentIndex;

    /// <summary>
    /// Returns a cached primary write access after the generated dense scope has
    /// validated the query. This is compiler support and must not be called
    /// without the preceding <see cref="OpenDense"/> validation.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WriteAccess GetPreparedWriteAccess(in Query query, Type runtimeType)
        => query.Cached.GetPreparedPrimaryWriteAccess(runtimeType);

    /// <summary>Returns a cached primary write access using the generated component type.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WriteAccess GetPreparedWriteAccess<T>(in Query query)
        => query.Cached.GetPreparedPrimaryWriteAccess<T>();

    /// <summary>Returns a cached primary write route without materializing an access token.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPreparedWriteRoute<T>(in Query query)
        => query.Cached.GetPreparedPrimaryWriteRoute<T>();

    /// <summary>Returns a cached explicit-component write route after sequence validation.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPreparedWriteRoute<T>(in Query query, ComponentId component)
        => query.Cached.GetPreparedWriteAccess(component, typeof(T)).QueryComponentIndex;

    /// <summary>Validates a filtered sequence before generated prepared routes are consumed.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateSequenceQuery(World world, in Query query)
        => _ = ValidateQuery(world, in query);

    /// <summary>Returns the trusted query-local route used by batch write marking.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetWriteQueryComponentIndex(WriteAccess access)
        => access.QueryComponentIndex;

    /// <summary>Returns a trusted query-local route used by generated parallel invokers.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetReadQueryComponentIndex(ReadAccess access)
        => access.QueryComponentIndex;

    /// <summary>
    /// Returns a cached explicit-component read access after dense scope
    /// validation. The component/type contract remains checked by the plan.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WriteAccess GetPreparedWriteAccess(
        in Query query,
        ComponentId component,
        Type runtimeType)
        => query.Cached.GetPreparedWriteAccess(component, runtimeType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AccessRead(
        World world,
        in Query query,
        Type runtimeType)
    {
        QueryPlan plan = ValidateQuery(world, in query, runtimeType);
        return plan.ResolvePrimaryReadRoute(runtimeType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AccessWrite(
        World world,
        in Query query,
        Type runtimeType)
    {
        QueryPlan plan = ValidateQuery(world, in query, runtimeType);
        return plan.UpgradeReadRouteToWrite(plan.ResolvePrimaryReadRoute(runtimeType));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AccessRead(
        World world,
        in Query query,
        ComponentId component,
        Type runtimeType)
    {
        QueryPlan plan = ValidateQuery(world, in query, runtimeType);
        return plan.ResolveReadRoute(component, runtimeType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static QueryPlan ValidateQuery(World world, in Query query, Type runtimeType)
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        ThrowHelper.ThrowIfNull(runtimeType, nameof(runtimeType));
        return ValidateQuery(world, in query);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static QueryPlan ValidateQuery(World world, in Query query)
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        if (!ReferenceEquals(query.Owner, world) || !query.IsValid)
        {
            ThrowHelper.ThrowGeneratedQueryInvalid(nameof(query));
        }

        return query.Cached;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
