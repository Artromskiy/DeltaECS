using BenchmarkDotNet.Attributes;
using Delta.ECS;

namespace Delta.ECS.MicroBenchmarks;

internal struct WhereApiValue
{
    public int Value;
}

internal struct WhereApiAccumulator
{
    public int Value;
}

internal struct WhereApiDead
{
}

/// <summary>Measures query-wide read-only predicates and stable structural mutation rounds.</summary>
public class WhereApiMicroBenchmarkImplementation
{
    public int Amount { get; set; } = MicroBenchmarkConfiguration.CurrentAmount;

    private World _world = null!;
    private Entity[] _entities = null!;
    private Query _query;
    private Query _deadQuery;
    private ComponentId _valueId;
    private int _targetCount;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _valueId = layouts.Register<WhereApiValue>(new SchemaId(92_001));
        ComponentId accumulatorId = layouts.Register<WhereApiAccumulator>(new SchemaId(92_002));
        ComponentId deadId = layouts.Register<WhereApiDead>(new SchemaId(92_003));
        _world = new World(layouts, initialEntityCapacity: Amount, chunkCapacity: 512);
        _entities = _world.Create(stackalloc[] { _valueId, accumulatorId }, Amount);
        _targetCount = (Amount + 1) / 2;

        for (int index = 0; index < _entities.Length; index++)
        {
            _world.Set(_entities[index], _valueId, new WhereApiValue { Value = (index & 1) == 0 ? -1 : 1 });
        }

        QuerySpec description = QuerySpec.WhereAll(_valueId, accumulatorId);
        _query = _world.CreateQuery(in description);
        QuerySpec deadDescription = QuerySpec.WhereAll(_valueId, accumulatorId, deadId);
        _deadQuery = _world.CreateQuery(in deadDescription);
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark(Baseline = true)]
    public int DirectForEach()
    {
        _world.ForEach(
            in _query,
            static (in WhereApiValue value, ref WhereApiAccumulator accumulator) =>
            {
                if (value.Value <= 0)
                {
                    accumulator.Value++;
                }
            });
        return _targetCount;
    }

    [Benchmark]
    public int WhereForEachIn()
    {
        _world.Where(
                in _query,
                static (Entity entity, in WhereApiValue value) => value.Value <= 0)
            .ForEach(static (ref WhereApiAccumulator accumulator) => accumulator.Value++);
        return _targetCount;
    }

    [Benchmark]
    public int WhereAddRemove()
    {
        int added = _world.Where(
                in _query,
                static (Entity entity, in WhereApiValue value) => value.Value <= 0)
            .Add<WhereApiDead>();
        int removed = _world.Where(
                in _deadQuery,
                static (Entity entity, in WhereApiValue value) => value.Value <= 0)
            .Remove<WhereApiDead>();
        return added + removed;
    }

    internal int ExpectedIterationCount => _targetCount;

    internal int ExpectedStructuralCount => _targetCount * 2;
}
