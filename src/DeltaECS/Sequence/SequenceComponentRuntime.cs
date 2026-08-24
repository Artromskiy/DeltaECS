namespace Delta.ECS;

using System;

internal interface ISequenceInvoker
{
    void Invoke(ref SequenceElementCursor cursor);
}

internal ref struct SequenceElementCursor
{
    private readonly QueryPlan _query;
    private readonly Chunk _chunk;
    private readonly ReadOnlySpan<int> _componentRows;
    private readonly QueryWriteSession _writeSession;
    private readonly int _sessionGeneration;

    internal SequenceElementCursor(
        QueryPlan query,
        ArchetypePlan plan,
        Chunk chunk,
        int slot,
        Entity entity,
        QueryWriteSession writeSession,
        int sessionGeneration)
    {
        _query = query;
        _chunk = chunk;
        _componentRows = plan.ComponentRows;
        _writeSession = writeSession;
        _sessionGeneration = sessionGeneration;
        Slot = slot;
        Entity = entity;
    }

    internal int Slot { get; }

    internal Entity Entity { get; }

    internal ReadValues Get(ReadAccess access)
    {
        _writeSession.EnsureActive(_sessionGeneration);
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        return new ReadValues(_chunk.GetRawComponentRow(physicalRow));
    }

    internal WriteValues Get(WriteAccess access)
    {
        if (!ReferenceEquals(access.Query, _query))
        {
            QueryThrowHelper.ThrowAccessMismatch();
        }

        int physicalRow = _componentRows[access.QueryComponentIndex];
        _writeSession.Acquire(_sessionGeneration, out uint writeTick, out Stamp writeStamp);
        _chunk.MarkComponentWritten(physicalRow, Slot, writeTick, writeStamp);
        return new WriteValues(_chunk.GetRawComponentRow(physicalRow));
    }
}

public sealed partial class World
{
    internal Query CreateSequenceQuery(ReadOnlySpan<ComponentId> components)
    {
        var spec = QuerySpec.ForComponents(components);
        return CreateQuery(in spec);
    }

    internal void ExecuteSequenceComponents<TInvoker>(ReadOnlySpan<Entity> entities, in Query query, ref TInvoker invoker, bool hasWrites)
        where TInvoker : struct, ISequenceInvoker
    {
        ValidateQuery(in query);
        QueryPlan cached = query.Cached;
        _ = cached.MatchingPlans(this);
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

                ref readonly var record = ref RecordAt(recordIndex);
                if (record.Archetype != lastArchetype)
                {
                    if (!cached.TryGetPlan(record.Archetype, out plan))
                    {
                        lastArchetype = -1;
                        continue;
                    }

                    lastArchetype = record.Archetype;
                }

                Chunk chunk = plan.Archetype.GetChunk(record.Chunk);
                var cursor = new SequenceElementCursor(
                    cached,
                    plan,
                    chunk,
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
