extern alias baselineAdapter;
extern alias candidateAdapter;

namespace DeltaECS.ProfileBenchmarks;

using BaselineBatchScenario = baselineAdapter::DeltaECS.ProfileAdapter.BatchStructuralScenario;
using BaselineHasScenario = baselineAdapter::DeltaECS.ProfileAdapter.HasScenario;
using BaselineScenario = baselineAdapter::DeltaECS.ProfileAdapter.ProfileScenario;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CandidateBatchScenario = candidateAdapter::DeltaECS.ProfileAdapter.BatchStructuralScenario;
using CandidateHasScenario = candidateAdapter::DeltaECS.ProfileAdapter.HasScenario;
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
        RequireEqual(_baseline.HasSequential(), _candidate.HasSequential(), "sequential Has setup");
        RequireEqual(_baseline.HasShuffled(), _candidate.HasShuffled(), "shuffled Has setup");
        RequireEqual(_baseline.GetSequential(), _candidate.GetSequential(), "sequential Get setup");
        RequireEqual(_baseline.GetShuffled(), _candidate.GetShuffled(), "shuffled Get setup");
        RequireEqual(_baseline.GetPrimarySequential(), _candidate.GetPrimarySequential(), "sequential primary Get setup");
        RequireEqual(_baseline.GetPrimaryShuffled(), _candidate.GetPrimaryShuffled(), "shuffled primary Get setup");
        RequireEqual(_baseline.TryGetPrimarySequential(), _candidate.TryGetPrimarySequential(), "sequential primary TryGet setup");
        RequireEqual(_baseline.TryGetPrimaryShuffled(), _candidate.TryGetPrimaryShuffled(), "shuffled primary TryGet setup");
        long expectedPrimarySum = (long)Amount * (Amount + 1) / 2;
        if (_baseline.GetPrimarySequential() != expectedPrimarySum
            || _baseline.TryGetPrimarySequential() != expectedPrimarySum)
        {
            throw new InvalidOperationException("Primary Get/TryGet benchmark fixture checksum differs from expected values.");
        }
        RequireEqual(_baseline.GetRefSequential(), _candidate.GetRefSequential(), "sequential GetRef setup");
        RequireEqual(_baseline.GetRefShuffled(), _candidate.GetRefShuffled(), "shuffled GetRef setup");
        RequireEqual(_baseline.StableSortTwoKeys(), _candidate.StableSortTwoKeys(), "sort setup");
        RequireEqual(_baseline.OrderedFirst(), _candidate.OrderedFirst(), "ordered First setup");
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
    protected long PreviousHasSequential() => _baseline.HasSequential();
    protected long CandidateHasSequential() => _candidate.HasSequential();
    protected long PreviousHasShuffled() => _baseline.HasShuffled();
    protected long CandidateHasShuffled() => _candidate.HasShuffled();
    protected long PreviousGetSequential() => _baseline.GetSequential();
    protected long CandidateGetSequential() => _candidate.GetSequential();
    protected long PreviousGetShuffled() => _baseline.GetShuffled();
    protected long CandidateGetShuffled() => _candidate.GetShuffled();
    protected long PreviousGetPrimarySequential() => _baseline.GetPrimarySequential();
    protected long CandidateGetPrimarySequential() => _candidate.GetPrimarySequential();
    protected long PreviousGetPrimaryShuffled() => _baseline.GetPrimaryShuffled();
    protected long CandidateGetPrimaryShuffled() => _candidate.GetPrimaryShuffled();
    protected long PreviousTryGetPrimarySequential() => _baseline.TryGetPrimarySequential();
    protected long CandidateTryGetPrimarySequential() => _candidate.TryGetPrimarySequential();
    protected long PreviousTryGetPrimaryShuffled() => _baseline.TryGetPrimaryShuffled();
    protected long CandidateTryGetPrimaryShuffled() => _candidate.TryGetPrimaryShuffled();
    protected long PreviousGetRefSequential() => _baseline.GetRefSequential();
    protected long CandidateGetRefSequential() => _candidate.GetRefSequential();
    protected long PreviousGetRefShuffled() => _baseline.GetRefShuffled();
    protected long CandidateGetRefShuffled() => _candidate.GetRefShuffled();
    protected long PreviousSort() => _baseline.StableSortTwoKeys();
    protected long CandidateSort() => _candidate.StableSortTwoKeys();
    protected int PreviousOrderedFirst() => _baseline.OrderedFirst();
    protected int CandidateOrderedFirst() => _candidate.OrderedFirst();
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

public class OrderedFirstBenchmarks : ProfileBenchmarkBase
{
    [Benchmark(Baseline = true)] public int Previous_OrderedFirst() => PreviousOrderedFirst();
    [Benchmark] public int Candidate_OrderedFirst() => CandidateOrderedFirst();
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

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ComponentAccessBenchmarks : ProfileBenchmarkBase
{
    [Benchmark(Baseline = true), BenchmarkCategory("Has sequential")]
    public long Mainline_HasSequential() => PreviousHasSequential();
    [Benchmark, BenchmarkCategory("Has sequential")]
    public long Candidate_HasSequential() => CandidateHasSequential();

    [Benchmark(Baseline = true), BenchmarkCategory("Has shuffled")]
    public long Mainline_HasShuffled() => PreviousHasShuffled();
    [Benchmark, BenchmarkCategory("Has shuffled")]
    public long Candidate_HasShuffled() => CandidateHasShuffled();

    [Benchmark(Baseline = true), BenchmarkCategory("Get sequential")]
    public long Mainline_GetSequential() => PreviousGetSequential();
    [Benchmark, BenchmarkCategory("Get sequential")]
    public long Candidate_GetSequential() => CandidateGetSequential();

    [Benchmark(Baseline = true), BenchmarkCategory("Get shuffled")]
    public long Mainline_GetShuffled() => PreviousGetShuffled();
    [Benchmark, BenchmarkCategory("Get shuffled")]
    public long Candidate_GetShuffled() => CandidateGetShuffled();

    [Benchmark(Baseline = true), BenchmarkCategory("Get primary sequential")]
    public long Mainline_GetPrimarySequential() => PreviousGetPrimarySequential();
    [Benchmark, BenchmarkCategory("Get primary sequential")]
    public long Candidate_GetPrimarySequential() => CandidateGetPrimarySequential();

    [Benchmark(Baseline = true), BenchmarkCategory("Get primary shuffled")]
    public long Mainline_GetPrimaryShuffled() => PreviousGetPrimaryShuffled();
    [Benchmark, BenchmarkCategory("Get primary shuffled")]
    public long Candidate_GetPrimaryShuffled() => CandidateGetPrimaryShuffled();

    [Benchmark(Baseline = true), BenchmarkCategory("TryGet primary sequential")]
    public long Mainline_TryGetPrimarySequential() => PreviousTryGetPrimarySequential();
    [Benchmark, BenchmarkCategory("TryGet primary sequential")]
    public long Candidate_TryGetPrimarySequential() => CandidateTryGetPrimarySequential();

    [Benchmark(Baseline = true), BenchmarkCategory("TryGet primary shuffled")]
    public long Mainline_TryGetPrimaryShuffled() => PreviousTryGetPrimaryShuffled();
    [Benchmark, BenchmarkCategory("TryGet primary shuffled")]
    public long Candidate_TryGetPrimaryShuffled() => CandidateTryGetPrimaryShuffled();

    [Benchmark(Baseline = true), BenchmarkCategory("GetRef sequential")]
    public long Mainline_GetRefSequential() => PreviousGetRefSequential();
    [Benchmark, BenchmarkCategory("GetRef sequential")]
    public long Candidate_GetRefSequential() => CandidateGetRefSequential();

    [Benchmark(Baseline = true), BenchmarkCategory("GetRef shuffled")]
    public long Mainline_GetRefShuffled() => PreviousGetRefShuffled();
    [Benchmark, BenchmarkCategory("GetRef shuffled")]
    public long Candidate_GetRefShuffled() => CandidateGetRefShuffled();
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class HasDispatchBenchmarks
{
    private BaselineHasScenario _baseline = null!;
    private CandidateHasScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineHasScenario(ProfileOptions.Amount);
        _candidate = new CandidateHasScenario(ProfileOptions.Amount);
        RequireEqual(_baseline.HasDataByIdSequential(), _candidate.HasDataByIdSequential(), "data ID sequential");
        RequireEqual(_baseline.HasDataByIdShuffled(), _candidate.HasDataByIdShuffled(), "data ID shuffled");
        RequireEqual(_baseline.HasTagByIdSequential(), _candidate.HasTagByIdSequential(), "tag ID sequential");
        RequireEqual(_baseline.HasTagByIdShuffled(), _candidate.HasTagByIdShuffled(), "tag ID shuffled");
        RequireEqual(_baseline.HasDataGenericSequential(), _candidate.HasDataGenericSequential(), "data generic sequential");
        RequireEqual(_baseline.HasDataGenericShuffled(), _candidate.HasDataGenericShuffled(), "data generic shuffled");
        RequireEqual(_baseline.TryGetTagGenericSequential(), _candidate.TryGetTagGenericSequential(), "tag TryGet sequential");
        RequireEqual(_baseline.TryGetTagGenericShuffled(), _candidate.TryGetTagGenericShuffled(), "tag TryGet shuffled");
        if (_baseline.HasDataByIdSequential() != _baseline.ExpectedDataCount
            || _baseline.HasTagByIdSequential() != _baseline.ExpectedTagCount
            || _baseline.TryGetTagGenericSequential() != _baseline.ExpectedTagCount)
        {
            throw new InvalidOperationException("Component access benchmark fixture does not contain the expected component mix.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _baseline.Dispose();
        _candidate.Dispose();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("data ID sequential")]
    public long Mainline_DataIdSequential() => _baseline.HasDataByIdSequential();
    [Benchmark, BenchmarkCategory("data ID sequential")]
    public long Candidate_DataIdSequential() => _candidate.HasDataByIdSequential();

    [Benchmark(Baseline = true), BenchmarkCategory("data ID shuffled")]
    public long Mainline_DataIdShuffled() => _baseline.HasDataByIdShuffled();
    [Benchmark, BenchmarkCategory("data ID shuffled")]
    public long Candidate_DataIdShuffled() => _candidate.HasDataByIdShuffled();

    [Benchmark(Baseline = true), BenchmarkCategory("tag ID sequential")]
    public long Mainline_TagIdSequential() => _baseline.HasTagByIdSequential();
    [Benchmark, BenchmarkCategory("tag ID sequential")]
    public long Candidate_TagIdSequential() => _candidate.HasTagByIdSequential();

    [Benchmark(Baseline = true), BenchmarkCategory("tag ID shuffled")]
    public long Mainline_TagIdShuffled() => _baseline.HasTagByIdShuffled();
    [Benchmark, BenchmarkCategory("tag ID shuffled")]
    public long Candidate_TagIdShuffled() => _candidate.HasTagByIdShuffled();

    [Benchmark(Baseline = true), BenchmarkCategory("data generic sequential")]
    public long Mainline_DataGenericSequential() => _baseline.HasDataGenericSequential();
    [Benchmark, BenchmarkCategory("data generic sequential")]
    public long Candidate_DataGenericSequential() => _candidate.HasDataGenericSequential();

    [Benchmark(Baseline = true), BenchmarkCategory("data generic shuffled")]
    public long Mainline_DataGenericShuffled() => _baseline.HasDataGenericShuffled();
    [Benchmark, BenchmarkCategory("data generic shuffled")]
    public long Candidate_DataGenericShuffled() => _candidate.HasDataGenericShuffled();

    [Benchmark(Baseline = true), BenchmarkCategory("tag TryGet sequential")]
    public long Mainline_TryGetTagGenericSequential() => _baseline.TryGetTagGenericSequential();
    [Benchmark, BenchmarkCategory("tag TryGet sequential")]
    public long Candidate_TryGetTagGenericSequential() => _candidate.TryGetTagGenericSequential();

    [Benchmark(Baseline = true), BenchmarkCategory("tag TryGet shuffled")]
    public long Mainline_TryGetTagGenericShuffled() => _baseline.TryGetTagGenericShuffled();
    [Benchmark, BenchmarkCategory("tag TryGet shuffled")]
    public long Candidate_TryGetTagGenericShuffled() => _candidate.TryGetTagGenericShuffled();

    private static void RequireEqual(long baseline, long candidate, string workload)
    {
        if (baseline != candidate)
        {
            throw new InvalidOperationException($"Has benchmark {workload} checksum differs: {baseline} != {candidate}.");
        }
    }
}

[MemoryDiagnoser]
public class BatchCreateBenchmarks
{
    private BaselineBatchScenario _baseline = null!;
    private CandidateBatchScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineBatchScenario(ProfileOptions.Amount);
        _candidate = new CandidateBatchScenario(ProfileOptions.Amount);
        _baseline.PrepareForCreate();
        _candidate.PrepareForCreate();
        RequireEqual(_baseline.CreateBatch(), _candidate.CreateBatch());
    }

    [IterationSetup]
    public void PrepareIteration()
    {
        _baseline.PrepareForCreate();
        _candidate.PrepareForCreate();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _baseline.Dispose();
        _candidate.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int Mainline_CreateBatch() => _baseline.CreateBatch();

    [Benchmark]
    public int Candidate_CreateBatch() => _candidate.CreateBatch();

    private static void RequireEqual(int baseline, int candidate)
    {
        if (baseline != candidate)
        {
            throw new InvalidOperationException($"Batch create count differs: {baseline} != {candidate}.");
        }
    }
}

[MemoryDiagnoser]
public class BatchCreateAppendOnlyBenchmarks
{
    private BaselineBatchScenario _baseline = null!;
    private CandidateBatchScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineBatchScenario(ProfileOptions.Amount);
        _candidate = new CandidateBatchScenario(ProfileOptions.Amount);
        _baseline.PrepareForAppendOnlyCreate();
        _candidate.PrepareForAppendOnlyCreate();
        RequireEqual(_baseline.CreateBatch(), _candidate.CreateBatch());
        _baseline.PrepareForAppendOnlyCreate();
        _candidate.PrepareForAppendOnlyCreate();
    }

    [IterationSetup]
    public void PrepareIteration()
    {
        _baseline.PrepareForAppendOnlyCreate();
        _candidate.PrepareForAppendOnlyCreate();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _baseline.Dispose();
        _candidate.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int Mainline_CreateAppendOnlyBatch() => _baseline.CreateBatch();

    [Benchmark]
    public int Candidate_CreateAppendOnlyBatch() => _candidate.CreateBatch();

    private static void RequireEqual(int baseline, int candidate)
    {
        if (baseline != candidate)
        {
            throw new InvalidOperationException($"Append-only batch create count differs: {baseline} != {candidate}.");
        }
    }
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BatchDestroyBenchmarks
{
    private BaselineBatchScenario _baseline = null!;
    private CandidateBatchScenario _candidate = null!;
    private int _amount;

    [GlobalSetup]
    public void Setup()
    {
        _amount = ProfileOptions.Amount;
        _baseline = new BaselineBatchScenario(_amount);
        _candidate = new CandidateBatchScenario(_amount);
        _baseline.PrepareForDestroy();
        _candidate.PrepareForDestroy();
        RequireEqual(_baseline.DestroyRandomHalf(), _candidate.DestroyRandomHalf());
        _baseline.PrepareForDestroy();
        _candidate.PrepareForDestroy();
        RequireEqual(_baseline.DestroyRandomFullBatch(), _candidate.DestroyRandomFullBatch());
        if (_baseline.AliveEntityCount != 0 || _candidate.AliveEntityCount != 0)
        {
            throw new InvalidOperationException("Full batch destroy left entities alive.");
        }
    }

    [IterationSetup]
    public void PrepareIteration()
    {
        _baseline.PrepareForDestroy();
        _candidate.PrepareForDestroy();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _baseline.Dispose();
        _candidate.Dispose();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Destroy random half")]
    public int Mainline_DestroyRandomHalf() => _baseline.DestroyRandomHalf();

    [Benchmark, BenchmarkCategory("Destroy random half")]
    public int Candidate_DestroyRandomHalf() => _candidate.DestroyRandomHalf();

    [Benchmark(Baseline = true), BenchmarkCategory("Destroy random full")]
    public int Mainline_DestroyRandomFullBatch() => _baseline.DestroyRandomFullBatch();

    [Benchmark, BenchmarkCategory("Destroy random full")]
    public int Candidate_DestroyRandomFullBatch() => _candidate.DestroyRandomFullBatch();

    private static void RequireEqual(int baseline, int candidate)
    {
        if (baseline != candidate)
        {
            throw new InvalidOperationException($"Batch destroy count differs: {baseline} != {candidate}.");
        }
    }
}
