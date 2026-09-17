using System.Text.RegularExpressions;
using Delta.ECS;
using Delta.ECS.Benchmarks;
using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed partial class ComparativeBenchmarkContractTests
{
    [Test]
    public void ManifestHasAllFiveEcsForEachIterationWorkload()
    {
        ComparativeBenchmarkCatalog.Validate();
        var expected = Enum.GetValues<ComparativeEcs>().Length;
        foreach (var workload in new[] { "Iteration.Dense", "Iteration.Movement2Components", "Iteration.Movement4Components", "Iteration.WideArchetypeNarrowQuery", "Iteration.SparseWorldQueryPlan", "Iteration.QueryPlanConstruction" })
        {
            Assert.That(ComparativeCapabilityManifest.Rows.Count(row => row.Workload == workload), Is.EqualTo(expected), workload);
        }
    }

    [Test]
    public void IterationCatalogContainsNoLegacyClass()
    {
        Assert.That(ComparativeBenchmarkCatalog.Iteration, Is.Not.Empty);
        Assert.That(ComparativeBenchmarkCatalog.Iteration.Any(type => type.Name.Contains("Legacy", StringComparison.OrdinalIgnoreCase)), Is.False);
    }

    [Test]
    public void Amount100ContractSmokeExecutesSupportedMethods()
    {
        Assert.DoesNotThrow(ComparativeBenchmarkExecutionSmoke.RunAmount100);
    }

    [Test]
    public void BenchmarkSourcesUseGeneratedAccess()
    {
        var benchmarkRoot = FindBenchmarkRoot();
        Regex ordinalAccess = OrdinalAccess();
        var benchmarkRoots = new[] { benchmarkRoot, Path.Combine(Path.GetDirectoryName(benchmarkRoot)!, "DeltaECS.VersionBenchmarks") };
        foreach (var source in benchmarkRoots.SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                     .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                                 && !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
        {
            Assert.That(ordinalAccess.IsMatch(File.ReadAllText(source)), Is.False, source);
        }

        Assert.That(typeof(World).Assembly.GetType("Delta.ECS.QueryChunkCursor"), Is.Null);
    }

    private static string FindBenchmarkRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "benchmarks", "DeltaECS.Benchmarks");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not locate benchmarks/Delta.ECS.Benchmarks from the test output directory.");
    }

    [GeneratedRegex(@"GetComponentRow<[^>]+>\(\s*\d+\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex OrdinalAccess();

    [Test]
    public void CombinedReportRejectsNaMeasuredRows()
    {
        var directory = Path.Combine(Path.GetTempPath(), "deltaecs-report-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "bad.csv"), "Method;Mean;Allocated;Amount\nDeltaECSDense;NA;0 B;100\n");
            Assert.Throws<InvalidOperationException>(() => ComparativeReportBuilder.WriteManifest(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void CombinedReportReadsCommaDelimitedLinuxCsv()
    {
        var directory = Path.Combine(Path.GetTempPath(), "deltaecs-linux-report-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "linux.csv"),
                "Method,Mean,Allocated,Amount\n" +
                "DeltaECSDense,100 ns,0 B,100\n" +
                "ArchDense,200 ns,\"1,024 B\",100\n" +
                "FrifloEngineECSDense,300 ns,88 B,100\n" +
                "DefaultEcsDense,400 ns,0 B,100\n" +
                "LeoEcsLiteDense,500 ns,0 B,100\n");

            ComparativeReportBuilder.WriteManifest(directory);
            var report = File.ReadAllText(Path.Combine(directory, "comparative-report.md"));
            var summary = File.ReadAllText(Path.Combine(directory, "comparative-summary.md"));
            Assert.That(report, Does.Contain("|Iteration.Dense|Amount=100|DeltaECS|100|1|"));
            Assert.That(report, Does.Contain("|Iteration.Dense|Amount=100|Arch|200|2|1,024 B|"));
            Assert.That(report, Does.Not.Contain("|Iteration.Dense|manifest|"));
            Assert.That(summary, Does.Contain("| Итерация | 1/1 |"));
            Assert.That(summary, Does.Contain("|Dense|1/1|Delta быстрее Arch в 2× (`Amount=100`)|"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void CombinedReportFormatsMeasurementsWithoutBinaryFloatTail()
    {
        var rows = new[]
        {
            new ComparativeReportRow("Iteration.Dense", "Amount=100", ComparativeEcs.DeltaECS, 65.379999999999995, 1, "0 B", true, "direct public API"),
            new ComparativeReportRow("Iteration.Dense", "Amount=100", ComparativeEcs.Arch, 312.62, 4.7800000000000002, "88 B", true, "direct public API")
        };

        var markdown = ComparativeReportBuilder.ToMarkdown(rows);

        Assert.That(markdown, Does.Contain("|65.38|1|"));
        Assert.That(markdown, Does.Contain("|312.62|4.78|"));
        Assert.That(markdown, Does.Not.Contain("65.379999999999995"));
        Assert.That(markdown, Does.Not.Contain("4.7800000000000002"));
    }

    [Test]
    public void CompactSummaryCountsVictoriesAndSelectsBestRival()
    {
        var rows = new[]
        {
            Row("Iteration.Dense", "Amount=100", ComparativeEcs.DeltaECS, 100),
            Row("Iteration.Dense", "Amount=100", ComparativeEcs.Arch, 200),
            Row("Iteration.Dense", "Amount=1000", ComparativeEcs.DeltaECS, 200),
            Row("Iteration.Dense", "Amount=1000", ComparativeEcs.Arch, 100)
        };

        var summary = ComparativeReportBuilder.ToSummaryMarkdown(rows);

        Assert.That(summary, Does.Contain("| Итерация | 1/2 |"));
        Assert.That(summary, Does.Contain("|Dense|1/2|Arch быстрее Delta в 2× (`Amount=1000`)|"));
    }

    private static ComparativeReportRow Row(
        string workload,
        string parameters,
        ComparativeEcs ecs,
        double mean) =>
        new(workload, parameters, ecs, mean, double.NaN, "0 B", true, "test");

}
