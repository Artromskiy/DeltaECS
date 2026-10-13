using BenchmarkDotNet.Attributes;

namespace Delta.ECS.MicroBenchmarks;

/// <summary>Measures query-wide read-only predicates and stable structural mutation rounds.</summary>
public class WhereApiMicroBenchmarkImplementation
{

    private World _world = null!;
    private Entity[] _entities = null!;
    private Query _query;
    private Query _deadQuery;
    private ComponentId _valueId;
    private IOperation? _directIteration;
    private IOperation? _whereIteration;
    public int Amount { get; set; } = MicroBenchmarkConfiguration.CurrentAmount;

    internal int ExpectedIterationCount { get; private set; }

    internal int ExpectedStructuralCount => ExpectedIterationCount * 2;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentRegistry();
        _valueId = layouts.Register<WhereApiValue>(new SchemaId(92_001));
        ComponentId accumulatorId = layouts.Register<WhereApiAccumulator>(new SchemaId(92_002));
        ComponentId deadId = layouts.Register<WhereApiDead>(new SchemaId(92_003));
        _world = new World(layouts, initialEntityCapacity: Amount);
        _entities = new Entity[Amount];
        _world.Create([_valueId, accumulatorId], Amount, _entities);
        ExpectedIterationCount = (Amount + 1) / 2;

        for (int index = 0; index < _entities.Length; index++)
        {
            _world.GetRef<WhereApiValue>(_entities[index], _valueId) = new WhereApiValue { Value = (index & 1) == 0 ? -1 : 1 };
        }

        QuerySpec description = QuerySpec.WhereAll([_valueId, accumulatorId]);
        _query = _world.CreateQuery(in description);
        QuerySpec deadDescription = QuerySpec.WhereAll([_valueId, accumulatorId, deadId]);
        _deadQuery = _world.CreateQuery(in deadDescription);
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark(Baseline = true)]
    public int DirectForEach()
    {
        _directIteration ??= _world.ForEach(
            in _query,
            static (ref readonly WhereApiValue value, ref WhereApiAccumulator accumulator) =>
            {
                if (value.Value <= 0)
                {
                    accumulator.Value++;
                }
            });
        _directIteration.Invoke();
        return ExpectedIterationCount;
    }

    [Benchmark]
    public int WhereForEachRefReadonly()
    {
        _whereIteration ??= _world.Where(
                in _query,
                static (ref readonly WhereApiValue value) => value.Value <= 0)
            .ForEach(static (ref WhereApiAccumulator accumulator) => accumulator.Value++);
        _whereIteration.Invoke();
        return ExpectedIterationCount;
    }

    [Benchmark]
    public int WhereAddRemove()
    {
        int added = _world.Where(
                in _query,
                static (ref readonly WhereApiValue value) => value.Value <= 0)
            .Add<WhereApiDead>().Invoke();
        int removed = _world.Where(
                in _deadQuery,
                static (ref readonly WhereApiValue value) => value.Value <= 0)
            .Remove<WhereApiDead>().Invoke();
        return added + removed;
    }

}

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
