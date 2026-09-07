using BenchmarkDotNet.Attributes;
using Delta.ECS;

namespace Delta.ECS.MicroBenchmarks;

internal struct RemainingApiA
{
    public int Value;
}

internal struct RemainingApiB
{
    public int Value;
}

internal struct RemainingApiMarker
{
    public int Value;
}

public class RemainingApiOptimizationMicroBenchmarkImplementation
{
    public int Amount { get; set; } = MicroBenchmarkConfiguration.CurrentAmount;

    private World _world = null!;
    private Entity[] _entities = null!;
    private Query _query;
    private ReadAccess _a;
    private ReadAccess _b;
    private ComponentId _aId;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _aId = layouts.Register<RemainingApiA>(new SchemaId(91_001));
        ComponentId bId = layouts.Register<RemainingApiB>(new SchemaId(91_002));
        ComponentId markerId = layouts.Register<RemainingApiMarker>(new SchemaId(91_003));
        _world = new World(layouts, initialEntityCapacity: Amount, chunkCapacity: 128);
        _entities = new Entity[Amount];

        for (int index = 0; index < Amount; index++)
        {
            Entity entity = (index % 3) switch
            {
                0 => _world.Create(_aId, bId),
                1 => _world.Create(_aId),
                _ => _world.Create(_aId, bId, markerId)
            };
            _entities[index] = entity;
            _world.Set(entity, _aId, new RemainingApiA { Value = 1 });
            _world.Set(entity, bId, new RemainingApiB { Value = 2 });
        }

        _query = _world.CreateQuery(QuerySpec.WhereAll(_aId, bId));
        _a = _query.AccessRead(_aId);
        _b = _query.AccessRead(bId);
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark]
    public int ThreeWhile()
    {
        int checksum = 0;
        using var scope = _world.BeginScope(in _query);
        QueryArchetypes archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            QueryArchetypeChunks chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                QuerySlots slots = chunks.Current.Slots;
                ReadRow a = slots.GetRow(_a);
                ReadRow b = slots.GetRow(_b);
                while (slots.MoveNext())
                {
                    checksum += a.Ref<RemainingApiA>(slots).Value;
                    checksum += b.Ref<RemainingApiB>(slots).Value;
                }
            }
        }

        return checksum;
    }

    [Benchmark]
    public int TwoWhile()
    {
        int checksum = 0;
        using var scope = _world.BeginScope(in _query);
        QueryChunks chunks = scope.Chunks;
        while (chunks.MoveNext())
        {
            QuerySlots slots = chunks.Current.Slots;
            ReadRow a = slots.GetRow(_a);
            ReadRow b = slots.GetRow(_b);
            while (slots.MoveNext())
            {
                checksum += a.Ref<RemainingApiA>(slots).Value;
                checksum += b.Ref<RemainingApiB>(slots).Value;
            }
        }

        return checksum;
    }

    [Benchmark]
    public int FilteredSequence()
    {
        int checksum = 0;
        _world.From(_entities).Where(in _query).ForEachEntity(
            ref checksum,
            static (ref int value, Entity entity) => value += entity.Index);
        return checksum;
    }

    [Benchmark]
    public int GeneratedFilteredSequence()
    {
        int checksum = 0;
        _world.From(_entities).Where(in _query).ForEach<int, RemainingApiA, RemainingApiB>(
            ref checksum,
            static (ref int value, in RemainingApiA a, in RemainingApiB b) =>
                value += a.Value + b.Value);
        return checksum;
    }

    [Benchmark]
    public ulong StampRows()
    {
        ulong checksum = 0;
        using var scope = _world.BeginScope(in _query);
        QueryArchetypes archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            QueryArchetypeChunks chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                QueryChunk chunk = chunks.Current;
                StampRow stamps = chunk.GetStampRow(_a);
                QuerySlots slots = chunk.Slots;
                while (slots.MoveNext())
                {
                    checksum += stamps.Get(in slots).Value;
                }
            }
        }

        return checksum;
    }

    [Benchmark]
    public int PointSet()
    {
        int written = 0;
        for (int index = 0; index < _entities.Length; index++)
        {
            written += _world.Set(
                _entities[index],
                _aId,
                new RemainingApiA { Value = index }) ? 1 : 0;
        }

        return written;
    }
}
