extern alias baselineAdapter;
extern alias candidateAdapter;

namespace DeltaECS.ProfileBenchmarks;

using BaselineScenario = baselineAdapter::DeltaECS.ProfileAdapter.ProfileScenario;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CandidateScenario = candidateAdapter::DeltaECS.ProfileAdapter.ProfileScenario;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class NarrowIndexBenchmarks : IDisposable
{
    private BaselineScenario _baseline = null!;
    private CandidateScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineScenario(ProfileOptions.Amount, ProfileOptions.RegisteredComponents, ProfileOptions.Archetypes);
        _candidate = new CandidateScenario(ProfileOptions.Amount, ProfileOptions.RegisteredComponents, ProfileOptions.Archetypes);
        long expected = ((long)ProfileOptions.Amount + 1) / 2;
        if (_baseline.HasVariedIdsSequential() != expected || _candidate.HasVariedIdsSequential() != expected
            || _baseline.HasVariedIdsShuffled() != expected || _candidate.HasVariedIdsShuffled() != expected)
        {
            throw new InvalidOperationException("Varied-ID Has fixture checksum differs from the expected half-present mix.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("varied IDs sequential")]
    public long Mainline_HasVariedIdsSequential() => _baseline.HasVariedIdsSequential();

    [Benchmark, BenchmarkCategory("varied IDs sequential")]
    public long Candidate_HasVariedIdsSequential() => _candidate.HasVariedIdsSequential();

    [Benchmark(Baseline = true), BenchmarkCategory("varied IDs shuffled")]
    public long Mainline_HasVariedIdsShuffled() => _baseline.HasVariedIdsShuffled();

    [Benchmark, BenchmarkCategory("varied IDs shuffled")]
    public long Candidate_HasVariedIdsShuffled() => _candidate.HasVariedIdsShuffled();

    [GlobalCleanup]
    public void Dispose()
    {
        _baseline?.Dispose();
        _candidate?.Dispose();
        GC.SuppressFinalize(this);
    }
}
