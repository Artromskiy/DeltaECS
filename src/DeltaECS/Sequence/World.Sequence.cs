namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

public sealed partial class World
{
    /// <summary>Creates a non-owning ordered view over explicit entity candidates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntitySequence Entities(ReadOnlySpan<Entity> entities) => new(this, entities);

    public void ForEachEntity(ReadOnlySpan<Entity> entities, ForEachEntityAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ExecuteSequence(entities, default, hasFilter: false, action);
    }

    public void ForEachEntity(ReadOnlySpan<Entity> entities, in Query query, ForEachEntityAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ExecuteSequence(entities, query, hasFilter: true, action);
    }

    public void ForEachEntity<TContext>(ReadOnlySpan<Entity> entities, ref TContext context, ForEachContextEntityAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ExecuteSequence(entities, ref context, default, hasFilter: false, action);
    }

    public void ForEachEntity<TContext>(ReadOnlySpan<Entity> entities, in Query query, ref TContext context, ForEachContextEntityAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ExecuteSequence(entities, ref context, query, hasFilter: true, action);
    }

    public void ForEachEntity<TFunctor>(ReadOnlySpan<Entity> entities, ref TFunctor functor)
        where TFunctor : struct, IForEachEntity
        => ExecuteSequence(entities, ref functor, default, hasFilter: false);

    public void ForEachEntity<TFunctor>(ReadOnlySpan<Entity> entities, in Query query, ref TFunctor functor)
        where TFunctor : struct, IForEachEntity
        => ExecuteSequence(entities, ref functor, query, hasFilter: true);

    public void ForEachEntity<TContext, TFunctor>(ReadOnlySpan<Entity> entities, ref TContext context, ref TFunctor functor)
        where TFunctor : struct, IForEachContextEntity<TContext>
        => ExecuteSequence(entities, ref context, ref functor, default, hasFilter: false);

    public void ForEachEntity<TContext, TFunctor>(ReadOnlySpan<Entity> entities, in Query query, ref TContext context, ref TFunctor functor)
        where TFunctor : struct, IForEachContextEntity<TContext>
        => ExecuteSequence(entities, ref context, ref functor, query, hasFilter: true);

    private void ExecuteSequence(
        ReadOnlySpan<Entity> entities,
        Query query,
        bool hasFilter,
        ForEachEntityAction action)
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
        ForEachContextEntityAction<TContext> action)
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

    private void ExecuteSequence<TFunctor>(
        ReadOnlySpan<Entity> entities,
        ref TFunctor functor,
        Query query,
        bool hasFilter)
        where TFunctor : struct, IForEachEntity
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

            functor.Invoke(entity);
        }
    }

    private void ExecuteSequence<TContext, TFunctor>(
        ReadOnlySpan<Entity> entities,
        ref TContext context,
        ref TFunctor functor,
        Query query,
        bool hasFilter)
        where TFunctor : struct, IForEachContextEntity<TContext>
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

            functor.Invoke(ref context, entity);
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

        EnsureSequenceScratch(entities.Length);
        int count = CopyMatchingSequenceEntities(entities, in query, _sequenceScratch.Span);
        return Destroy(_sequenceScratch.ReadOnlySpan[..count]);
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

        EnsureSequenceScratch(entities.Length);
        int count = CopyMatchingSequenceEntities(entities, in query, _sequenceScratch.Span);
        return isAdd
            ? AddComponents(componentIds, _sequenceScratch.ReadOnlySpan[..count])
            : RemoveComponents(componentIds, _sequenceScratch.ReadOnlySpan[..count]);
    }

    private int CopyMatchingSequenceEntities(
        ReadOnlySpan<Entity> entities,
        in Query query,
        Span<Entity> destination)
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

    private void EnsureSequenceScratch(int length)
    {
        if (_sequenceScratch.Length < length)
        {
            _sequenceScratch.Resize(length);
        }
    }
}
