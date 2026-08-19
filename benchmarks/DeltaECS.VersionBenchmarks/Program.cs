namespace DeltaECS.VersionBenchmarks;

using BenchmarkDotNet.Running;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "smoke", StringComparison.OrdinalIgnoreCase))
        {
            VersionBenchmarkSmoke.Run();
            return;
        }

        BenchmarkSwitcher.FromTypes(VersionBenchmarkCatalog.Types).Run(args);
    }
}
