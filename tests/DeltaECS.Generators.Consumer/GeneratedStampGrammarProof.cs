using System;
using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

internal static class GeneratedStampGrammarProof
{
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
        // Query-wide stamp callbacks, including explicit and dynamic selectors.
        world.ForEachStamp<Cmp1>(in query, static (in Stamp stamp) => _ = stamp).Invoke();
        world.ForEachEntityStamp<Cmp1, Cmp2>(in query, cmp1Id, cmp2Id,
            static (EntityRef entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode()).Invoke();
        world.ForEachStamp<Cmp1, Cmp2>(in query, componentIds,
            static (in Stamp first, in Stamp second) => _ = first.GetHashCode() + second.GetHashCode()).Invoke();
        world.ForEachStamp(in query, componentIds,
            static (in Stamp first, in Stamp second) => _ = first.GetHashCode() + second.GetHashCode()).Invoke();
        world.ForEachStamp(in query, cmp1Id,
            static (in Stamp stamp) => _ = stamp.GetHashCode()).Invoke();
        world.ForEachEntityStamp(in query, cmp1Id,
            static (EntityRef entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode()).Invoke();
        world.ForEachEntityStamp(in query, componentIds,
            static (EntityRef entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode()).Invoke();
        world.ForEachStamp<Context, Cmp1, Cmp2>(in query, cmp1Id, cmp2Id, ref context,
            static (ref Context state, in Stamp first, in Stamp second) => state.Value += first.GetHashCode() + second.GetHashCode()).Invoke();
        world.ForEachStamp<Context, Cmp1, Cmp2>(in query, componentIds, ref context,
            static (ref Context state, in Stamp first, in Stamp second) => state.Value += first.GetHashCode() + second.GetHashCode()).Invoke();
        world.ForEachEntityStamp<Context, Cmp1>(in query, ref context,
            static (ref Context state, EntityRef entity, in Stamp stamp) => state.Value += entity.Index + stamp.GetHashCode()).Invoke();
        world.ForEachEntityStamp<Context, Cmp1>(in query, cmp1Id, ref context,
            static (ref Context state, EntityRef entity, in Stamp stamp) => state.Value += entity.Index + stamp.GetHashCode()).Invoke();

        // Entity-list stamp callbacks.
        world.ForEachStamp(entities, in query, cmp1Id,
            static (in Stamp stamp) => _ = stamp).Invoke();
        world.ForEachStamp(entities, cmp1Id,
            static (in Stamp stamp) => _ = stamp.GetHashCode()).Invoke();
        world.ForEachEntityStamp(entities, cmp1Id,
            static (EntityRef entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode()).Invoke();
        world.ForEachEntityStamp<Cmp1>(entities, in query,
            static (EntityRef entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode()).Invoke();
        world.ForEachStamp<Cmp1, Cmp2>(entities, componentIds,
            static (in Stamp first, in Stamp second) => _ = first.GetHashCode() + second.GetHashCode()).Invoke();
        world.ForEachEntityStamp(entities, componentIds,
            static (EntityRef entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode()).Invoke();
        world.ForEachStamp<Cmp1>(entities,
            static (in Stamp stamp) => _ = stamp.GetHashCode()).Invoke();
        world.ForEachEntityStamp<Cmp1>(entities, cmp1Id,
            static (EntityRef entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode()).Invoke();
        world.ForEachEntityStamp<Cmp1>(entityArray, in query,
            static (EntityRef entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode()).Invoke();
        world.ForEachStamp<Context, Cmp1>(entities, in query, ref context,
            static (ref Context state, in Stamp stamp) => state.Value += stamp.GetHashCode()).Invoke();
        world.ForEachStamp<Context, Cmp1>(entities, cmp1Id, ref context,
            static (ref Context state, in Stamp stamp) => state.Value += stamp.GetHashCode()).Invoke();
        world.ForEachStamp<Context, Cmp1>(entities, stackalloc ComponentId[] { cmp1Id }, ref context,
            static (ref Context state, in Stamp stamp) => state.Value += stamp.GetHashCode()).Invoke();
        world.ForEachEntityStamp<Context, Cmp1>(entities, ref context,
            static (ref Context state, EntityRef entity, in Stamp stamp) => state.Value += entity.Index + stamp.GetHashCode()).Invoke();
        world.ForEachEntityStamp<Context, Cmp1>(entityArray, in query, cmp1Id, ref context,
            static (ref Context state, EntityRef entity, in Stamp stamp) => state.Value += entity.Index + stamp.GetHashCode()).Invoke();

        // Parallel stamp callbacks, followed by functor and context forms.
        world.ForEachStampParallel<Cmp1>(in query, cmp1Id,
            static (in Stamp stamp) => _ = stamp,
            workerCount: 2).Invoke();
        world.ForEachStampParallel(in query, cmp1Id,
            static (in Stamp stamp) => _ = stamp.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachEntityStampParallel<Cmp1, Cmp2>(in query, componentIds,
            static (EntityRef entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachStampParallel<Context, Cmp1>(in query, in readContext,
            static (in Context state, in Stamp stamp) => _ = state.Value + stamp.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachEntityStampParallel<Context, Cmp1>(in query, in readContext,
            static (in Context state, EntityRef entity, in Stamp stamp) => _ = state.Value + entity.Index + stamp.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachStampParallel<Cmp1>(entities, in query, cmp1Id,
            static (in Stamp stamp) => _ = stamp,
            workerCount: 2).Invoke();
        world.ForEachStampParallel<Cmp1, Cmp2>(in query, componentIds,
            static (in Stamp first, in Stamp second) => _ = first.GetHashCode() + second.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachEntityStampParallel(in query, cmp1Id,
            static (EntityRef entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachEntityStampParallel(in query, componentIds,
            static (EntityRef entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachEntityStampParallel(in query, cmp1Id,
            static (EntityRef entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachEntityStampParallel<Cmp1, Cmp2>(entities, in query, componentIds,
            static (EntityRef entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachStampParallel<Cmp1, Cmp2>(entities, componentIds,
            static (in Stamp first, in Stamp second) => _ = first.GetHashCode() + second.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachStampParallel(entities, in query, cmp1Id,
            static (in Stamp stamp) => _ = stamp.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachEntityStampParallel(entities, componentIds,
            static (EntityRef entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachStampParallel<Cmp1>(entityArray,
            static (in Stamp stamp) => _ = stamp.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachEntityStampParallel<Cmp1>(entityArray,
            static (EntityRef entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode(),
            workerCount: 2).Invoke();
        world.ForEachEntityStampParallel<Cmp1>(entities, cmp1Id,
            static (EntityRef entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode(),
            workerCount: 2).Invoke();

        var functorStamp = new StampFunctor();
        world.ForEachStamp<Cmp1>(in query, ref functorStamp).Invoke();
        world.ForEachStamp(in query, cmp1Id, ref functorStamp).Invoke();
        world.ForEachStamp(in query, stackalloc ComponentId[] { cmp1Id }, ref functorStamp).Invoke();
        world.ForEachStamp(in query, singleComponentId, ref functorStamp).Invoke();
        world.ForEachStamp(entities, in query, cmp1Id, ref functorStamp).Invoke();
        world.ForEachStamp(entities, stackalloc ComponentId[] { cmp1Id }, ref functorStamp).Invoke();
        world.ForEachStamp(entities, in query, singleComponentId, ref functorStamp).Invoke();
        var functorEntityStamp = new EntityStampFunctor();
        world.ForEachEntityStamp<Cmp1>(in query, ref functorEntityStamp).Invoke();
        world.ForEachEntityStamp<Cmp1>(entities, ref functorEntityStamp).Invoke();
        world.ForEachEntityStamp<Cmp1>(entityArray, in query, ref functorEntityStamp).Invoke();
        var functorEntityStampPair = new EntityStampPairFunctor();
        world.ForEachEntityStamp<Cmp1, Cmp2>(entities, componentIds, ref functorEntityStampPair).Invoke();
        world.ForEachEntityStamp(entities, in query, componentIds, ref functorEntityStampPair).Invoke();
        var functorContext = new StampContextFunctor();
        world.ForEachStamp<Cmp1>(in query, ref context, ref functorContext).Invoke();
        world.ForEachStamp<Cmp1>(entities, cmp1Id, ref context, ref functorContext).Invoke();
        var functorParallelStampContext = new StampParallelContextFunctor();
        world.ForEachStampParallel<Cmp1>(in query, in readContext, ref functorParallelStampContext, workerCount: 2).Invoke();
        world.ForEachStampParallel(in query, cmp1Id, ref functorStamp, workerCount: 2).Invoke();
        world.ForEachStampParallel(in query, singleComponentId, ref functorStamp, workerCount: 2).Invoke();
        world.ForEachStampParallel(entities, in query, singleComponentId, ref functorStamp, workerCount: 2).Invoke();
        world.ForEachEntityStampParallel(entities, in query, componentIds, ref functorEntityStampPair, workerCount: 2).Invoke();
        var functorEntityContext = new EntityStampContextFunctor();
        world.ForEachEntityStamp<Cmp1>(entities, ref context, ref functorEntityContext).Invoke();
        world.ForEachEntityStamp<Cmp1>(entityArray, in query, ref context, ref functorEntityContext).Invoke();
    }
}
