extern alias baselineAdapter;
extern alias candidateAdapter;

namespace DeltaECS.ProfileBenchmarks;

using BaselineBatchScenario = baselineAdapter::DeltaECS.ProfileAdapter.BatchStructuralScenario;
using BaselineScenario = baselineAdapter::DeltaECS.ProfileAdapter.ProfileScenario;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using CandidateBatchScenario = candidateAdapter::DeltaECS.ProfileAdapter.BatchStructuralScenario;
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
        RequireEqual(_baseline.GetRefSequential(), _candidate.GetRefSequential(), "sequential GetRef setup");
        RequireEqual(_baseline.GetRefShuffled(), _candidate.GetRefShuffled(), "shuffled GetRef setup");
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
    protected long PreviousHasSequential() => _baseline.HasSequential();
    protected long CandidateHasSequential() => _candidate.HasSequential();
    protected long PreviousHasShuffled() => _baseline.HasShuffled();
    protected long CandidateHasShuffled() => _candidate.HasShuffled();
    protected long PreviousGetSequential() => _baseline.GetSequential();
    protected long CandidateGetSequential() => _candidate.GetSequential();
    protected long PreviousGetShuffled() => _baseline.GetShuffled();
    protected long CandidateGetShuffled() => _candidate.GetShuffled();
    protected long PreviousGetRefSequential() => _baseline.GetRefSequential();
    protected long CandidateGetRefSequential() => _candidate.GetRefSequential();
    protected long PreviousGetRefShuffled() => _baseline.GetRefShuffled();
    protected long CandidateGetRefShuffled() => _candidate.GetRefShuffled();
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
        _baseline.PrepareForCreateWithoutOutput();
        _candidate.PrepareForCreateWithoutOutput();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _baseline.Dispose();
        _candidate.Dispose();
    }

    [IterationSetup]
    public void PrepareIteration()
    {
        _baseline.PrepareForCreateWithoutOutput();
        _candidate.PrepareForCreateWithoutOutput();
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

    [GlobalCleanup]
    public void Cleanup()
    {
        _baseline.Dispose();
        _candidate.Dispose();
    }

    [IterationSetup]
    public void PrepareIteration()
    {
        _baseline.PrepareForAppendOnlyCreate();
        _candidate.PrepareForAppendOnlyCreate();
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
public class BatchCreateWithoutOutputBenchmarks
{
    private BaselineBatchScenario _baseline = null!;
    private CandidateBatchScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineBatchScenario(ProfileOptions.Amount);
        _candidate = new CandidateBatchScenario(ProfileOptions.Amount);
        _baseline.PrepareForCreateWithoutOutput();
        _candidate.PrepareForCreateWithoutOutput();
        RequireEqual(_baseline.CreateBatchWithoutOutput(), _candidate.CreateBatchWithoutOutput());
        _baseline.PrepareForCreateWithoutOutput();
        _candidate.PrepareForCreateWithoutOutput();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _baseline.Dispose();
        _candidate.Dispose();
    }

    [IterationSetup]
    public void PrepareIteration()
    {
        _baseline.PrepareForCreateWithoutOutput();
        _candidate.PrepareForCreateWithoutOutput();
    }

    [Benchmark(Baseline = true)]
    public int Mainline_CreateWithoutOutputBatch() => _baseline.CreateBatchWithoutOutput();

    [Benchmark]
    public int Candidate_CreateWithoutOutputBatch() => _candidate.CreateBatchWithoutOutput();

    private static void RequireEqual(int baseline, int candidate)
    {
        if (baseline != candidate)
        {
            throw new InvalidOperationException($"Batch creation count differs: {baseline} != {candidate}.");
        }
    }
}

[MemoryDiagnoser]
public class BatchCreateWithoutOutputAppendOnlyBenchmarks
{
    private BaselineBatchScenario _baseline = null!;
    private CandidateBatchScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineBatchScenario(ProfileOptions.Amount);
        _candidate = new CandidateBatchScenario(ProfileOptions.Amount);
        _baseline.PrepareForAppendOnlyCreateWithoutOutput();
        _candidate.PrepareForAppendOnlyCreateWithoutOutput();
        RequireEqual(_baseline.CreateBatchWithoutOutput(), _candidate.CreateBatchWithoutOutput());
        _baseline.PrepareForAppendOnlyCreateWithoutOutput();
        _candidate.PrepareForAppendOnlyCreateWithoutOutput();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _baseline.Dispose();
        _candidate.Dispose();
    }

    [IterationSetup]
    public void PrepareIteration()
    {
        _baseline.PrepareForAppendOnlyCreateWithoutOutput();
        _candidate.PrepareForAppendOnlyCreateWithoutOutput();
    }

    [Benchmark(Baseline = true)]
    public int Mainline_CreateWithoutOutputAppendOnlyBatch() => _baseline.CreateBatchWithoutOutput();

    [Benchmark]
    public int Candidate_CreateWithoutOutputAppendOnlyBatch() => _candidate.CreateBatchWithoutOutput();

    private static void RequireEqual(int baseline, int candidate)
    {
        if (baseline != candidate)
        {
            throw new InvalidOperationException($"Append-only batch creation count differs: {baseline} != {candidate}.");
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
