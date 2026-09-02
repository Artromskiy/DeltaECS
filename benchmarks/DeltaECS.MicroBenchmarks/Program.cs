using BenchmarkDotNet.Running;

namespace Delta.ECS.MicroBenchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "contract-smoke", StringComparison.OrdinalIgnoreCase))
        {
            MicroContractSmoke.Run();
            return;
        }

        var benchmarkArgs = MicroBenchmarkConfiguration.Extract(args, out int[]? requestedAmounts);
        foreach (Type benchmarkType in MicroBenchmarkCatalog.Types)
        {
            int[] amounts = requestedAmounts ?? MicroBenchmarkConfiguration.DefaultAmounts(benchmarkType);
            foreach (int amount in amounts)
            {
                MicroBenchmarkConfiguration.CurrentAmount = amount;
                BenchmarkSwitcher.FromTypes([benchmarkType]).Run(benchmarkArgs);
            }
        }
    }
}
