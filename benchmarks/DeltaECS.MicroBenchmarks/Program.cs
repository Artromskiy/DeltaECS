using BenchmarkDotNet.Running;

namespace DeltaECS.MicroBenchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "contract-smoke", StringComparison.OrdinalIgnoreCase))
        {
            MicroContractSmoke.Run();
            return;
        }

        BenchmarkSwitcher.FromTypes(MicroBenchmarkCatalog.Types).Run(args);
    }
}
