namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public sealed partial class World
{
    private readonly Dictionary<GroupedArchetypeKey, Archetype> _groupedArchetypes = new();
    private readonly Dictionary<(int Source, int Target), TransitionEdge> _groupingTransitions = new();
    private readonly List<Chunk> _dirtyGroupingChunks = new();
    private bool _groupingDirty;
    private bool _resolvingGroups;

    /// <summary>Requires the primary grouping component to equal the supplied value.</summary>
    public Query WhereAll<T>(in T value) where T : struct
        => CreateQuery(CreateGroupFilter(GetPrimaryComponentId<T>(), in value, GroupMatchKind.All));

    /// <summary>Matches the supplied value of a primary grouping component.</summary>
    public Query WhereAny<T>(in T value) where T : struct
        => CreateQuery(CreateGroupFilter(GetPrimaryComponentId<T>(), in value, GroupMatchKind.Any));

    /// <summary>Excludes the supplied value of a primary grouping component.</summary>
    public Query WhereNone<T>(in T value) where T : struct
        => CreateQuery(CreateGroupFilter(GetPrimaryComponentId<T>(), in value, GroupMatchKind.None));

    internal QuerySpec CreateGroupFilter<T>(ComponentId componentId, in T value, GroupMatchKind kind) where T : struct
    {
        if (_layouts.GetGrouping(componentId) is not ComponentGrouping<T> grouping)
        {
            return ThrowHelper.ThrowComponentNotGrouped<QuerySpec>(componentId);
        }
        QuerySpec spec = kind switch
        {
            GroupMatchKind.All => QuerySpec.WhereAll(componentId),
            GroupMatchKind.Any => QuerySpec.WhereAny(componentId),
            _ => QuerySpec.WhereNone(componentId)
        };
        if (_layouts.IsTag(componentId))
        {
            return spec;
        }
        return (kind == GroupMatchKind.All ? spec : QuerySpec.Empty)
            .WithGroup(new GroupFilter(grouping, grouping.GetValueId(in value), kind));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void PrepareOperation()
    {
        EnsureExecutionAccess();
        if (_groupingDirty && _activeChunkLeases == 0 && !_queryPlanBatchActive && !_generatedWhereStructuralActive && !_orderedQueryOperationActive && !_resolvingGroups)
        {
            ResolveGroupedChanges();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkGroupingDirty(Chunk chunk)
    {
        if (!_resolvingGroups && chunk.Archetype.GroupingRows.Length != 0)
        {
            chunk.GroupingDirty = true;
            _groupingDirty = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkGroupingWrite(Chunk chunk, ComponentId id)
    {
        if (_layouts.GetGrouping(id) is not null)
        {
            MarkGroupingDirty(chunk);
        }
    }

    internal void RecordGroupedComponentWrites(QueryPlan plan, ReadOnlySpan<int> routes)
    {
        if (!_layouts.HasGrouping)
        {
            return;
        }
        bool writesGrouping = false;
        foreach (int route in routes)
        {
            if (!QueryPlan.IsTagRoute(route) && _layouts.GetGrouping(plan.GetDataComponentId(route)) is not null)
            {
                writesGrouping = true;
                break;
            }
        }
        if (!writesGrouping)
        {
            return;
        }
        foreach (ref readonly ChunkPlan chunk in plan.MatchingChunkPlans())
        {
            if (!plan.TryGetTagSlots(chunk.Chunk, out ReadOnlySpan<int> slots) || !slots.IsEmpty)
            {
                MarkGroupingDirty(chunk.Chunk);
            }
        }
    }

    internal void RecordGroupedEntityListWrites(QueryPlan plan, ReadOnlySpan<Entity> entities, ReadOnlySpan<int> routes)
    {
        if (!_layouts.HasGrouping)
        {
            return;
        }
        foreach (int route in routes)
        {
            if (QueryPlan.IsTagRoute(route) || _layouts.GetGrouping(plan.GetDataComponentId(route)) is null)
            {
                continue;
            }
            foreach (Entity entity in entities)
            {
                if (TryResolveAliveLocation(entity, out Chunk chunk, out int slot)
                    && plan.MatchesArchetype(chunk.Archetype) && plan.MatchesTagSlot(chunk, slot))
                {
                    MarkGroupingDirty(chunk);
                }
            }
            return;
        }
    }

    private void ResolveGroupedChanges()
    {
        _dirtyGroupingChunks.Clear();
        foreach (Chunk? chunk in _chunksById)
        {
            if (chunk is not null && chunk.GroupingDirty)
            {
                _dirtyGroupingChunks.Add(chunk);
            }
        }

        _resolvingGroups = true;
        BeginQueryPlanBatch();
        // Equality/hash implementations are user code. Retain pending work and release batch state if they fail.
        try
        {
            foreach (Chunk chunk in _dirtyGroupingChunks)
            {
                ResolveGroupedChunk(chunk);
                chunk.GroupingDirty = false;
            }
            _groupingDirty = false;
        }
        finally
        {
            EndQueryPlanBatch();
            _resolvingGroups = false;
            _dirtyGroupingChunks.Clear();
        }
    }

    private Archetype GetTransitionArchetype(Archetype source, ComponentMask mask)
    {
        if (source.GroupingRows.Length == 0)
        {
            return GetOrCreateArchetype(mask);
        }

        var componentIds = new ComponentId[mask.Count];
        mask.CopyComponentIds(componentIds);
        var values = new List<int>();
        foreach (ComponentId id in componentIds)
        {
            ComponentGrouping? grouping = _layouts.GetGrouping(id);
            if (grouping is null)
            {
                continue;
            }
            int valueId = 0;
            foreach (GroupedComponentRow row in source.GroupingRows)
            {
                if (ReferenceEquals(row.Grouping, grouping))
                {
                    valueId = row.ValueId;
                    break;
                }
            }
            values.Add(valueId);
        }
        return GetOrCreateArchetype(mask, values.Count == 0 ? null : values.ToArray());
    }

    private void ResolveGroupedChunk(Chunk chunk)
    {
        Archetype source = chunk.Archetype;
        GroupedComponentRow[] groups = source.GroupingRows;
        int[] values = source.GroupingScratch;
        // Descending slots remain valid while MoveEntity swap-removes each selected entity.
        for (int slot = chunk.Count - 1; slot >= 0; slot--)
        {
            bool changed = false;
            for (int index = 0; index < groups.Length; index++)
            {
                GroupedComponentRow group = groups[index];
                Array row = chunk.RawComponentRows[group.Row];
                bool matches = group.Grouping.Matches(row, slot, group.ValueId);
                values[index] = matches ? group.ValueId : group.Grouping.GetValueId(row, slot);
                changed |= !matches;
            }
            if (!changed)
            {
                continue;
            }

            Archetype target = GetOrCreateArchetype(source.Mask, values);
            if (!_groupingTransitions.TryGetValue((source.Id, target.Id), out TransitionEdge edge))
            {
                int[] rows = new int[source.ComponentCount];
                for (int index = 0; index < rows.Length; index++)
                {
                    rows[index] = index;
                }
                edge = new TransitionEdge(target.Id, rows, []);
                _groupingTransitions.Add((source.Id, target.Id), edge);
            }
            ReadOnlySpan<Entity> entities = chunk.RawEntities.Slice(slot, 1);
            for (int index = 0; index < groups.Length; index++)
            {
                GroupedComponentRow group = groups[index];
                if (group.ValueId != values[index])
                {
                    RecordFilterMembershipRange(group.Grouping.ComponentId, entities);
                    if (_componentChangeObservers is not null)
                    {
                        RecordComponentMembershipRange(_componentChangeObservers, group.Grouping.ComponentId, entities, true);
                    }
                }
            }
            MoveEntity(entities[0].Index, edge);
        }
    }
}
