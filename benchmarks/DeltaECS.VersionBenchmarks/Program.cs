namespace DeltaECS.VersionBenchmarks;

using System.Reflection;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
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

        var baselineRoot = ResolveVersionRoot("BASELINE_ROOT", "DeltaECS.BaselineRoot");
        var candidateRoot = ResolveVersionRoot("CANDIDATE_ROOT", "DeltaECS.CandidateRoot");
        var config = ManualConfig.Create(DefaultConfig.Instance);
        config.AddJob(Job.Default
            .WithArguments(
            [
                new MsBuildArgument($"/p:BaselineRoot={baselineRoot}"),
                new MsBuildArgument($"/p:CandidateRoot={candidateRoot}")
            ])
            .AsMutator());

        BenchmarkSwitcher.FromTypes(VersionBenchmarkCatalog.Types).Run(args, config);
    }

    private static string ResolveVersionRoot(string environmentName, string metadataName)
    {
        var environmentValue = Environment.GetEnvironmentVariable(environmentName);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return Path.GetFullPath(environmentValue);
        }

        var metadataValue = typeof(Program).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, metadataName, StringComparison.Ordinal))
            ?.Value;
        if (!string.IsNullOrWhiteSpace(metadataValue))
        {
            return Path.GetFullPath(metadataValue);
        }

        throw new InvalidOperationException(
            $"Version root is unavailable. Set {environmentName} or rebuild the version suite with its matching MSBuild root property.");
    }
}
