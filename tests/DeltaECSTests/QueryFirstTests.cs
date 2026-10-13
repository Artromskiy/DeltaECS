namespace Delta.ECS.Tests;

using NUnit.Framework;

[TestFixture]
internal sealed class QueryFirstTests
{
    [Test]
    public void FirstReturnsFirstMatchingEntityAndReusesLiveQuery()
    {
        var layouts = new ComponentRegistry();
        ComponentId mapId = layouts.Register<HexMap>(new SchemaId(97_001));
        ComponentId visibleId = layouts.Register<VisibleOnMap>(new SchemaId(97_002));
        using var world = new World(layouts);
        Query query = world.WhereAll<HexMap>().WhereAll<VisibleOnMap>();
        EcsResultOperation<Entity> first = query.First();

        Assert.That(first.Invoke(), Is.EqualTo(default(Entity)));

        Entity hidden = world.Create(mapId);
        Entity firstVisible = world.Create([mapId, visibleId]);
        Entity secondVisible = world.Create([mapId, visibleId]);

        Assert.That(first.Invoke(), Is.EqualTo(firstVisible));

        world.Destroy(firstVisible);
        Assert.That(first.Invoke(), Is.EqualTo(secondVisible));
        Assert.That(world.IsAlive(hidden), Is.True);
    }

    private struct HexMap { }

    private struct VisibleOnMap { }
}
