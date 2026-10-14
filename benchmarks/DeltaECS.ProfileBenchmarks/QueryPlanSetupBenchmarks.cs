extern alias baselineAdapter;
extern alias candidateAdapter;

using BaselineScenario = baselineAdapter::DeltaECS.ProfileAdapter.QueryPlanScenario;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CandidateScenario = candidateAdapter::DeltaECS.ProfileAdapter.QueryPlanScenario;

namespace DeltaECS.ProfileBenchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class QueryPlanSetupBenchmarks
{
    private BaselineScenario _baseline = null!;
    private CandidateScenario _candidate = null!;

    [IterationSetup]
    public void SetupIteration()
    {
        _baseline?.Dispose();
        _candidate?.Dispose();
        _baseline = new BaselineScenario();
        _candidate = new CandidateScenario();
    }

    [IterationCleanup]
    public void CleanupIteration()
    {
        _baseline.Dispose();
        _candidate.Dispose();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("CreateQuery plan setup")]
    public int Mainline() => _baseline.CreateQueryBatch();

    [Benchmark, BenchmarkCategory("CreateQuery plan setup")]
    public int Candidate() => _candidate.CreateQueryBatch();
}
