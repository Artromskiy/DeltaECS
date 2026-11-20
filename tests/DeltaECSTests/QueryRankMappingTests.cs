using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed class QueryRankMappingTests
{
    [Test]
    public void GenericPreparedAccessResolvesRegisteredDataAndTagTypes()
    {
        var layouts = new ComponentRegistry();
        ComponentId selected = layouts.Register<QueryRankSelected>(new SchemaId(50_201));
        ComponentId tag = layouts.Register<QueryRankTag>(new SchemaId(50_202));
        using var world = new World(layouts);
        Entity entity = world.Create(selected, tag);
        Query query = world.WhereAll(selected, tag);

        Assert.Multiple(() =>
        {
            Assert.That(GeneratedForEachRuntime.GetPreparedReadRoute<QueryRankSelected>(in query),
                Is.EqualTo(GeneratedForEachRuntime.GetPreparedReadRoute<QueryRankSelected>(in query, selected)));
            Assert.That(GeneratedForEachRuntime.GetPreparedWriteRoute<QueryRankSelected>(in query),
                Is.EqualTo(GeneratedForEachRuntime.GetPreparedWriteRoute<QueryRankSelected>(in query, selected)));
            Assert.That(GeneratedForEachRuntime.GetPreparedReadRoute<QueryRankTag>(in query),
                Is.EqualTo(GeneratedForEachRuntime.GetPreparedReadRoute<QueryRankTag>(in query, tag)));
            Assert.That(GeneratedForEachRuntime.GetPreparedWriteRoute<QueryRankTag>(in query),
                Is.EqualTo(GeneratedForEachRuntime.GetPreparedWriteRoute<QueryRankTag>(in query, tag)));
        });

        var operation = world.ForEach(in query, static (ref QueryRankSelected value, in QueryRankTag _) => value.Value++);
        operation.Invoke();
        operation.Invoke();
        Assert.That(world.Get<QueryRankSelected>(entity).Value, Is.EqualTo(2));
    }

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

internal struct QueryRankTag { }
