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
        foreach (var workload in new[] { "Iteration.Dense", "Iteration.Movement", "Iteration.DistinctRows", "Iteration.WideNarrow", "Iteration.SparseCached", "Iteration.SparseCold" })
        {
            Assert.That(ComparativeCapabilityManifest.Rows.Count(row => row.Workload == workload), Is.EqualTo(expected), workload);
        }
    }

    [Test]
    public void Unsupported_capabilities_are_explicit_infinity_rows()
    {
        var rows = ComparativeReportBuilder.BuildManifestRows();
        Assert.That(rows.Any(row => !row.Supported), Is.True);
        Assert.That(rows.Where(row => !row.Supported).All(row => double.IsPositiveInfinity(row.Mean) && double.IsPositiveInfinity(row.RatioToDelta)), Is.True);
        Assert.That(ComparativeReportBuilder.ToMarkdown(rows), Does.Contain("∞"));
    }

    [Test]
    public void Full_comparison_catalog_contains_no_legacy_class()
    {
        Assert.That(ComparativeBenchmarkCatalog.FullComparison, Is.Not.Empty);
        Assert.That(ComparativeBenchmarkCatalog.FullComparison.Any(type => type.Name.Contains("Legacy", StringComparison.OrdinalIgnoreCase)), Is.False);
    }
}
