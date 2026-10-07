using System;
using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

internal static class GeneratedQueryGrammarProof
{
    internal static void Run(World world, in Query query, ComponentId cmp1Id, ComponentId cmp2Id, ReadOnlySpan<ComponentId> ids)
    {
        // World query factories.
        _ = world.WhereAll<Cmp1, Cmp2>();
        _ = world.WhereAny<Cmp1, Cmp2>();
        _ = world.WhereNone<Cmp1, Cmp2>();
        _ = world.WhereAll<Cmp1, Cmp2>(ids);
        _ = world.WhereAny<Cmp1, Cmp2>(ids);
        _ = world.WhereNone<Cmp1, Cmp2>(ids);
        _ = world.WhereAll(cmp1Id, cmp2Id);
        _ = world.WhereAny(cmp1Id, cmp2Id);
        _ = world.WhereAny(ids);
        _ = world.WhereAll(ids);
        _ = world.WhereNone(cmp1Id);
        _ = world.WhereNone(cmp1Id, cmp2Id);
        _ = world.WhereNone(ids);

        // Query chaining.
        _ = query.WhereAll<Cmp1, Cmp2>();
        _ = query.WhereAny<Cmp1, Cmp2>();
        _ = query.WhereNone<Cmp1, Cmp2>();
        _ = query.WhereAll(cmp1Id, cmp2Id);
        _ = query.WhereAny(cmp1Id, cmp2Id);
        _ = query.WhereNone(cmp1Id, cmp2Id);
        _ = query.WhereAll(ids);
        _ = query.WhereAny(ids);
        _ = query.WhereNone(ids);
        _ = query.WhereAll<Cmp1, Cmp2>(ids);
        _ = query.WhereAny<Cmp1, Cmp2>(ids);
        _ = query.WhereNone<Cmp1, Cmp2>(ids);

        // QuerySpec span factories and positional fluent filters.
        _ = QuerySpec.WhereAll(ids).WithAny(ids).WithNone(ids);
        _ = QuerySpec.WhereAll(ids);
        _ = QuerySpec.WhereAny(ids);
        _ = QuerySpec.WhereNone(ids);
        _ = QuerySpec.Empty
            .WhereAll(cmp1Id, cmp2Id)
            .WhereAny(cmp1Id, cmp2Id)
            .WhereNone(cmp1Id)
            .WithAll(ids)
            .WithAny(ids)
            .WithNone(ids);
    }
}
