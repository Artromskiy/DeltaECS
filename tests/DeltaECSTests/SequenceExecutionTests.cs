using System.Collections.Generic;
using NUnit.Framework;
using Delta.ECS;

namespace Delta.ECS.Tests;

[TestFixture]
public sealed class SequenceExecutionTests
{
    [Test]
    public void SequenceForEachPreservesOrderAndDuplicatesAndSkipsStale()
    {
        var layouts = new ComponentLayoutRegistry();
        var valueId = layouts.Register(typeof(int), new SchemaId(60_001));
        using var world = new World(layouts);
        var created = new Entity[4];
        world.Create(new[] { valueId }, created);
        Entity stale = created[1];
        Assert.That(world.Destroy(stale), Is.True);

        var candidates = new[] { created[3], created[0], created[3], stale };
        var visited = new List<Entity>();
        world.Entities(candidates).ForEachEntity(entity => visited.Add(entity));

        Assert.That(visited, Is.EqualTo(new[] { created[3], created[0], created[3] }));
    }

    [Test]
    public void SequenceWhereFiltersOnlyTheCandidateSequence()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register(typeof(int), new SchemaId(60_002));
        var markerId = layouts.Register(typeof(byte), new SchemaId(60_003));
        using var world = new World(layouts);
        var positionOnly = world.Create(new[] { positionId });
        var marked = world.Create(new[] { positionId, markerId });
        var markerOnly = world.Create(new[] { markerId });
        var outsideCandidateSequence = world.Create(new[] { markerId });
        var query = world.CreateQuery(QuerySpec.ForComponents(markerId));
        var candidates = new[] { positionOnly, marked, markerOnly, marked };
        var visited = new List<Entity>();

        world.Entities(candidates).Where(in query).ForEachEntity(entity => visited.Add(entity));

        Assert.That(visited, Is.EqualTo(new[] { marked, markerOnly, marked }));
        Assert.That(visited, Does.Not.Contain(outsideCandidateSequence));
    }

    [Test]
    public void SequenceStructuralTerminalsForwardBatchSemanticsAndHonorFilter()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register(typeof(int), new SchemaId(60_004));
        var markerId = layouts.Register(typeof(byte), new SchemaId(60_005));
        var velocityId = layouts.Register(typeof(short), new SchemaId(60_006));
        using var world = new World(layouts);
        var marked = world.Create(new[] { positionId, markerId });
        var unmarked = world.Create(new[] { positionId });
        var outsideCandidateSequence = world.Create(new[] { positionId, markerId });
        var stale = world.Create(new[] { positionId, markerId });
        Assert.That(world.Destroy(stale), Is.True);
        var query = world.CreateQuery(QuerySpec.ForComponents(markerId));
        Assert.That(world.TryGetComponentStamp(marked, positionId, out Stamp markedPositionBefore), Is.True);
        Assert.That(world.TryGetComponentStamp(unmarked, positionId, out Stamp unmarkedPositionBefore), Is.True);

        var candidates = new[] { unmarked, marked, marked, stale };
        Assert.That(world.Entities(candidates).Where(in query).Add(new[] { velocityId }), Is.EqualTo(1));
        Stamp afterAdd = world.Stamp;
        Assert.That(world.TryGet<short>(marked, velocityId, out _), Is.True);
        Assert.That(world.TryGet<short>(unmarked, velocityId, out _), Is.False);
        Assert.That(world.TryGet<short>(outsideCandidateSequence, velocityId, out _), Is.False);
        Assert.That(world.TryGetComponentStamp(marked, positionId, out Stamp markedPositionAfterAdd), Is.True);
        Assert.That(world.TryGetComponentStamp(marked, velocityId, out Stamp markedVelocityAfterAdd), Is.True);
        Assert.That(markedPositionAfterAdd, Is.EqualTo(markedPositionBefore));
        Assert.That(markedVelocityAfterAdd, Is.EqualTo(afterAdd));

        Assert.That(world.Entities(candidates).Where(in query).Remove(new[] { velocityId }), Is.EqualTo(1));
        Stamp afterRemove = world.Stamp;
        Assert.That(world.TryGet<short>(marked, velocityId, out _), Is.False);
        Assert.That(world.TryGetComponentStamp(marked, positionId, out Stamp markedPositionAfterRemove), Is.True);
        Assert.That(markedPositionAfterRemove, Is.EqualTo(markedPositionBefore));
        Assert.That(afterRemove, Is.Not.EqualTo(afterAdd));

        Assert.That(world.Entities(candidates).Where(in query).Destroy(), Is.EqualTo(1));
        Assert.That(world.IsAlive(marked), Is.False);
        Assert.That(world.IsAlive(unmarked), Is.True);
        Assert.That(world.TryGetComponentStamp(unmarked, positionId, out Stamp unmarkedPositionAfter), Is.True);
        Assert.That(unmarkedPositionAfter, Is.EqualTo(unmarkedPositionBefore));

        Stamp afterDestroy = world.Stamp;
        Assert.That(world.Entities(candidates).Where(in query).Destroy(), Is.Zero);
        Assert.That(world.Entities(candidates).Where(in query).Add(new[] { velocityId }), Is.Zero);
        Assert.That(world.Stamp, Is.EqualTo(afterDestroy));
    }

    [Test]
    public void SequenceFunctorFormsPreserveOrderFilterAndCallerState()
    {
        var layouts = new ComponentLayoutRegistry();
        var markerId = layouts.Register(typeof(byte), new SchemaId(60_007));
        var otherId = layouts.Register(typeof(short), new SchemaId(60_008));
        using var world = new World(layouts);
        var first = world.Create(otherId);
        var second = world.Create(markerId);
        var third = world.Create(markerId);
        var query = world.CreateQuery(QuerySpec.ForComponents(markerId));
        var candidates = new[] { third, first, second, third };

        var collector = new EntityCollector();
        world.Entities(candidates).Where(in query).ForEachEntity(ref collector);

        var context = 10;
        var contextCollector = new ContextCollector();
        world.Entities(candidates).Where(in query).ForEachEntity(ref context, ref contextCollector);

        Assert.Multiple(() =>
        {
            Assert.That(collector.Entities, Is.EqualTo(new[] { third, second, third }));
            Assert.That(context, Is.EqualTo(13));
            Assert.That(contextCollector.Last, Is.EqualTo(third));
        });
    }

    [Test]
    public void TypedSequenceMixedReadWritePreservesOrderAndExactStamps()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register<SequencePosition>(new SchemaId(60_009));
        var velocityId = layouts.Register<SequenceVelocity>(new SchemaId(60_010));
        using var world = new World(layouts, chunkCapacity: 2);
        var first = world.Create(positionId, velocityId);
        var second = world.Create(positionId, velocityId);
        var third = world.Create(positionId, velocityId);
        Assert.That(world.Set(first, positionId, new SequencePosition(1)), Is.True);
        Assert.That(world.Set(second, positionId, new SequencePosition(2)), Is.True);
        Assert.That(world.Set(third, positionId, new SequencePosition(3)), Is.True);
        Assert.That(world.TryGetComponentStamp(first, positionId, out Stamp firstPosition), Is.True);
        Assert.That(world.TryGetComponentStamp(third, positionId, out Stamp thirdPosition), Is.True);
        Assert.That(world.TryGetComponentStamp(second, velocityId, out Stamp untouchedVelocity), Is.True);
        var query = world.CreateQuery(QuerySpec.ForComponents(positionId, velocityId));
        var candidates = new[] { third, first, third };
        var context = new List<Entity>();

        world.Entities(candidates).Where(in query).ForEachEntity<List<Entity>, SequencePosition, SequenceVelocity>(
            ref context,
            static (ref List<Entity> visited, Entity entity, in SequencePosition position, ref SequenceVelocity velocity) =>
            {
                visited.Add(entity);
                velocity.Value += position.Value;
            });

        Assert.That(context, Is.EqualTo(candidates));
        Assert.That(world.Get<SequenceVelocity>(first, velocityId).Value, Is.EqualTo(1));
        Assert.That(world.Get<SequenceVelocity>(second, velocityId).Value, Is.Zero);
        Assert.That(world.Get<SequenceVelocity>(third, velocityId).Value, Is.EqualTo(6));
        Assert.That(world.TryGetComponentStamp(first, positionId, out Stamp firstPositionAfter), Is.True);
        Assert.That(world.TryGetComponentStamp(third, positionId, out Stamp thirdPositionAfter), Is.True);
        Assert.That(world.TryGetComponentStamp(first, velocityId, out Stamp firstVelocityAfter), Is.True);
        Assert.That(world.TryGetComponentStamp(third, velocityId, out Stamp thirdVelocityAfter), Is.True);
        Assert.That(world.TryGetComponentStamp(second, velocityId, out Stamp untouchedVelocityAfter), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(firstPositionAfter, Is.EqualTo(firstPosition));
            Assert.That(thirdPositionAfter, Is.EqualTo(thirdPosition));
            Assert.That(firstVelocityAfter, Is.EqualTo(world.Stamp));
            Assert.That(thirdVelocityAfter, Is.EqualTo(world.Stamp));
            Assert.That(untouchedVelocityAfter, Is.EqualTo(untouchedVelocity));
        });
    }

    [Test]
    public void TypedSequenceReadOnlyDoesNotAdvanceStampsAndFunctorMatchesDelegateContract()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register<SequencePosition>(new SchemaId(60_011));
        var velocityId = layouts.Register<SequenceVelocity>(new SchemaId(60_012));
        using var world = new World(layouts);
        var entity = world.Create(positionId, velocityId);
        Assert.That(world.Set(entity, positionId, new SequencePosition(5)), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionBefore), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, velocityId, out Stamp velocityBefore), Is.True);
        Stamp beforeRead = world.Stamp;
        var query = world.CreateQuery(QuerySpec.ForComponents(positionId, velocityId));
        var candidates = new[] { entity, entity };
        int sum = 0;

        world.Entities(candidates).Where(in query).ForEach<int, SequencePosition, SequenceVelocity>(
            ref sum,
            static (ref int total, in SequencePosition position, in SequenceVelocity velocity) =>
                total += position.Value + velocity.Value);

        Assert.That(sum, Is.EqualTo(10));
        Assert.That(world.Stamp, Is.EqualTo(beforeRead));
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionAfterRead), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, velocityId, out Stamp velocityAfterRead), Is.True);
        Assert.That(positionAfterRead, Is.EqualTo(positionBefore));
        Assert.That(velocityAfterRead, Is.EqualTo(velocityBefore));

        var functor = new SequenceMovementFunctor();
        world.Entities(candidates).Where(in query).ForEachEntity(ref functor);

        Assert.That(functor.Count, Is.EqualTo(2));
        Assert.That(world.Get<SequenceVelocity>(entity, velocityId).Value, Is.EqualTo(10));
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionAfterWrite), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, velocityId, out Stamp velocityAfterWrite), Is.True);
        Assert.That(positionAfterWrite, Is.EqualTo(positionBefore));
        Assert.That(velocityAfterWrite, Is.EqualTo(world.Stamp));
    }

    [Test]
    public void TypedSequenceRefreshesPlansAndCachesAlternatingArchetypes()
    {
        var layouts = new ComponentLayoutRegistry();
        var positionId = layouts.Register<SequencePosition>(new SchemaId(60_013));
        var velocityId = layouts.Register<SequenceVelocity>(new SchemaId(60_014));
        var markerId = layouts.Register(typeof(byte), new SchemaId(60_015));
        using var world = new World(layouts, chunkCapacity: 1);
        var query = world.CreateQuery(QuerySpec.ForComponents(positionId, velocityId));
        var plain = world.Create(positionId, velocityId);
        var marked = world.Create(positionId, velocityId, markerId);
        Assert.That(world.Set(plain, positionId, new SequencePosition(2)), Is.True);
        Assert.That(world.Set(marked, positionId, new SequencePosition(3)), Is.True);
        var candidates = new[] { marked, plain, marked, plain };

        world.Entities(candidates).Where(in query).ForEach<SequencePosition, SequenceVelocity>(
            static (in SequencePosition position, ref SequenceVelocity velocity) =>
                velocity.Value += position.Value);

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<SequenceVelocity>(plain, velocityId).Value, Is.EqualTo(4));
            Assert.That(world.Get<SequenceVelocity>(marked, velocityId).Value, Is.EqualTo(6));
        });
    }

    [Test]
    public void EmptyAndStaleOnlyTypedWriteSequencesDoNotAdvanceStamp()
    {
        var layouts = new ComponentLayoutRegistry();
        var velocityId = layouts.Register<SequenceVelocity>(new SchemaId(60_016));
        using var world = new World(layouts);
        Entity stale = world.Create(velocityId);
        Assert.That(world.Destroy(stale), Is.True);
        Stamp before = world.Stamp;

        world.Entities(Array.Empty<Entity>()).ForEach<SequenceVelocity>(
            static (ref SequenceVelocity velocity) => velocity.Value++);
        world.Entities(new[] { stale }).ForEach<SequenceVelocity>(
            static (ref SequenceVelocity velocity) => velocity.Value++);

        Assert.That(world.Stamp, Is.EqualTo(before));
    }

    internal struct EntityCollector : IForEachEntity
    {
        public EntityCollector() => Entities = [];

        public readonly List<Entity> Entities { get; }

        public void Invoke(Entity entity) => Entities.Add(entity);
    }

    internal struct ContextCollector : IForEachContextEntity<int>
    {
        public Entity Last { get; private set; }

        public void Invoke(ref int context, Entity entity)
        {
            context++;
            Last = entity;
        }
    }

    internal struct SequenceMovementFunctor : IForEachEntity
    {
        public int Count { get; private set; }

        public void Invoke(Entity entity, in SequencePosition position, ref SequenceVelocity velocity)
        {
            _ = entity;
            velocity.Value += position.Value;
            Count++;
        }
    }

    internal readonly record struct SequencePosition(int Value);

    internal struct SequenceVelocity
    {
        public int Value;
    }
}
