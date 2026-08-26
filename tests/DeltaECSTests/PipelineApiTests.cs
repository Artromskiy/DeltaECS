using Delta.ECS;
using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
public sealed class PipelineApiTests
{
    [Test]
    public void InferredForEachCachesPrimaryRoutesPerQueryPlan()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_005));
        ComponentId velocityId = layouts.Register<PipelineVelocity>(new SchemaId(70_006));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, velocityId);
        Assert.That(world.Set(entity, positionId, new PipelinePosition { Value = 1 }), Is.True);
        Assert.That(world.Set(entity, velocityId, new PipelineVelocity { Value = 2 }), Is.True);

        var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
        Assert.That(query.Cached.PreparedPrimaryReadRouteCount, Is.EqualTo(2));
        Assert.That(query.Cached.HasWriteAccess, Is.False);

        world.ForEach(in query, static (ref PipelinePosition position, in PipelineVelocity velocity) =>
            position.Value += velocity.Value);

        Assert.That(query.Cached.PreparedPrimaryReadRouteCount, Is.EqualTo(2));
        Assert.That(query.Cached.HasWriteAccess, Is.True);

        world.ForEach(in query, static (ref PipelinePosition position, in PipelineVelocity velocity) =>
            position.Value += velocity.Value);

        Assert.That(query.Cached.PreparedPrimaryReadRouteCount, Is.EqualTo(2));
        Assert.That(world.Get<PipelinePosition>(entity, positionId).Value, Is.EqualTo(5));
    }

    [Test]
    public void WriteAccessUpgradesPreparedReadRoute()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_007));
        using var world = new World(layouts);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));

        ReadAccess read = query.AccessRead(positionId);
        Assert.That(query.Cached.HasWriteAccess, Is.False);

        WriteAccess write = query.AccessWrite(positionId);

        Assert.That(write.Query, Is.SameAs(read.Query));
        Assert.That(write.QueryComponentIndex, Is.EqualTo(read.QueryComponentIndex));
        Assert.That(query.Cached.HasWriteAccess, Is.True);
    }

    [Test]
    public void FromPipelineFiltersAndDestroysCandidates()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_003));
        ComponentId markerId = layouts.Register<PipelineMarker>(new SchemaId(70_004));
        using var world = new World(layouts);
        Entity matching = world.Create(positionId, markerId);
        Entity nonMatching = world.Create(markerId);
        Entity[] candidates = { matching, nonMatching };
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));

        int destroyed = world.From(candidates).Where(in query).Destroy();

        Assert.That(destroyed, Is.EqualTo(1));
        Assert.That(world.IsAlive(matching), Is.False);
        Assert.That(world.IsAlive(nonMatching), Is.True);
    }

    internal struct PipelinePosition
    {
        public int Value;
    }

    internal struct PipelineVelocity
    {
        public int Value;
    }

    internal struct PipelineMarker
    {
    }
}
