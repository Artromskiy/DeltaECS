using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace DeltaECS.EntityResolutionBenchmarks;

internal static class Program
{
    public static int Amount { get; set; } = 100_000;

    public static void Main(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--amount" && ++i < args.Length && int.TryParse(args[i], out int amount))
                Amount = amount;
        }

        var benchmarkArgs = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--amount")
            {
                i++;
                continue;
            }

            benchmarkArgs.Add(args[i]);
        }

        string baselineRoot = Environment.GetEnvironmentVariable("DELTAECS_BASELINE_ROOT")
            ?? throw new InvalidOperationException("Set DELTAECS_BASELINE_ROOT to a checkout of the baseline revision.");
        baselineRoot = Path.GetFullPath(baselineRoot);
        string candidateRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var config = ManualConfig.Create(DefaultConfig.Instance);
        config.AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance).WithArguments(
        [
            new MsBuildArgument($"/p:BaselineRoot={baselineRoot}"),
            new MsBuildArgument($"/p:CandidateRoot={candidateRoot}")
        ]).AsMutator());
        BenchmarkSwitcher.FromTypes([typeof(EntityResolutionBenchmarks)]).Run(benchmarkArgs.ToArray(), config);
    }
}
