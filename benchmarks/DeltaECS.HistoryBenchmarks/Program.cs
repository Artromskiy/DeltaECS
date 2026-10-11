namespace Delta.ECS.HistoryBenchmarks;

using System.Globalization;
using BenchmarkDotNet.Running;

internal static class Program
{
    private static void Main(string[] args)
    {
        var benchmarkArguments = new List<string>();
        int entityCount = HistoryBenchmarkOptions.EntityCount;
        for (int index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--entities", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Length || !int.TryParse(args[index], out entityCount) || entityCount <= 0)
                {
                    throw new ArgumentException("--entities requires a positive integer.", nameof(args));
                }

                continue;
            }

            benchmarkArguments.Add(args[index]);
        }

        Environment.SetEnvironmentVariable("DELTA_ECS_HISTORY_ENTITY_COUNT", entityCount.ToString(CultureInfo.InvariantCulture));
        HistoryBenchmarkOptions.EntityCount = entityCount;
        int componentMembershipCount = ValidateEquivalentResults(entityCount);
        Console.WriteLine($"History workload: {entityCount:N0} entities, {componentMembershipCount:N0} entity-component pairs, " +
            $"{(double)componentMembershipCount / entityCount:F2} component visits per entity on average.");
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(benchmarkArguments.ToArray());
    }

    private static int ValidateEquivalentResults(int entityCount)
    {
        using var perComponent = new WorldHistory(entityCount);
        using var archetype = new WorldHistory(entityCount);
        perComponent.RestoreByComponent();
        archetype.RestoreByArchetype();

        ulong expected = perComponent.ExpectedChecksum();
        if (perComponent.Checksum() != expected || archetype.Checksum() != expected)
        {
            throw new InvalidOperationException("A history restore path produced incorrect component values.");
        }

        return archetype.ComponentMembershipCount;
    }
}
