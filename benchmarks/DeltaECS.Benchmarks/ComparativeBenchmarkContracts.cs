using System.Globalization;
using System.Text;

namespace DVG.ECS.Benchmarks;

/// <summary>One source of truth for the comparable workload sizes.</summary>
public static class ComparativeBenchmarkParameters
{
    public static readonly int[] Amounts = { 100, 1_000, 10_000, 100_000 };
    public static readonly int[] ChangeWidths = { 1, 4 };
    public const int SparseMatchStride = 4;
    public const int WideComponentCount = 8;
}

public enum ComparativeEcs
{
    DeltaECS,
    Arch,
    FrifloEngineECS,
    DefaultEcs,
    LeoEcsLite
}

public enum ComparativeCapabilityMode
{
    Native,
    Emulated,
    Unsupported
}

public sealed record ComparativeCapability(
    string Workload,
    ComparativeEcs Ecs,
    bool Supported,
    ComparativeCapabilityMode Mode,
    string Note);

/// <summary>
/// Explicit capability data. Batch means a native bulk operation; an atomic
/// loop is deliberately not represented as batch support.
/// </summary>
public static class ComparativeCapabilityManifest
{
    private static readonly string[] s_iteration =
    {
        "Iteration.Dense", "Iteration.Movement", "Iteration.DistinctRows",
        "Iteration.WideNarrow", "Iteration.SparseCached", "Iteration.SparseCold"
    };

    private static readonly string[] s_atomic =
    {
        "Structural.Atomic.Create", "Structural.Atomic.Destroy",
        "Structural.Atomic.Add", "Structural.Atomic.Remove"
    };

    private static readonly string[] s_batch =
    {
        "Structural.List.CreateBatch", "Structural.List.DestroyBatch",
        "Structural.List.AddBatch", "Structural.List.RemoveBatch",
        "Structural.Query.CreateBatch", "Structural.Query.DestroyBatch",
        "Structural.Query.AddBatch", "Structural.Query.RemoveBatch"
    };

    public static IReadOnlyList<ComparativeCapability> Rows { get; } = BuildRows();

    private static IReadOnlyList<ComparativeCapability> BuildRows()
    {
        var rows = new List<ComparativeCapability>();
        foreach (var workload in s_iteration)
        {
            foreach (var ecs in Enum.GetValues<ComparativeEcs>())
            {
                rows.Add(new(workload, ecs, true, ComparativeCapabilityMode.Native, "direct public API"));
            }
        }

        foreach (var workload in s_atomic)
        {
            foreach (var ecs in Enum.GetValues<ComparativeEcs>())
            {
                rows.Add(new(workload, ecs, true, ComparativeCapabilityMode.Native, "one structural operation"));
            }
        }

        foreach (var workload in s_batch)
        {
            rows.Add(new(workload, ComparativeEcs.DeltaECS, true, ComparativeCapabilityMode.Native, "DeltaECS native Span batch API"));
            foreach (var ecs in Enum.GetValues<ComparativeEcs>())
            {
                if (ecs == ComparativeEcs.DeltaECS) continue;
                rows.Add(new(workload, ecs, false, ComparativeCapabilityMode.Unsupported, "no native bulk API with matching semantics"));
            }
        }

        return rows;
    }
}

public sealed record ComparativeReportRow(
    string Workload,
    string Params,
    ComparativeEcs Ecs,
    double Mean,
    double RatioToDelta,
    string Allocated,
    bool Supported,
    ComparativeCapabilityMode Mode);

/// <summary>Produces the stable combined schema, including explicit infinity rows.</summary>
public static class ComparativeReportBuilder
{
    public static IReadOnlyList<ComparativeReportRow> BuildManifestRows(string? parameters = null)
    {
        var result = new List<ComparativeReportRow>(ComparativeCapabilityManifest.Rows.Count);
        foreach (var capability in ComparativeCapabilityManifest.Rows)
        {
            var mean = capability.Supported ? double.NaN : double.PositiveInfinity;
            var ratio = capability.Supported ? double.NaN : double.PositiveInfinity;
            result.Add(new(capability.Workload, parameters ?? "manifest", capability.Ecs,
                mean, ratio, "N/A", capability.Supported, capability.Mode));
        }

        return result;
    }

    public static string ToMarkdown(IEnumerable<ComparativeReportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("| Workload | Params | ECS | Mean | RatioToDelta | Allocated | Supported | Mode |");
        builder.AppendLine("|---|---|---|---:|---:|---|:---:|---|");
        foreach (var row in rows)
        {
            builder.Append('|').Append(row.Workload).Append('|').Append(row.Params).Append('|')
                .Append(DisplayName(row.Ecs)).Append('|').Append(FormatNumber(row.Mean)).Append('|')
                .Append(FormatNumber(row.RatioToDelta)).Append('|').Append(row.Allocated).Append('|')
                .Append(row.Supported ? "true" : "false").Append('|').Append(row.Mode).Append('|').AppendLine();
        }

        return builder.ToString();
    }

    public static string ToCsv(IEnumerable<ComparativeReportRow> rows)
    {
        var builder = new StringBuilder("Workload,Params,ECS,Mean,RatioToDelta,Allocated,Supported,Mode\n");
        foreach (var row in rows)
        {
            builder.Append(Csv(row.Workload)).Append(',').Append(Csv(row.Params)).Append(',')
                .Append(DisplayName(row.Ecs)).Append(',').Append(FormatNumber(row.Mean)).Append(',')
                .Append(FormatNumber(row.RatioToDelta)).Append(',').Append(Csv(row.Allocated)).Append(',')
                .Append(row.Supported ? "true" : "false").Append(',').Append(row.Mode).AppendLine();
        }

        return builder.ToString();
    }

    public static void WriteManifest(string directory)
    {
        Directory.CreateDirectory(directory);
        var rows = BuildCombinedRows(directory);
        File.WriteAllText(Path.Combine(directory, "comparative-report.md"), ToMarkdown(rows));
        File.WriteAllText(Path.Combine(directory, "comparative-report.csv"), ToCsv(rows));
    }

    private static IReadOnlyList<ComparativeReportRow> BuildCombinedRows(string directory)
    {
        var measured = new List<ComparativeReportRow>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.csv", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file).StartsWith("comparative-report", StringComparison.OrdinalIgnoreCase)) continue;
            var lines = File.ReadAllLines(file);
            if (lines.Length == 0) continue;
            var header = lines[0].Split(';');
            var methodIndex = Array.IndexOf(header, "Method");
            var meanIndex = Array.IndexOf(header, "Mean");
            var allocatedIndex = Array.IndexOf(header, "Allocated");
            var ratioIndex = Array.IndexOf(header, "Ratio");
            if (methodIndex < 0 || meanIndex < 0) continue;
            var amountIndex = Array.IndexOf(header, "Amount");
            var widthIndex = Array.IndexOf(header, "ChangeWidth");
            foreach (var line in lines.Skip(1))
            {
                var fields = line.Split(';');
                if (fields.Length <= Math.Max(methodIndex, meanIndex)) continue;
                if (!TryMapMethod(fields[methodIndex], out var workload, out var ecs)) continue;
                var mean = ParseMeasurement(fields[meanIndex]);
                var ratio = ratioIndex >= 0 && ratioIndex < fields.Length ? ParseMeasurement(fields[ratioIndex]) : double.NaN;
                var parameters = amountIndex >= 0 && amountIndex < fields.Length ? $"Amount={fields[amountIndex]}" : "raw";
                if (widthIndex >= 0 && widthIndex < fields.Length) parameters += $";ChangeWidth={fields[widthIndex]}";
                measured.Add(new(workload, parameters, ecs, mean, ratio,
                    allocatedIndex >= 0 && allocatedIndex < fields.Length ? fields[allocatedIndex] : "N/A", true,
                    ComparativeCapabilityMode.Native));
            }
        }

        if (measured.Count == 0) return BuildManifestRows();
        var result = new List<ComparativeReportRow>(measured);
        foreach (var group in measured.GroupBy(row => (row.Workload, row.Params)))
        {
            var delta = group.FirstOrDefault(row => row.Ecs == ComparativeEcs.DeltaECS);
            foreach (var capability in ComparativeCapabilityManifest.Rows.Where(row => row.Workload == group.Key.Workload))
            {
                if (group.Any(row => row.Ecs == capability.Ecs)) continue;
                result.Add(new(capability.Workload, group.Key.Params, capability.Ecs,
                    double.PositiveInfinity, double.PositiveInfinity, "N/A", false, ComparativeCapabilityMode.Unsupported));
            }

            if (delta is not null && !double.IsNaN(delta.Mean) && delta.Mean != 0)
            {
                for (var i = 0; i < result.Count; i++)
                {
                    var row = result[i];
                    if (row.Workload == group.Key.Workload && row.Params == group.Key.Params && double.IsNaN(row.RatioToDelta))
                        result[i] = row with { RatioToDelta = row.Mean / delta.Mean };
                }
            }
        }

        return result;
    }

    private static bool TryMapMethod(string method, out string workload, out ComparativeEcs ecs)
    {
        workload = method.Contains("Dense", StringComparison.OrdinalIgnoreCase) ? "Iteration.Dense" :
            method.Contains("Movement", StringComparison.OrdinalIgnoreCase) ? "Iteration.Movement" :
            method.Contains("DistinctRows", StringComparison.OrdinalIgnoreCase) ? "Iteration.DistinctRows" :
            method.Contains("WideNarrow", StringComparison.OrdinalIgnoreCase) ? "Iteration.WideNarrow" :
            method.Contains("SparseCached", StringComparison.OrdinalIgnoreCase) ? "Iteration.SparseCached" :
            method.Contains("SparseCold", StringComparison.OrdinalIgnoreCase) ? "Iteration.SparseCold" :
            method.Contains("List_CreateBatch", StringComparison.OrdinalIgnoreCase) ? "Structural.List.CreateBatch" :
            method.Contains("List_DestroyBatch", StringComparison.OrdinalIgnoreCase) ? "Structural.List.DestroyBatch" :
            method.Contains("List_AddBatch", StringComparison.OrdinalIgnoreCase) ? "Structural.List.AddBatch" :
            method.Contains("List_RemoveBatch", StringComparison.OrdinalIgnoreCase) ? "Structural.List.RemoveBatch" :
            method.Contains("Query_CreateBatch", StringComparison.OrdinalIgnoreCase) ? "Structural.Query.CreateBatch" :
            method.Contains("Query_DestroyBatch", StringComparison.OrdinalIgnoreCase) ? "Structural.Query.DestroyBatch" :
            method.Contains("Query_AddBatch", StringComparison.OrdinalIgnoreCase) ? "Structural.Query.AddBatch" :
            method.Contains("Query_RemoveBatch", StringComparison.OrdinalIgnoreCase) ? "Structural.Query.RemoveBatch" :
            method.Contains("Atomic_Create", StringComparison.OrdinalIgnoreCase) ? "Structural.Atomic.Create" :
            method.Contains("Atomic_Destroy", StringComparison.OrdinalIgnoreCase) ? "Structural.Atomic.Destroy" :
            method.Contains("Atomic_Add", StringComparison.OrdinalIgnoreCase) ? "Structural.Atomic.Add" :
            method.Contains("Atomic_Remove", StringComparison.OrdinalIgnoreCase) ? "Structural.Atomic.Remove" : "";
        ecs = method.StartsWith("DeltaECS_", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.DeltaECS :
            method.StartsWith("Arch_", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.Arch :
            method.StartsWith("Friflo", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.FrifloEngineECS :
            method.StartsWith("DefaultEcs_", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.DefaultEcs :
            method.StartsWith("LeoEcsLite_", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.LeoEcsLite : default;
        return workload.Length != 0;
    }

    private static double ParseMeasurement(string value)
    {
        var number = new string(value.TakeWhile(character => char.IsDigit(character) || character is '.' or ',' or '-').ToArray());
        if (!double.TryParse(number.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return double.NaN;
        if (value.Contains("ms", StringComparison.OrdinalIgnoreCase)) return parsed * 1_000_000;
        if (value.Contains("μs", StringComparison.OrdinalIgnoreCase) || value.Contains("us", StringComparison.OrdinalIgnoreCase)) return parsed * 1_000;
        if (value.Contains("ns", StringComparison.OrdinalIgnoreCase)) return parsed;
        return parsed;
    }

    private static string FormatNumber(double value) =>
        double.IsPositiveInfinity(value) ? "∞" : double.IsNaN(value) ? "N/A" : value.ToString("G17", CultureInfo.InvariantCulture);

    private static string DisplayName(ComparativeEcs ecs) => ecs == ComparativeEcs.FrifloEngineECS ? "Friflo.Engine.ECS" : ecs.ToString();

    private static string Csv(string value) => value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}

/// <summary>Only unified classes are included here; no Legacy class is reachable from the new routes.</summary>
public static class ComparativeBenchmarkCatalog
{
    public static readonly Type[] Iteration =
    {
        typeof(ComparativeDenseIterationBenchmarks),
        typeof(ComparativeMovementBenchmarks),
        typeof(ComparativeDistinctRowsBenchmarks),
        typeof(ComparativeWideNarrowBenchmarks),
        typeof(ComparativeSparseQueryBenchmarks)
    };

    public static readonly Type[] StructuralList = { typeof(ComparativeStructuralListBenchmarks) };
    public static readonly Type[] StructuralQuery = { typeof(ComparativeStructuralQueryBenchmarks) };
    public static readonly Type[] StructuralAtomic = { typeof(ComparativeStructuralAtomicBenchmarks) };

    public static readonly Type[] FullComparison = Iteration
        .Concat(StructuralList).Concat(StructuralQuery).Concat(StructuralAtomic).ToArray();

    public static Type[] ForRoute(string route) => route.ToLowerInvariant() switch
    {
        "iteration" => Iteration,
        "structural-list" => StructuralList,
        "structural-query" => StructuralQuery,
        "structural-atomic" => StructuralAtomic,
        "full-comparison" => FullComparison,
        _ => throw new ArgumentException($"Unknown comparative route '{route}'.", nameof(route))
    };

    public static void Validate()
    {
        if (FullComparison.Any(type => type.Name.Contains("Legacy", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Legacy is not allowed in the new comparative catalog.");

        var ecsCount = Enum.GetValues<ComparativeEcs>().Length;
        foreach (var workload in new[] { "Iteration.Dense", "Iteration.Movement", "Iteration.DistinctRows", "Iteration.WideNarrow", "Iteration.SparseCached", "Iteration.SparseCold" })
        {
            if (ComparativeCapabilityManifest.Rows.Count(row => row.Workload == workload) != ecsCount)
                throw new InvalidOperationException($"Capability manifest is incomplete for {workload}.");
        }

        var unsupported = ComparativeReportBuilder.BuildManifestRows().Where(row => !row.Supported).ToArray();
        if (unsupported.Length == 0 || unsupported.Any(row => !double.IsPositiveInfinity(row.Mean) || !double.IsPositiveInfinity(row.RatioToDelta)))
            throw new InvalidOperationException("Unsupported capability rows must render as infinity.");
    }
}
