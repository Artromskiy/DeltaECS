namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

public sealed partial class World
{
    /// <summary>Creates a non-owning ordered view over explicit entity candidates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntitySequence From(ReadOnlySpan<Entity> entities) => new(this, entities);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExecuteSequence(ReadOnlySpan<Entity> entities, ForEachEntityAction action)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        ExecuteUnfilteredSequenceCore(entities, action);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExecuteSequence(ReadOnlySpan<Entity> entities, in Query query, ForEachEntityAction action)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        ValidateQuery(in query);
        ExecuteFilteredSequenceCore(entities, query.Cached, action);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExecuteSequence<TContext>(ReadOnlySpan<Entity> entities, ref TContext context, ForEachContextEntityAction<TContext> action)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        ExecuteUnfilteredSequenceCore(entities, ref context, action);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExecuteSequence<TContext>(ReadOnlySpan<Entity> entities, in Query query, ref TContext context, ForEachContextEntityAction<TContext> action)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        ValidateQuery(in query);
        ExecuteFilteredSequenceCore(entities, query.Cached, ref context, action);
    }

    private void ExecuteUnfilteredSequenceCore(
        ReadOnlySpan<Entity> entities,
        ForEachEntityAction action)
    {
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities.RefAt(index);
            if (TryResolve(entity, out _))
            {
                action(entity);
            }
        }
    }

    private void ExecuteFilteredSequenceCore(
        ReadOnlySpan<Entity> entities,
        QueryPlan plan,
        ForEachEntityAction action)
    {
        int lastArchetype = -1;
        bool lastMatches = false;
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities.RefAt(index);
            if (!TryResolve(entity, out int recordIndex))
            {
                continue;
            }

            ref readonly var record = ref RecordAt(recordIndex);
            if (record.Archetype != lastArchetype)
            {
                lastArchetype = record.Archetype;
                lastMatches = plan.MatchesArchetype(lastArchetype);
            }

            if (lastMatches)
            {
                action(entity);
            }
        }
    }

    private void ExecuteUnfilteredSequenceCore<TContext>(
        ReadOnlySpan<Entity> entities,
        ref TContext context,
        ForEachContextEntityAction<TContext> action)
    {
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities.RefAt(index);
            if (TryResolve(entity, out _))
            {
                action(ref context, entity);
            }
        }
    }

    private void ExecuteFilteredSequenceCore<TContext>(
        ReadOnlySpan<Entity> entities,
        QueryPlan plan,
        ref TContext context,
        ForEachContextEntityAction<TContext> action)
    {
        int lastArchetype = -1;
        bool lastMatches = false;
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities.RefAt(index);
            if (!TryResolve(entity, out int recordIndex))
            {
                continue;
            }

            ref readonly EntityRecord record = ref RecordAt(recordIndex);
            if (record.Archetype != lastArchetype)
            {
                lastArchetype = record.Archetype;
                lastMatches = plan.MatchesArchetype(lastArchetype);
            }

            if (lastMatches)
            {
                action(ref context, entity);
            }
        }
    }

    internal int Add(ReadOnlySpan<Entity> entities, in Query query, ReadOnlySpan<ComponentId> componentIds)
    {
        return ApplyFilteredSequenceComponents(entities, in query, componentIds, isAdd: true);
    }

    internal int Remove(ReadOnlySpan<Entity> entities, in Query query, ReadOnlySpan<ComponentId> componentIds)
    {
        return ApplyFilteredSequenceComponents(entities, in query, componentIds, isAdd: false);
    }

    internal int Destroy(ReadOnlySpan<Entity> entities, in Query query)
    {
        ValidateQuery(in query);
        if (entities.Length == 0)
        {
            return 0;
        }

        EnsureSequenceScratch(entities.Length);
        int count = CopyMatchingSequenceEntities(entities, query.Cached, _sequenceScratch.Span);
        return Destroy(_sequenceScratch.ReadOnlySpan[..count]);
    }

    private int ApplyFilteredSequenceComponents(
        ReadOnlySpan<Entity> entities,
        in Query query,
        ReadOnlySpan<ComponentId> componentIds,
        bool isAdd)
    {
        ValidateQuery(in query);
        if (componentIds.Length == 0 || entities.Length == 0)
        {
            return 0;
        }

        EnsureSequenceScratch(entities.Length);
        int count = CopyMatchingSequenceEntities(entities, query.Cached, _sequenceScratch.Span);
        return isAdd
            ? Add(componentIds, _sequenceScratch.ReadOnlySpan[..count])
            : Remove(componentIds, _sequenceScratch.ReadOnlySpan[..count]);
    }

    private int CopyMatchingSequenceEntities(
        ReadOnlySpan<Entity> entities,
        QueryPlan plan,
        Span<Entity> destination)
    {
        int count = 0;
        int lastArchetype = -1;
        bool lastMatches = false;
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities.RefAt(index);
            if (!TryResolve(entity, out int recordIndex))
            {
                continue;
            }

            ref readonly var record = ref RecordAt(recordIndex);
            if (record.Archetype != lastArchetype)
            {
                lastArchetype = record.Archetype;
                lastMatches = plan.MatchesArchetype(lastArchetype);
            }

            if (lastMatches)
            {
                destination.RefAt(count++) = entity;
            }
        }

        return count;
    }

    private void EnsureSequenceScratch(int length)
    {
        if (_sequenceScratch.Length < length)
        {
            _sequenceScratch.Resize(length);
        }
    }
}
