namespace Delta.ECS;

using System;

public sealed partial class World
{
    /// <summary>Executes a compiler-generated invoker over an explicit entity sequence.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void ExecuteGeneratedSequence<TInvoker>(
        ReadOnlySpan<Entity> entities,
        in Query query,
        ref TInvoker invoker,
        bool hasWrites)
        where TInvoker : struct, IGeneratedSequenceInvoker
    {
        ValidateQuery(in query);
        QueryPlan cached = query.Cached;
        _ = cached.MatchingPlans();
        QueryWriteSession writeSession = RentQueryWriteSession(hasWrites, out int sessionGeneration);
        BeginQueryLease();
        try
        {
            int lastArchetype = -1;
            ArchetypePlan plan = default;
            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities[index];
                if (!TryResolve(entity, out int recordIndex))
                {
                    continue;
                }

                ref readonly EntityRecord record = ref RecordAt(recordIndex);
                if (record.Archetype != lastArchetype)
                {
                    if (!cached.TryGetPlan(record.Archetype, out plan))
                    {
                        lastArchetype = -1;
                        continue;
                    }

                    lastArchetype = record.Archetype;
                }

                var cursor = new GeneratedSequenceCursor(
                    plan,
                    plan.Chunks.Ref(record.Chunk),
                    record.SlotIndex,
                    entity,
                    writeSession,
                    sessionGeneration);
                invoker.Invoke(ref cursor);
            }
        }
        finally
        {
            ReturnQueryWriteSession(writeSession, sessionGeneration);
        }
    }
}
