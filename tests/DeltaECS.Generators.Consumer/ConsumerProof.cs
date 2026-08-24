using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

public struct Position
{
    public int Value;
}

public struct Velocity
{
    public int Value;
}

public struct Acceleration
{
    public int Value;
}

public struct Lifetime
{
    public int Value;
}

public struct ConsumerContext
{
    public int Value;
}

public struct ContextEntityFunctor : IForEachContextEntity_RWRW<ConsumerContext, Position, Velocity, Acceleration, Lifetime>
{
    public void Invoke(
        ref ConsumerContext context,
        Entity entity,
        in Position position,
        ref Velocity velocity,
        in Acceleration acceleration,
        ref Lifetime lifetime)
    {
        velocity.Value += position.Value + acceleration.Value + entity.Index;
        lifetime.Value += context.Value;
        context.Value++;
    }
}

public struct SequenceFunctor : IForEachEntity_RWRW<Position, Velocity, Acceleration, Lifetime>
{
    public void Invoke(
        Entity entity,
        in Position position,
        ref Velocity velocity,
        in Acceleration acceleration,
        ref Lifetime lifetime)
    {
        velocity.Value += position.Value + acceleration.Value + entity.Index;
        lifetime.Value++;
    }
}

/// <summary>
/// Compile-only consumer assembly. Its generated calls are intentionally outside
/// DeltaECS and therefore prove that the analyzer is attached to a consumer.
/// </summary>
public static class ConsumerProof
{
    public static void DenseNoExplicitIdsWithExtraQueryComponent(
        World world,
        in Query query,
        in Query queryWithExtraComponent,
        ComponentId extraComponentId,
        ref ConsumerContext context)
    {
        _ = extraComponentId;
        world.ForEach<Position>(
            in query,
            static (ref Position position) => position.Value++);

        world.ForEach<Position>(
            in queryWithExtraComponent,
            static (Entity entity, ref Position position) => position.Value += entity.Index);

        world.ForEach<ConsumerContext, Position, Velocity, Acceleration, Lifetime>(
            in query,
            ref context,
            static (
                ref ConsumerContext state,
                in Position position,
                ref Velocity velocity,
                in Acceleration acceleration,
                ref Lifetime lifetime) =>
            {
                velocity.Value += position.Value + acceleration.Value;
                lifetime.Value += state.Value;
                state.Value++;
            },
            ForEachAccessTag_RWRW.Instance);

        var functor = new ContextEntityFunctor();
        world.ForEach<ConsumerContext, ContextEntityFunctor, Position, Velocity, Acceleration, Lifetime>(
            in query,
            ref context,
            ref functor,
            ForEachEntityTag.Instance,
            ForEachAccessTag_RWRW.Instance);
    }

    public static void SequenceWithExplicitIds(
        World world,
        ReadOnlySpan<Entity> entities,
        in Query query,
        ComponentId positionId,
        ComponentId velocityId,
        ComponentId accelerationId,
        ComponentId lifetimeId)
    {
        world.ForEach<Position, Velocity, Acceleration, Lifetime>(
            entities,
            in query,
            positionId,
            velocityId,
            accelerationId,
            lifetimeId,
            static (
                Entity entity,
                in Position position,
                ref Velocity velocity,
                in Acceleration acceleration,
                ref Lifetime lifetime) =>
            {
                velocity.Value += position.Value + acceleration.Value + entity.Index;
                lifetime.Value++;
            },
            ForEachAccessTag_RWRW.Instance);

        var sequence = world.Entities(entities).Where(in query);
        sequence.ForEach<Position, Velocity, Acceleration, Lifetime>(
            positionId,
            velocityId,
            accelerationId,
            lifetimeId,
            static (
                Entity entity,
                in Position position,
                ref Velocity velocity,
                in Acceleration acceleration,
                ref Lifetime lifetime) =>
            {
                velocity.Value += position.Value + acceleration.Value + entity.Index;
                lifetime.Value++;
            },
            ForEachAccessTag_RWRW.Instance);

        var sequenceFunctor = new SequenceFunctor();
        sequence.ForEach<SequenceFunctor, Position, Velocity, Acceleration, Lifetime>(
            positionId,
            velocityId,
            accelerationId,
            lifetimeId,
            ref sequenceFunctor,
            ForEachEntityTag.Instance,
            ForEachAccessTag_RWRW.Instance);

        var sequenceContextFunctor = new ContextEntityFunctor();
        var context = new ConsumerContext();
        sequence.ForEach<ConsumerContext, ContextEntityFunctor, Position, Velocity, Acceleration, Lifetime>(
            ref context,
            positionId,
            velocityId,
            accelerationId,
            lifetimeId,
            ref sequenceContextFunctor,
            ForEachEntityTag.Instance,
            ForEachAccessTag_RWRW.Instance);
    }

    public static int AssemblyMarker => 4;
}
