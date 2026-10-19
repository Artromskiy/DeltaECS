#pragma warning disable CS1591 // Public members are compiler-support API hidden with EditorBrowsable(Never).

namespace Delta.ECS;

using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>Compiler-support contract for a generated query predicate.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedWhereInvoker
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Invoke(ref GeneratedQuerySlots slots);
}

/// <summary>Compiler-support contract for a generated structural query predicate.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedWhereStructuralInvoker
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Execute(
        ref GeneratedQuerySlots slots,
        ref GeneratedWhereStructuralContext context);
}

/// <summary>Compiler-support state for one immediate structural Where chunk.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public struct GeneratedWhereStructuralContext
{
    private readonly World _world;
    private readonly Chunk _sourceChunk;
    private readonly GeneratedWhereStructuralPlan _plan;
    private readonly int _sourceCount;
    private int _writeSlot;
    private int _changedCount;
    private bool _queryPlansDeferred;
    private bool _tagsChanged;
    private bool _completed;

    internal GeneratedWhereStructuralContext(
        World world,
        Chunk sourceChunk,
        in GeneratedWhereStructuralPlan plan,
        int sourceCount)
    {
        _world = world;
        _sourceChunk = sourceChunk;
        _plan = plan;
        _sourceCount = sourceCount;
        _writeSlot = 0;
        _changedCount = 0;
        _queryPlansDeferred = false;
        _tagsChanged = false;
        _completed = false;
    }

    /// <summary>Processes one contiguous predicate run in source order.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ProcessRun(int sourceSlot, int count, bool selected)
    {
        var initializer = new GeneratedWhereNoopInitializer();
        ProcessRun(sourceSlot, count, selected, ref initializer);
    }

    /// <summary>Processes a selected run and initializes newly added values in place.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ProcessRun<TInitializer>(
        int sourceSlot,
        int count,
        bool selected,
        ref TInitializer initializer)
        where TInitializer : struct, IGeneratedComponentValueInitializer
    {
        if (count == 0)
        {
            return;
        }

        if (_plan.IsNoOp)
        {
            _writeSlot = sourceSlot + count;
            return;
        }

        if (selected)
        {
            if (!_plan.IsDestroy)
            {
                _world.RecordComponentMembershipChanges(_sourceChunk, sourceSlot, count, _plan.ComponentIds, _plan.IsAdd);
            }

            if (!_plan.IsDestroy && _plan.TargetArchetype is null)
            {
                int changedTags = _world.ApplyGeneratedWhereTags(
                    _sourceChunk,
                    sourceSlot,
                    count,
                    _plan.TagIndices,
                    _plan.IsAdd);
                _changedCount += changedTags;
                _tagsChanged |= changedTags != 0;
                _writeSlot = sourceSlot + count;
                return;
            }

            if (!_queryPlansDeferred)
            {
                _world.BeginGeneratedWhereMutation(
                    _plan.SourceArchetype,
                    _plan.TargetArchetype);
                _queryPlansDeferred = true;
            }

            if (_plan.IsDestroy)
            {
                _world.FreeGeneratedWhereRun(_sourceChunk, sourceSlot, count);
            }
            else
            {
                bool tagsChanged = _world.CopyGeneratedWhereRun(
                    _sourceChunk,
                    sourceSlot,
                    count,
                    _plan.TargetArchetype!,
                    _plan.SourceToTargetRows,
                    _plan.AddedTargetRows,
                    _plan.TargetCursor!,
                    _plan.TagIndices,
                    _plan.IsAdd,
                    ref initializer);
                _tagsChanged |= tagsChanged;
            }

            _changedCount += count;
            return;
        }

        if (_writeSlot != sourceSlot)
        {
            _world.CopyChunkRangeWithin(_sourceChunk, sourceSlot, _writeSlot, count);
            _world.UpdateChunkRecordSlotsSameChunk(_sourceChunk, _writeSlot, count);
        }

        _writeSlot += count;
    }

    /// <summary>Finishes source compaction and returns the changed row count.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Complete()
    {
        if (_completed)
        {
            return 0;
        }

        _completed = true;
        if (_changedCount == 0)
        {
            return 0;
        }

        if (!_plan.IsDestroy && _plan.TargetArchetype is null)
        {
            if (_tagsChanged)
            {
                _world.MarkGeneratedWhereTagsChanged();
            }

            return _changedCount;
        }

        int appendedCount = _sourceChunk.Count - _sourceCount;
        if (appendedCount != 0 && _writeSlot != _sourceCount)
        {
            _world.CopyChunkRangeWithin(_sourceChunk, _sourceCount, _writeSlot, appendedCount);
            _world.UpdateChunkRecordSlotsSameChunk(_sourceChunk, _writeSlot, appendedCount);
        }

        _world.TrimChunkTail(_sourceChunk, _sourceCount - _writeSlot);
        _plan.SourceArchetype.ReleaseChunk(_sourceChunk.ArchetypeIndex);
        if (_plan.IsDestroy)
        {
            _world.CommitGeneratedWhereDestroy(_changedCount);
        }
        else if (_tagsChanged)
        {
            _world.MarkGeneratedWhereTagsChanged();
        }

        _world.MarkGeneratedWhereAffected(_plan.SourceArchetype);
        if (_plan.TargetArchetype is { } targetArchetype)
        {
            _world.MarkGeneratedWhereAffected(targetArchetype);
        }

        return _changedCount;
    }

    private readonly struct GeneratedWhereNoopInitializer : IGeneratedComponentValueInitializer
    {
        public void Initialize(ref GeneratedComponentValueWriter writer) { }
    }
}

/// <summary>Compiler-support contract for one direct generated parallel chunk invocation.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedParallelInvoker : IGeneratedWhereInvoker
{
    bool RequiresSingleThread { get; }
}

/// <summary>Compiler-support contract for generated multi-component value operations.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedComponentValueInitializer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Initialize(ref GeneratedComponentValueWriter writer);
}

/// <summary>Trusted writer for values written by generated structural operations.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly ref struct GeneratedComponentValueWriter
{
    private readonly ComponentRegistry _layouts;
    private readonly Chunk _targetChunk;
    private readonly Archetype _targetArchetype;
    private readonly int _targetSlotIndex;
    private readonly ReadOnlySpan<int> _addedTargetRows;
    private readonly int _targetCount;
    private readonly bool _initializeExisting;

    internal GeneratedComponentValueWriter(
        ComponentRegistry layouts,
        Chunk targetChunk,
        Archetype targetArchetype,
        int targetSlotIndex,
        ReadOnlySpan<int> addedTargetRows,
        int targetCount = 1,
        bool initializeExisting = false)
    {
        _layouts = layouts;
        _targetChunk = targetChunk;
        _targetArchetype = targetArchetype;
        _targetSlotIndex = targetSlotIndex;
        _addedTargetRows = addedTargetRows;
        _targetCount = targetCount;
        _initializeExisting = initializeExisting;
    }

    internal GeneratedComponentValueWriter(
        ComponentRegistry layouts,
        Chunk targetChunk,
        Archetype targetArchetype,
        int targetSlotIndex)
        : this(layouts, targetChunk, targetArchetype, targetSlotIndex, default)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InitializeAdded<T>(ComponentId componentId, in T value)
    {
        if (_layouts.IsTag(componentId))
        {
            return;
        }

        _targetArchetype.TryGetComponentIndex(componentId, out int componentIndex);
        if (_initializeExisting)
        {
            _targetChunk.GetComponentRow<T>(componentIndex).Slice(_targetSlotIndex, _targetCount).Fill(value);
            return;
        }

        for (int index = 0; index < _addedTargetRows.Length; index++)
        {
            if (_addedTargetRows[index] != componentIndex)
            {
                continue;
            }

            _targetChunk
                .GetComponentRow<T>(componentIndex)
                .Slice(_targetSlotIndex, _targetCount)
                .Fill(value);
            return;
        }
    }

    /// <summary>Initializes a row created together with its archetype.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InitializeCreated<T>(ComponentId componentId, in T value)
    {
        if (_layouts.IsTag(componentId))
        {
            return;
        }

        _targetArchetype.TryGetComponentIndex(componentId, out int componentIndex);
        _targetChunk.GetComponentRef<T>(componentIndex, _targetSlotIndex) = value;
    }

}

/// <summary>Trusted compiler-support execution state for generated write queries.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedDenseExecution
{
    private World? _owner;
    private readonly ReadOnlySpan<ArchetypePlan> _plans;
    private readonly ReadOnlySpan<ChunkPlan> _chunkPlans;
    private readonly ReadOnlySpan<int> _chunkCounts;
    private readonly QueryPlan? _queryPlan;
    private readonly bool _ownsLease;
    private int _chunkIndex;

    /// <summary>Reports whether this execution applies tag filters.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly bool HasTagFilters => _queryPlan?.HasTagFilters ?? false;

    internal GeneratedDenseExecution(
        World owner,
        ReadOnlySpan<ArchetypePlan> plans,
        ReadOnlySpan<ChunkPlan> chunkPlans,
        ReadOnlySpan<int> chunkCounts = default,
        bool ownsLease = true,
        QueryPlan? queryPlan = null)
    {
        _owner = owner;
        _plans = plans;
        _chunkPlans = chunkPlans;
        _chunkCounts = chunkCounts;
        _queryPlan = queryPlan;
        _ownsLease = ownsLease;
        _chunkIndex = -1;
    }

    /// <summary>Returns the selected physical entity slots for the current chunk, when filtered by tags.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryGetTagSlots(out ReadOnlySpan<int> slots)
    {
        if (_queryPlan is not null && (uint)_chunkIndex < (uint)_chunkPlans.Length)
        {
            return _queryPlan.TryGetTagSlots(_chunkPlans.RefAt(_chunkIndex).Chunk, out slots);
        }

        slots = default;
        return false;
    }

    /// <summary>Reports one generated component write before the chunk loop starts.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void RecordComponentWrite(int queryComponentIndex)
    {
        if (_queryPlan is not null)
        {
            GeneratedForEachRuntime.RecordComponentWrite(_queryPlan, queryComponentIndex);
        }
    }

    /// <summary>Reports generated component writes before the chunk loop starts.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void RecordComponentWrites(scoped ReadOnlySpan<int> queryComponentIndices)
    {
        if (_queryPlan is not null)
        {
            GeneratedForEachRuntime.RecordComponentWrites(_queryPlan, queryComponentIndices);
        }
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
            ref readonly ChunkPlan chunkPlan = ref _chunkPlans.RefAt(_chunkIndex);
            int count = _chunkCounts.IsEmpty ? chunkPlan.Chunk.Count : _chunkCounts.RefAt(_chunkIndex);
            slots = new GeneratedQuerySlots(_owner!, in chunkPlan, count, _queryPlan);
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

        if (_ownsLease)
        {
            owner.EndQueryLease();
        }

        _owner = null;
    }
}

/// <summary>Trusted compiler-support execution state for generated read queries.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedReadDenseExecution
{
    private World? _owner;
    private readonly ReadOnlySpan<ChunkPlan> _chunkPlans;
    private readonly QueryPlan? _queryPlan;
    private int _chunkIndex;

    /// <summary>Reports whether this execution applies tag filters.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly bool HasTagFilters => _queryPlan?.HasTagFilters ?? false;

    internal GeneratedReadDenseExecution(World owner, ReadOnlySpan<ChunkPlan> chunkPlans, QueryPlan? queryPlan = null)
    {
        _owner = owner;
        _chunkPlans = chunkPlans;
        _queryPlan = queryPlan;
        _chunkIndex = -1;
    }

    /// <summary>Returns the selected physical entity slots for the current chunk, when filtered by tags.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryGetTagSlots(out ReadOnlySpan<int> slots)
    {
        if (_queryPlan is not null && (uint)_chunkIndex < (uint)_chunkPlans.Length)
        {
            return _queryPlan.TryGetTagSlots(_chunkPlans.RefAt(_chunkIndex).Chunk, out slots);
        }

        slots = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNextTrusted(out GeneratedReadQuerySlots slots)
    {
        int nextChunk = _chunkIndex + 1;
        if ((uint)nextChunk < (uint)_chunkPlans.Length)
        {
            _chunkIndex = nextChunk;
            slots = new GeneratedReadQuerySlots(_owner!, in _chunkPlans.RefAt(_chunkIndex), _queryPlan);
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

/// <summary>Non-generic runtime services consumed by generated ForEach code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static partial class GeneratedForEachRuntime
{

    /// <summary>Gets the first entity reference for generated iteration.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref Entity GetGeneratedEntityReference(ref GeneratedQuerySlots slots)
        => ref slots.GetGeneratedEntityReference();

    /// <summary>Gets the first entity reference for generated read-only iteration.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref Entity GetGeneratedEntityReference(ref GeneratedReadQuerySlots slots)
        => ref Unsafe.AsRef(in slots.GetGeneratedEntityReference());

    /// <summary>Gets the first tag slot for generated iteration.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref int GetGeneratedTagSlotReference(ReadOnlySpan<int> slots)
        => ref Unsafe.AsRef(in MemoryMarshal.GetReference(slots));

    /// <summary>Creates a reusable entity view for a generated entity iteration.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityRef CreateEntityRef(World world, ref GeneratedEntityRefView view)
    {
        view.SetWorld(world);
        return new EntityRef(ref view);
    }

    /// <summary>Executes one generated multi-component value add without an intermediate transition.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExecuteGeneratedAdd<TInitializer>(
        World world,
        Entity entity,
        ReadOnlySpan<ComponentId> componentIds,
        ref TInitializer initializer)
        where TInitializer : struct, IGeneratedComponentValueInitializer
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        return world.AddGeneratedComponentValues(entity, componentIds, ref initializer);
    }

    /// <summary>Executes one generated multi-component value add for an entity batch.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ExecuteGeneratedAdd<TInitializer>(
        World world,
        ReadOnlySpan<Entity> entities,
        ReadOnlySpan<ComponentId> componentIds,
        ref TInitializer initializer)
        where TInitializer : struct, IGeneratedComponentValueInitializer
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        return world.AddGeneratedComponentValues(entities, componentIds, ref initializer);
    }

    /// <summary>Validates that a registration uses the generated component type.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateComponentType<T>(World world, ComponentId componentId)
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        world.ValidateGeneratedComponentType<T>(componentId);
    }

    /// <summary>Checks whether a registration uses the generated component type.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsComponentType<T>(World world, ComponentId componentId)
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        return world.IsGeneratedComponentType<T>(componentId);
    }

    /// <summary>Validates that a query registration uses the generated component type.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateComponentType<T>(in Query query, ComponentId componentId)
    {
        if (!query.IsValid)
        {
            ThrowHelper.ThrowInvalidEntityQueryHandle();
        }

        query.Owner.ValidateGeneratedComponentType<T>(componentId);
    }

    /// <summary>Validates a dynamic registration list against a generated component arity.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateComponentIdCount(ReadOnlySpan<ComponentId> componentIds, int expected)
    {
        if (componentIds.Length != expected)
        {
            ThrowHelper.ThrowComponentIdCountMismatch(nameof(componentIds), expected, componentIds.Length);
        }
    }

    /// <summary>Creates one entity and initializes its generated component values.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity ExecuteGeneratedCreate<TInitializer>(
        World world,
        ReadOnlySpan<ComponentId> componentIds,
        ref TInitializer initializer)
        where TInitializer : struct, IGeneratedComponentValueInitializer
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        return world.CreateGeneratedComponentValues(componentIds, ref initializer);
    }

    /// <summary>Validates a generated callback without requiring a modern BCL.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull([NotNull] object? value, string parameterName)
        => ThrowHelper.ThrowIfNull(value, parameterName);

    /// <summary>Gets the first element of a validated generated component row.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetGeneratedRow<T>(Array[] componentRows, int queryComponentIndex)
    {
        if (QueryPlan.IsTagRoute(queryComponentIndex))
        {
            return ref GeneratedTagRows.GetReference<T>(0);
        }

        return ref Unsafe.As<T[]>(componentRows.RefAt(queryComponentIndex)).GetRefAtZero();
    }

    /// <summary>Validates the world/query pair used by a generated mutation view.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateGeneratedWhere(World world, in Query query) => _ = ValidateQuery(world, in query);

    /// <summary>Composes a generated typed query with an additional query specification.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Query ComposeGeneratedQuery(in Query query, QuerySpec additions)
    {
        if (!query.IsValid)
        {
            ThrowHelper.ThrowInvalidEntityQueryHandle();
        }

        QuerySpec composed = query.Description.Compose(in additions);
        return query.Owner.CreateQuery(in composed);
    }

    /// <summary>Resolves and caches an ordered primary component set for generated code.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<ComponentId> GetGeneratedPrimaryComponentIds<TKey>(
        World world,
        Func<World, ComponentId[]> resolver)
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        ThrowHelper.ThrowIfNull(resolver, nameof(resolver));
        return world.GetOrCreateComponentSet(typeof(TKey).TypeHandle, resolver).ComponentIds;
    }

    /// <summary>Resolves and caches an ordered primary component set owned by a generated query.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<ComponentId> GetGeneratedPrimaryComponentIds<TKey>(
        in Query query,
        Func<World, ComponentId[]> resolver)
    {
        if (!query.IsValid)
        {
            ThrowHelper.ThrowInvalidEntityQueryHandle();
        }

        ThrowHelper.ThrowIfNull(resolver, nameof(resolver));
        return query.Owner.GetOrCreateComponentSet(typeof(TKey).TypeHandle, resolver).ComponentIds;
    }

    /// <summary>Executes a generated predicate and immediately destroys its matches.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ExecuteGeneratedWhereDestroy<TInvoker>(
        World world,
        in Query query,
        ref TInvoker invoker,
        scoped ReadOnlySpan<int> writeComponentIndices)
        where TInvoker : struct, IGeneratedWhereStructuralInvoker
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        return ExecuteGeneratedWhereStructural(
            world,
            in query,
            ref invoker,
            writeComponentIndices,
            isDestroy: true,
            isAdd: false,
            componentIds: default);
    }

    /// <summary>Executes a generated predicate and immediately adds components to its matches.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ExecuteGeneratedWhereAdd<TInvoker>(
        World world,
        in Query query,
        ref TInvoker invoker,
        scoped ReadOnlySpan<int> writeComponentIndices,
        scoped ReadOnlySpan<ComponentId> componentIds)
        where TInvoker : struct, IGeneratedWhereStructuralInvoker
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        return ExecuteGeneratedWhereStructural(
            world,
            in query,
            ref invoker,
            writeComponentIndices,
            isDestroy: false,
            isAdd: true,
            componentIds);
    }

    /// <summary>Executes a generated predicate and immediately removes components from its matches.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ExecuteGeneratedWhereRemove<TInvoker>(
        World world,
        in Query query,
        ref TInvoker invoker,
        scoped ReadOnlySpan<int> writeComponentIndices,
        scoped ReadOnlySpan<ComponentId> componentIds)
        where TInvoker : struct, IGeneratedWhereStructuralInvoker
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        return ExecuteGeneratedWhereStructural(
            world,
            in query,
            ref invoker,
            writeComponentIndices,
            isDestroy: false,
            isAdd: false,
            componentIds);
    }

    /// <summary>Executes a generated predicate and a terminal callback while the query lease is active.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ExecuteGeneratedWhereForEach<TInvoker>(
        World world,
        in Query query,
        ref TInvoker invoker,
        scoped ReadOnlySpan<int> writeComponentIndices)
        where TInvoker : struct, IGeneratedWhereInvoker
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        QueryPlan plan = ValidateQuery(world, in query);
        ExecuteGeneratedWhereForEach(world, plan, ref invoker, writeComponentIndices);
    }

    /// <summary>Executes a generated callback for the alive entities selected by a caller-owned list.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ExecuteEntityList<TInvoker>(
        World world,
        in Query query,
        ReadOnlySpan<Entity> entities,
        ref TInvoker invoker,
        scoped ReadOnlySpan<int> writeComponentIndices)
        where TInvoker : struct, IGeneratedParallelInvoker
    {
        QueryPlan plan = ValidateQuery(world, in query);
        ExecuteEntityList(world, plan, entities, ref invoker, writeComponentIndices);
    }

    /// <summary>Executes a generated callback for a caller-owned entity list in parallel.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ExecuteEntityListParallel<TInvoker>(
        World world,
        in Query query,
        ReadOnlySpan<Entity> entities,
        ref TInvoker invoker,
        scoped ReadOnlySpan<int> writeComponentIndices,
        int requestedWorkerCount = 0)
        where TInvoker : struct, IGeneratedParallelInvoker
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        QueryPlan plan = ValidateQuery(world, in query);
        ThrowHelper.ThrowIfNegative(requestedWorkerCount, nameof(requestedWorkerCount));
        if (plan.MatchingChunkPlans().Length <= 1)
        {
            ExecuteEntityList(world, plan, entities, ref invoker, writeComponentIndices);
            return;
        }

        RecordEntityListWrites(world, plan, entities, writeComponentIndices);
        world.PrepareComponentChangeObserverChunks(plan.MatchingChunkPlans());
        plan.PrepareTagSlotCache();
        var executor = world.GetParallelQueryExecutor<TInvoker>();
        world.EnterParallelExecution();
        world.BeginQueryLease();
        try
        {
            executor.ExecuteEntityList(
                world,
                plan,
                entities,
                ref invoker,
                requestedWorkerCount);
        }
        finally
        {
            world.ExitParallelExecution();
            world.EndQueryLease();
        }
    }

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
        ThrowHelper.ThrowIfNull(world, nameof(world));
        QueryPlan plan = ValidateQuery(world, in query);
        ThrowHelper.ThrowIfNegative(requestedWorkerCount, nameof(requestedWorkerCount));
        if (plan.MatchingChunkPlans().Length <= 1)
        {
            ExecuteGeneratedWhereForEach(world, plan, ref invoker, writeComponentIndices);
            return;
        }

        RecordComponentWrites(plan, writeComponentIndices);
        world.PrepareComponentChangeObserverChunks(plan.MatchingChunkPlans());
        plan.PrepareTagSlotCache();
        var executor = world.GetParallelQueryExecutor<TInvoker>();
        world.EnterParallelExecution();
        world.BeginQueryLease();
        try
        {
            executor.Execute(
                plan,
                ref invoker,
                requestedWorkerCount);
        }
        finally
        {
            world.ExitParallelExecution();
            world.EndQueryLease();
        }
    }

    /// <summary>Opens the trusted dense execution used by generated callbacks.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GeneratedDenseExecution OpenDense(World world, in Query query)
    {
        QueryPlan plan = ValidateQuery(world, in query);
        ReadOnlySpan<ArchetypePlan> plans = plan.MatchingPlans();
        ReadOnlySpan<ChunkPlan> chunks = plan.MatchingChunkPlans();
        world.BeginQueryLease();
        return new GeneratedDenseExecution(world, plans, chunks, queryPlan: plan);
    }

    /// <summary>Opens a validated read-only dense execution without write state.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GeneratedReadDenseExecution OpenReadDense(World world, in Query query)
    {
        QueryPlan plan = ValidateQuery(world, in query);
        ReadOnlySpan<ChunkPlan> chunks = plan.MatchingChunkPlans();
        world.BeginQueryLease();
        return new GeneratedReadDenseExecution(world, chunks, plan);
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
        return new GeneratedDenseExecution(world, plans, chunks, queryPlan: plan);
    }

    /// <summary>Returns a cached primary read access using the generated component type.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadAccess GetPreparedReadAccess<T>(in Query query) => query.Cached.GetPreparedPrimaryReadAccess<T>();

    /// <summary>Returns a cached primary read route without materializing an access token.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPreparedReadRoute<T>(in Query query) => query.Cached.GetPreparedPrimaryReadRoute<T>();

    /// <summary>Returns a cached explicit-component read route after sequence validation.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPreparedReadRoute<T>(in Query query, ComponentId component)
        => query.Cached.GetPreparedReadAccess(component, typeof(T)).QueryComponentIndex;

    /// <summary>Returns a cached primary write access using the generated component type.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WriteAccess GetPreparedWriteAccess<T>(in Query query)
        => query.Cached.GetPreparedPrimaryWriteAccess<T>();

    /// <summary>Returns a cached primary write route without materializing an access token.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPreparedWriteRoute<T>(in Query query) => query.Cached.GetPreparedPrimaryWriteRoute<T>();

    /// <summary>Returns a cached explicit-component write route after sequence validation.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPreparedWriteRoute<T>(in Query query, ComponentId component)
        => query.Cached.GetPreparedWriteAccess(component, typeof(T)).QueryComponentIndex;

    /// <summary>Returns the trusted query-local component route used for write tracking.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetWriteQueryComponentIndex(WriteAccess access)
        => access.QueryComponentIndex;

    /// <summary>Returns a trusted query-local route used by generated parallel invokers.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetReadQueryComponentIndex(ReadAccess access) => access.QueryComponentIndex;

    /// <summary>
    /// Returns a cached explicit-component read access after dense scope
    /// validation. The component/type contract remains checked by the plan.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadAccess GetPreparedReadAccess(in Query query, ComponentId component, Type runtimeType)
        => query.Cached.GetPreparedReadAccess(component, runtimeType);

    /// <summary>
    /// Returns a cached explicit-component write access after dense scope
    /// validation. The component/type contract remains checked by the plan.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WriteAccess GetPreparedWriteAccess(in Query query, ComponentId component, Type runtimeType)
        => query.Cached.GetPreparedWriteAccess(component, runtimeType);

    internal static void RecordComponentWrites(QueryPlan plan, scoped ReadOnlySpan<int> routes)
    {
        if (routes.IsEmpty || !plan.Owner.HasComponentChangeObserverRoutes)
        {
            return;
        }

        if (routes.Length == 1)
        {
            RecordComponentWrite(plan, routes[0]);
            return;
        }

        World world = plan.Owner;
        ComponentChangeWriteRoute[] writeRoutes = ArrayPool<ComponentChangeWriteRoute>.Shared.Rent(routes.Length);
        int writeRouteCount = 0;
        try
        {
            for (int routeIndex = 0; routeIndex < routes.Length; routeIndex++)
            {
                int route = routes.RefAt(routeIndex);
                bool isTag = QueryPlan.IsTagRoute(route);
                ComponentId componentId = isTag ? QueryPlan.GetTagComponentId(route) : plan.GetDataComponentId(route);
                int tagIndex = isTag ? world.Components.GetTagIndex(componentId) : -1;
                ComponentChangeObserver[]? observers = world.ResolveComponentChangeObservers(componentId, out int observerCount);
                if (observers is null)
                {
                    continue;
                }
                writeRoutes[writeRouteCount++] = new ComponentChangeWriteRoute
                {
                    Observers = observers,
                    ObserverCount = observerCount,
                    TagIndex = tagIndex
                };
            }

            if (writeRouteCount == 0)
            {
                return;
            }

            ReadOnlySpan<ChunkPlan> chunks = plan.MatchingChunkPlans();
            Span<ulong> writeMask = stackalloc ulong[Chunk.WordCount];
            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                Chunk chunk = chunks.RefAt(chunkIndex).Chunk;
                bool hasSelection = plan.TryGetTagMask(chunk, out ReadOnlySpan<ulong> selectionMask);
                for (int routeIndex = 0; routeIndex < writeRouteCount; routeIndex++)
                {
                    ref ComponentChangeWriteRoute writeRoute = ref writeRoutes[routeIndex];
                    RecordComponentWriteChunk(chunk, writeRoute.Observers!, writeRoute.ObserverCount,
                        writeRoute.TagIndex, hasSelection, selectionMask, writeMask);
                }
            }
        }
        finally
        {
            for (int routeIndex = 0; routeIndex < writeRouteCount; routeIndex++)
            {
                ref ComponentChangeWriteRoute writeRoute = ref writeRoutes[routeIndex];
                World.ReturnComponentChangeObservers(writeRoute.Observers!, writeRoute.ObserverCount);
                writeRoute.Observers = null;
            }

            ArrayPool<ComponentChangeWriteRoute>.Shared.Return(writeRoutes);
        }
    }

    internal static void RecordComponentWrite(QueryPlan plan, int route)
    {
        World world = plan.Owner;
        if (!world.HasComponentChangeObserverRoutes)
        {
            return;
        }

        bool isTag = QueryPlan.IsTagRoute(route);
        ComponentId componentId = isTag ? QueryPlan.GetTagComponentId(route) : plan.GetDataComponentId(route);
        int tagIndex = isTag ? world.Components.GetTagIndex(componentId) : -1;
        ComponentChangeObserver[]? observers = world.ResolveComponentChangeObservers(componentId, out int observerCount);
        if (observers is null)
        {
            return;
        }

        try
        {
            ReadOnlySpan<ChunkPlan> chunks = plan.MatchingChunkPlans();
            Span<ulong> writeMask = stackalloc ulong[Chunk.WordCount];
            for (int i = 0; i < chunks.Length; i++)
            {
                RecordComponentWriteChunk(plan, chunks.RefAt(i).Chunk,
                    observers, observerCount, tagIndex, writeMask);
            }
        }
        finally
        {
            World.ReturnComponentChangeObservers(observers, observerCount);
        }
    }

    internal static void RecordComponentWriteChunk(
        QueryPlan plan,
        Chunk chunk,
        ComponentChangeObserver[] observers,
        int observerCount,
        int tagIndex,
        Span<ulong> writeMask)
    {
        bool hasSelection = plan.TryGetTagMask(chunk, out ReadOnlySpan<ulong> selectionMask);
        RecordComponentWriteChunk(chunk, observers, observerCount, tagIndex, hasSelection, selectionMask, writeMask);
    }

    private static void RecordComponentWriteChunk(
        Chunk chunk,
        ComponentChangeObserver[] observers,
        int observerCount,
        int tagIndex,
        bool hasSelection,
        ReadOnlySpan<ulong> selectionMask,
        Span<ulong> writeMask)
    {
        if (tagIndex < 0 && !hasSelection)
        {
            for (int observerIndex = 0; observerIndex < observerCount; observerIndex++)
            {
                observers[observerIndex].RecordChunk(chunk);
            }

            return;
        }

        int wordCount = ComponentChangeMasks.WordCount(chunk.Count);
        ReadOnlySpan<ulong> mask = selectionMask;
        DirtyKind kind;
        if (tagIndex < 0)
        {
            kind = ComponentChangeMasks.Classify(mask, chunk.Count);
        }
        else
        {
            kind = ComponentChangeMasks.SelectTagChanges(chunk, tagIndex,
                hasSelection ? DirtyKind.Mask : DirtyKind.All, selectionMask, writeMask);
            mask = writeMask.Slice(0, wordCount);
        }

        if (kind == DirtyKind.None)
        {
            return;
        }

        if (kind == DirtyKind.All)
        {
            for (int observerIndex = 0; observerIndex < observerCount; observerIndex++)
            {
                observers[observerIndex].RecordChunk(chunk);
            }
            return;
        }

        for (int observerIndex = 0; observerIndex < observerCount; observerIndex++)
        {
            observers[observerIndex].RecordChunkMask(chunk, mask, kind);
        }
    }

    private static void ExecuteGeneratedWhereForEach<TInvoker>(
        World world,
        QueryPlan plan,
        ref TInvoker invoker,
        scoped ReadOnlySpan<int> writeComponentIndices)
        where TInvoker : struct, IGeneratedWhereInvoker
    {
        world.BeginQueryLease();
        var execution = new GeneratedDenseExecution(
            world,
            plan.MatchingPlans(),
            plan.MatchingChunkPlans(),
            ownsLease: true,
            queryPlan: plan);
        try
        {
            execution.RecordComponentWrites(writeComponentIndices);
            TInvoker invocation = invoker;
            while (execution.MoveNextTrusted(out var slots))
            {
                invocation.Invoke(ref slots);
            }

            invoker = invocation;
        }
        finally
        {
            execution.Dispose();
        }
    }

    private static void ExecuteEntityList<TInvoker>(
        World world,
        QueryPlan plan,
        ReadOnlySpan<Entity> entities,
        ref TInvoker invoker,
        scoped ReadOnlySpan<int> writeComponentIndices)
        where TInvoker : struct, IGeneratedParallelInvoker
    {
        RecordEntityListWrites(world, plan, entities, writeComponentIndices);
        TInvoker invocation = invoker;
        world.BeginQueryLease();
        try
        {
            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities.RefAt(index);
                if (!world.TryResolveEntityLocation(entity, out Chunk chunk, out int slot)
                    || !plan.TryGetChunkPlan(chunk.ArchetypeId, chunk, out ChunkPlan chunkPlan)
                    || !plan.MatchesTagSlot(chunk, slot))
                {
                    continue;
                }

                var slots = new GeneratedQuerySlots(world, in chunkPlan, 1, slot, plan, tagFiltersAlreadyMatched: true);
                invocation.Invoke(ref slots);
            }

            invoker = invocation;
        }
        finally
        {
            world.EndQueryLease();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ExecuteGeneratedWhereStructural<TInvoker>(
        World world,
        in Query query,
        ref TInvoker invoker,
        scoped ReadOnlySpan<int> writeComponentIndices,
        bool isDestroy,
        bool isAdd,
        scoped ReadOnlySpan<ComponentId> componentIds)
        where TInvoker : struct, IGeneratedWhereStructuralInvoker
    {
        if (!world.BeginGeneratedWhereStructural(
                in query,
                isDestroy,
                isAdd,
                componentIds,
                out GeneratedDenseExecution execution))
        {
            return 0;
        }

        int changed = 0;
        TInvoker invocation = invoker;
        try
        {
            execution.RecordComponentWrites(writeComponentIndices);
            while (execution.MoveNextTrusted(out var slots))
            {
                GeneratedWhereStructuralContext context = world.BeginGeneratedWhereChunk(
                    slots.ChunkId,
                    slots.PhysicalCount);
                invocation.Execute(ref slots, ref context);
                changed += context.Complete();
            }

            invoker = invocation;
        }
        finally
        {
            execution.Dispose();
            world.EndGeneratedWhereStructural();
        }

        return changed;
    }

    private static void RecordEntityListWrites(
        World world,
        QueryPlan plan,
        ReadOnlySpan<Entity> entities,
        ReadOnlySpan<int> routes)
    {
        if (entities.IsEmpty || routes.IsEmpty || !world.HasComponentChangeObserverRoutes)
        {
            return;
        }

        ComponentChangeWriteRoute[] active = ArrayPool<ComponentChangeWriteRoute>.Shared.Rent(routes.Length);
        int routeCount = 0;
        try
        {
            for (int routeIndex = 0; routeIndex < routes.Length; routeIndex++)
            {
                int route = routes.RefAt(routeIndex);
                bool isTag = QueryPlan.IsTagRoute(route);
                ComponentId id = isTag ? QueryPlan.GetTagComponentId(route) : plan.GetDataComponentId(route);
                int tagIndex = isTag ? world.Components.GetTagIndex(id) : -1;
                ComponentChangeObserver[]? observers = world.ResolveComponentChangeObservers(id, out int observerCount);
                if (observers is null)
                {
                    continue;
                }

                active[routeCount++] = new ComponentChangeWriteRoute
                {
                    Observers = observers,
                    ObserverCount = observerCount,
                    TagIndex = tagIndex
                };
            }

            // Resolve each entity, matching chunk and tag filter only once for all write routes.
            for (int entityIndex = 0; entityIndex < entities.Length && routeCount != 0; entityIndex++)
            {
                Entity entity = entities.RefAt(entityIndex);
                if (!world.TryResolveEntityLocation(entity, out Chunk chunk, out int slotIndex)
                    || !plan.TryGetChunkPlan(chunk.ArchetypeId, chunk, out _)
                    || !plan.MatchesTagSlot(chunk, slotIndex))
                {
                    continue;
                }

                for (int routeIndex = 0; routeIndex < routeCount; routeIndex++)
                {
                    ref ComponentChangeWriteRoute route = ref active[routeIndex];
                    if (route.TagIndex >= 0 && !chunk.HasTag(route.TagIndex, slotIndex))
                    {
                        continue;
                    }

                    World.RecordComponentChangeObservers(route.Observers!, route.ObserverCount, chunk, slotIndex);
                }
            }
        }
        finally
        {
            for (int index = 0; index < routeCount; index++)
            {
                ref ComponentChangeWriteRoute route = ref active[index];
                World.ReturnComponentChangeObservers(route.Observers!, route.ObserverCount);
                route.Observers = null;
            }

            ArrayPool<ComponentChangeWriteRoute>.Shared.Return(active);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static QueryPlan ValidateQuery(World world, in Query query)
    {
        ThrowHelper.ThrowIfNull(world, nameof(world));
        world.EnsureExecutionAccess();
        if (!ReferenceEquals(query.Owner, world) || !query.IsValid)
        {
            ThrowHelper.ThrowInvalidQuery(nameof(query));
        }

        return query.Cached;
    }
    private struct ComponentChangeWriteRoute
    {
        internal ComponentChangeObserver[]? Observers;
        internal int ObserverCount;
        internal int TagIndex;
    }

}

internal sealed class GeneratedWhereTargetCursor
{
    internal Chunk? Chunk;
    internal int Slot;
    internal int Remaining;

    internal void Reset()
    {
        Chunk = null;
        Slot = 0;
        Remaining = 0;
    }
}

internal readonly struct GeneratedWhereStructuralPlan
{

    internal Archetype SourceArchetype { get; }
    internal Archetype? TargetArchetype { get; }
    internal int[] SourceToTargetRows { get; }
    internal int[] AddedTargetRows { get; }
    internal GeneratedWhereTargetCursor? TargetCursor { get; }
    internal ComponentId[] ComponentIds { get; }
    internal int[] TagIndices { get; }
    internal bool IsAdd { get; }
    internal bool IsDestroy { get; }

    internal bool IsNoOp => !IsDestroy && TargetArchetype is null && TagIndices.Length == 0;
    internal GeneratedWhereStructuralPlan(
        Archetype sourceArchetype,
        Archetype? targetArchetype,
        int[] sourceToTargetRows,
        int[] addedTargetRows,
        GeneratedWhereTargetCursor? targetCursor,
        ComponentId[] componentIds,
        int[] tagIndices,
        bool isAdd,
        bool isDestroy)
    {
        SourceArchetype = sourceArchetype;
        TargetArchetype = targetArchetype;
        SourceToTargetRows = sourceToTargetRows;
        AddedTargetRows = addedTargetRows;
        TargetCursor = targetCursor;
        ComponentIds = componentIds;
        TagIndices = tagIndices;
        IsAdd = isAdd;
        IsDestroy = isDestroy;
    }
}
