using System;
using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

public struct Cmp1 { public int Value; }
public struct Cmp2 { public int Value; }
public struct Cmp3 { public int Value; }
public struct Cmp4 { public int Value; }

public struct Context
{
    public int Value;
}

public struct FunctorR : IForEach
{
    public void Invoke(ref readonly Cmp1 cmp1) => _ = cmp1.Value;
}

public struct FunctorW : IForEach
{
    public void Invoke(ref Cmp1 cmp1) => cmp1.Value++;
}

public struct FunctorI : IForEach
{
    public void Invoke(in Cmp1 cmp1) => _ = cmp1.Value;
}

public struct FunctorV : IForEach
{
    public void Invoke(Cmp1 cmp1) => _ = cmp1.Value;
}

public struct FunctorRW : IForEach
{
    public void Invoke(ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value;
}

public struct FunctorParallelContext : IForEachContext<Context>
{
    public void Invoke(in Context context, ref Cmp1 cmp1) => cmp1.Value += context.Value;
}

public struct FunctorContext : IForEachContext<Context>
{
    public void Invoke(ref Context context, ref Cmp1 cmp1) => cmp1.Value += context.Value;
}

public struct FunctorEntityW : IForEachEntity
{
    public void Invoke(Entity entity, ref Cmp1 cmp1) => cmp1.Value += entity.Index;
}

public struct FunctorEntityRW : IForEachEntity
{
    public void Invoke(Entity entity, ref Cmp1 cmp1, in Cmp2 cmp2)
        => cmp1.Value += entity.Index + cmp2.Value;
}

public struct FunctorEntityContext : IForEachContextEntity<Context>
{
    public void Invoke(ref Context context, Entity entity, ref Cmp1 cmp1)
        => cmp1.Value += context.Value + entity.Index;
}

public struct FunctorEntity : IForEachEntity
{
    public void Invoke(Entity entity) => _ = entity;
}

public struct WhereFunctorContext : IForEachContext<Context>
{
    public void Invoke(ref Context context, ref Cmp3 cmp3) => cmp3.Value += context.Value;
}

public struct WhereEntityFunctorContext : IForEachContextEntity<Context>
{
    public void Invoke(ref Context context, Entity entity, ref Cmp3 cmp3)
        => cmp3.Value += context.Value + entity.Index;
}

public struct WherePredicateContext : IWherePredicate
{
    public bool Invoke(ref Context context, in Cmp3 cmp3, in Cmp4 cmp4)
    {
        context.Value++;
        return cmp3.Value < cmp4.Value;
    }
}

public struct WhereEntityPredicateContext : IWherePredicate
{
    public bool Invoke(ref Context context, Entity entity, in Cmp3 cmp3, in Cmp4 cmp4)
    {
        context.Value += entity.Index;
        return cmp3.Value < cmp4.Value;
    }
}

public struct WhereEntityPredicate : IWherePredicate
{
    public bool Invoke(Entity entity, in Cmp3 cmp3) => cmp3.Value < entity.Index;
}

public struct WherePredicate : IWherePredicate
{
    public bool Invoke(in Cmp3 cmp3, in Cmp4 cmp4) => cmp3.Value < cmp4.Value;
}

public struct StampFunctor : IForEach
{
    public void Invoke(in Stamp stamp) => _ = stamp;
}

public struct EntityStampFunctor : IForEachEntity
{
    public void Invoke(Entity entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode();
}

public struct EntityStampPairFunctor : IForEachEntity
{
    public void Invoke(Entity entity, in Stamp first, in Stamp second)
        => _ = entity.Index + first.GetHashCode() + second.GetHashCode();
}

public struct StampContextFunctor : IForEachContext<Context>
{
    public void Invoke(ref Context context, in Stamp stamp) => context.Value += stamp.GetHashCode();
}

public struct StampParallelContextFunctor : IForEachContext<Context>
{
    public void Invoke(in Context context, in Stamp stamp) => _ = context.Value + stamp.GetHashCode();
}

public struct EntityStampContextFunctor : IForEachContextEntity<Context>
{
    public void Invoke(ref Context context, Entity entity, in Stamp stamp)
        => context.Value += entity.Index + stamp.GetHashCode();
}

public struct GenericFunctor<TFirst, TSecond> : IForEach
{
    public void Invoke(in TFirst first, in TSecond second) { }
}

public struct GenericEntityFunctor<TFirst, TSecond> : IForEachEntity
{
    public void Invoke(Entity entity, in TFirst first, in TSecond second) { }
}

public struct GenericContextFunctor<TFirst, TSecond> : IForEachContext<Context>
{
    public void Invoke(ref Context context, in TFirst first, in TSecond second) => context.Value++;
}

public struct GenericEntityContextFunctor<T> : IForEachContextEntity<Context>
{
    public void Invoke(ref Context context, Entity entity, in T value) => context.Value += entity.Index;
}

public struct GenericParallelContextFunctor<T> : IForEachContext<Context>
{
    public void Invoke(in Context context, in T value) => _ = context.Value + value.GetHashCode();
}

public struct GenericParallelEntityContextFunctor<T> : IForEachContextEntity<Context>
{
    public void Invoke(in Context context, Entity entity, in T value) => _ = context.Value + entity.Index + value.GetHashCode();
}

public struct Cmp1Cmp2Comparer : IComponentComparer
{
    public int Invoke(in Cmp1 left1, in Cmp2 left2, in Cmp1 right1, in Cmp2 right2)
    {
        int first = left1.Value.CompareTo(right1.Value);
        return first != 0 ? first : left2.Value.CompareTo(right2.Value);
    }
}

/// <summary>
/// Compile-time call-site matrix for every API family and grammar branch in API-GRAMMAR.md.
/// The consumer project must compile these calls before the generator test suite can run.
/// </summary>
public static class ApiGrammarProof
{
    public const int Compiled = 1;

    /// <summary>Registers concrete fixture rows so the generic-binding generator discovers them.</summary>
    private static ComponentLayoutRegistry RegisterComponents()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.Register<Cmp1>(new SchemaId(920001));
        layouts.Register<Cmp2>(new SchemaId(920002));
        layouts.Register<Cmp3>(new SchemaId(920003));
        layouts.Register<Cmp4>(new SchemaId(920004));
        layouts.Register<Dead>(new SchemaId(920005));
        layouts.Register<Alive>(new SchemaId(920006));
        layouts.Register<NeedsRespawn>(new SchemaId(920007));
        return layouts;
    }

    /// <summary>Executes every grammar section against a small, valid world.</summary>
    public static void Run()
    {
        var layouts = RegisterComponents();
        ComponentId cmp1Id = layouts.GetPrimary<Cmp1>();
        ComponentId cmp2Id = layouts.GetPrimary<Cmp2>();
        ComponentId cmp3Id = layouts.GetPrimary<Cmp3>();
        ComponentId cmp4Id = layouts.GetPrimary<Cmp4>();
        ComponentId deadId = layouts.GetPrimary<Dead>();
        ComponentId aliveId = layouts.GetPrimary<Alive>();
        ReadOnlySpan<ComponentId> allComponentIds = stackalloc ComponentId[]
        {
            cmp1Id,
            cmp2Id,
            cmp3Id,
            cmp4Id
        };
        ReadOnlySpan<ComponentId> componentIds = stackalloc ComponentId[] { cmp1Id, cmp2Id };
        ReadOnlySpan<ComponentId> singleComponentId = stackalloc ComponentId[] { cmp1Id };

        using var world = new World(layouts);
        var entityArray = new Entity[2];
        world.Create(allComponentIds, entityArray.Length, entityArray);
        Span<Entity> additionalEntity = stackalloc Entity[1];
        world.Create(allComponentIds, 1, additionalEntity);
        Entity entity = additionalEntity[0];

        for (int index = 0; index < entityArray.Length; index++)
        {
            world.GetRef<Cmp3>(entityArray[index], cmp3Id).Value = 2;
            world.GetRef<Cmp4>(entityArray[index], cmp4Id).Value = 1;
        }

        world.GetRef<Cmp3>(entity, cmp3Id).Value = 2;
        world.GetRef<Cmp4>(entity, cmp4Id).Value = 1;

        Query query = world.CreateQuery(QuerySpec.WhereAll(allComponentIds));
        ReadOnlySpan<Entity> entities = entityArray;
        Context context = default;
        Context readContext = new() { Value = 1 };

        Iteration(world, in query, entities, entityArray, cmp1Id, cmp2Id,
            componentIds, singleComponentId, ref context, in readContext);
        Stamps(world, in query, entities, entityArray, cmp1Id, cmp2Id,
            componentIds, singleComponentId, ref context, in readContext);
        Queries(world, in query, cmp1Id, cmp2Id, componentIds);
        WherePipeline(world, in query, deadId, aliveId, ref context);
        OpenGenericFunctors(world, in query, entities, entityArray, cmp1Id, cmp2Id,
            componentIds, ref context);
        OrderedIteration(world, in query, cmp1Id, cmp2Id, componentIds, ref context);

        // Structural mutations consume and destroy the fixture entities, so run last.
        Structural(world, in query, entities, entity, cmp1Id, cmp2Id, componentIds);
    }

    public static void Iteration(
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
        world.ForEach(in query, static (ref Cmp1 cmp1) => cmp1.Value++);
        world.ForEach<Cmp1>(in query, cmp1Id, static (in Cmp1 cmp1) => _ = cmp1.Value);
        world.ForEach<Cmp1, Cmp2>(in query, componentIds,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value);
        world.ForEach<Cmp1, Cmp2>(in query, cmp1Id, cmp2Id,
            static (Cmp1 cmp1, ref Cmp2 cmp2) => cmp2.Value += cmp1.Value);
        world.ForEach<Cmp1, Cmp2>(in query, componentIds,
            static (ref readonly Cmp1 cmp1, Cmp2 cmp2) => _ = cmp1.Value + cmp2.Value);
        world.ForEach(in query, ref context,
            static (ref Context state, in Cmp1 cmp1, ref Cmp2 cmp2) =>
            {
                state.Value += cmp1.Value;
                cmp2.Value += state.Value;
            });
        world.ForEach<Context, Cmp1, Cmp2>(in query, cmp1Id, cmp2Id, ref context,
            static (ref Context state, in Cmp1 cmp1, ref Cmp2 cmp2) =>
                cmp2.Value += state.Value + cmp1.Value);

        // Query-wide entity-aware callbacks.
        world.ForEachEntity(in query, static (Entity entity, in Cmp1 cmp1) => _ = entity.Index + cmp1.Value);
        world.ForEachEntity<Cmp1>(in query, cmp1Id,
            static (Entity entity, ref Cmp1 cmp1) => cmp1.Value += entity.Index);
        world.ForEachEntity<Cmp1, Cmp2>(in query, componentIds,
            static (Entity entity, in Cmp1 cmp1, ref Cmp2 cmp2) => cmp2.Value += entity.Index + cmp1.Value);
        world.ForEachEntity<Context, Cmp1>(in query, ref context,
            static (ref Context state, Entity entity, in Cmp1 cmp1) => state.Value += entity.Index + cmp1.Value);
        world.ForEachEntity<Context, Cmp1>(in query, cmp1Id, ref context,
            static (ref Context state, Entity entity, in Cmp1 cmp1) => state.Value += entity.Index + cmp1.Value);

        // Entity-list delegates: span/array, optional query, selectors, and context.
        world.ForEach(entities, static (ref Cmp1 cmp1) => cmp1.Value++);
        world.ForEach<Cmp1>(entities, in query, cmp1Id,
            static (ref Cmp1 cmp1) => cmp1.Value++);
        world.ForEach<Cmp1, Cmp2>(entities, in query, componentIds,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value);
        world.ForEach<Cmp1, Cmp2>(entities, cmp1Id, cmp2Id,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value);
        world.ForEach(entities, in query, ref context,
            (ForEachContextAction<Context, Cmp1>)AddContextFromCmp1);
        world.ForEach<Context, Cmp1>(entities, in query, cmp1Id, ref context,
            static (ref Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value);
        world.ForEach<Context, Cmp1>(entities, singleComponentId, ref context,
            static (ref Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value);
        world.ForEach(entityArray, static (ref Cmp1 cmp1) => cmp1.Value++);
        world.ForEach<Cmp1>(entityArray, in query, cmp1Id,
            static (ref Cmp1 cmp1) => cmp1.Value++);
        world.ForEachEntity<Cmp1>(entities, in query, cmp1Id,
            static (Entity entity, ref Cmp1 cmp1) => cmp1.Value += entity.Index);
        world.ForEachEntity<Cmp1, Cmp2>(entities, componentIds,
            static (Entity entity, in Cmp1 cmp1, ref Cmp2 cmp2) => cmp2.Value += entity.Index + cmp1.Value);
        world.ForEachEntity(entities, in query,
            static (Entity entity) => _ = entity.Index);
        world.ForEachEntity(entities,
            static (Entity entity) => _ = entity.Index);
        world.ForEachEntity(entities, ref context,
            static (ref Context state, Entity entity) => state.Value += entity.Index);
        world.ForEachEntity(entityArray, in query,
            static (Entity entity, ref Cmp1 cmp1) => cmp1.Value += entity.Index);
        world.ForEachEntity<Context, Cmp1>(entityArray, in query, cmp1Id, ref context,
            static (ref Context state, Entity entity, ref Cmp1 cmp1) => cmp1.Value += state.Value + entity.Index);

        // Typed functors: row modes R/W/I/V/RW, selectors, entity access, and context.
        var functorW = new FunctorW();
        world.ForEach(in query, ref functorW);
        world.ForEach(entities, in query, ref functorW);
        world.ForEach(entities, ref functorW);
        var functorR = new FunctorR();
        world.ForEach(in query, in functorR);
        var functorI = new FunctorI();
        world.ForEach(in query, in functorI);
        var functorV = new FunctorV();
        world.ForEach(in query, functorV);
        world.ForEach(in query, cmp1Id, ref functorW);
        world.ForEach(entities, in query, cmp1Id, ref functorW);
        world.ForEach(entityArray, cmp1Id, ref functorW);
        var functorRW = new FunctorRW();
        world.ForEach(in query, componentIds, ref functorRW);
        world.ForEach(entities, in query, componentIds, ref functorRW);
        world.ForEach(entityArray, componentIds, ref functorRW);
        var functorEntityW = new FunctorEntityW();
        world.ForEachEntity(in query, ref functorEntityW);
        world.ForEachEntity(entities, in query, ref functorEntityW);
        world.ForEachEntity(entities, ref functorEntityW);
        world.ForEachEntity(in query, cmp1Id, ref functorEntityW);
        world.ForEachEntity(entityArray, cmp1Id, ref functorEntityW);
        var functorEntityRW = new FunctorEntityRW();
        world.ForEachEntity(entities, in query, componentIds, ref functorEntityRW);
        world.ForEachEntity(entities, componentIds, ref functorEntityRW);
        var functorEntity = new FunctorEntity();
        world.ForEachEntity(in query, ref functorEntity);
        world.ForEachEntity(entities, in query, ref functorEntity);
        world.ForEachEntity(entities, ref functorEntity);

        // Parallel delegates: query-wide callback and context forms.
        world.ForEachParallel(in query, static (ref Cmp1 cmp1) => cmp1.Value++, workerCount: 2);
        world.ForEachParallel<Cmp1>(in query, cmp1Id,
            static (ref Cmp1 cmp1) => cmp1.Value++, workerCount: 2);
        world.ForEachParallel<Cmp1, Cmp2>(in query, componentIds,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value,
            workerCount: 2);
        world.ForEachParallel(in query, in readContext,
            static (in Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value,
            workerCount: 2);
        world.ForEachParallel<Context, Cmp1>(in query, cmp1Id, in readContext,
            static (in Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value,
            workerCount: 2);
        world.ForEachParallel<Context, Cmp1>(in query, singleComponentId, readContext,
            static (Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value,
            workerCount: 2);
        world.ForEach(in query, in readContext,
            static (in Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value);
        world.ForEach(in query, readContext,
            static (Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value);
        world.ForEachEntity(in query, readContext,
            static (Context state, Entity entity, ref Cmp1 cmp1) => cmp1.Value += state.Value + entity.Index);
        world.ForEachEntityParallel(in query,
            static (Entity entity) => _ = entity.Index,
            workerCount: 2);
        world.ForEachEntityParallel<Cmp1>(in query, cmp1Id,
            static (Entity entity, ref Cmp1 cmp1) => cmp1.Value += entity.Index,
            workerCount: 2);
        world.ForEachEntityParallel<Context, Cmp1>(in query, in readContext,
            static (in Context state, Entity entity, ref Cmp1 cmp1) => cmp1.Value += state.Value + entity.Index,
            workerCount: 2);

        // Parallel entity-list delegates and entity-aware variants.
        world.ForEachParallel(entities, in query,
            static (ref Cmp1 cmp1) => cmp1.Value++,
            workerCount: 2);
        world.ForEachParallel(entityArray, static (ref Cmp1 cmp1) => cmp1.Value++, workerCount: 2);
        world.ForEachParallel(entityArray, in query, componentIds,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value,
            workerCount: 2);
        world.ForEachParallel<Context, Cmp1>(entities, in query, cmp1Id, in readContext,
            static (in Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value,
            workerCount: 2);
        world.ForEachParallel<Context, Cmp1>(entities, singleComponentId, readContext,
            static (Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value,
            workerCount: 2);
        world.ForEachParallel<Cmp1>(entities, cmp1Id,
            static (ref Cmp1 cmp1) => cmp1.Value++,
            workerCount: 2);
        world.ForEachParallel<Cmp1, Cmp2>(entities, in query, componentIds,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value,
            workerCount: 2);
        world.ForEachEntityParallel(entities,
            static (Entity entity) => _ = entity.Index,
            workerCount: 2);
        world.ForEachEntityParallel(entities, in query,
            static (Entity entity) => _ = entity.Index,
            workerCount: 2);
        world.ForEachEntityParallel(entityArray, static (Entity entity) => _ = entity.Index, workerCount: 2);
        world.ForEachEntityParallel<Cmp1, Cmp2>(entities, cmp1Id, cmp2Id,
            static (Entity entity, ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += entity.Index + cmp2.Value,
            workerCount: 2);
        world.ForEachEntityParallel<Context, Cmp1>(entities, in query, in readContext,
            static (in Context state, Entity entity, ref Cmp1 cmp1) => cmp1.Value += state.Value + entity.Index,
            workerCount: 2);
        world.ForEachEntityParallel<Context, Cmp1>(entityArray, cmp1Id, in readContext,
            static (in Context state, Entity entity, ref Cmp1 cmp1) => cmp1.Value += state.Value + entity.Index,
            workerCount: 2);

        // Parallel functors use the same selector/context order and retain caller state by ref.
        var functorContext = new FunctorContext();
        world.ForEach(in query, ref context, ref functorContext);
        world.ForEach(in query, cmp1Id, ref context, ref functorContext);
        world.ForEach(entityArray, in query, ref context, ref functorContext);
        var functorEntityContext = new FunctorEntityContext();
        world.ForEachEntity(in query, ref context, ref functorEntityContext);
        world.ForEachEntity(entityArray, ref context, ref functorEntityContext);
        var functorParallelW = new FunctorW();
        world.ForEachParallel(in query, ref functorParallelW, workerCount: 2);
        world.ForEachParallel(entityArray, in query, ref functorParallelW, workerCount: 2);
        world.ForEachParallel(entityArray, ref functorParallelW, workerCount: 2);
        world.ForEachParallel(in query, cmp1Id, ref functorParallelW, workerCount: 2);
        world.ForEachParallel(in query, componentIds, ref functorRW, workerCount: 2);
        world.ForEachParallel(entities, in query, componentIds, ref functorRW, workerCount: 2);
        world.ForEachParallel(entityArray, componentIds, ref functorRW, workerCount: 2);
        var parallelReadFunctor = new FunctorR();
        world.ForEachParallel(in query, in parallelReadFunctor, workerCount: 2);
        var parallelValueFunctor = new FunctorV();
        world.ForEachParallel(in query, parallelValueFunctor, workerCount: 2);
        var parallelContextFunctor = new FunctorParallelContext();
        world.ForEachParallel(in query, in readContext, ref parallelContextFunctor, workerCount: 2);
        world.ForEachParallel(in query, cmp1Id, in readContext, ref parallelContextFunctor, workerCount: 2);
        world.ForEachParallel(entityArray, in query, in readContext, ref parallelContextFunctor, workerCount: 2);
        world.ForEachParallel(entityArray, in query, componentIds, ref functorRW, workerCount: 2);
        world.ForEachParallel(entityArray, in query, cmp1Id, ref functorParallelW, workerCount: 2);
        var functorParallelEntity = new FunctorEntity();
        world.ForEachEntityParallel(in query, ref functorParallelEntity, workerCount: 2);
        world.ForEachEntityParallel(entityArray, in query, ref functorParallelEntity, workerCount: 2);
        world.ForEachEntityParallel(entityArray, ref functorParallelEntity, workerCount: 2);
        world.ForEachEntityParallel(in query, cmp1Id, ref functorEntityW, workerCount: 2);
        world.ForEachEntityParallel(entities, in query, componentIds, ref functorEntityRW, workerCount: 2);
        world.ForEachEntityParallel(entityArray, componentIds, ref functorEntityRW, workerCount: 2);
    }

    public static void OrderedIteration(
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
            .First();
        _ = world.WhereEntity(in query,
                static (Entity entity, in Cmp3 cmp3, in Cmp4 cmp4) => cmp3.Value < entity.Index + cmp4.Value)
            .OrderBy(cmp1Id, cmp2Id, ref comparer)
            .ThenBy(componentIds, ref comparer)
            .FirstEntity();

        var predicate = new WherePredicate();
        _ = world.Where(in query, ref predicate)
            .OrderBy(ref comparer)
            .First();
        var entityPredicate = new WhereEntityPredicate();
        _ = world.WhereEntity(in query, ref entityPredicate)
            .OrderBy(ref comparer)
            .First();

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
            .First();
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
            .ForEach(static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value);
        world.WhereEntity(in query,
                static (Entity current, in Cmp3 cmp3, in Cmp4 cmp4) => cmp3.Value < current.Index + cmp4.Value)
            .OrderBy(ref comparer)
            .ForEachEntity(static (Entity current, ref Cmp1 cmp1) => cmp1.Value += current.Index);
        var orderedFunctor = new FunctorRW();
        world.Where(in query, ref predicate)
            .OrderBy(ref comparer)
            .ForEach(componentIds, ref orderedFunctor);

        _ = ordered.First();
        _ = ordered.First(static entity => entity.IsValid);
        _ = ordered.FirstEntity();
        _ = ordered.FirstEntity(static entity => entity.IsValid);

        ordered.ForEach(static (ref Cmp1 cmp1) => cmp1.Value++);
        ordered.ForEach<Cmp1>(cmp1Id, static (ref Cmp1 cmp1) => cmp1.Value++);
        ordered.ForEach<Cmp1, Cmp2>(componentIds,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value);
        ordered.ForEach<Cmp1, Cmp2>(cmp1Id, cmp2Id,
            static (ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += cmp2.Value);
        ordered.ForEachEntity(static (Entity entity) => _ = entity.Index);
        ordered.ForEachEntity<Cmp1, Cmp2>(cmp1Id, cmp2Id,
            static (Entity entity, ref Cmp1 cmp1, in Cmp2 cmp2) => cmp1.Value += entity.Index + cmp2.Value);
        ordered.ForEach(ref context,
            static (ref Context state, ref Cmp1 cmp1) => cmp1.Value += state.Value++);
        ordered.ForEachEntity(ref context,
            static (ref Context state, Entity entity, ref Cmp1 cmp1) => cmp1.Value += state.Value + entity.Index);

        var functor = new FunctorRW();
        ordered.ForEach(componentIds, ref functor);
        var entityFunctor = new FunctorEntityRW();
        ordered.ForEachEntity(cmp1Id, cmp2Id, ref entityFunctor);
        var contextFunctor = new FunctorContext();
        ordered.ForEach(ref context, ref contextFunctor);
        var entityContextFunctor = new FunctorEntityContext();
        ordered.ForEachEntity(ref context, ref entityContextFunctor);
    }

    private static void AddContextFromCmp1(ref Context context, in Cmp1 cmp1)
        => context.Value += cmp1.Value;

    public static void Stamps(
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
        world.ForEachStamp<Cmp1>(in query, static (in Stamp stamp) => _ = stamp);
        world.ForEachEntityStamp<Cmp1, Cmp2>(in query, cmp1Id, cmp2Id,
            static (Entity entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode());
        world.ForEachStamp<Cmp1, Cmp2>(in query, componentIds,
            static (in Stamp first, in Stamp second) => _ = first.GetHashCode() + second.GetHashCode());
        world.ForEachStamp(in query, componentIds,
            static (in Stamp first, in Stamp second) => _ = first.GetHashCode() + second.GetHashCode());
        world.ForEachStamp(in query, cmp1Id,
            static (in Stamp stamp) => _ = stamp.GetHashCode());
        world.ForEachEntityStamp(in query, cmp1Id,
            static (Entity entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode());
        world.ForEachEntityStamp(in query, componentIds,
            static (Entity entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode());
        world.ForEachStamp<Context, Cmp1, Cmp2>(in query, cmp1Id, cmp2Id, ref context,
            static (ref Context state, in Stamp first, in Stamp second) => state.Value += first.GetHashCode() + second.GetHashCode());
        world.ForEachStamp<Context, Cmp1, Cmp2>(in query, componentIds, ref context,
            static (ref Context state, in Stamp first, in Stamp second) => state.Value += first.GetHashCode() + second.GetHashCode());
        world.ForEachEntityStamp<Context, Cmp1>(in query, ref context,
            static (ref Context state, Entity entity, in Stamp stamp) => state.Value += entity.Index + stamp.GetHashCode());
        world.ForEachEntityStamp<Context, Cmp1>(in query, cmp1Id, ref context,
            static (ref Context state, Entity entity, in Stamp stamp) => state.Value += entity.Index + stamp.GetHashCode());

        // Entity-list stamp callbacks.
        world.ForEachStamp(entities, in query, cmp1Id,
            static (in Stamp stamp) => _ = stamp);
        world.ForEachStamp(entities, cmp1Id,
            static (in Stamp stamp) => _ = stamp.GetHashCode());
        world.ForEachEntityStamp(entities, cmp1Id,
            static (Entity entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode());
        world.ForEachEntityStamp<Cmp1>(entities, in query,
            static (Entity entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode());
        world.ForEachStamp<Cmp1, Cmp2>(entities, componentIds,
            static (in Stamp first, in Stamp second) => _ = first.GetHashCode() + second.GetHashCode());
        world.ForEachEntityStamp(entities, componentIds,
            static (Entity entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode());
        world.ForEachStamp<Cmp1>(entities,
            static (in Stamp stamp) => _ = stamp.GetHashCode());
        world.ForEachEntityStamp<Cmp1>(entities, cmp1Id,
            static (Entity entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode());
        world.ForEachEntityStamp<Cmp1>(entityArray, in query,
            static (Entity entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode());
        world.ForEachStamp<Context, Cmp1>(entities, in query, ref context,
            static (ref Context state, in Stamp stamp) => state.Value += stamp.GetHashCode());
        world.ForEachStamp<Context, Cmp1>(entities, cmp1Id, ref context,
            static (ref Context state, in Stamp stamp) => state.Value += stamp.GetHashCode());
        world.ForEachStamp<Context, Cmp1>(entities, stackalloc ComponentId[] { cmp1Id }, ref context,
            static (ref Context state, in Stamp stamp) => state.Value += stamp.GetHashCode());
        world.ForEachEntityStamp<Context, Cmp1>(entities, ref context,
            static (ref Context state, Entity entity, in Stamp stamp) => state.Value += entity.Index + stamp.GetHashCode());
        world.ForEachEntityStamp<Context, Cmp1>(entityArray, in query, cmp1Id, ref context,
            static (ref Context state, Entity entity, in Stamp stamp) => state.Value += entity.Index + stamp.GetHashCode());

        // Parallel stamp callbacks, followed by functor and context forms.
        world.ForEachStampParallel<Cmp1>(in query, cmp1Id,
            static (in Stamp stamp) => _ = stamp,
            workerCount: 2);
        world.ForEachStampParallel(in query, cmp1Id,
            static (in Stamp stamp) => _ = stamp.GetHashCode(),
            workerCount: 2);
        world.ForEachEntityStampParallel<Cmp1, Cmp2>(in query, componentIds,
            static (Entity entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode(),
            workerCount: 2);
        world.ForEachStampParallel<Context, Cmp1>(in query, in readContext,
            static (in Context state, in Stamp stamp) => _ = state.Value + stamp.GetHashCode(),
            workerCount: 2);
        world.ForEachEntityStampParallel<Context, Cmp1>(in query, in readContext,
            static (in Context state, Entity entity, in Stamp stamp) => _ = state.Value + entity.Index + stamp.GetHashCode(),
            workerCount: 2);
        world.ForEachStampParallel<Cmp1>(entities, in query, cmp1Id,
            static (in Stamp stamp) => _ = stamp,
            workerCount: 2);
        world.ForEachStampParallel<Cmp1, Cmp2>(in query, componentIds,
            static (in Stamp first, in Stamp second) => _ = first.GetHashCode() + second.GetHashCode(),
            workerCount: 2);
        world.ForEachEntityStampParallel(in query, cmp1Id,
            static (Entity entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode(),
            workerCount: 2);
        world.ForEachEntityStampParallel(in query, componentIds,
            static (Entity entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode(),
            workerCount: 2);
        world.ForEachEntityStampParallel(in query, cmp1Id,
            static (Entity entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode(),
            workerCount: 2);
        world.ForEachEntityStampParallel<Cmp1, Cmp2>(entities, in query, componentIds,
            static (Entity entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode(),
            workerCount: 2);
        world.ForEachStampParallel<Cmp1, Cmp2>(entities, componentIds,
            static (in Stamp first, in Stamp second) => _ = first.GetHashCode() + second.GetHashCode(),
            workerCount: 2);
        world.ForEachStampParallel(entities, in query, cmp1Id,
            static (in Stamp stamp) => _ = stamp.GetHashCode(),
            workerCount: 2);
        world.ForEachEntityStampParallel(entities, componentIds,
            static (Entity entity, in Stamp first, in Stamp second) => _ = entity.Index + first.GetHashCode() + second.GetHashCode(),
            workerCount: 2);
        world.ForEachStampParallel<Cmp1>(entityArray,
            static (in Stamp stamp) => _ = stamp.GetHashCode(),
            workerCount: 2);
        world.ForEachEntityStampParallel<Cmp1>(entityArray,
            static (Entity entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode(),
            workerCount: 2);
        world.ForEachEntityStampParallel<Cmp1>(entities, cmp1Id,
            static (Entity entity, in Stamp stamp) => _ = entity.Index + stamp.GetHashCode(),
            workerCount: 2);

        var functorStamp = new StampFunctor();
        world.ForEachStamp<Cmp1>(in query, ref functorStamp);
        world.ForEachStamp(in query, cmp1Id, ref functorStamp);
        world.ForEachStamp(in query, stackalloc ComponentId[] { cmp1Id }, ref functorStamp);
        world.ForEachStamp(in query, singleComponentId, ref functorStamp);
        world.ForEachStamp(entities, in query, cmp1Id, ref functorStamp);
        world.ForEachStamp(entities, stackalloc ComponentId[] { cmp1Id }, ref functorStamp);
        world.ForEachStamp(entities, in query, singleComponentId, ref functorStamp);
        var functorEntityStamp = new EntityStampFunctor();
        world.ForEachEntityStamp<Cmp1>(in query, ref functorEntityStamp);
        world.ForEachEntityStamp<Cmp1>(entities, ref functorEntityStamp);
        world.ForEachEntityStamp<Cmp1>(entityArray, in query, ref functorEntityStamp);
        var functorEntityStampPair = new EntityStampPairFunctor();
        world.ForEachEntityStamp<Cmp1, Cmp2>(entities, componentIds, ref functorEntityStampPair);
        world.ForEachEntityStamp(entities, in query, componentIds, ref functorEntityStampPair);
        var functorContext = new StampContextFunctor();
        world.ForEachStamp<Cmp1>(in query, ref context, ref functorContext);
        world.ForEachStamp<Cmp1>(entities, cmp1Id, ref context, ref functorContext);
        var functorParallelStampContext = new StampParallelContextFunctor();
        world.ForEachStampParallel<Cmp1>(in query, in readContext, ref functorParallelStampContext, workerCount: 2);
        world.ForEachStampParallel(in query, cmp1Id, ref functorStamp, workerCount: 2);
        world.ForEachStampParallel(in query, singleComponentId, ref functorStamp, workerCount: 2);
        world.ForEachStampParallel(entities, in query, singleComponentId, ref functorStamp, workerCount: 2);
        world.ForEachEntityStampParallel(entities, in query, componentIds, ref functorEntityStampPair, workerCount: 2);
        var functorEntityContext = new EntityStampContextFunctor();
        world.ForEachEntityStamp<Cmp1>(entities, ref context, ref functorEntityContext);
        world.ForEachEntityStamp<Cmp1>(entityArray, in query, ref context, ref functorEntityContext);
    }

    public static void Structural(
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

    public static void Queries(World world, in Query query, ComponentId cmp1Id, ComponentId cmp2Id, ReadOnlySpan<ComponentId> ids)
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

    public static void WherePipeline(World world, in Query query, ComponentId deadId, ComponentId aliveId, ref Context context)
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

    public static void OpenGenericFunctors(
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
        world.ForEach(in query, cmp1Id, cmp2Id, typeof(GenericFunctor<,>));
        world.ForEach(in query, genericArguments, typeof(GenericFunctor<,>));
        world.ForEach(entityArray, in query, genericArguments, typeof(GenericFunctor<,>));
        world.ForEach(entityArray, cmp1Id, cmp2Id, typeof(GenericFunctor<,>));
        world.ForEach(entities, in query, cmp1Id, cmp2Id, typeof(GenericFunctor<,>));
        world.ForEach(entities, in query, genericArguments, typeof(GenericFunctor<,>));
        world.ForEach(entities, cmp1Id, cmp2Id, typeof(GenericFunctor<,>));
        world.ForEach(entities, genericArguments, typeof(GenericFunctor<,>));
        world.ForEachParallel(in query, genericArguments, typeof(GenericFunctor<,>), workerCount: 2);
        world.ForEachParallel(in query, cmp1Id, cmp2Id, typeof(GenericFunctor<,>), workerCount: 2);
        world.ForEachParallel(entityArray, in query, cmp1Id, cmp2Id, typeof(GenericFunctor<,>), workerCount: 2);
        world.ForEachParallel(entityArray, cmp1Id, cmp2Id, typeof(GenericFunctor<,>), workerCount: 2);
        world.ForEachParallel(entities, in query, genericArguments, typeof(GenericFunctor<,>), workerCount: 2);
        world.ForEachParallel(entities, genericArguments, typeof(GenericFunctor<,>), workerCount: 2);

        // Entity-aware generic functors.
        world.ForEachEntity(in query, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>));
        world.ForEachEntity(in query, genericArguments, typeof(GenericEntityFunctor<,>));
        world.ForEachEntity(entityArray, in query, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>));
        world.ForEachEntity(entityArray, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>));
        world.ForEachEntity(entities, in query, genericArguments, typeof(GenericEntityFunctor<,>));
        world.ForEachEntity(entities, genericArguments, typeof(GenericEntityFunctor<,>));
        world.ForEachEntityParallel(in query, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>), workerCount: 2);
        world.ForEachEntityParallel(in query, genericArguments, typeof(GenericEntityFunctor<,>), workerCount: 2);
        world.ForEachEntityParallel(in query, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>), workerCount: 2);
        world.ForEachEntityParallel(entityArray, in query, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>), workerCount: 2);
        world.ForEachEntityParallel(entityArray, cmp1Id, cmp2Id, typeof(GenericEntityFunctor<,>), workerCount: 2);
        world.ForEachEntityParallel(entities, in query, genericArguments, typeof(GenericEntityFunctor<,>), workerCount: 2);
        world.ForEachEntityParallel(entities, genericArguments, typeof(GenericEntityFunctor<,>), workerCount: 2);

        // Context-bearing forms; parallel Invoke methods expose a read-only context.
        world.ForEach(in query, ref context, cmp1Id, cmp2Id, typeof(GenericContextFunctor<,>));
        world.ForEach(entityArray, in query, ref context, cmp1Id, cmp2Id, typeof(GenericContextFunctor<,>));
        world.ForEach(entities, in query, ref context, genericArguments, typeof(GenericContextFunctor<,>));
        world.ForEach(entityArray, in query, ref context, genericArguments, typeof(GenericContextFunctor<,>));
        world.ForEach(entityArray, ref context, genericArguments, typeof(GenericContextFunctor<,>));
        world.ForEach(entities, ref context, cmp1Id, cmp2Id, typeof(GenericContextFunctor<,>));
        world.ForEachEntity(in query, ref context, cmp1Id, typeof(GenericEntityContextFunctor<>));
        world.ForEachEntity(entityArray, in query, ref context, cmp1Id, typeof(GenericEntityContextFunctor<>));
        world.ForEachEntity(entityArray, ref context, cmp1Id, typeof(GenericEntityContextFunctor<>));
        world.ForEachEntity(entities, ref context, cmp1Id, typeof(GenericEntityContextFunctor<>));
        world.ForEachEntity(in query, ref context, singleGenericArgument, typeof(GenericEntityContextFunctor<>));
        world.ForEachEntity(entityArray, in query, ref context, singleGenericArgument, typeof(GenericEntityContextFunctor<>));
        world.ForEachEntity(entityArray, ref context, singleGenericArgument, typeof(GenericEntityContextFunctor<>));
        world.ForEachEntity(entities, ref context, singleGenericArgument, typeof(GenericEntityContextFunctor<>));
        world.ForEachParallel(in query, ref context, cmp1Id, typeof(GenericParallelContextFunctor<>), workerCount: 2);
        world.ForEachParallel(entities, in query, ref context, cmp1Id, typeof(GenericParallelContextFunctor<>), workerCount: 2);
        world.ForEachParallel(entities, ref context, cmp1Id, typeof(GenericParallelContextFunctor<>), workerCount: 2);
        world.ForEachEntityParallel(in query, ref context, cmp1Id, typeof(GenericParallelEntityContextFunctor<>), workerCount: 2);
        world.ForEachEntityParallel(entityArray, in query, ref context, cmp1Id, typeof(GenericParallelEntityContextFunctor<>), workerCount: 2);
        world.ForEachEntityParallel(entityArray, ref context, cmp1Id, typeof(GenericParallelEntityContextFunctor<>), workerCount: 2);
        world.ForEachParallel(in query, ref context, singleGenericArgument, typeof(GenericParallelContextFunctor<>), workerCount: 2);
        world.ForEachParallel(entities, in query, ref context, singleGenericArgument, typeof(GenericParallelContextFunctor<>), workerCount: 2);
        world.ForEachParallel(entityArray, ref context, singleGenericArgument, typeof(GenericParallelContextFunctor<>), workerCount: 2);
        world.ForEachEntityParallel(in query, ref context, singleGenericArgument, typeof(GenericParallelEntityContextFunctor<>), workerCount: 2);
        world.ForEachEntityParallel(entityArray, in query, ref context, singleGenericArgument, typeof(GenericParallelEntityContextFunctor<>), workerCount: 2);
        world.ForEachEntityParallel(entityArray, ref context, singleGenericArgument, typeof(GenericParallelEntityContextFunctor<>), workerCount: 2);
    }
}
