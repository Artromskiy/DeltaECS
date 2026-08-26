using System.Globalization;
using System.Text;

namespace Delta.ECS.Benchmarks;

/// <summary>Shared parameters for the cross-ECS iteration comparison.</summary>
public static class ComparativeBenchmarkParameters
{
    public static readonly int[] Amounts = { 100, 1_000, 10_000, 100_000 };
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

public sealed record ComparativeCapability(string Workload, ComparativeEcs Ecs, bool Supported, string Note);

/// <summary>Declares the complete iteration matrix used by the comparative suite.</summary>
public static class ComparativeCapabilityManifest
{
    private static readonly string[] s_workloads =
    {
        "Iteration.Dense",
        "Iteration.Movement2Components",
        "Iteration.Movement4Components",
        "Iteration.WideArchetypeNarrowQuery",
        "Iteration.SparseWorldQueryPlan",
        "Iteration.QueryPlanConstruction"
    };

    public static IReadOnlyList<ComparativeCapability> Rows { get; } = BuildRows();

    private static IReadOnlyList<ComparativeCapability> BuildRows()
    {
        var rows = new List<ComparativeCapability>(s_workloads.Length * Enum.GetValues<ComparativeEcs>().Length);
        foreach (var workload in s_workloads)
        {
            foreach (var ecs in Enum.GetValues<ComparativeEcs>())
            {
                rows.Add(new(workload, ecs, true, "direct public API"));
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
    string Note);

/// <summary>Builds machine-readable and GitHub-friendly iteration reports.</summary>
public static class ComparativeReportBuilder
{
    private static readonly (string Workload, string DisplayName)[] s_iterationWorkloads =
    {
        ("Iteration.Dense", "Dense"),
        ("Iteration.Movement2Components", "Movement — 2 компонента"),
        ("Iteration.Movement4Components", "Movement — 4 компонента"),
        ("Iteration.SparseWorldQueryPlan", "Sparse world — cached query"),
        ("Iteration.QueryPlanConstruction", "Query-plan construction"),
        ("Iteration.WideArchetypeNarrowQuery", "Wide archetype, narrow query")
    };

    public static IReadOnlyList<ComparativeReportRow> BuildManifestRows(string? parameters = null)
    {
        var result = new List<ComparativeReportRow>(ComparativeCapabilityManifest.Rows.Count);
        foreach (var capability in ComparativeCapabilityManifest.Rows)
        {
            result.Add(new(capability.Workload, parameters ?? "manifest", capability.Ecs,
                capability.Supported ? double.NaN : double.PositiveInfinity,
                capability.Supported ? double.NaN : double.PositiveInfinity,
                "N/A", capability.Supported, capability.Note));
        }

        return result;
    }

    public static string ToMarkdown(IEnumerable<ComparativeReportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("| Workload | Params | ECS | Mean | RatioToDelta | Allocated | Supported | Note |");
        builder.AppendLine("|---|---|---|---:|---:|---|:---:|---|");
        foreach (var row in rows)
        {
            builder.Append('|').Append(row.Workload).Append('|').Append(row.Params).Append('|')
                .Append(DisplayName(row.Ecs)).Append('|').Append(FormatNumber(row.Mean)).Append('|')
                .Append(FormatNumber(row.RatioToDelta)).Append('|').Append(row.Allocated).Append('|')
                .Append(row.Supported ? "true" : "false").Append('|').Append(Markdown(row.Note)).Append('|')
                .AppendLine();
        }

        return builder.ToString();
    }

    public static string ToSummaryMarkdown(IEnumerable<ComparativeReportRow> rows)
    {
        var scenarios = rows
            .Where(IsMeasured)
            .GroupBy(row => (row.Workload, row.Params))
            .Select(group =>
            {
                var delta = group.FirstOrDefault(row => row.Ecs == ComparativeEcs.DeltaECS);
                var rivals = group.Where(row => row.Ecs != ComparativeEcs.DeltaECS).ToArray();
                return delta is null || rivals.Length == 0
                    ? null
                    : new ComparisonScenario(group.Key.Workload, group.Key.Params, delta, rivals);
            })
            .Where(scenario => scenario is not null)
            .Cast<ComparisonScenario>()
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("## Сводка сравнительных бенчмарков DeltaECS").AppendLine();
        if (scenarios.Length == 0)
        {
            builder.AppendLine("Измеренные сравнительные результаты отсутствуют.");
            return builder.ToString();
        }

        builder.AppendLine("| Категория | Победы Delta |");
        builder.AppendLine("|---|---:|");
        builder.Append("| Итерация | ").Append(CountVictories(scenarios)).Append('/').Append(scenarios.Length).AppendLine(" |");
        builder.AppendLine().AppendLine("### Итерация").AppendLine();
        builder.AppendLine("| Тест | Победы Delta | Лучший конкурент и отношение времени |");
        builder.AppendLine("|---|---:|---|");
        foreach (var workload in s_iterationWorkloads)
        {
            var workloadScenarios = scenarios.Where(scenario => scenario.Workload == workload.Workload).ToArray();
            if (workloadScenarios.Length == 0) continue;
            builder.Append('|').Append(workload.DisplayName).Append('|')
                .Append(CountVictories(workloadScenarios)).Append('/').Append(workloadScenarios.Length).Append('|')
                .Append(FormatBestRival(FindBestRival(workloadScenarios))).Append('|').AppendLine();
        }

        builder.AppendLine().AppendLine(
            "Победа — минимальное среднее время для конкретного workload и набора параметров. " +
            "Сравнение включает только iteration-сценарии; отношение рассчитано по среднему времени.");
        return builder.ToString();
    }

    public static string ToCsv(IEnumerable<ComparativeReportRow> rows)
    {
        var builder = new StringBuilder("Workload,Params,ECS,Mean,RatioToDelta,Allocated,Supported,Note\n");
        foreach (var row in rows)
        {
            builder.Append(Csv(row.Workload)).Append(',').Append(Csv(row.Params)).Append(',')
                .Append(DisplayName(row.Ecs)).Append(',').Append(FormatNumber(row.Mean)).Append(',')
                .Append(FormatNumber(row.RatioToDelta)).Append(',').Append(Csv(row.Allocated)).Append(',')
                .Append(row.Supported ? "true" : "false").Append(',').Append(Csv(row.Note)).AppendLine();
        }

        return builder.ToString();
    }

    public static void WriteManifest(string directory)
    {
        Directory.CreateDirectory(directory);
        var rows = BuildCombinedRows(directory);
        File.WriteAllText(Path.Combine(directory, "comparative-summary.md"), ToSummaryMarkdown(rows));
        File.WriteAllText(Path.Combine(directory, "comparative-report.md"), ToMarkdown(rows));
        File.WriteAllText(Path.Combine(directory, "comparative-report.csv"), ToCsv(rows));
    }

    private static int CountVictories(IEnumerable<ComparisonScenario> scenarios) =>
        scenarios.Count(scenario => scenario.Rivals.All(rival => scenario.Delta.Mean <= rival.Mean));

    private static BestRival? FindBestRival(IEnumerable<ComparisonScenario> scenarios)
    {
        BestRival? best = null;
        foreach (var scenario in scenarios)
        {
            foreach (var rival in scenario.Rivals)
            {
                var ratio = rival.Mean / scenario.Delta.Mean;
                if (!double.IsFinite(ratio) || ratio <= 0) continue;
                if (best is null || ratio < best.RatioToDelta) best = new(rival.Ecs, scenario.Params, ratio);
            }
        }

        return best;
    }

    private static string FormatBestRival(BestRival? best)
    {
        if (best is null) return "—";
        var rival = DisplayName(best.Ecs);
        var parameters = Markdown(best.Params);
        return best.RatioToDelta < 1
            ? $"{rival} быстрее Delta в {FormatRatio(1 / best.RatioToDelta)}× (`{parameters}`)"
            : best.RatioToDelta > 1
                ? $"Delta быстрее {rival} в {FormatRatio(best.RatioToDelta)}× (`{parameters}`)"
                : $"Ничья с {rival} (`{parameters}`)";
    }

    private static bool IsMeasured(ComparativeReportRow row) => row.Supported && double.IsFinite(row.Mean) && row.Mean > 0;

    private sealed record ComparisonScenario(string Workload, string Params, ComparativeReportRow Delta, ComparativeReportRow[] Rivals);
    private sealed record BestRival(ComparativeEcs Ecs, string Params, double RatioToDelta);

    private static IReadOnlyList<ComparativeReportRow> BuildCombinedRows(string directory)
    {
        var measured = new List<ComparativeReportRow>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.csv", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file).StartsWith("comparative-report", StringComparison.OrdinalIgnoreCase)) continue;
            var lines = File.ReadAllLines(file);
            if (lines.Length == 0) continue;
            var separator = DetectSeparator(lines[0]);
            var header = ParseDelimitedLine(lines[0], separator);
            var methodIndex = Array.IndexOf(header, "Method");
            var meanIndex = Array.IndexOf(header, "Mean");
            var allocatedIndex = Array.IndexOf(header, "Allocated");
            var ratioIndex = Array.IndexOf(header, "Ratio");
            var amountIndex = Array.IndexOf(header, "Amount");
            if (methodIndex < 0 || meanIndex < 0) continue;

            foreach (var line in lines.Skip(1))
            {
                var fields = ParseDelimitedLine(line, separator);
                if (fields.Length <= Math.Max(methodIndex, meanIndex)) continue;
                if (!TryMapMethod(fields[methodIndex], out var workload, out var ecs)) continue;
                var mean = ParseMeasurement(fields[meanIndex], separator);
                if (double.IsNaN(mean)) throw new InvalidOperationException($"BenchmarkDotNet produced an invalid Mean for '{fields[methodIndex]}' in '{file}'.");
                var ratio = ratioIndex >= 0 && ratioIndex < fields.Length ? ParseMeasurement(fields[ratioIndex], separator) : double.NaN;
                var parameters = amountIndex >= 0 && amountIndex < fields.Length ? $"Amount={fields[amountIndex]}" : "raw";
                var capability = ComparativeCapabilityManifest.Rows.FirstOrDefault(row => row.Workload == workload && row.Ecs == ecs);
                measured.Add(new(workload, parameters, ecs, mean, ratio,
                    allocatedIndex >= 0 && allocatedIndex < fields.Length ? fields[allocatedIndex] : "N/A",
                    true, capability?.Note ?? "capability not declared"));
            }
        }

        if (measured.Count == 0) return BuildManifestRows();
        var result = new List<ComparativeReportRow>(measured);
        foreach (var group in measured.GroupBy(row => (row.Workload, row.Params)))
        {
            var delta = group.FirstOrDefault(row => row.Ecs == ComparativeEcs.DeltaECS);
            foreach (var capability in ComparativeCapabilityManifest.Rows.Where(row => row.Workload == group.Key.Workload))
            {
                if (!group.Any(row => row.Ecs == capability.Ecs) && !capability.Supported)
                    result.Add(new(capability.Workload, group.Key.Params, capability.Ecs, double.PositiveInfinity, double.PositiveInfinity, "N/A", false, capability.Note));
            }

            if (delta is not null && delta.Mean != 0)
            {
                for (var index = 0; index < result.Count; index++)
                {
                    var row = result[index];
                    if (row.Workload == group.Key.Workload && row.Params == group.Key.Params && double.IsNaN(row.RatioToDelta))
                        result[index] = row with { RatioToDelta = row.Mean / delta.Mean };
                }
            }
        }

        return result.OrderBy(row => row.Workload, StringComparer.Ordinal)
            .ThenBy(row => row.Params, StringComparer.Ordinal).ThenBy(row => row.Ecs).ToArray();
    }

    internal static bool TryMapMethod(string method, out string workload, out ComparativeEcs ecs)
    {
        workload = method.Contains("Dense", StringComparison.OrdinalIgnoreCase) ? "Iteration.Dense" :
            method.Contains("Movement2Components", StringComparison.OrdinalIgnoreCase) ? "Iteration.Movement2Components" :
            method.Contains("Movement4Components", StringComparison.OrdinalIgnoreCase) ? "Iteration.Movement4Components" :
            method.Contains("WideArchetypeNarrowQuery", StringComparison.OrdinalIgnoreCase) ? "Iteration.WideArchetypeNarrowQuery" :
            method.Contains("SparseWorldQueryPlan", StringComparison.OrdinalIgnoreCase) ? "Iteration.SparseWorldQueryPlan" :
            method.Contains("QueryPlanConstruction", StringComparison.OrdinalIgnoreCase) ? "Iteration.QueryPlanConstruction" : "";
        var mapped = true;
        ecs = method.StartsWith("DeltaECS_", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.DeltaECS :
            method.StartsWith("Arch_", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.Arch :
            method.StartsWith("Friflo", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.FrifloEngineECS :
            method.StartsWith("DefaultEcs_", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.DefaultEcs :
            method.StartsWith("LeoEcsLite_", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.LeoEcsLite : UnknownEcs();
        return workload.Length != 0 && mapped;

        ComparativeEcs UnknownEcs() { mapped = false; return default; }
    }

    private static double ParseMeasurement(string value, char csvSeparator)
    {
        var number = new string(value.TrimStart().TakeWhile(character => char.IsDigit(character) || character is '.' or ',' or '-' or '+' or 'e' or 'E' || char.IsWhiteSpace(character)).ToArray());
        number = number.Replace(" ", string.Empty).Replace("\u00a0", string.Empty).Replace("\u202f", string.Empty);
        if (csvSeparator == ';' && number.Contains(',') && !number.Contains('.')) number = number.Replace(',', '.');
        if (!double.TryParse(number, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)) return double.NaN;
        if (value.Contains("ms", StringComparison.OrdinalIgnoreCase)) return parsed * 1_000_000;
        if (value.Contains("μs", StringComparison.OrdinalIgnoreCase) || value.Contains("us", StringComparison.OrdinalIgnoreCase)) return parsed * 1_000;
        if (value.Contains("ns", StringComparison.OrdinalIgnoreCase)) return parsed;
        return parsed;
    }

    private static char DetectSeparator(string header) => header.Contains(';') ? ';' : header.Contains(',') ? ',' : throw new InvalidOperationException("Benchmark CSV header has no supported delimiter.");

    private static string[] ParseDelimitedLine(string line, char separator)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"') { field.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == separator && !quoted) { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(character);
        }

        if (quoted) throw new InvalidOperationException("Benchmark CSV contains an unterminated quoted field.");
        fields.Add(field.ToString());
        return fields.ToArray();
    }

    private static string FormatNumber(double value) => double.IsPositiveInfinity(value) ? "∞" : double.IsNaN(value) ? "N/A" : value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string FormatRatio(double ratio) => ratio.ToString("0.##", CultureInfo.InvariantCulture);
    private static string DisplayName(ComparativeEcs ecs) => ecs == ComparativeEcs.FrifloEngineECS ? "Friflo.Engine.ECS" : ecs.ToString();
    private static string Csv(string value) => value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    private static string Markdown(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}

/// <summary>Only unified iteration benchmarks are exposed by this project.</summary>
public static class ComparativeBenchmarkCatalog
{
    public static readonly Type[] Iteration =
    {
        typeof(ComparativeDenseIterationBenchmarks),
        typeof(ComparativeMovement2ComponentsBenchmarks),
        typeof(ComparativeMovement4ComponentsBenchmarks),
        typeof(ComparativeWideArchetypeNarrowQueryBenchmarks),
        typeof(ComparativeSparseQueryBenchmarks)
    };

    public static Type[] ForRoute(string route) => route.ToLowerInvariant() switch
    {
        "iteration" => Iteration,
        _ => throw new ArgumentException($"Unknown comparative route '{route}'. Only 'iteration' is supported.", nameof(route))
    };

    public static void Validate()
    {
        if (Iteration.Any(HasEmbeddedMeasurementJob))
            throw new InvalidOperationException("Unified iteration benchmarks must take their measurement job from the selected workflow mode.");

        var ecsCount = Enum.GetValues<ComparativeEcs>().Length;
        foreach (var workload in ComparativeCapabilityManifest.Rows.Select(row => row.Workload).Distinct(StringComparer.Ordinal))
        {
            if (ComparativeCapabilityManifest.Rows.Count(row => row.Workload == workload) != ecsCount)
                throw new InvalidOperationException($"Capability manifest is incomplete for {workload}.");
        }

        var benchmarkAttribute = typeof(BenchmarkDotNet.Attributes.BenchmarkAttribute);
        var measuredCapabilities = new HashSet<(string Workload, ComparativeEcs Ecs)>();
        foreach (var type in Iteration)
        {
            var methods = type.GetMethods().Where(method => method.GetCustomAttributes(benchmarkAttribute, true).Length != 0).ToArray();
            if (methods.Length == 0) throw new InvalidOperationException($"Comparative class {type.Name} has no benchmark methods.");
            var baselines = methods.Where(method => ((BenchmarkDotNet.Attributes.BenchmarkAttribute)method.GetCustomAttributes(benchmarkAttribute, true).Single()).Baseline).ToArray();
            if (baselines.Length == 0 || baselines.Any(method => !method.Name.StartsWith("DeltaECS_", StringComparison.Ordinal)))
                throw new InvalidOperationException($"Comparative class {type.Name} must expose a DeltaECS baseline.");
            foreach (var method in methods)
            {
                if (!ComparativeReportBuilder.TryMapMethod(method.Name, out var workload, out var ecs))
                    throw new InvalidOperationException($"Comparative method {type.Name}.{method.Name} is not mapped to iteration and ECS.");
                if (!measuredCapabilities.Add((workload, ecs))) throw new InvalidOperationException($"Duplicate comparative method for {workload} and {ecs}.");
            }
        }

        foreach (var capability in ComparativeCapabilityManifest.Rows.Where(row => row.Supported))
        {
            if (!measuredCapabilities.Contains((capability.Workload, capability.Ecs)))
                throw new InvalidOperationException($"Missing benchmark method for supported capability {capability.Workload} and {capability.Ecs}.");
        }
    }

    private static bool HasEmbeddedMeasurementJob(Type type) => type.GetCustomAttributes(inherit: true).Any(attribute => attribute is BenchmarkDotNet.Attributes.ShortRunJobAttribute or BenchmarkDotNet.Attributes.SimpleJobAttribute);
}
