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
        world.CreateBatch(new[] { valueId }, created);
        Entity stale = created[1];
        Assert.That(world.Destroy(stale), Is.True);

        var candidates = new[] { created[3], created[0], created[3], stale };
        var visited = new List<Entity>();
        world.Entities(candidates).ForEach(entity => visited.Add(entity));

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

        world.Entities(candidates).Where(in query).ForEach(entity => visited.Add(entity));

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

        var candidates = new[] { unmarked, marked, marked, stale };
        Assert.That(world.Entities(candidates).Where(in query).Add(new[] { velocityId }), Is.EqualTo(1));
        Assert.That(world.TryGetComponent<short>(marked, velocityId, out _), Is.True);
        Assert.That(world.TryGetComponent<short>(unmarked, velocityId, out _), Is.False);
        Assert.That(world.TryGetComponent<short>(outsideCandidateSequence, velocityId, out _), Is.False);

        Assert.That(world.Entities(candidates).Where(in query).Remove(new[] { velocityId }), Is.EqualTo(1));
        Assert.That(world.TryGetComponent<short>(marked, velocityId, out _), Is.False);

        Assert.That(world.Entities(candidates).Where(in query).Destroy(), Is.EqualTo(1));
        Assert.That(world.IsAlive(marked), Is.False);
        Assert.That(world.IsAlive(unmarked), Is.True);
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
        world.Entities(candidates).Where(in query).ForEach(ref collector);

        var context = 10;
        var contextCollector = new ContextCollector();
        world.Entities(candidates).Where(in query).ForEach(ref context, ref contextCollector);

        Assert.Multiple(() =>
        {
            Assert.That(collector.Entities, Is.EqualTo(new[] { third, second, third }));
            Assert.That(context, Is.EqualTo(13));
            Assert.That(contextCollector.Last, Is.EqualTo(third));
        });
    }

    private struct EntityCollector : IForEachEntity
    {
        public EntityCollector() => Entities = [];

        public readonly List<Entity> Entities { get; }

        public void Invoke(Entity entity) => Entities.Add(entity);
    }

    private struct ContextCollector : IForEachContextEntity<int>
    {
        public Entity Last { get; private set; }

        public void Invoke(ref int context, Entity entity)
        {
            context++;
            Last = entity;
        }
    }
}
