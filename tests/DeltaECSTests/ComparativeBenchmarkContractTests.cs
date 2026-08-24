using Delta.ECS.Benchmarks;
using Delta.ECS;
using NUnit.Framework;
using System.Text.RegularExpressions;

namespace Delta.ECS.Tests;

[TestFixture]
public sealed class ComparativeBenchmarkContractTests
{
    [Test]
    public void Manifest_has_all_five_ecs_for_each_iteration_workload()
    {
        ComparativeBenchmarkCatalog.Validate();
        var expected = Enum.GetValues<ComparativeEcs>().Length;
        foreach (var workload in new[] { "Iteration.Dense", "Iteration.Movement2Components", "Iteration.Movement4Components", "Iteration.WideArchetypeNarrowQuery", "Iteration.SparseWorldQueryPlan", "Iteration.QueryPlanConstruction" })
        {
            Assert.That(ComparativeCapabilityManifest.Rows.Count(row => row.Workload == workload), Is.EqualTo(expected), workload);
        }
    }

    [Test]
    public void Structural_capabilities_use_explicit_fallback_levels()
    {
        var rows = ComparativeReportBuilder.BuildManifestRows();
        Assert.That(rows.Where(row => !row.Supported).All(row => double.IsPositiveInfinity(row.Mean) && double.IsPositiveInfinity(row.RatioToDelta)), Is.True);
        Assert.That(rows.Single(row => row.Workload == "Structural.Query.AddBatch" && row.Ecs == ComparativeEcs.Arch).Mode, Is.EqualTo(ComparativeCapabilityMode.Native));
        Assert.That(rows.Single(row => row.Workload == "Structural.Query.AddBatch" && row.Ecs == ComparativeEcs.DeltaECS).Mode, Is.EqualTo(ComparativeCapabilityMode.ListFallback));
        Assert.That(rows.Single(row => row.Workload == "Structural.Query.AddBatch" && row.Ecs == ComparativeEcs.LeoEcsLite).Mode, Is.EqualTo(ComparativeCapabilityMode.AtomicFallback));
        Assert.That(rows.Single(row => row.Workload == "Structural.Atomic.Add" && row.Ecs == ComparativeEcs.Arch).Mode, Is.EqualTo(ComparativeCapabilityMode.Native));
        Assert.That(rows.Single(row => row.Workload == "Structural.Atomic.Remove" && row.Ecs == ComparativeEcs.FrifloEngineECS).Mode, Is.EqualTo(ComparativeCapabilityMode.Native));
        Assert.That(rows.Single(row => row.Workload == "Structural.Atomic.Add" && row.Ecs == ComparativeEcs.DefaultEcs).Mode, Is.EqualTo(ComparativeCapabilityMode.AtomicFallback));
    }

    [Test]
    public void Full_comparison_catalog_contains_no_legacy_class()
    {
        Assert.That(ComparativeBenchmarkCatalog.FullComparison, Is.Not.Empty);
        Assert.That(ComparativeBenchmarkCatalog.FullComparison.Any(type => type.Name.Contains("Legacy", StringComparison.OrdinalIgnoreCase)), Is.False);
    }

    [Test]
    public void Amount_100_contract_smoke_executes_supported_methods()
    {
        Assert.DoesNotThrow(ComparativeBenchmarkExecutionSmoke.RunAmount100);
    }

    [Test]
    public void Benchmark_sources_use_cursor_row_access()
    {
        var benchmarkRoot = FindBenchmarkRoot();
        var ordinalAccess = new Regex(@"GetComponentRow<[^>]+>\(\s*\d+\s*\)", RegexOptions.CultureInvariant);
        var benchmarkRoots = new[] { benchmarkRoot, Path.Combine(Path.GetDirectoryName(benchmarkRoot)!, "DeltaECS.VersionBenchmarks") };
        foreach (var source in benchmarkRoots.SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                     .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                                 && !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
        {
            Assert.That(ordinalAccess.IsMatch(File.ReadAllText(source)), Is.False, source);
        }

        var publicCursorMethods = typeof(QueryChunkCursor).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        Assert.That(publicCursorMethods.Any(method => method.Name == "GetRow"), Is.False);
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

        throw new DirectoryNotFoundException("Could not locate benchmarks/DeltaECS.Benchmarks from the test output directory.");
    }

    [Test]
    public void Combined_report_rejects_na_measured_rows()
    {
        var directory = Path.Combine(Path.GetTempPath(), "deltaecs-report-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "bad.csv"), "Method;Mean;Allocated;Amount\nDeltaECS_Dense;NA;0 B;100\n");
            Assert.Throws<InvalidOperationException>(() => ComparativeReportBuilder.WriteManifest(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Combined_report_reads_comma_delimited_linux_csv()
    {
        var directory = Path.Combine(Path.GetTempPath(), "deltaecs-linux-report-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "linux.csv"),
                "Method,Mean,Allocated,Amount\n" +
                "DeltaECS_Dense,100 ns,0 B,100\n" +
                "Arch_Dense,200 ns,\"1,024 B\",100\n" +
                "FrifloEngineECS_Dense,300 ns,88 B,100\n" +
                "DefaultEcs_Dense,400 ns,0 B,100\n" +
                "LeoEcsLite_Dense,500 ns,0 B,100\n");

            ComparativeReportBuilder.WriteManifest(directory);
            var report = File.ReadAllText(Path.Combine(directory, "comparative-report.md"));
            var summary = File.ReadAllText(Path.Combine(directory, "comparative-summary.md"));
            Assert.That(report, Does.Contain("|Iteration.Dense|Amount=100|DeltaECS|100|1|"));
            Assert.That(report, Does.Contain("|Iteration.Dense|Amount=100|Arch|200|2|1,024 B|"));
            Assert.That(report, Does.Not.Contain("|Iteration.Dense|manifest|"));
            Assert.That(summary, Does.Contain("|Итерация|1/1|"));
            Assert.That(summary, Does.Contain("|Dense|1/1|Delta быстрее Arch в 2× (`Amount=100`)|"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Combined_report_formats_measurements_without_binary_float_tail()
    {
        var rows = new[]
        {
            new ComparativeReportRow("Iteration.Dense", "Amount=100", ComparativeEcs.DeltaECS, 65.379999999999995, 1, "0 B", true, ComparativeCapabilityMode.Native, "direct public API"),
            new ComparativeReportRow("Iteration.Dense", "Amount=100", ComparativeEcs.Arch, 312.62, 4.7800000000000002, "88 B", true, ComparativeCapabilityMode.Native, "direct public API")
        };

        var markdown = ComparativeReportBuilder.ToMarkdown(rows);

        Assert.That(markdown, Does.Contain("|65.38|1|"));
        Assert.That(markdown, Does.Contain("|312.62|4.78|"));
        Assert.That(markdown, Does.Not.Contain("65.379999999999995"));
        Assert.That(markdown, Does.Not.Contain("4.7800000000000002"));
    }

    [Test]
    public void Compact_summary_counts_victories_and_selects_best_native_and_fallback_rivals()
    {
        var rows = new[]
        {
            Row("Iteration.Dense", "Amount=100", ComparativeEcs.DeltaECS, 100, ComparativeCapabilityMode.Native),
            Row("Iteration.Dense", "Amount=100", ComparativeEcs.Arch, 200, ComparativeCapabilityMode.Native),
            Row("Iteration.Dense", "Amount=1000", ComparativeEcs.DeltaECS, 200, ComparativeCapabilityMode.Native),
            Row("Iteration.Dense", "Amount=1000", ComparativeEcs.Arch, 100, ComparativeCapabilityMode.Native),
            Row("Structural.Atomic.Add", "Amount=100;ChangeWidth=1", ComparativeEcs.DeltaECS, 100, ComparativeCapabilityMode.Native),
            Row("Structural.Atomic.Add", "Amount=100;ChangeWidth=1", ComparativeEcs.Arch, 80, ComparativeCapabilityMode.Native),
            Row("Structural.Atomic.Add", "Amount=100;ChangeWidth=1", ComparativeEcs.DefaultEcs, 50, ComparativeCapabilityMode.AtomicFallback)
        };

        var summary = ComparativeReportBuilder.ToSummaryMarkdown(rows);

        Assert.That(summary, Does.Contain("|Итерация|1/2|"));
        Assert.That(summary, Does.Contain("|Atomic structural|0/1|"));
        Assert.That(summary, Does.Contain("|Dense|1/2|Arch быстрее Delta в 2× (`Amount=1000`)|"));
        Assert.That(summary, Does.Contain("|Add|0/1|Arch быстрее Delta в 1.25× (`Amount=100;ChangeWidth=1`)|DefaultEcs быстрее Delta в 2× (`Amount=100;ChangeWidth=1`)|"));
    }

    private static ComparativeReportRow Row(
        string workload,
        string parameters,
        ComparativeEcs ecs,
        double mean,
        ComparativeCapabilityMode mode) =>
        new(workload, parameters, ecs, mean, double.NaN, "0 B", true, mode, "test");

}
