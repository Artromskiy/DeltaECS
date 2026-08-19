using DVG.ECS.Benchmarks;
using NUnit.Framework;

namespace DVG.ECS.Tests;

[TestFixture]
public sealed class ComparativeBenchmarkContractTests
{
    [Test]
    public void Manifest_has_all_five_ecs_for_each_iteration_workload()
    {
        ComparativeBenchmarkCatalog.Validate();
        var expected = Enum.GetValues<ComparativeEcs>().Length;
        foreach (var workload in new[] { "Iteration.Dense", "Iteration.Movement2Components", "Iteration.Movement4Components", "Iteration.WideArchetypeNarrowQuery", "Iteration.SparseWorldCachedQuery", "Iteration.SparseWorldColdQuery" })
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
            Assert.That(report, Does.Contain("|Iteration.Dense|Amount=100|DeltaECS|100|1|"));
            Assert.That(report, Does.Contain("|Iteration.Dense|Amount=100|Arch|200|2|1,024 B|"));
            Assert.That(report, Does.Not.Contain("|Iteration.Dense|manifest|"));
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

}
