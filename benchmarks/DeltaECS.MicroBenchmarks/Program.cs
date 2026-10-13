using BenchmarkDotNet.Running;

namespace Delta.ECS.MicroBenchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        bool changeTrackingOnly = args.Length > 0
            && string.Equals(args[0], "change-tracking", StringComparison.OrdinalIgnoreCase);
        Type[] benchmarkTypes = changeTrackingOnly
            ? [typeof(ForEachWriteObserverMicroBenchmarks), typeof(ForEachChangeMicroBenchmarks)]
            : MicroBenchmarkCatalog.Types;
        string[] workloadArgs = changeTrackingOnly ? args[1..] : args;

        if (!changeTrackingOnly && args.Length > 0 && string.Equals(args[0], "contract-smoke", StringComparison.OrdinalIgnoreCase))
        {
            MicroContractSmoke.Run();
            return;
        }

        string[] benchmarkArgs = MicroBenchmarkConfiguration.Extract(workloadArgs, out int[]? requestedAmounts);
        foreach (Type benchmarkType in benchmarkTypes)
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
