namespace Delta.ECS.Runtime.Consumer;

using System.Threading;

public static partial class RuntimeApiGrammarProof
{
    private static void VerifyEntityIteration()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(934_001));
        using var world = new World(layouts);
        Span<Entity> entities = stackalloc Entity[8];
        Require(world.Create(positionId, entities.Length, entities) == entities.Length);
        Query query = world.WhereAll(positionId);

        int sequentialCount = 0;
        world.ForEachEntity(in query, _ => sequentialCount++).Invoke();
        Require(sequentialCount == entities.Length);

        var mutableContext = new CountContext();
        world.ForEachEntity(
            in query,
            ref mutableContext,
            static (ref CountContext context, EntityRef _) => context.Count++).Invoke(ref mutableContext);
        Require(mutableContext.Count == entities.Length);

        int parallelCount = 0;
        world.ForEachEntityParallel(
            in query,
            _ => Interlocked.Increment(ref parallelCount),
            workerCount: 2).Invoke();
        Require(parallelCount == entities.Length);

        var readonlyContext = new IterationContext { Counter = new SharedCounter() };
        world.ForEachEntityParallel(
            in query,
            in readonlyContext,
            static (in IterationContext context, EntityRef _) =>
                Interlocked.Increment(ref context.Counter.Count),
            workerCount: 2).Invoke();
        Require(readonlyContext.Counter.Count == entities.Length);

        var valueContext = new IterationContext { Counter = new SharedCounter() };
        world.ForEachEntityParallel(
            in query,
            valueContext,
            static (IterationContext context, EntityRef _) =>
                Interlocked.Increment(ref context.Counter.Count),
            workerCount: 2).Invoke();
        Require(valueContext.Counter.Count == entities.Length);
    }
}
