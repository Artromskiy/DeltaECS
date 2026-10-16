extern alias baselineAdapter;
extern alias candidateAdapter;

using BaselineScenario = baselineAdapter::DeltaECS.VersionAdapter.EntityResolutionScenario;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CandidateScenario = candidateAdapter::DeltaECS.VersionAdapter.EntityResolutionScenario;

namespace DeltaECS.EntityResolutionBenchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class EntityResolutionBenchmarks
{
    private BaselineScenario _baseline = null!;
    private CandidateScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineScenario(Program.Amount);
        _candidate = new CandidateScenario(Program.Amount);
        if (_baseline.ResolveAll(false) != Program.Amount
            || _candidate.ResolveAll(false) != Program.Amount
            || _baseline.HasAll(false) != Program.Amount
            || _candidate.HasAll(false) != Program.Amount
            || _baseline.GetRefAll(false) != (long)Program.Amount * (Program.Amount + 1) / 2
            || _candidate.GetRefAll(false) != (long)Program.Amount * (Program.Amount + 1) / 2
            || _baseline.TryGetAll() != (long)Program.Amount * (Program.Amount + 1) / 2
            || _candidate.TryGetAll() != (long)Program.Amount * (Program.Amount + 1) / 2
            || _baseline.GetAll() != (long)Program.Amount * (Program.Amount + 1) / 2
            || _candidate.GetAll() != (long)Program.Amount * (Program.Amount + 1) / 2)
        {
            throw new InvalidOperationException("Mainline and candidate entity lookup checksums differ.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("IsAlive sequential")]
    public int Mainline_Sequential() => _baseline.ResolveAll(false);
    [Benchmark, BenchmarkCategory("IsAlive sequential")]
    public int Candidate_Sequential() => _candidate.ResolveAll(false);

    [Benchmark(Baseline = true), BenchmarkCategory("IsAlive shuffled")]
    public int Mainline_Shuffled() => _baseline.ResolveAll(true);
    [Benchmark, BenchmarkCategory("IsAlive shuffled")]
    public int Candidate_Shuffled() => _candidate.ResolveAll(true);

    [Benchmark, BenchmarkCategory("Has")]
    public int Candidate_Has() => _candidate.HasAll(false);
    [Benchmark(Baseline = true), BenchmarkCategory("Has")]
    public int Mainline_Has() => _baseline.HasAll(false);

    [Benchmark(Baseline = true), BenchmarkCategory("GetRef")]
    public long Mainline_GetRef() => _baseline.GetRefAll(false);
    [Benchmark, BenchmarkCategory("GetRef")]
    public long Candidate_GetRef() => _candidate.GetRefAll(false);

    [Benchmark(Baseline = true), BenchmarkCategory("TryGet")]
    public long Mainline_TryGet() => _baseline.TryGetAll();
    [Benchmark, BenchmarkCategory("TryGet")]
    public long Candidate_TryGet() => _candidate.TryGetAll();

    [Benchmark(Baseline = true), BenchmarkCategory("Get")]
    public long Mainline_Get() => _baseline.GetAll();
    [Benchmark, BenchmarkCategory("Get")]
    public long Candidate_Get() => _candidate.GetAll();
}
