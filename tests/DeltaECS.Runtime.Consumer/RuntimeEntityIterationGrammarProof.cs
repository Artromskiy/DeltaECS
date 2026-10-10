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
        var contextOperation = world.ForEachEntity(
            in query,
            ref mutableContext,
            static (ref CountContext context, EntityRef _) => context.Count++);
        InvokeUnboxed(ref mutableContext, contextOperation);
        Require(mutableContext.Count == entities.Length);
#pragma warning disable CA1859 // Exercise storing a concrete operation behind its stateful interface.
        IOperation<CountContext> boxedContextOperation = contextOperation;
        boxedContextOperation.Invoke(ref mutableContext);
#pragma warning restore CA1859
        Require(mutableContext.Count == entities.Length * 2);

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

    private static void VerifyEntityRefAccess()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(934_011));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(934_012));
        ComponentId markerId = layouts.Register<Marker>(new SchemaId(934_013));
        using var world = new World(layouts);
        Entity entity = world.Create(stackalloc ComponentId[] { positionId, markerId });
        world.GetRef<Position>(entity, positionId).Value = 7;
        Require(world.Get<Position>(entity, positionId).Value == 7);
        Query query = world.WhereAll(positionId);
        var unregisteredId = new ComponentId(int.MaxValue);
        int visits = 0;

        var operation = world.ForEachEntity(in query, current =>
        {
            Entity handle = current;
            Require(current == entity && handle == entity);
            Require(current.Index == entity.Index && current.Generation == entity.Generation);
            Require(current.Has(positionId));
            Require(current.Has<Position>() && current.Has<Position>(positionId));
            Require(!current.Has<Velocity>() && !current.Has<Velocity>(velocityId) && !current.Has<Velocity>(positionId));
            Require(!current.Has(unregisteredId));

            Require(current.TryGet<Position>(out Position primaryValue));
            Require(current.TryGet(positionId, out Position dynamicValue));
            Require(primaryValue.Value == 7 + visits * 2);
            Require(dynamicValue.Value == primaryValue.Value);
            Require(current.Get<Position>().Value == primaryValue.Value);
            Require(current.Get<Position>(positionId).Value == primaryValue.Value);
            Require(!current.TryGet<Position>(velocityId, out _));
            Require(!current.TryGet<Position>(unregisteredId, out _));
            Require(!current.TryGet<Velocity>(out _) && !current.TryGet<Velocity>(velocityId, out _));

            ref readonly Position primaryRead = ref current.GetReadRef<Position>();
            ref readonly Position dynamicRead = ref current.GetReadRef<Position>(positionId);
            Require(primaryRead.Value == primaryValue.Value && dynamicRead.Value == primaryValue.Value);

            Require(current.Has<Marker>() && current.Has(markerId));
            Require(current.TryGet<Marker>(out Marker tagValue) && tagValue.Equals(default(Marker)));
            Require(current.Get<Marker>().Equals(default(Marker)));
            Require(current.GetReadRef<Marker>().Equals(default(Marker)));
            Require(current.TryGetComponentStamp<Marker>(out Stamp tagStamp));
            Require(tagStamp == new Stamp(1));
            Require(current.TryGetComponentStamp(markerId, out Stamp dynamicTagStamp));
            Require(current.TryGetComponentStamp<Marker>(markerId, out Stamp typedTagStamp));
            Require(dynamicTagStamp == tagStamp && typedTagStamp == tagStamp);
            _ = current.GetRef<Marker>();

            Require(current.TryGetComponentStamp<Position>(out Stamp before));
            ref Position writable = ref current.GetRef<Position>();
            writable.Value++;
            ref Position dynamicWritable = ref current.GetRef<Position>(positionId);
            dynamicWritable.Value++;
            Require(current.TryGetComponentStamp(positionId, out Stamp after));
            Require(current.TryGetComponentStamp<Position>(positionId, out Stamp typedAfter));
            Require(after == typedAfter && after != before);
            Require(!current.TryGetComponentStamp<Velocity>(out _));
            Require(!current.TryGetComponentStamp(velocityId, out _));
            Require(!current.TryGetComponentStamp<Velocity>(positionId, out _));
            Require(!current.TryGetComponentStamp(unregisteredId, out _));
            visits++;
        });

        InvokeUnboxed(operation);
#pragma warning disable CA1859 // Exercise storing one concrete operation behind its interface.
        IOperation boxedOperation = operation;
        boxedOperation.Invoke();
#pragma warning restore CA1859
        Require(visits == 2);
        Require(world.Get<Position>(entity, positionId).Value == 11);
    }

    private static void InvokeUnboxed<TOperation>(TOperation operation)
        where TOperation : struct, IOperation
        => operation.Invoke();

    private static void InvokeUnboxed<TState, TOperation>(ref TState state, TOperation operation)
        where TOperation : struct, IOperation<TState>
        => operation.Invoke(ref state);
}
