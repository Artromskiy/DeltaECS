using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed class QueryRankMappingTests
{
    [Test]
    public void QueryRowsMapHighIdComponentsInArchetypesWithDifferentLowIdRows()
    {
        var layouts = new ComponentRegistry();
        ComponentId[] padding = new ComponentId[128];
        for (int index = 0; index < padding.Length; index++)
        {
            padding[index] = layouts.Register<QueryRankPadding>(new SchemaId((ulong)(50_000 + index)));
        }

        ComponentId selected = layouts.Register<QueryRankSelected>(new SchemaId(50_128));
        using var world = new World(layouts);
        Entity first = world.Create(selected);
        Entity shifted = world.Create([padding[^1], selected]);
        world.GetRef<QueryRankPadding>(shifted, padding[^1]).Value = 7;
        world.GetRef<QueryRankSelected>(first).Value = 1;
        world.GetRef<QueryRankSelected>(shifted).Value = 2;

        Query query = world.WhereAll<QueryRankSelected>();
        int checksum = 0;
        world.ForEach(in query, ref checksum,
            static (ref int total, in QueryRankSelected value) => total += value.Value).Invoke(ref checksum);

        Assert.That(checksum, Is.EqualTo(3));
    }
}

internal struct QueryRankPadding
{
    public int Value;
}

internal struct QueryRankSelected
{
    public int Value;
}
