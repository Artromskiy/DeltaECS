using System;
using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

internal static class GeneratedIterationGrammarProof
{
    private static void AddContextFromCmp1(ref Context context, ref Cmp1 cmp1)
        => context.Value += cmp1.Value;

    internal static void Run(
        World world,
        in Query query,
        ReadOnlySpan<Entity> entities,
        Entity[] entityArray,
        ComponentId cmp1Id,
        ComponentId cmp2Id,
        ReadOnlySpan<ComponentId> componentIds,
        ReadOnlySpan<ComponentId> singleComponentId,
        ref Context context,
        in Context readContext)
    {
        // Query-wide delegate callbacks.
        world.ForEach(in query, static (ref Cmp1 cmp1) => cmp1.Value++).Invoke();
        world.ForEach<Cmp1>(in query, cmp1Id, static (in Cmp1 cmp1) => _ = cmp1.Value).Invoke();
        world.ForEach<Cmp1, Cmp2>(in query, componentIds,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value).Invoke();
        world.ForEach<Cmp1, Cmp2>(in query, cmp1Id, cmp2Id,
            static (Cmp1 cmp1, ref Cmp2 cmp2) => cmp2.Value += cmp1.Value).Invoke();
        world.ForEach<Cmp1, Cmp2>(in query, componentIds,
            static (ref readonly Cmp1 cmp1, Cmp2 cmp2) => _ = cmp1.Value + cmp2.Value).Invoke();
        world.ForEach(in query, ref context,
            static (ref Context state, in Cmp1 cmp1, ref Cmp2 cmp2) =>
            {
                state.Value += cmp1.Value;
                cmp2.Value += state.Value;
            }).Invoke(ref context);
        world.ForEach<Context, Cmp1, Cmp2>(in query, cmp1Id, cmp2Id, ref context,
            static (ref Context state, in Cmp1 cmp1, ref Cmp2 cmp2) =>
                cmp2.Value += state.Value + cmp1.Value).Invoke(ref context);

        // Query-wide entity-aware callbacks.
        world.ForEachEntity(in query, static (EntityRef entity, in Cmp1 cmp1) => _ = entity.Index + cmp1.Value).Invoke();
        world.ForEachEntity<Cmp1>(in query, cmp1Id,
            static (EntityRef entity, ref Cmp1 cmp1) => cmp1.Value += entity.Index).Invoke();
        world.ForEachEntity<Cmp1, Cmp2>(in query, componentIds,
            static (EntityRef entity, in Cmp1 cmp1, ref Cmp2 cmp2) => cmp2.Value += entity.Index + cmp1.Value).Invoke();
        world.ForEachEntity<Context, Cmp1>(in query, ref context,
            static (ref Context state, EntityRef entity, in Cmp1 cmp1) => state.Value += entity.Index + cmp1.Value).Invoke(ref context);
        world.ForEachEntity<Context, Cmp1>(in query, cmp1Id, ref context,
            static (ref Context state, EntityRef entity, in Cmp1 cmp1) => state.Value += entity.Index + cmp1.Value).Invoke(ref context);

        // Entity-list delegates: span/array, optional query, selectors, and context.
        world.ForEach(entities, static (ref Cmp1 cmp1) => cmp1.Value++).Invoke();
        world.ForEach<Cmp1>(entities, in query, cmp1Id,
            static (ref Cmp1 cmp1) => cmp1.Value++).Invoke();
        world.ForEach<Cmp1, Cmp2>(entities, in query, componentIds,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value).Invoke();
        world.ForEach<Cmp1, Cmp2>(entities, cmp1Id, cmp2Id,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value).Invoke();
        world.ForEach(entities, in query, ref context,
            (ForEachContextAction<Context, Cmp1>)AddContextFromCmp1).Invoke(ref context);
        world.ForEach<Context, Cmp1>(entities, in query, cmp1Id, ref context,
            static (ref Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value).Invoke(ref context);
        world.ForEach<Context, Cmp1>(entities, singleComponentId, ref context,
            static (ref Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value).Invoke(ref context);
        world.ForEach(entityArray, static (ref Cmp1 cmp1) => cmp1.Value++).Invoke();
        world.ForEach<Cmp1>(entityArray, in query, cmp1Id,
            static (ref Cmp1 cmp1) => cmp1.Value++).Invoke();
        world.ForEachEntity<Cmp1>(entities, in query, cmp1Id,
            static (EntityRef entity, ref Cmp1 cmp1) => cmp1.Value += entity.Index).Invoke();
        world.ForEachEntity<Cmp1, Cmp2>(entities, componentIds,
            static (EntityRef entity, in Cmp1 cmp1, ref Cmp2 cmp2) => cmp2.Value += entity.Index + cmp1.Value).Invoke();
        world.ForEachEntity(entities, in query,
            static (EntityRef entity) => _ = entity.Index).Invoke();
        world.ForEachEntity(entities,
            static (EntityRef entity) => _ = entity.Index).Invoke();
        world.ForEachEntity(entities, ref context,
            static (ref Context state, EntityRef entity) => state.Value += entity.Index).Invoke(ref context);
        world.ForEachEntity(entityArray, in query,
            static (EntityRef entity, ref Cmp1 cmp1) => cmp1.Value += entity.Index).Invoke();
        world.ForEachEntity<Context, Cmp1>(entityArray, in query, cmp1Id, ref context,
            static (ref Context state, EntityRef entity, ref Cmp1 cmp1) => cmp1.Value += state.Value + entity.Index).Invoke(ref context);

        // Typed functors: row modes R/W/I/V/RW, selectors, entity access, and context.
        var functorW = new FunctorW();
        world.ForEach(in query, ref functorW).Invoke(ref functorW);
        world.ForEach(entities, in query, ref functorW).Invoke(ref functorW);
        world.ForEach(entities, ref functorW).Invoke(ref functorW);
        var functorR = new FunctorR();
        world.ForEach(in query, in functorR).Invoke();
        var functorI = new FunctorI();
        world.ForEach(in query, in functorI).Invoke();
        var functorV = new FunctorV();
        world.ForEach(in query, functorV).Invoke();
        world.ForEach(in query, cmp1Id, ref functorW).Invoke(ref functorW);
        world.ForEach(entities, in query, cmp1Id, ref functorW).Invoke(ref functorW);
        world.ForEach(entityArray, cmp1Id, ref functorW).Invoke(ref functorW);
        var functorRW = new FunctorRW();
        world.ForEach(in query, componentIds, ref functorRW).Invoke(ref functorRW);
        world.ForEach(entities, in query, componentIds, ref functorRW).Invoke(ref functorRW);
        world.ForEach(entityArray, componentIds, ref functorRW).Invoke(ref functorRW);
        var functorEntityW = new FunctorEntityW();
        world.ForEachEntity(in query, ref functorEntityW).Invoke(ref functorEntityW);
        world.ForEachEntity(entities, in query, ref functorEntityW).Invoke(ref functorEntityW);
        world.ForEachEntity(entities, ref functorEntityW).Invoke(ref functorEntityW);
        world.ForEachEntity(in query, cmp1Id, ref functorEntityW).Invoke(ref functorEntityW);
        world.ForEachEntity(entityArray, cmp1Id, ref functorEntityW).Invoke(ref functorEntityW);
        var functorEntityRW = new FunctorEntityRW();
        world.ForEachEntity(entities, in query, componentIds, ref functorEntityRW).Invoke(ref functorEntityRW);
        world.ForEachEntity(entities, componentIds, ref functorEntityRW).Invoke(ref functorEntityRW);
        var functorEntity = new FunctorEntity();
        world.ForEachEntity(in query, ref functorEntity).Invoke(ref functorEntity);
        world.ForEachEntity(entities, in query, ref functorEntity).Invoke(ref functorEntity);
        world.ForEachEntity(entities, ref functorEntity).Invoke(ref functorEntity);

        // Parallel delegates: query-wide callback and context forms.
        world.ForEachParallel(in query, static (ref Cmp1 cmp1) => cmp1.Value++, workerCount: 2).Invoke();
        world.ForEachParallel<Cmp1>(in query, cmp1Id,
            static (ref Cmp1 cmp1) => cmp1.Value++, workerCount: 2).Invoke();
        world.ForEachParallel<Cmp1, Cmp2>(in query, componentIds,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value,
            workerCount: 2).Invoke();
        world.ForEachParallel(in query, in readContext,
            static (in Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value,
            workerCount: 2).Invoke();
        world.ForEachParallel<Context, Cmp1>(in query, cmp1Id, in readContext,
            static (in Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value,
            workerCount: 2).Invoke();
        world.ForEachParallel<Context, Cmp1>(in query, singleComponentId, readContext,
            static (Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value,
            workerCount: 2).Invoke();
        world.ForEach(in query, in readContext,
            static (in Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value).Invoke();
        world.ForEach(in query, readContext,
            static (Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value).Invoke();
        world.ForEachEntity(in query, readContext,
            static (Context state, EntityRef entity, ref Cmp1 cmp1) => cmp1.Value += state.Value + entity.Index).Invoke();
        world.ForEachEntityParallel(in query,
            static (EntityRef entity) => _ = entity.Index,
            workerCount: 2).Invoke();
        world.ForEachEntityParallel<Cmp1>(in query, cmp1Id,
            static (EntityRef entity, ref Cmp1 cmp1) => cmp1.Value += entity.Index,
            workerCount: 2).Invoke();
        world.ForEachEntityParallel<Context, Cmp1>(in query, in readContext,
            static (in Context state, EntityRef entity, ref Cmp1 cmp1) => cmp1.Value += state.Value + entity.Index,
            workerCount: 2).Invoke();

        // Parallel entity-list delegates and entity-aware variants.
        world.ForEachParallel(entities, in query,
            static (ref Cmp1 cmp1) => cmp1.Value++,
            workerCount: 2).Invoke();
        world.ForEachParallel(entityArray, static (ref Cmp1 cmp1) => cmp1.Value++, workerCount: 2).Invoke();
        world.ForEachParallel(entityArray, in query, componentIds,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value,
            workerCount: 2).Invoke();
        world.ForEachParallel<Context, Cmp1>(entities, in query, cmp1Id, in readContext,
            static (in Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value,
            workerCount: 2).Invoke();
        world.ForEachParallel<Context, Cmp1>(entities, singleComponentId, readContext,
            static (Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value,
            workerCount: 2).Invoke();
        world.ForEachParallel<Cmp1>(entities, cmp1Id,
            static (ref Cmp1 cmp1) => cmp1.Value++,
            workerCount: 2).Invoke();
        world.ForEachParallel<Cmp1, Cmp2>(entities, in query, componentIds,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value,
            workerCount: 2).Invoke();
        world.ForEachEntityParallel(entities,
            static (EntityRef entity) => _ = entity.Index,
            workerCount: 2).Invoke();
        world.ForEachEntityParallel(entities, in query,
            static (EntityRef entity) => _ = entity.Index,
            workerCount: 2).Invoke();
        world.ForEachEntityParallel(entityArray, static (EntityRef entity) => _ = entity.Index, workerCount: 2).Invoke();
        world.ForEachEntityParallel<Cmp1, Cmp2>(entities, cmp1Id, cmp2Id,
            static (EntityRef entity, ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += entity.Index + cmp2.Value,
            workerCount: 2).Invoke();
        world.ForEachEntityParallel<Context, Cmp1>(entities, in query, in readContext,
            static (in Context state, EntityRef entity, ref Cmp1 cmp1) => cmp1.Value += state.Value + entity.Index,
            workerCount: 2).Invoke();
        world.ForEachEntityParallel<Context, Cmp1>(entityArray, cmp1Id, in readContext,
            static (in Context state, EntityRef entity, ref Cmp1 cmp1) => cmp1.Value += state.Value + entity.Index,
            workerCount: 2).Invoke();

        // Parallel functors use the same selector/context order and retain caller state by ref.
        var functorContext = new FunctorContext();
        world.ForEach(in query, ref context, ref functorContext).Invoke(ref context, ref functorContext);
        world.ForEach(in query, cmp1Id, ref context, ref functorContext).Invoke(ref context, ref functorContext);
        world.ForEach(entityArray, in query, ref context, ref functorContext).Invoke(ref context, ref functorContext);
        var functorEntityContext = new FunctorEntityContext();
        world.ForEachEntity(in query, ref context, ref functorEntityContext).Invoke(ref context, ref functorEntityContext);
        world.ForEachEntity(entityArray, ref context, ref functorEntityContext).Invoke(ref context, ref functorEntityContext);
        var functorParallelW = new FunctorW();
        world.ForEachParallel(in query, ref functorParallelW, workerCount: 2).Invoke(ref functorParallelW);
        world.ForEachParallel(entityArray, in query, ref functorParallelW, workerCount: 2).Invoke(ref functorParallelW);
        world.ForEachParallel(entityArray, ref functorParallelW, workerCount: 2).Invoke(ref functorParallelW);
        world.ForEachParallel(in query, cmp1Id, ref functorParallelW, workerCount: 2).Invoke(ref functorParallelW);
        world.ForEachParallel(in query, componentIds, ref functorRW, workerCount: 2).Invoke(ref functorRW);
        world.ForEachParallel(entities, in query, componentIds, ref functorRW, workerCount: 2).Invoke(ref functorRW);
        world.ForEachParallel(entityArray, componentIds, ref functorRW, workerCount: 2).Invoke(ref functorRW);
        var parallelReadFunctor = new FunctorR();
        world.ForEachParallel(in query, in parallelReadFunctor, workerCount: 2).Invoke();
        var parallelValueFunctor = new FunctorV();
        world.ForEachParallel(in query, parallelValueFunctor, workerCount: 2).Invoke();
        var parallelContextFunctor = new FunctorParallelContext();
        world.ForEachParallel(in query, in readContext, ref parallelContextFunctor, workerCount: 2).Invoke(ref parallelContextFunctor);
        world.ForEachParallel(in query, cmp1Id, in readContext, ref parallelContextFunctor, workerCount: 2).Invoke(ref parallelContextFunctor);
        world.ForEachParallel(entityArray, in query, in readContext, ref parallelContextFunctor, workerCount: 2).Invoke(ref parallelContextFunctor);
        world.ForEachParallel(entityArray, in query, componentIds, ref functorRW, workerCount: 2).Invoke(ref functorRW);
        world.ForEachParallel(entityArray, in query, cmp1Id, ref functorParallelW, workerCount: 2).Invoke(ref functorParallelW);
        var functorParallelEntity = new FunctorEntity();
        world.ForEachEntityParallel(in query, ref functorParallelEntity, workerCount: 2).Invoke(ref functorParallelEntity);
        world.ForEachEntityParallel(entityArray, in query, ref functorParallelEntity, workerCount: 2).Invoke(ref functorParallelEntity);
        world.ForEachEntityParallel(entityArray, ref functorParallelEntity, workerCount: 2).Invoke(ref functorParallelEntity);
        world.ForEachEntityParallel(in query, cmp1Id, ref functorEntityW, workerCount: 2).Invoke(ref functorEntityW);
        world.ForEachEntityParallel(entities, in query, componentIds, ref functorEntityRW, workerCount: 2).Invoke(ref functorEntityRW);
        world.ForEachEntityParallel(entityArray, componentIds, ref functorEntityRW, workerCount: 2).Invoke(ref functorEntityRW);
    }
}
