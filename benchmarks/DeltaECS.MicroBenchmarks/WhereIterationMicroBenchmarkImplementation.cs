using BenchmarkDotNet.Attributes;
using Delta.ECS;

namespace Delta.ECS.MicroBenchmarks;

internal struct WhereIterationValue
{
    public int Value;
}

internal struct WhereIterationAccumulator
{
    public int Value;
}

/// <summary>Compares direct iteration with always-true Where pipelines.</summary>
public class WhereIterationMicroBenchmarkImplementation
{
    public int Amount { get; set; } = MicroBenchmarkConfiguration.CurrentAmount;

    private World _world = null!;
    private Entity[] _entities = null!;
    private Query _query;
    private ComponentId _valueId;
    private ComponentId _accumulatorId;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _valueId = layouts.Register<WhereIterationValue>(new SchemaId(92_101));
        _accumulatorId = layouts.Register<WhereIterationAccumulator>(new SchemaId(92_102));
        _world = new World(layouts, initialEntityCapacity: Amount);
        _entities = new Entity[Amount];
        _world.Create(stackalloc[] { _valueId, _accumulatorId }, Amount, _entities);

        for (int index = 0; index < _entities.Length; index++)
        {
            _world.Set(_entities[index], _valueId, new WhereIterationValue { Value = index });
        }

        QuerySpec description = QuerySpec.WhereAll(_valueId, _accumulatorId);
        _query = _world.CreateQuery(in description);
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark(Baseline = true)]
    public int ForEach()
    {
        _world.ForEach(
            in _query,
            static (ref readonly WhereIterationValue _, ref WhereIterationAccumulator accumulator) =>
                accumulator.Value++);
        return Amount;
    }

    [Benchmark]
    public int WhereForEachTrue()
    {
        _world.Where(
                in _query,
                static (ref readonly WhereIterationValue _) => true)
            .ForEach(static (ref WhereIterationAccumulator accumulator) => accumulator.Value++);
        return Amount;
    }

    [Benchmark]
    public int WhereEntityForEachTrue()
    {
        _world.WhereEntity(
                in _query,
                static (Entity entity, ref readonly WhereIterationValue value) => true)
            .ForEach(static (ref WhereIterationAccumulator accumulator) => accumulator.Value++);
        return Amount;
    }

    internal int ExpectedIterationCount => Amount;

    internal int SumAccumulators()
    {
        int sum = 0;
        for (int index = 0; index < _entities.Length; index++)
        {
            sum += _world.Get<WhereIterationAccumulator>(_entities[index], _accumulatorId).Value;
        }

        return sum;
    }
}
