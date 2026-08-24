namespace Delta.ECS;

using System;
using System.Buffers;

public sealed partial class World
{
    /// <summary>Creates a non-owning ordered view over explicit entity candidates.</summary>
    public EntitySequence Entities(ReadOnlySpan<Entity> entities) => new(this, entities);

    public void ForEach(ReadOnlySpan<Entity> entities, SequenceAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ExecuteSequence(entities, default, hasFilter: false, action);
    }

    public void ForEach(ReadOnlySpan<Entity> entities, in Query query, SequenceAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ExecuteSequence(entities, query, hasFilter: true, action);
    }

    public void ForEach<TContext>(ReadOnlySpan<Entity> entities, ref TContext context, SequenceAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ExecuteSequence(entities, ref context, default, hasFilter: false, action);
    }

    public void ForEach<TContext>(ReadOnlySpan<Entity> entities, in Query query, ref TContext context, SequenceAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ExecuteSequence(entities, ref context, query, hasFilter: true, action);
    }

    private void ExecuteSequence(
        ReadOnlySpan<Entity> entities,
        Query query,
        bool hasFilter,
        SequenceAction action)
    {
        if (hasFilter)
        {
            ValidateQuery(in query);
        }

        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            if (!TryResolve(entity, out int recordIndex))
            {
                continue;
            }

            ref readonly var record = ref RecordAt(recordIndex);
            if (hasFilter && !MatchesSequenceQuery(record.Archetype, in query))
            {
                continue;
            }

            action(entity);
        }
    }

    private void ExecuteSequence<TContext>(
        ReadOnlySpan<Entity> entities,
        ref TContext context,
        Query query,
        bool hasFilter,
        SequenceAction<TContext> action)
    {
        if (hasFilter)
        {
            ValidateQuery(in query);
        }

        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            if (!TryResolve(entity, out int recordIndex))
            {
                continue;
            }

            ref readonly var record = ref RecordAt(recordIndex);
            if (hasFilter && !MatchesSequenceQuery(record.Archetype, in query))
            {
                continue;
            }

            action(ref context, entity);
        }
    }

    private bool MatchesSequenceQuery(int archetypeId, in Query query)
    {
        var mask = _archetypes[archetypeId].Mask;
        var description = query.Description;
        return mask.ContainsAll(description.AllMask)
            && (description.AnyMask.IsEmpty || mask.Intersects(description.AnyMask))
            && !mask.Intersects(description.NoneMask);
    }

    internal int AddComponents(ReadOnlySpan<Entity> entities, in Query query, ComponentId[] componentIds)
    {
        return ApplyFilteredSequenceComponents(entities, in query, componentIds, isAdd: true);
    }

    internal int RemoveComponents(ReadOnlySpan<Entity> entities, in Query query, ComponentId[] componentIds)
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

        Entity[] rented = ArrayPool<Entity>.Shared.Rent(entities.Length);
        try
        {
            int count = CopyMatchingSequenceEntities(entities, in query, rented);
            return DestroyBatch(rented.AsSpan(0, count));
        }
        finally
        {
            ArrayPool<Entity>.Shared.Return(rented);
        }
    }

    private int ApplyFilteredSequenceComponents(
        ReadOnlySpan<Entity> entities,
        in Query query,
        ComponentId[] componentIds,
        bool isAdd)
    {
        ValidateQuery(in query);
        if (componentIds.Length == 0 || entities.Length == 0)
        {
            return 0;
        }

        Entity[] rented = ArrayPool<Entity>.Shared.Rent(entities.Length);
        try
        {
            int count = CopyMatchingSequenceEntities(entities, in query, rented);
            return isAdd
                ? AddComponents(componentIds, rented.AsSpan(0, count))
                : RemoveComponents(componentIds, rented.AsSpan(0, count));
        }
        finally
        {
            ArrayPool<Entity>.Shared.Return(rented);
        }
    }

    private int CopyMatchingSequenceEntities(
        ReadOnlySpan<Entity> entities,
        in Query query,
        Entity[] destination)
    {
        int count = 0;
        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            if (!TryResolve(entity, out int recordIndex))
            {
                continue;
            }

            ref readonly var record = ref RecordAt(recordIndex);
            if (MatchesSequenceQuery(record.Archetype, in query))
            {
                destination[count++] = entity;
            }
        }

        return count;
    }
}
