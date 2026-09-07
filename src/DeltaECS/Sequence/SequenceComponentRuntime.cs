namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

public sealed partial class World
{
    /// <summary>Executes a compiler-generated invoker over an explicit entity sequence.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExecuteGeneratedSequence<TInvoker>(
        ReadOnlySpan<Entity> entities,
        in Query query,
        ref TInvoker invoker,
        bool hasWrites)
        where TInvoker : struct, IGeneratedSequenceInvoker
    {
        ValidateQuery(in query);
        ExecuteGeneratedSequenceTrusted(entities, in query, ref invoker, hasWrites);
    }

    /// <summary>Executes a compiler-generated invoker after its query and routes were validated.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExecuteGeneratedSequenceTrusted<TInvoker>(
        ReadOnlySpan<Entity> entities,
        in Query query,
        ref TInvoker invoker,
        bool hasWrites)
        where TInvoker : struct, IGeneratedSequenceInvoker
    {
        QueryPlan cached = query.Cached;
        BeginQueryLease();
        try
        {
            ReadOnlySpan<ArchetypePlan> plans = cached.MatchingPlans();
            int lastArchetype = -1;
            int lastPlanIndex = -1;
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
                    lastPlanIndex = cached.MatchingPlanIndex(lastArchetype);
                }

                if (lastPlanIndex < 0)
                {
                    continue;
                }

                ref readonly ArchetypePlan plan = ref plans.RefAt(lastPlanIndex);
                var cursor = new GeneratedSequenceCursor(
                    in plan,
                    in plan.Chunks.RefAt(record.Chunk),
                    record.SlotIndex,
                    entity,
                    hasWrites);
                invoker.Invoke(ref cursor);
            }
        }
        finally
        {
            EndQueryLease();
        }
    }
}
