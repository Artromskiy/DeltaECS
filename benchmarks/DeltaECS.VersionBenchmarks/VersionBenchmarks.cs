extern alias baselineAdapter;
extern alias candidateAdapter;

namespace DeltaECS.VersionBenchmarks;

using BaselineIterationScenario = baselineAdapter::DeltaECS.VersionAdapter.IterationScenario;
using CandidateIterationScenario = candidateAdapter::DeltaECS.VersionAdapter.IterationScenario;
using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class VersionDenseBenchmarks
{
    public int Amount { get; set; } = VersionBenchmarkConfiguration.CurrentAmount;
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
public class VersionMovement2Benchmarks
{
    public int Amount { get; set; } = VersionBenchmarkConfiguration.CurrentAmount;
    private BaselineIterationScenario _baseline = null!;
    private CandidateIterationScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineIterationScenario(Amount);
        _candidate = new CandidateIterationScenario(Amount);
    }

    public void Reset()
    {
        _baseline.ResetMovements();
        _candidate.ResetMovements();
    }

    [Benchmark(Baseline = true)] public double Previous_Movement2() => _baseline.Movement2();
    [Benchmark] public double Candidate_Movement2() => _candidate.Movement2();
}

[MemoryDiagnoser]
public class VersionMovement4Benchmarks
{
    public int Amount { get; set; } = VersionBenchmarkConfiguration.CurrentAmount;
    private BaselineIterationScenario _baseline = null!;
    private CandidateIterationScenario _candidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new BaselineIterationScenario(Amount);
        _candidate = new CandidateIterationScenario(Amount);
    }

    public void Reset()
    {
        _baseline.ResetMovements();
        _candidate.ResetMovements();
    }

    [Benchmark(Baseline = true)] public int Previous_Movement4() => _baseline.Movement4();
    [Benchmark] public int Candidate_Movement4() => _candidate.Movement4();
}

internal static class VersionBenchmarkCatalog
{
    public static readonly Type[] Types =
    [
        typeof(VersionDenseBenchmarks),
        typeof(VersionMovement2Benchmarks),
        typeof(VersionMovement4Benchmarks)
    ];
}

internal static class VersionBenchmarkSmoke
{
    public static void Run()
    {
        RequireThroughputIterationConfiguration();
        const int amount = 100;
        var baseline = new BaselineIterationScenario(amount);
        var candidate = new CandidateIterationScenario(amount);
        RequireEqual(baseline.DenseRead(), candidate.DenseRead(), "Dense");
        baseline.ResetMovements();
        candidate.ResetMovements();
        RequireNear(baseline.Movement2(), candidate.Movement2(), "Movement2");
        baseline.ResetMovements();
        candidate.ResetMovements();
        RequireEqual(baseline.Movement4(), candidate.Movement4(), "Movement4");
        Console.WriteLine("Version comparison smoke passed: 3 iteration workloads.");
    }

    private static void RequireThroughputIterationConfiguration()
    {
        foreach (var type in VersionBenchmarkCatalog.Types)
        {
            if (type.GetCustomAttributes(inherit: true).Any(attribute => attribute is ShortRunJobAttribute or SimpleJobAttribute))
                throw new InvalidOperationException($"Version benchmark {type.Name} must take its measurement job from the selected workflow mode.");
            if (type.GetMethods().Any(method => method.GetCustomAttributes(typeof(IterationSetupAttribute), inherit: true).Length != 0))
                throw new InvalidOperationException($"Iteration benchmark {type.Name} must not force InvocationCount=1 through IterationSetup.");
        }
    }

    private static void RequireEqual(long baseline, long candidate, string workload)
    {
        if (baseline != candidate) throw new InvalidOperationException($"{workload} mismatch: baseline={baseline}, candidate={candidate}.");
    }

    private static void RequireNear(double baseline, double candidate, string workload)
    {
        if (Math.Abs(baseline - candidate) > 0.0001) throw new InvalidOperationException($"{workload} mismatch: baseline={baseline}, candidate={candidate}.");
    }
}
