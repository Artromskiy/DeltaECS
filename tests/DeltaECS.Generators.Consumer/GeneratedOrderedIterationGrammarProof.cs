using System;
using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

internal static class GeneratedOrderedIterationGrammarProof
{
    internal static void Run(
        World world,
        in Query query,
        ComponentId cmp1Id,
        ComponentId cmp2Id,
        ReadOnlySpan<ComponentId> componentIds,
        ref Context context)
    {
        var comparer = default(Cmp1Cmp2Comparer);
        OrderedQuery ordered = query
            .OrderBy(cmp1Id, cmp2Id, ref comparer)
            .ThenBy(ref comparer)
            .ThenBy(componentIds, ref comparer);

        _ = query.OrderBy(static (in Cmp1 left, in Cmp1 right) => left.Value.CompareTo(right.Value));
        _ = query.OrderBy(cmp1Id, in context,
            static (in Context state, in Cmp1 left, in Cmp1 right) =>
                (left.Value + state.Value).CompareTo(right.Value + state.Value));
        _ = query.OrderBy(componentIds[..1],
            static (in Cmp1 left, in Cmp1 right) => left.Value.CompareTo(right.Value));
        _ = query.OrderBy(static (Entity leftEntity, in Cmp1 left, Entity rightEntity, in Cmp1 right) =>
            (left.Value + leftEntity.Index).CompareTo(right.Value + rightEntity.Index));
        _ = query.OrderBy(in context,
                static (in Context state, Entity leftEntity, in Cmp1 left, Entity rightEntity, in Cmp1 right) =>
                    (left.Value + state.Value + leftEntity.Index).CompareTo(right.Value + state.Value + rightEntity.Index))
            .ThenBy(static (Entity leftEntity, in Cmp2 left, Entity rightEntity, in Cmp2 right) =>
                (left.Value + leftEntity.Index).CompareTo(right.Value + rightEntity.Index));

        _ = world.Where(in query,
                static (in Cmp3 cmp3, in Cmp4 cmp4) => cmp3.Value < cmp4.Value)
            .OrderBy(ref comparer)
            .ThenBy(cmp1Id, cmp2Id, ref comparer)
            .First().Invoke();
        _ = world.WhereEntity(in query,
                static (Entity entity, in Cmp3 cmp3, in Cmp4 cmp4) => cmp3.Value < entity.Index + cmp4.Value)
            .OrderBy(cmp1Id, cmp2Id, ref comparer)
            .ThenBy(componentIds, ref comparer)
            .FirstEntity().Invoke();

        var predicate = new WherePredicate();
        _ = world.Where(in query, ref predicate)
            .OrderBy(ref comparer)
            .First().Invoke();
        var entityPredicate = new WhereEntityPredicate();
        _ = world.WhereEntity(in query, ref entityPredicate)
            .OrderBy(ref comparer)
            .First().Invoke();

        _ = world.Where(in query, ref context,
                static (ref Context state, in Cmp3 cmp3, in Cmp4 cmp4) =>
                {
                    state.Value++;
                    return cmp3.Value < cmp4.Value;
                })
            .OrderBy(in context,
                static (in Context state, in Cmp1 left1, in Cmp2 left2, in Cmp1 right1, in Cmp2 right2) =>
                {
                    int first = (left1.Value + state.Value).CompareTo(right1.Value + state.Value);
                    return first != 0 ? first : left2.Value.CompareTo(right2.Value);
                })
            .ThenBy(ref comparer)
            .First().Invoke();
        world.Where(in query, ref context,
                static (ref Context state, in Cmp3 cmp3, in Cmp4 cmp4) =>
                {
                    state.Value++;
                    return cmp3.Value < cmp4.Value;
                })
            .OrderBy(in context,
                static (in Context state, in Cmp1 left1, in Cmp2 left2, in Cmp1 right1, in Cmp2 right2) =>
                {
                    int first = (left1.Value + state.Value).CompareTo(right1.Value + state.Value);
                    return first != 0 ? first : left2.Value.CompareTo(right2.Value);
                })
            .ThenBy(ref comparer)
            .ForEach(static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value).Invoke();
        world.WhereEntity(in query,
                static (Entity current, in Cmp3 cmp3, in Cmp4 cmp4) => cmp3.Value < current.Index + cmp4.Value)
            .OrderBy(ref comparer)
            .ForEachEntity(static (EntityRef current, ref Cmp1 cmp1) => cmp1.Value += current.Index).Invoke();
        var orderedFunctor = new FunctorRW();
        world.Where(in query, ref predicate)
            .OrderBy(ref comparer)
            .ForEach(componentIds, ref orderedFunctor).Invoke(ref orderedFunctor);

        _ = ordered.First().Invoke();
        _ = ordered.First(static entity => entity.IsValid).Invoke();
        _ = ordered.FirstEntity().Invoke();
        _ = ordered.FirstEntity(static entity => entity.IsValid).Invoke();

        ordered.ForEach(static (ref Cmp1 cmp1) => cmp1.Value++).Invoke();
        ordered.ForEach<Cmp1>(cmp1Id, static (ref Cmp1 cmp1) => cmp1.Value++).Invoke();
        ordered.ForEach<Cmp1, Cmp2>(componentIds,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value).Invoke();
        ordered.ForEach<Cmp1, Cmp2>(cmp1Id, cmp2Id,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value).Invoke();
        ordered.ForEachEntity(static (EntityRef entity) => _ = entity.Index).Invoke();
        ordered.ForEachEntity<Cmp1, Cmp2>(cmp1Id, cmp2Id,
            static (EntityRef entity, ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += entity.Index + cmp2.Value).Invoke();
        ordered.ForEach(ref context,
            static (ref Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value++).Invoke(ref context);
        ordered.ForEachEntity(ref context,
            static (ref Context state, EntityRef entity, ref Cmp1 cmp1) => cmp1.Value += state.Value + entity.Index).Invoke(ref context);

        var functor = new FunctorRW();
        ordered.ForEach(componentIds, ref functor).Invoke(ref functor);
        var entityFunctor = new FunctorEntityRW();
        ordered.ForEachEntity(cmp1Id, cmp2Id, ref entityFunctor).Invoke(ref entityFunctor);
        var contextFunctor = new FunctorContext();
        ordered.ForEach(ref context, ref contextFunctor).Invoke(ref context, ref contextFunctor);
        var entityContextFunctor = new FunctorEntityContext();
        ordered.ForEachEntity(ref context, ref entityContextFunctor).Invoke(ref context, ref entityContextFunctor);
    }
}
