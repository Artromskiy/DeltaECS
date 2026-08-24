using System.Collections.Generic;
using NUnit.Framework;
using Delta.ECS;

namespace Delta.ECS.Tests;

[TestFixture]
public sealed class SequenceExecutionTests
{
    [Test]
    public void SequenceForEach_PreservesOrderAndDuplicates_AndSkipsStale()
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
    public void SequenceWhere_FiltersOnlyTheCandidateSequence()
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
    public void SequenceStructuralTerminals_ForwardBatchSemantics_AndHonorFilter()
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
}
