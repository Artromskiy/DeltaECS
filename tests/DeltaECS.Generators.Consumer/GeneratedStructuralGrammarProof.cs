using System;
using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

internal static class GeneratedStructuralGrammarProof
{
    internal static void Run(
        World world,
        in Query query,
        ReadOnlySpan<Entity> entities,
        Entity entity,
        ComponentId cmp1Id,
        ComponentId cmp2Id,
        ReadOnlySpan<ComponentId> componentIds)
    {
        // Create: count/output, typed/ID/span selectors, and initial values.
        _ = world.Create<Cmp1, Cmp2>(2);
        Span<Entity> output = stackalloc Entity[2];
        _ = world.Create<Cmp1, Cmp2>(2, output);
        _ = world.Create<Cmp1, Cmp2>(cmp1Id, cmp2Id, 2);
        _ = world.Create<Cmp1, Cmp2>(componentIds, 2);
        _ = world.Create<Cmp1, Cmp2>(componentIds, 2, output);
        _ = world.Create(cmp1Id, cmp2Id, 2);
        _ = world.Create(cmp1Id, cmp2Id, 2, output);
        _ = world.Create(componentIds, 2, output);
        _ = world.Create<Cmp1, Cmp2>(cmp1Id, cmp2Id, new Cmp1(), new Cmp2());
        _ = world.Create<Cmp1, Cmp2>(componentIds, new Cmp1(), new Cmp2());
        _ = world.Create(cmp1Id, cmp2Id, new Cmp1(), new Cmp2());

        // Add: entity, batch, and query targets with defaults or explicit values.
        _ = world.Add<Cmp1, Cmp2>(entity);
        _ = world.Add<Cmp1, Cmp2>(entity, cmp1Id, cmp2Id);
        _ = world.Add<Cmp1, Cmp2>(entity, componentIds);
        _ = world.Add<Cmp1, Cmp2>(entity, new Cmp1(), new Cmp2());
        _ = world.Add<Cmp1, Cmp2>(entities, new Cmp1(), new Cmp2());
        _ = world.Add<Cmp1, Cmp2>(entities, cmp1Id, cmp2Id, new Cmp1(), new Cmp2());
        _ = world.Add<Cmp1, Cmp2>(entities, componentIds, new Cmp1(), new Cmp2());
        _ = world.Add<Cmp1, Cmp2>(entity, cmp1Id, cmp2Id, new Cmp1(), new Cmp2());
        _ = world.Add<Cmp1, Cmp2>(entity, componentIds, new Cmp1(), new Cmp2());
        _ = world.Add(entity, cmp1Id, cmp2Id);
        _ = world.Add(entity, componentIds);
        _ = world.Add(entity, new Cmp1(), new Cmp2());
        _ = world.Add<Cmp1, Cmp2>(entities);
        _ = world.Add<Cmp1, Cmp2>(entities, cmp1Id, cmp2Id);
        _ = world.Add<Cmp1, Cmp2>(entities, componentIds);
        _ = world.Add(entities, cmp1Id, cmp2Id);
        _ = world.Add(entities, componentIds);
        _ = world.Add<Cmp1, Cmp2>(in query);
        _ = world.Add<Cmp1, Cmp2>(in query, cmp1Id, cmp2Id);
        _ = world.Add<Cmp1, Cmp2>(in query, componentIds);
        _ = world.Add(in query, componentIds);
        _ = world.Add(in query, cmp1Id, cmp2Id);

        // Remove has matching entity, batch, and query selector forms.
        _ = world.Remove<Cmp1, Cmp2>(entity);
        _ = world.Remove<Cmp1, Cmp2>(entity, cmp1Id, cmp2Id);
        _ = world.Remove<Cmp1, Cmp2>(entity, componentIds);
        _ = world.Remove(entity, cmp1Id, cmp2Id);
        _ = world.Remove(entity, componentIds);
        _ = world.Remove<Cmp1, Cmp2>(entities);
        _ = world.Remove<Cmp1, Cmp2>(entities, cmp1Id, cmp2Id);
        _ = world.Remove<Cmp1, Cmp2>(entities, componentIds);
        _ = world.Remove(entities, cmp1Id, cmp2Id);
        _ = world.Remove(entities, componentIds);
        _ = world.Remove<Cmp1, Cmp2>(in query);
        _ = world.Remove<Cmp1, Cmp2>(in query, cmp1Id, cmp2Id);
        _ = world.Remove<Cmp1, Cmp2>(in query, componentIds);
        _ = world.Remove(in query, componentIds);
        _ = world.Remove(in query, cmp1Id, cmp2Id);

        // Destroy accepts each structural target shape.
        _ = world.Destroy(entity);
        _ = world.Destroy(entities);
        _ = world.Destroy(in query);
    }
}
