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
    public void StaticLambdaDelegatePathInvokesOnceAndPreservesRefWriteSemantics()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_008));
        ComponentId velocityId = layouts.Register<PipelineVelocity>(new SchemaId(70_009));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, velocityId);
        Assert.That(world.Set(entity, positionId, new PipelinePosition { Value = 1 }), Is.True);
        Assert.That(world.Set(entity, velocityId, new PipelineVelocity { Value = 2 }), Is.True);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));

        int calls = 0;
        world.ForEach(in query, static (ref PipelinePosition position, in PipelineVelocity velocity) =>
        {
            position.Value += velocity.Value;
        });
        world.ForEach(in query, (ref PipelinePosition position, in PipelineVelocity velocity) =>
        {
            calls++;
            position.Value += velocity.Value;
        });

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(world.Get<PipelinePosition>(entity, positionId).Value, Is.EqualTo(5));
    }

    [Test]
    public void PrecreatedDelegateFallbackStillExecutesOnce()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_010));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        Assert.That(world.Set(entity, positionId, new PipelinePosition { Value = 3 }), Is.True);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        int calls = 0;
        ForEachAction_W<PipelinePosition> action = (ref PipelinePosition position) =>
        {
            calls++;
            position.Value++;
        };

        world.ForEach(in query, action);

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(world.Get<PipelinePosition>(entity, positionId).Value, Is.EqualTo(4));
    }

    [Test]
    public void MethodGroupDelegateFallbackStillExecutesOnce()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<PipelinePosition>(new SchemaId(70_011));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        Assert.That(world.Set(entity, positionId, new PipelinePosition { Value = 3 }), Is.True);
        Query query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        s_methodGroupCalls = 0;

        world.ForEach(in query, ApplyMethodGroup);

        Assert.That(s_methodGroupCalls, Is.EqualTo(1));
        Assert.That(world.Get<PipelinePosition>(entity, positionId).Value, Is.EqualTo(4));
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

    private static int s_methodGroupCalls;

    private static void ApplyMethodGroup(ref PipelinePosition position)
    {
        s_methodGroupCalls++;
        position.Value++;
    }
}
