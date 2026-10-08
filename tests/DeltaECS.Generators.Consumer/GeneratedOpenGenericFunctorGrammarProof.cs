using System;
using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

internal static class GeneratedOpenGenericFunctorGrammarProof
{
    internal static void Run(
        World world,
        in Query query,
        ReadOnlySpan<Entity> entities,
        Entity[] entityArray,
        ComponentId cmp1Id,
        ComponentId cmp2Id,
        ReadOnlySpan<ComponentId> genericArguments,
        ref Context context)
    {
        ReadOnlySpan<ComponentId> singleGenericArgument = stackalloc ComponentId[] { cmp1Id };

        // Component-only generic functors: world/query and span/array targets, positional IDs or D.
        world.ForEach(in query, cmp1Id, cmp2Id, typeof(GenericFunctor<,>)).Invoke();
        world.ForEach(in query, genericArguments, typeof(GenericFunctor<,>)).Invoke();
        world.ForEach(entityArray, in query, genericArguments, typeof(GenericFunctor<,>)).Invoke();
        world.ForEach(entityArray, cmp1Id, cmp2Id, typeof(GenericFunctor<,>)).Invoke();
        world.ForEach(entities, in query, cmp1Id, cmp2Id, typeof(GenericFunctor<,>)).Invoke();
        world.ForEach(entities, in query, genericArguments, typeof(GenericFunctor<,>)).Invoke();
        world.ForEach(entities, cmp1Id, cmp2Id, typeof(GenericFunctor<,>)).Invoke();
        world.ForEach(entities, genericArguments, typeof(GenericFunctor<,>)).Invoke();
        world.ForEachParallel(in query, genericArguments, typeof(GenericFunctor<,>), workerCount: 2).Invoke();
        world.ForEachParallel(in query, cmp1Id, cmp2Id, typeof(GenericFunctor<,>), workerCount: 2).Invoke();
        world.ForEachParallel(entityArray, in query, cmp1Id, cmp2Id, typeof(GenericFunctor<,>), workerCount: 2).Invoke();
        world.ForEachParallel(entityArray, cmp1Id, cmp2Id, typeof(GenericFunctor<,>), workerCount: 2).Invoke();
        world.ForEachParallel(entities, in query, genericArguments, typeof(GenericFunctor<,>), workerCount: 2).Invoke();
        world.ForEachParallel(entities, genericArguments, typeof(GenericFunctor<,>), workerCount: 2).Invoke();

        // Entity-aware generic functors.
        world.ForEachEntity(in query, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>)).Invoke();
        world.ForEachEntity(in query, genericArguments, typeof(GenericEntityFunctor<,>)).Invoke();
        world.ForEachEntity(entityArray, in query, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>)).Invoke();
        world.ForEachEntity(entityArray, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>)).Invoke();
        world.ForEachEntity(entities, in query, genericArguments, typeof(GenericEntityFunctor<,>)).Invoke();
        world.ForEachEntity(entities, genericArguments, typeof(GenericEntityFunctor<,>)).Invoke();
        world.ForEachEntityParallel(in query, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>), workerCount: 2).Invoke();
        world.ForEachEntityParallel(in query, genericArguments, typeof(GenericEntityFunctor<,>), workerCount: 2).Invoke();
        world.ForEachEntityParallel(in query, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>), workerCount: 2).Invoke();
        world.ForEachEntityParallel(entityArray, in query, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>), workerCount: 2).Invoke();
        world.ForEachEntityParallel(entityArray, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>), workerCount: 2).Invoke();
        world.ForEachEntityParallel(entities, in query, genericArguments, typeof(GenericEntityFunctor<,>), workerCount: 2).Invoke();
        world.ForEachEntityParallel(entities, genericArguments, typeof(GenericEntityFunctor<,>), workerCount: 2).Invoke();

        // Context-bearing forms; parallel Invoke methods expose a read-only context.
        world.ForEach(in query, ref context, cmp1Id, cmp2Id, typeof(GenericContextFunctor<,>)).Invoke(ref context);
        world.ForEach(entityArray, in query, ref context, cmp1Id, cmp2Id, typeof(GenericContextFunctor<,>)).Invoke(ref context);
        world.ForEach(entities, in query, ref context, genericArguments, typeof(GenericContextFunctor<,>)).Invoke(ref context);
        world.ForEach(entityArray, in query, ref context, genericArguments, typeof(GenericContextFunctor<,>)).Invoke(ref context);
        world.ForEach(entityArray, ref context, genericArguments, typeof(GenericContextFunctor<,>)).Invoke(ref context);
        world.ForEach(entities, ref context, cmp1Id, cmp2Id, typeof(GenericContextFunctor<,>)).Invoke(ref context);
        world.ForEachEntity(in query, ref context, cmp1Id, typeof(GenericEntityContextFunctor<>)).Invoke(ref context);
        world.ForEachEntity(entityArray, in query, ref context, cmp1Id, typeof(GenericEntityContextFunctor<>)).Invoke(ref context);
        world.ForEachEntity(entityArray, ref context, cmp1Id, typeof(GenericEntityContextFunctor<>)).Invoke(ref context);
        world.ForEachEntity(entities, ref context, cmp1Id, typeof(GenericEntityContextFunctor<>)).Invoke(ref context);
        world.ForEachEntity(in query, ref context, singleGenericArgument, typeof(GenericEntityContextFunctor<>)).Invoke(ref context);
        world.ForEachEntity(entityArray, in query, ref context, singleGenericArgument, typeof(GenericEntityContextFunctor<>)).Invoke(ref context);
        world.ForEachEntity(entityArray, ref context, singleGenericArgument, typeof(GenericEntityContextFunctor<>)).Invoke(ref context);
        world.ForEachEntity(entities, ref context, singleGenericArgument, typeof(GenericEntityContextFunctor<>)).Invoke(ref context);
        world.ForEachParallel(in query, ref context, cmp1Id, typeof(GenericParallelContextFunctor<>), workerCount: 2).Invoke(ref context);
        world.ForEachParallel(entities, in query, ref context, cmp1Id, typeof(GenericParallelContextFunctor<>), workerCount: 2).Invoke(ref context);
        world.ForEachParallel(entities, ref context, cmp1Id, typeof(GenericParallelContextFunctor<>), workerCount: 2).Invoke(ref context);
        world.ForEachEntityParallel(in query, ref context, cmp1Id, typeof(GenericParallelEntityContextFunctor<>), workerCount: 2).Invoke(ref context);
        world.ForEachEntityParallel(entityArray, in query, ref context, cmp1Id, typeof(GenericParallelEntityContextFunctor<>), workerCount: 2).Invoke(ref context);
        world.ForEachEntityParallel(entityArray, ref context, cmp1Id, typeof(GenericParallelEntityContextFunctor<>), workerCount: 2).Invoke(ref context);
        world.ForEachParallel(in query, ref context, singleGenericArgument, typeof(GenericParallelContextFunctor<>), workerCount: 2).Invoke(ref context);
        world.ForEachParallel(entities, in query, ref context, singleGenericArgument, typeof(GenericParallelContextFunctor<>), workerCount: 2).Invoke(ref context);
        world.ForEachParallel(entityArray, ref context, singleGenericArgument, typeof(GenericParallelContextFunctor<>), workerCount: 2).Invoke(ref context);
        world.ForEachEntityParallel(in query, ref context, singleGenericArgument, typeof(GenericParallelEntityContextFunctor<>), workerCount: 2).Invoke(ref context);
        world.ForEachEntityParallel(entityArray, in query, ref context, singleGenericArgument, typeof(GenericParallelEntityContextFunctor<>), workerCount: 2).Invoke(ref context);
        world.ForEachEntityParallel(entityArray, ref context, singleGenericArgument, typeof(GenericParallelEntityContextFunctor<>), workerCount: 2).Invoke(ref context);
    }
}
