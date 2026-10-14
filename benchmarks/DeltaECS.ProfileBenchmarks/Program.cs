extern alias baselineAdapter;
extern alias candidateAdapter;

namespace DeltaECS.ProfileBenchmarks;

using System.Globalization;
using System.Reflection;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

public static class Program
{
    public static void Main(string[] args)
    {
        string[] benchmarkArgs = ProfileOptions.Parse(args);
        if (ProfileOptions.Memory || ProfileOptions.Allocations)
        {
            using var baseline = new baselineAdapter::DeltaECS.ProfileAdapter.ProfileScenario(ProfileOptions.Amount, ProfileOptions.RegisteredComponents, ProfileOptions.Archetypes);
            using var candidate = new candidateAdapter::DeltaECS.ProfileAdapter.ProfileScenario(ProfileOptions.Amount, ProfileOptions.RegisteredComponents, ProfileOptions.Archetypes);
            Console.WriteLine("Baseline: " + (ProfileOptions.Memory ? baseline.DescribeStorage() : baseline.DescribeAllocations()));
            Console.WriteLine("Candidate: " + (ProfileOptions.Memory ? candidate.DescribeStorage() : candidate.DescribeAllocations()));
            return;
        }
        if (ProfileOptions.Smoke)
        {
            ProfileSmoke.Run();
            return;
        }
        var baselineRoot = ResolvePath("BASELINE_ROOT", "DeltaECS.BaselineRoot");
        var candidateRoot = ResolvePath("CANDIDATE_ROOT", "DeltaECS.CandidateRoot");
        var framework = ResolveValue("PROFILE_RUNTIME_FRAMEWORK", "DeltaECS.ProfileRuntimeFramework");
        Environment.SetEnvironmentVariable("DELTAECS_PROFILE_AMOUNT", ProfileOptions.Amount.ToString(CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("DELTAECS_PROFILE_REGISTERED", ProfileOptions.RegisteredComponents.ToString(CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("DELTAECS_PROFILE_ARCHETYPES", ProfileOptions.Archetypes.ToString(CultureInfo.InvariantCulture));
        var config = ManualConfig.Create(DefaultConfig.Instance);
        config.AddJob(Job.Default.WithArguments(
        [
            new MsBuildArgument($"/p:BaselineRoot={baselineRoot}"),
            new MsBuildArgument($"/p:CandidateRoot={candidateRoot}"),
            new MsBuildArgument($"/p:ProfileRuntimeFramework={framework}"),
            new MsBuildArgument("/m:1")
        ]).AsMutator());
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(benchmarkArgs, config);
    }

    private static string ResolveValue(string environmentName, string metadataName)
    {
        var value = Environment.GetEnvironmentVariable(environmentName);
        if (string.IsNullOrWhiteSpace(value))
            value = typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => string.Equals(attribute.Key, metadataName, StringComparison.Ordinal))?.Value;
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Set {environmentName} or build with the matching MSBuild property.")
            : value;
    }

    private static string ResolvePath(string environmentName, string metadataName)
        => Path.GetFullPath(ResolveValue(environmentName, metadataName));
}

internal static class ProfileOptions
{
    internal static int Amount { get; private set; } = ReadEnvironment("DELTAECS_PROFILE_AMOUNT", 10_000);
    internal static int RegisteredComponents { get; private set; } = ReadEnvironment("DELTAECS_PROFILE_REGISTERED", 256);
    internal static int Archetypes { get; private set; } = ReadEnvironment("DELTAECS_PROFILE_ARCHETYPES", 8);
    internal static bool Smoke { get; private set; }
    internal static bool Memory { get; private set; }
    internal static bool Allocations { get; private set; }

    internal static string[] Parse(string[] args)
    {
        var rest = new List<string>();
        for (int index = 0; index < args.Length; index++)
        {
            string arg = args[index];
            if (arg.Equals("allocations", StringComparison.OrdinalIgnoreCase)) { Allocations = true; continue; }
            if (arg.Equals("memory", StringComparison.OrdinalIgnoreCase)) { Memory = true; continue; }
            if (arg.Equals("smoke", StringComparison.OrdinalIgnoreCase)) { Smoke = true; continue; }
            if (TryOption(arg, "--amount", args, ref index, out string? value)) { Amount = ParsePositive(value!, "amount"); continue; }
            if (TryOption(arg, "--registered-components", args, ref index, out value)) { RegisteredComponents = ParseNonnegative(value!, "registered-components"); continue; }
            if (TryOption(arg, "--archetypes", args, ref index, out value)) { Archetypes = ParsePositive(value!, "archetypes"); continue; }
            rest.Add(arg);
        }
        if (Archetypes > 256) throw new ArgumentOutOfRangeException("archetypes", "Maximum supported archetypes is 256.");
        return rest.ToArray();
    }

    private static bool TryOption(string arg, string name, string[] args, ref int index, out string? value)
    {
        if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)) { value = arg[(name.Length + 1)..]; return true; }
        if (arg.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            if (++index >= args.Length) throw new ArgumentException($"Missing value for {name}.");
            value = args[index]; return true;
        }
        value = null; return false;
    }

    private static int ParsePositive(string value, string name) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? parsed : throw new ArgumentException($"{name} must be a positive integer: {value}");

    private static int ParseNonnegative(string value, string name) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) && parsed >= 0
            ? parsed : throw new ArgumentException($"{name} must be a nonnegative integer: {value}");

    private static int ReadEnvironment(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
            ? parsed : fallback;
}

internal static class ProfileSmoke
{
    internal static void Run()
    {
        using var baseline = new baselineAdapter::DeltaECS.ProfileAdapter.ProfileScenario(20, 16, 4);
        using var candidate = new candidateAdapter::DeltaECS.ProfileAdapter.ProfileScenario(20, 16, 4);
        using var baselineHas = new baselineAdapter::DeltaECS.ProfileAdapter.HasScenario(21);
        using var candidateHas = new candidateAdapter::DeltaECS.ProfileAdapter.HasScenario(21);
        using var baselineQueryPlans = new baselineAdapter::DeltaECS.ProfileAdapter.QueryPlanScenario();
        using var candidateQueryPlans = new candidateAdapter::DeltaECS.ProfileAdapter.QueryPlanScenario();
        using var baselineBatchStructural = new baselineAdapter::DeltaECS.ProfileAdapter.BatchStructuralScenario(64);
        using var candidateBatchStructural = new candidateAdapter::DeltaECS.ProfileAdapter.BatchStructuralScenario(64);
        const long expectedLookup = 210 + 20 + 10 + 20;
        Require(baseline.LookupShuffled() == expectedLookup && candidate.LookupShuffled() == expectedLookup, "shuffled high-ID lookup checksum");
        Require(baseline.HasSequential() == 20 && candidate.HasSequential() == 20, "sequential component presence");
        Require(baseline.HasShuffled() == 20 && candidate.HasShuffled() == 20, "shuffled component presence");
        Require(baselineHas.HasDataByIdSequential() == 11 && candidateHas.HasDataByIdSequential() == 11, "mixed data presence by ID");
        Require(baselineHas.HasTagByIdSequential() == 11 && candidateHas.HasTagByIdSequential() == 11, "mixed tag presence by ID");
        Require(baselineHas.HasDataGenericSequential() == 11 && candidateHas.HasDataGenericSequential() == 11, "mixed data presence by type");
        Require(baseline.GetSequential() == 210 && candidate.GetSequential() == 210, "sequential Get checksum");
        Require(baseline.GetShuffled() == 210 && candidate.GetShuffled() == 210, "shuffled Get checksum");
        Require(baseline.GetRefSequential() == 210 && candidate.GetRefSequential() == 210, "sequential GetRef checksum");
        Require(baseline.GetRefShuffled() == 210 && candidate.GetRefShuffled() == 210, "shuffled GetRef checksum");
        Require(baseline.DenseEntityWalk() == 190 && candidate.DenseEntityWalk() == 190, "dense traversal checksum");
        Require(baseline.StructuralToggle() == 1 && baseline.StructuralToggle() == -2, "baseline add/remove state");
        Require(candidate.StructuralToggle() == 1 && candidate.StructuralToggle() == -2, "candidate add/remove state");
        Require(baseline.CreateDestroy() == 1 && candidate.CreateDestroy() == 1, "create/destroy state");
        Require(baseline.CreateDestroy() == 2 && candidate.CreateDestroy() == 2, "repeated create/destroy state");
        Require(baselineQueryPlans.CreateQueryBatch() == 512, "baseline query plan creation");
        Require(candidateQueryPlans.CreateQueryBatch() == 512, "candidate query plan creation");
        baselineBatchStructural.PrepareForCreate();
        candidateBatchStructural.PrepareForCreate();
        Require(baselineBatchStructural.CreateBatch() == 64, "baseline batch creation");
        Require(candidateBatchStructural.CreateBatch() == 64, "candidate batch creation");
        baselineBatchStructural.PrepareForDestroy();
        candidateBatchStructural.PrepareForDestroy();
        Require(baselineBatchStructural.DestroyRandomHalf() == 32, "baseline partial batch destroy");
        Require(candidateBatchStructural.DestroyRandomHalf() == 32, "candidate partial batch destroy");
        baselineBatchStructural.PrepareForDestroy();
        candidateBatchStructural.PrepareForDestroy();
        Require(baselineBatchStructural.DestroyRandomFullBatch() == 64, "baseline full batch destroy");
        Require(candidateBatchStructural.DestroyRandomFullBatch() == 64, "candidate full batch destroy");
        Require(baselineBatchStructural.AliveEntityCount == 0 && candidateBatchStructural.AliveEntityCount == 0,
            "full batch destroy state");
        long expectedSort = 0;
        foreach (int index in Enumerable.Range(0, 20)
                     .OrderBy(static index => (index * 73) % 19)
                     .ThenBy(static index => index))
            expectedSort = unchecked(expectedSort * 31 + index);
        Require(baseline.StableSortTwoKeys() == expectedSort && candidate.StableSortTwoKeys() == expectedSort, "two-key stable sort checksum");
        Console.WriteLine("Profile benchmark smoke passed: query plans, Has presence, component access, batch create/destroy, add/remove, single create/destroy, sorting, dense traversal.");
    }

    private static void Require(bool condition, string workload)
    {
        if (!condition) throw new InvalidOperationException($"Profile smoke failed: {workload}.");
    }
}
