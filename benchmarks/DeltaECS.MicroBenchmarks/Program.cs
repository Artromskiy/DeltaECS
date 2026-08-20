using BenchmarkDotNet.Running;

namespace Delta.ECS.MicroBenchmarks;

internal static class Program
{
    private static readonly Type[] s_benchmarks =
    [
        typeof(EntityRecordResolveMicroBenchmarks),
        typeof(CreateKnownArchetypeMicroBenchmarks),
        typeof(CachedBindingIterationMicroBenchmarks),
        typeof(AtomicStructuralMicroBenchmarks),
        typeof(ListBatchMicroBenchmarks),
        typeof(QueryBatchMicroBenchmarks),
        typeof(StorageAndOverlayMicroBenchmarks)
    ];

    public static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "contract-smoke", StringComparison.OrdinalIgnoreCase))
        {
            MicroContractSmoke.Run();
            return;
        }

        BenchmarkSwitcher.FromTypes(s_benchmarks).Run(args);
    }
}
