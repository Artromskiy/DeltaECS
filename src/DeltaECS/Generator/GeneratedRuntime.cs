namespace Delta.ECS;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>Compiler-support contract for generated entity-sequence functor invokers.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedSequenceInvoker
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Invoke(ref GeneratedSequenceCursor cursor);
}

/// <summary>Compiler-support contract for generated archetype stamp writers.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedArchetypeStampWriter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Write(nint[] stampAddresses);
}

/// <summary>Trusted compiler-support execution state for generated write queries.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedDenseExecution
{
    private World? _owner;
    private readonly ReadOnlySpan<ArchetypePlan> _plans;
    private ChunkPlan[] _chunks;
    private int _chunkCount;
    private int _planIndex;
    private int _chunkIndex;

    internal GeneratedDenseExecution(
        World owner,
        ReadOnlySpan<ArchetypePlan> plans)
    {
        _owner = owner;
        _plans = plans;
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
            ref readonly ArchetypePlan plan = ref _plans[planIndex];
            if (plan.ChunkCount == 0)
            {
                continue;
            }

            ref nint firstStampAddress = ref MemoryMarshal.GetArrayDataReference(plan.ArchetypeStampAddresses);
            GeneratedForEachRuntime.IncrementArchetypeStamp(
                Unsafe.Add(ref firstStampAddress, queryComponentIndex));
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
            ref readonly ArchetypePlan plan = ref _plans[planIndex];
            if (plan.ChunkCount == 0)
            {
                continue;
            }

            nint[] stampAddresses = plan.ArchetypeStampAddresses;
            ref nint firstStampAddress = ref MemoryMarshal.GetArrayDataReference(stampAddresses);
            for (int accessIndex = 0; accessIndex < queryComponentIndices.Length; accessIndex++)
            {
                GeneratedForEachRuntime.IncrementArchetypeStamp(
                    Unsafe.Add(ref firstStampAddress, queryComponentIndices[accessIndex]));
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
            ref readonly ArchetypePlan plan = ref _plans[planIndex];
            if (plan.ChunkCount == 0)
            {
                continue;
            }

            writer.Write(plan.ArchetypeStampAddresses);
        }
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
                _chunks.Ref(_chunkIndex));
            return true;
        }

        while ((uint)++_planIndex < (uint)_plans.Length)
        {
            ref readonly ArchetypePlan plan = ref _plans[_planIndex];
            _chunks = plan.ChunkArray;
            _chunkCount = plan.ChunkCount;
            if (_chunkCount == 0)
            {
                continue;
            }

            _chunkIndex = 0;
            slots = new GeneratedQuerySlots(plan, _chunks[_chunkIndex]);
            return true;
        }

        _planIndex = _plans.Length;
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

/// <summary>Trusted compiler-support execution state for generated read queries.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedReadDenseExecution
{
    private World? _owner;
    private readonly ReadOnlySpan<ArchetypePlan> _plans;
    private ChunkPlan[] _chunks;
    private int _chunkCount;
    private int _planIndex;
    private int _chunkIndex;

    internal GeneratedReadDenseExecution(World owner, ReadOnlySpan<ArchetypePlan> plans)
    {
        _owner = owner;
        _plans = plans;
        _chunks = Array.Empty<ChunkPlan>();
        _chunkCount = 0;
        _planIndex = -1;
        _chunkIndex = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNextTrusted(out GeneratedReadQuerySlots slots)
    {
        int nextChunk = _chunkIndex + 1;
        if ((uint)nextChunk < (uint)_chunkCount)
        {
            _chunkIndex = nextChunk;
            slots = new GeneratedReadQuerySlots(_chunks.Ref(_chunkIndex));
            return true;
        }

        while ((uint)++_planIndex < (uint)_plans.Length)
        {
            ref readonly ArchetypePlan plan = ref _plans.Ref(_planIndex);
            _chunks = plan.ChunkArray;
            _chunkCount = plan.ChunkCount;
            if (_chunkCount == 0)
            {
                continue;
            }

            _chunkIndex = 0;
            slots = new GeneratedReadQuerySlots(_chunks.Ref(_chunkIndex));
            return true;
        }

        _planIndex = _plans.Length;
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
        _writeSession.Acquire(_sessionGeneration);
        int physicalRow = _componentRows.Ref(queryComponentIndex);
        Stamp stamp = _chunk.IncrementComponentStamp(physicalRow, Slot);
        new EntityComponentStampWriter(
            _chunk,
            physicalRow,
            Slot,
            stamp).Mark();
        return ref Unsafe.As<byte, T>(ref ArrayAccess.DataReference(_resolvedRowsByQuery.Ref(queryComponentIndex)));
    }
}

/// <summary>Non-generic runtime services consumed by generated ForEach code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedForEachRuntime
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IncrementArchetypeStamp(nint stampAddress)
    {
        unsafe
        {
            Stamp* current = (Stamp*)stampAddress;
            *current = current->Next();
        }
    }

    /// <summary>Opens the trusted dense execution used by generated callbacks.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GeneratedDenseExecution OpenDense(World world, in Query query, bool hasWrites)
    {
        QueryPlan plan = ValidateQuery(world, in query);
        ReadOnlySpan<ArchetypePlan> plans = plan.MatchingPlans();
        world.BeginQueryLease();
        return new GeneratedDenseExecution(world, plans);
    }

    /// <summary>Opens a validated read-only dense execution without write state.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GeneratedReadDenseExecution OpenReadDense(World world, in Query query)
    {
        QueryPlan plan = ValidateQuery(world, in query);
        ReadOnlySpan<ArchetypePlan> plans = plan.MatchingPlans();
        world.BeginQueryLease();
        return new GeneratedReadDenseExecution(world, plans);
    }

    /// <summary>Opens a validated write dense execution.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GeneratedDenseExecution OpenWriteDense(World world, in Query query)
    {
        QueryPlan plan = ValidateQuery(world, in query);
        ReadOnlySpan<ArchetypePlan> plans = plan.MatchingPlans();
        world.BeginQueryLease();
        return new GeneratedDenseExecution(world, plans);
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

    /// <summary>
    /// Returns a cached primary write access after the generated dense scope has
    /// validated the query. This is compiler support and must not be called
    /// without the preceding <see cref="OpenDense"/> validation.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WriteAccess GetPreparedWriteAccess(in Query query, Type runtimeType)
        => query.Cached.GetPreparedPrimaryWriteAccess(runtimeType);

    /// <summary>Returns the trusted query-local route used by batch write marking.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetWriteQueryComponentIndex(WriteAccess access)
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
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(runtimeType);
        return ValidateQuery(world, in query);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static QueryPlan ValidateQuery(World world, in Query query)
    {
        ArgumentNullException.ThrowIfNull(world);
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
