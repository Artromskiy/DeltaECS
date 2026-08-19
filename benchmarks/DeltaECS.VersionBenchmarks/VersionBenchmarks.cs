extern alias baselineAdapter;
extern alias candidateAdapter;

namespace DeltaECS.VersionBenchmarks;

using BaselineAtomicOperation = baselineAdapter::DeltaECS.VersionAdapter.AtomicOperation;
using BaselineAtomicScenario = baselineAdapter::DeltaECS.VersionAdapter.AtomicScenario;
using BaselineBatchOperation = baselineAdapter::DeltaECS.VersionAdapter.BatchOperation;
using BaselineBatchScenario = baselineAdapter::DeltaECS.VersionAdapter.BatchScenario;
using BaselineIterationScenario = baselineAdapter::DeltaECS.VersionAdapter.IterationScenario;
using BenchmarkDotNet.Attributes;
using CandidateAtomicOperation = candidateAdapter::DeltaECS.VersionAdapter.AtomicOperation;
using CandidateAtomicScenario = candidateAdapter::DeltaECS.VersionAdapter.AtomicScenario;
using CandidateBatchOperation = candidateAdapter::DeltaECS.VersionAdapter.BatchOperation;
using CandidateBatchScenario = candidateAdapter::DeltaECS.VersionAdapter.BatchScenario;
using CandidateIterationScenario = candidateAdapter::DeltaECS.VersionAdapter.IterationScenario;

[MemoryDiagnoser]
[ShortRunJob]
public class VersionDenseBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private BaselineIterationScenario _baseline = null!;
    private CandidateIterationScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineIterationScenario(Amount);
        _candidate = new CandidateIterationScenario(Amount);
    }

    [Benchmark(Baseline = true)] public long Previous_Dense() => _baseline.DenseRead();
    [Benchmark] public long Candidate_Dense() => _candidate.DenseRead();
}

[MemoryDiagnoser]
[ShortRunJob]
public class VersionMovement2Benchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private BaselineIterationScenario _baseline = null!;
    private CandidateIterationScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineIterationScenario(Amount);
        _candidate = new CandidateIterationScenario(Amount);
    }

    [IterationSetup]
    public void Reset()
    {
        _baseline.ResetMovements();
        _candidate.ResetMovements();
    }

    [Benchmark(Baseline = true)] public double Previous_Movement2() => _baseline.Movement2();
    [Benchmark] public double Candidate_Movement2() => _candidate.Movement2();
}

[MemoryDiagnoser]
[ShortRunJob]
public class VersionMovement4Benchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private BaselineIterationScenario _baseline = null!;
    private CandidateIterationScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineIterationScenario(Amount);
        _candidate = new CandidateIterationScenario(Amount);
    }

    [IterationSetup]
    public void Reset()
    {
        _baseline.ResetMovements();
        _candidate.ResetMovements();
    }

    [Benchmark(Baseline = true)] public int Previous_Movement4() => _baseline.Movement4();
    [Benchmark] public int Candidate_Movement4() => _candidate.Movement4();
}

public enum VersionAtomicOperation { Create, Destroy, Add, Remove }

[MemoryDiagnoser]
[ShortRunJob]
public class VersionAtomicBenchmarks
{
    [ParamsAllValues] public VersionAtomicOperation Operation { get; set; }
    private BaselineAtomicScenario _baseline = null!;
    private CandidateAtomicScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineAtomicScenario();
        _candidate = new CandidateAtomicScenario();
    }

    [IterationSetup]
    public void Reset()
    {
        _baseline.Reset();
        _candidate.Reset();
    }

    [Benchmark(Baseline = true)] public int Previous_Atomic() => _baseline.Run((BaselineAtomicOperation)(int)Operation);
    [Benchmark] public int Candidate_Atomic() => _candidate.Run((CandidateAtomicOperation)(int)Operation);
}

public enum VersionBatchOperation { Create, Destroy, Add, Remove }

[MemoryDiagnoser]
[ShortRunJob]
public class VersionBatchBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    [ParamsAllValues] public VersionBatchOperation Operation { get; set; }
    private BaselineBatchScenario _baseline = null!;
    private CandidateBatchScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineBatchScenario(Amount);
        _candidate = new CandidateBatchScenario(Amount);
    }

    [IterationSetup]
    public void Reset()
    {
        _baseline.Reset();
        _candidate.Reset();
    }

    [Benchmark(Baseline = true)] public int Previous_Batch() => _baseline.Run((BaselineBatchOperation)(int)Operation);
    [Benchmark] public int Candidate_Batch() => _candidate.Run((CandidateBatchOperation)(int)Operation);
}

internal static class VersionBenchmarkCatalog
{
    public static readonly Type[] Types =
    [
        typeof(VersionDenseBenchmarks),
        typeof(VersionMovement2Benchmarks),
        typeof(VersionMovement4Benchmarks),
        typeof(VersionAtomicBenchmarks),
        typeof(VersionBatchBenchmarks)
    ];
}

internal static class VersionBenchmarkSmoke
{
    public static void Run()
    {
        const int amount = 100;
        var baselineIteration = new BaselineIterationScenario(amount);
        var candidateIteration = new CandidateIterationScenario(amount);
        RequireEqual(baselineIteration.DenseRead(), candidateIteration.DenseRead(), "Dense");
        baselineIteration.ResetMovements(); candidateIteration.ResetMovements();
        RequireNear(baselineIteration.Movement2(), candidateIteration.Movement2(), "Movement2");
        baselineIteration.ResetMovements(); candidateIteration.ResetMovements();
        RequireEqual(baselineIteration.Movement4(), candidateIteration.Movement4(), "Movement4");

        foreach (var operation in Enum.GetValues<VersionAtomicOperation>())
        {
            var baseline = new BaselineAtomicScenario();
            var candidate = new CandidateAtomicScenario();
            RequireEqual(
                baseline.Run((BaselineAtomicOperation)(int)operation),
                candidate.Run((CandidateAtomicOperation)(int)operation),
                $"Atomic.{operation}");
        }

        foreach (var operation in Enum.GetValues<VersionBatchOperation>())
        {
            var baseline = new BaselineBatchScenario(amount);
            var candidate = new CandidateBatchScenario(amount);
            RequireEqual(
                baseline.Run((BaselineBatchOperation)(int)operation),
                candidate.Run((CandidateBatchOperation)(int)operation),
                $"Batch.{operation}");
        }

        Console.WriteLine("Version comparison smoke passed: 3 iteration workloads, 4 atomic operations, 4 batch operations.");
    }

    private static void RequireEqual(long baseline, long candidate, string workload)
    {
        if (baseline != candidate)
        {
            throw new InvalidOperationException($"{workload} mismatch: baseline={baseline}, candidate={candidate}.");
        }
    }

    private static void RequireNear(double baseline, double candidate, string workload)
    {
        if (Math.Abs(baseline - candidate) > 0.0001)
        {
            throw new InvalidOperationException($"{workload} mismatch: baseline={baseline}, candidate={candidate}.");
        }
    }
}
