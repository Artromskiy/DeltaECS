namespace Delta.ECS.HistoryBenchmarks;

using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class HistoryRestoreBenchmarks : IDisposable
{
    private WorldHistory _history = null!;
    private SingleComponentRowHistory _singleComponentRow = null!;

    [GlobalSetup]
    public void Setup()
    {
        _history = new WorldHistory(HistoryBenchmarkOptions.EntityCount);
        _singleComponentRow = new SingleComponentRowHistory(HistoryBenchmarkOptions.EntityCount);
    }

    [GlobalCleanup]
    public void Cleanup() => Dispose();

    [Benchmark(Baseline = true)]
    public void PerComponentQueries() => _history.RestoreByComponent();

    [Benchmark]
    public void ForEachArchetype() => _history.RestoreByArchetype();

    [Benchmark]
    public void SingleComponentRow() => _singleComponentRow.Restore();

    public void Dispose()
    {
        _history?.Dispose();
        GC.SuppressFinalize(this);
    }
}
