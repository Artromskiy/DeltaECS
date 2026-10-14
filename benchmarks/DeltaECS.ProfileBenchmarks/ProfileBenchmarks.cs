extern alias baselineAdapter;
extern alias candidateAdapter;

namespace DeltaECS.ProfileBenchmarks;

using BaselineScenario = baselineAdapter::DeltaECS.ProfileAdapter.ProfileScenario;
using BenchmarkDotNet.Attributes;
using CandidateScenario = candidateAdapter::DeltaECS.ProfileAdapter.ProfileScenario;

[MemoryDiagnoser]
public abstract class ProfileBenchmarkBase
{
    private BaselineScenario _baseline = null!;
    private CandidateScenario _candidate = null!;
    public int Amount { get; set; } = ProfileOptions.Amount;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineScenario(Amount, ProfileOptions.RegisteredComponents, ProfileOptions.Archetypes);
        _candidate = new CandidateScenario(Amount, ProfileOptions.RegisteredComponents, ProfileOptions.Archetypes);
        RequireEqual(_baseline.LookupShuffled(), _candidate.LookupShuffled(), "lookup setup");
        RequireEqual(_baseline.StableSortTwoKeys(), _candidate.StableSortTwoKeys(), "sort setup");
        RequireEqual(_baseline.DenseEntityWalk(), _candidate.DenseEntityWalk(), "dense setup");
        RequireEqual(_baseline.StructuralToggle(), _candidate.StructuralToggle(), "add setup");
        RequireEqual(_baseline.StructuralToggle(), _candidate.StructuralToggle(), "remove setup");
        RequireEqual(_baseline.CreateDestroy(), _candidate.CreateDestroy(), "create/destroy setup");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _baseline.Dispose();
        _candidate.Dispose();
    }

    protected long PreviousLookup() => _baseline.LookupShuffled();
    protected long CandidateLookup() => _candidate.LookupShuffled();
    protected long PreviousSort() => _baseline.StableSortTwoKeys();
    protected long CandidateSort() => _candidate.StableSortTwoKeys();
    protected int PreviousStructural() => _baseline.StructuralToggle();
    protected int CandidateStructural() => _candidate.StructuralToggle();
    protected int PreviousCreateDestroy() => _baseline.CreateDestroy();
    protected int CandidateCreateDestroy() => _candidate.CreateDestroy();
    protected long PreviousDense() => _baseline.DenseEntityWalk();
    protected long CandidateDense() => _candidate.DenseEntityWalk();

    private static void RequireEqual<T>(T previous, T candidate, string workload) where T : IEquatable<T>
    {
        if (!previous.Equals(candidate)) throw new InvalidOperationException($"Profile benchmark {workload} checksum differs: {previous} != {candidate}.");
    }
}

public class HighIdLookupBenchmarks : ProfileBenchmarkBase
{
    [Benchmark(Baseline = true)] public long Previous_ShuffledLookup() => PreviousLookup();
    [Benchmark] public long Candidate_ShuffledLookup() => CandidateLookup();
}

public class StableSortBenchmarks : ProfileBenchmarkBase
{
    [Benchmark(Baseline = true)] public long Previous_StableTwoKeySort() => PreviousSort();
    [Benchmark] public long Candidate_StableTwoKeySort() => CandidateSort();
}

public class StructuralBenchmarks : ProfileBenchmarkBase
{
    [Benchmark(Baseline = true)] public int Previous_AddRemove() => PreviousStructural();
    [Benchmark] public int Candidate_AddRemove() => CandidateStructural();
}

public class CreateDestroyBenchmarks : ProfileBenchmarkBase
{
    [Benchmark(Baseline = true)] public int Previous_CreateDestroy() => PreviousCreateDestroy();
    [Benchmark] public int Candidate_CreateDestroy() => CandidateCreateDestroy();
}

public class DenseGuardrailBenchmarks : ProfileBenchmarkBase
{
    [Benchmark(Baseline = true)] public long Previous_DenseEntityWalk() => PreviousDense();
    [Benchmark] public long Candidate_DenseEntityWalk() => CandidateDense();
}
