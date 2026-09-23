using BenchmarkDotNet.Attributes;
using Delta.ECS;

namespace Delta.ECS.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TagFilteringBenchmarks
{
    public int Amount { get; set; } = BenchmarkConfiguration.GetAmount();

    private World _tagWorld = null!;
    private World _componentWorld = null!;
    private Query _tagQuery;
    private Query _componentQuery;
    private Query _tagEntityQuery;
    private Query _componentEntityQuery;
    private long _expectedChecksum;

    [GlobalSetup]
    public void Setup()
    {
        var tagLayouts = new ComponentLayoutRegistry();
        ComponentId tagValueId = tagLayouts.Register<TagFilteringValueComponent>(new SchemaId(207_000));
        _ = tagLayouts.RegisterTag<TagFilteringMarkerTag>(new SchemaId(207_001));
        _tagWorld = new World(tagLayouts, initialEntityCapacity: Amount);

        var componentLayouts = new ComponentLayoutRegistry();
        ComponentId componentValueId = componentLayouts.Register<TagFilteringValueComponent>(new SchemaId(207_000));
        ComponentId componentMarkerId = componentLayouts.Register<TagFilteringMarkerComponent>(new SchemaId(207_002));
        _componentWorld = new World(componentLayouts, initialEntityCapacity: Amount);

        var tagEntities = new Entity[Amount];
        var componentEntities = new Entity[Amount];
        _tagWorld.Create(new[] { tagValueId }, tagEntities);
        _componentWorld.Create(new[] { componentValueId, componentMarkerId }, componentEntities);

        _expectedChecksum = (long)Amount * (Amount + 1) / 2;
        for (int index = 0; index < Amount; index++)
        {
            int value = index + 1;
            _tagWorld.Set(tagEntities[index], tagValueId, new TagFilteringValueComponent { Value = value });
            _componentWorld.Set(componentEntities[index], componentValueId, new TagFilteringValueComponent { Value = value });
            _tagWorld.Add<TagFilteringMarkerTag>(tagEntities[index]);
            _componentWorld.Set(componentEntities[index], componentMarkerId, new TagFilteringMarkerComponent { Value = value });
        }

        _tagQuery = _tagWorld.WhereAll<TagFilteringValueComponent>().WhereAll<TagFilteringMarkerTag>();
        _componentQuery = _componentWorld.WhereAll<TagFilteringValueComponent, TagFilteringMarkerComponent>();
        _tagEntityQuery = _tagWorld.WhereAll<TagFilteringMarkerTag>();
        _componentEntityQuery = _componentWorld.WhereAll<TagFilteringMarkerComponent>();

        Validate(ComponentFilterAndValueIteration(), "component filter + component iteration");
        Validate(TagFilterAndValueIteration(), "tag filter + component iteration");
        Validate(ComponentFilterEntityIteration(), "component filter + entity iteration");
        Validate(TagFilterEntityIteration(), "tag filter + entity iteration");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _tagWorld?.Dispose();
        _componentWorld?.Dispose();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TagFiltering.ComponentIteration")]
    public long ComponentFilterAndValueIteration()
    {
        long checksum = 0;
        _componentWorld.ForEach(
            in _componentQuery,
            ref checksum,
            static (ref long sum, ref readonly TagFilteringValueComponent value) => sum += value.Value);
        return Validate(checksum, "component filter + component iteration");
    }

    [Benchmark]
    [BenchmarkCategory("TagFiltering.ComponentIteration")]
    public long TagFilterAndValueIteration()
    {
        long checksum = 0;
        _tagWorld.ForEach(
            in _tagQuery,
            ref checksum,
            static (ref long sum, ref readonly TagFilteringValueComponent value) => sum += value.Value);
        return Validate(checksum, "tag filter + component iteration");
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TagFiltering.EntityIteration")]
    public long ComponentFilterEntityIteration()
    {
        long checksum = 0;
        _componentWorld.ForEachEntity(
            in _componentEntityQuery,
            ref checksum,
            static (ref long sum, Entity entity) => sum += entity.Index + 1L);
        return Validate(checksum, "component filter + entity iteration");
    }

    [Benchmark]
    [BenchmarkCategory("TagFiltering.EntityIteration")]
    public long TagFilterEntityIteration()
    {
        long checksum = 0;
        _tagWorld.ForEachEntity(
            in _tagEntityQuery,
            ref checksum,
            static (ref long sum, Entity entity) => sum += entity.Index + 1L);
        return Validate(checksum, "tag filter + entity iteration");
    }

    private long Validate(long checksum, string scenario)
    {
        if (checksum != _expectedChecksum)
        {
            throw new InvalidOperationException(
                $"{scenario} visited a different entity set: {checksum} != {_expectedChecksum}.");
        }

        return checksum;
    }
}

internal struct TagFilteringValueComponent
{
    internal int Value;
}

internal struct TagFilteringMarkerComponent
{
    internal int Value;
}

internal readonly struct TagFilteringMarkerTag;
