using System;
using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

internal static class GeneratedWherePipelineGrammarProof
{
    internal static void Run(World world, in Query query, ComponentId deadId, ComponentId aliveId, ref Context context)
    {
        ComponentId needsRespawnId = world.Layouts.GetPrimary<NeedsRespawn>();
        ReadOnlySpan<ComponentId> tagIds = stackalloc ComponentId[] { deadId, aliveId };

        // Component-only/entity-aware predicates: delegates, contexts, and functors.
        var simplePredicate = new WherePredicate();
        world.Where(in query, ref simplePredicate).Destroy();
        var entityOnlyPredicate = new WhereEntityPredicate();
        world.WhereEntity(in query, ref entityOnlyPredicate).Destroy();
        world.Where(in query, static (in Cmp3 cmp3, in Cmp4 cmp4) => cmp3.Value < cmp4.Value).Destroy();
        world.WhereEntity(in query, static (Entity entity, in Cmp3 cmp3, in Cmp4 cmp4) => cmp3.Value < cmp4.Value + entity.Index).Destroy();
        world.Where(in query, ref context,
            static (ref Context state, in Cmp3 cmp3, in Cmp4 cmp4) =>
            {
                state.Value++;
                return cmp3.Value < cmp4.Value;
            }).Add<Dead>();
        world.WhereEntity(in query, ref context,
            static (ref Context state, Entity entity, in Cmp3 cmp3, in Cmp4 cmp4) =>
            {
                state.Value += entity.Index;
                return cmp3.Value < cmp4.Value;
            }).Remove<Alive>(aliveId);

        var predicate = new WherePredicateContext();
        world.Where(in query, ref context, ref predicate).Add<Dead>(deadId);
        world.Where(in query, ref context, ref predicate).Add<Dead, Alive>(tagIds);
        world.Where(in query, ref context, ref predicate).Add<Dead, Alive>(deadId, aliveId);
        world.Where(in query, ref context, ref predicate).Add<Dead, Alive>(new Dead(), new Alive());
        world.Where(in query, ref context, ref predicate).Add<Dead, Alive>(tagIds, new Dead(), new Alive());
        var entityPredicate = new WhereEntityPredicateContext();
        world.WhereEntity(in query, ref context, ref entityPredicate).Remove<Dead, Alive>(tagIds);

        // Structural terminals (Destroy, Add, Remove) and iteration terminals.
        world.Where(in query, static (in Cmp3 cmp3) => cmp3.Value < 0)
            .Add<Dead>(new Dead());
        world.WhereEntity(in query, static (Entity entity, in Cmp3 cmp3) => cmp3.Value < entity.Index)
            .Add<Dead, NeedsRespawn>(deadId, needsRespawnId, new Dead(), new NeedsRespawn());
        world.Where(in query, static (in Cmp3 cmp3) => cmp3.Value < 0)
            .Remove<Dead>(deadId);
        world.Where(in query, static (in Cmp3 cmp3) => cmp3.Value < 0)
            .Remove<Dead>();
        world.WhereEntity(in query, static (Entity entity, in Cmp3 cmp3) => cmp3.Value < entity.Index)
            .Remove<Dead, Alive>(tagIds);

        world.Where(in query, static (in Cmp3 cmp3) => cmp3.Value < 0)
            .ForEach(static (ref Cmp3 cmp3) => cmp3.Value = 0);
        world.WhereEntity(in query, static (Entity entity, in Cmp3 cmp3) => cmp3.Value < entity.Index)
            .ForEachEntity(static (Entity entity, ref Cmp3 cmp3) => cmp3.Value += entity.Index);

        var terminalState = new Context();
        var action = new WhereFunctorContext();
        var functorWithoutContext = new FunctorW();
        world.Where(in query, static (in Cmp3 cmp3) => cmp3.Value < 0)
            .ForEach(ref terminalState, ref action);
        world.Where(in query, static (in Cmp3 cmp3) => cmp3.Value < 0)
            .ForEach(ref functorWithoutContext);
        var entityAction = new WhereEntityFunctorContext();
        var entityActionWithoutContext = new FunctorEntityW();
        world.WhereEntity(in query, static (Entity entity, in Cmp3 cmp3) => cmp3.Value < entity.Index)
            .ForEachEntity(ref terminalState, ref entityAction);
        world.WhereEntity(in query, static (Entity entity, in Cmp3 cmp3) => cmp3.Value < entity.Index)
            .ForEachEntity(static (Entity entity) => _ = entity.Index);
        world.WhereEntity(in query, static (Entity entity, in Cmp3 cmp3) => cmp3.Value < entity.Index)
            .ForEachEntity(ref entityActionWithoutContext);
    }
}
