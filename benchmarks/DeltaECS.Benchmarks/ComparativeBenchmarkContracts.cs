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
    QueryFallback,
    ListFallback,
    AtomicFallback,
    Unsupported
}

public sealed record ComparativeCapability(
    string Workload,
    ComparativeEcs Ecs,
    bool Supported,
    ComparativeCapabilityMode Mode,
    string Note);

/// <summary>
/// Explicit capability data. Every operation uses the highest common semantic
/// level exposed by an ECS and falls back to a lower level when necessary.
/// </summary>
public static class ComparativeCapabilityManifest
{
    private static readonly string[] s_iteration =
    {
        "Iteration.Dense", "Iteration.Movement2Components", "Iteration.Movement4Components",
        "Iteration.WideArchetypeNarrowQuery", "Iteration.SparseWorldCachedQuery", "Iteration.SparseWorldColdQuery"
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
                var multiComponent = workload is "Structural.Atomic.Add" or "Structural.Atomic.Remove";
                var hasNativeMultiComponentTransition = ecs is ComparativeEcs.DeltaECS or ComparativeEcs.Arch or ComparativeEcs.FrifloEngineECS;
                var mode = multiComponent && !hasNativeMultiComponentTransition
                    ? ComparativeCapabilityMode.AtomicFallback
                    : ComparativeCapabilityMode.Native;
                var note = mode == ComparativeCapabilityMode.Native
                    ? "one structural operation"
                    : "one atomic call per component";
                rows.Add(new(workload, ecs, true, mode, note));
            }
        }

        foreach (var workload in s_batch)
        {
            foreach (var ecs in Enum.GetValues<ComparativeEcs>())
            {
                rows.Add(BuildBatchCapability(workload, ecs));
            }
        }

        return rows;
    }

    private static ComparativeCapability BuildBatchCapability(string workload, ComparativeEcs ecs)
    {
        if (ecs == ComparativeEcs.DeltaECS)
        {
            var queryTransition = workload is "Structural.Query.DestroyBatch" or "Structural.Query.AddBatch" or "Structural.Query.RemoveBatch";
            return queryTransition
                ? new(workload, ecs, true, ComparativeCapabilityMode.ListFallback, "query selection followed by native Span batch")
                : new(workload, ecs, true, ComparativeCapabilityMode.Native, "native Span batch API");
        }

        if (ecs == ComparativeEcs.Arch)
        {
            return workload switch
            {
                "Structural.Query.DestroyBatch" => new(workload, ecs, true, ComparativeCapabilityMode.Native, "native query destroy"),
                "Structural.Query.AddBatch" or "Structural.Query.RemoveBatch" => new(workload, ecs, true, ComparativeCapabilityMode.Native, "native multi-component query operation"),
                _ => new(workload, ecs, true, ComparativeCapabilityMode.AtomicFallback, "atomic loop fallback")
            };
        }

        if (ecs == ComparativeEcs.FrifloEngineECS)
        {
            return workload switch
            {
                "Structural.List.AddBatch" or "Structural.List.RemoveBatch" or
                "Structural.Query.AddBatch" or "Structural.Query.RemoveBatch" =>
                    new(workload, ecs, true, ComparativeCapabilityMode.Native, "native EntityBatch bulk operation"),
                _ => new(workload, ecs, true, ComparativeCapabilityMode.AtomicFallback, "atomic loop fallback")
            };
        }

        return new(workload, ecs, true, ComparativeCapabilityMode.AtomicFallback, "query/list selection followed by atomic loop");
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
    ComparativeCapabilityMode Mode,
    string Note);

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
                mean, ratio, "N/A", capability.Supported, capability.Mode, capability.Note));
        }

        return result;
    }

    public static string ToMarkdown(IEnumerable<ComparativeReportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("| Workload | Params | ECS | Mean | RatioToDelta | Allocated | Supported | Mode | Note |");
        builder.AppendLine("|---|---|---|---:|---:|---|:---:|---|---|");
        foreach (var row in rows)
        {
            builder.Append('|').Append(row.Workload).Append('|').Append(row.Params).Append('|')
                .Append(DisplayName(row.Ecs)).Append('|').Append(FormatNumber(row.Mean)).Append('|')
                .Append(FormatNumber(row.RatioToDelta)).Append('|').Append(row.Allocated).Append('|')
                .Append(row.Supported ? "true" : "false").Append('|').Append(row.Mode).Append('|')
                .Append(Markdown(row.Note)).Append('|').AppendLine();
        }

        return builder.ToString();
    }

    public static string ToCsv(IEnumerable<ComparativeReportRow> rows)
    {
        var builder = new StringBuilder("Workload,Params,ECS,Mean,RatioToDelta,Allocated,Supported,Mode,Note\n");
        foreach (var row in rows)
        {
            builder.Append(Csv(row.Workload)).Append(',').Append(Csv(row.Params)).Append(',')
                .Append(DisplayName(row.Ecs)).Append(',').Append(FormatNumber(row.Mean)).Append(',')
                .Append(FormatNumber(row.RatioToDelta)).Append(',').Append(Csv(row.Allocated)).Append(',')
                .Append(row.Supported ? "true" : "false").Append(',').Append(row.Mode).Append(',')
                .Append(Csv(row.Note)).AppendLine();
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
            var separator = DetectSeparator(lines[0]);
            var header = ParseDelimitedLine(lines[0], separator);
            var methodIndex = Array.IndexOf(header, "Method");
            var meanIndex = Array.IndexOf(header, "Mean");
            var allocatedIndex = Array.IndexOf(header, "Allocated");
            var ratioIndex = Array.IndexOf(header, "Ratio");
            if (methodIndex < 0 || meanIndex < 0) continue;
            var amountIndex = Array.IndexOf(header, "Amount");
            var widthIndex = Array.IndexOf(header, "ChangeWidth");
            foreach (var line in lines.Skip(1))
            {
                var fields = ParseDelimitedLine(line, separator);
                if (fields.Length <= Math.Max(methodIndex, meanIndex)) continue;
                if (!TryMapMethod(fields[methodIndex], out var workload, out var ecs)) continue;
                var mean = ParseMeasurement(fields[meanIndex], separator);
                if (double.IsNaN(mean))
                    throw new InvalidOperationException($"BenchmarkDotNet produced an invalid Mean for '{fields[methodIndex]}' in '{file}'. NA rows are not reportable results.");
                var ratio = ratioIndex >= 0 && ratioIndex < fields.Length ? ParseMeasurement(fields[ratioIndex], separator) : double.NaN;
                var parameters = amountIndex >= 0 && amountIndex < fields.Length ? $"Amount={fields[amountIndex]}" : "raw";
                if (widthIndex >= 0 && widthIndex < fields.Length) parameters += $";ChangeWidth={fields[widthIndex]}";
                var capability = ComparativeCapabilityManifest.Rows.FirstOrDefault(row => row.Workload == workload && row.Ecs == ecs);
                measured.Add(new(workload, parameters, ecs, mean, ratio,
                    allocatedIndex >= 0 && allocatedIndex < fields.Length ? fields[allocatedIndex] : "N/A", true,
                    capability?.Mode ?? ComparativeCapabilityMode.Native,
                    capability?.Note ?? "capability not declared"));
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
                if (capability.Supported)
                    throw new InvalidOperationException($"Measured report is missing supported ECS '{capability.Ecs}' for {group.Key.Workload} ({group.Key.Params}).");
                result.Add(new(capability.Workload, group.Key.Params, capability.Ecs,
                    double.PositiveInfinity, double.PositiveInfinity, "N/A", false,
                    ComparativeCapabilityMode.Unsupported, capability.Note));
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

        return result
            .OrderBy(row => row.Workload, StringComparer.Ordinal)
            .ThenBy(row => row.Params, StringComparer.Ordinal)
            .ThenBy(row => row.Ecs)
            .ToArray();
    }

    internal static bool TryMapMethod(string method, out string workload, out ComparativeEcs ecs)
    {
        workload = method.Contains("Dense", StringComparison.OrdinalIgnoreCase) ? "Iteration.Dense" :
            method.Contains("Movement2Components", StringComparison.OrdinalIgnoreCase) ? "Iteration.Movement2Components" :
            method.Contains("Movement4Components", StringComparison.OrdinalIgnoreCase) ? "Iteration.Movement4Components" :
            method.Contains("WideArchetypeNarrowQuery", StringComparison.OrdinalIgnoreCase) ? "Iteration.WideArchetypeNarrowQuery" :
            method.Contains("SparseWorldCachedQuery", StringComparison.OrdinalIgnoreCase) ? "Iteration.SparseWorldCachedQuery" :
            method.Contains("SparseWorldColdQuery", StringComparison.OrdinalIgnoreCase) ? "Iteration.SparseWorldColdQuery" :
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
        var mappedEcs = true;
        ecs = method.StartsWith("DeltaECS_", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.DeltaECS :
            method.StartsWith("Arch_", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.Arch :
            method.StartsWith("Friflo", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.FrifloEngineECS :
            method.StartsWith("DefaultEcs_", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.DefaultEcs :
            method.StartsWith("LeoEcsLite_", StringComparison.OrdinalIgnoreCase) ? ComparativeEcs.LeoEcsLite : UnknownEcs();
        return workload.Length != 0 && mappedEcs;

        ComparativeEcs UnknownEcs()
        {
            mappedEcs = false;
            return default;
        }
    }

    private static double ParseMeasurement(string value, char csvSeparator)
    {
        var number = new string(value.TrimStart().TakeWhile(character =>
            char.IsDigit(character) || character is '.' or ',' or '-' or '+' or 'e' or 'E' || char.IsWhiteSpace(character)).ToArray());
        number = number.Replace(" ", string.Empty).Replace("\u00a0", string.Empty).Replace("\u202f", string.Empty);
        if (csvSeparator == ';' && number.Contains(',') && !number.Contains('.'))
        {
            number = number.Replace(',', '.');
        }

        if (!double.TryParse(number, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)) return double.NaN;
        if (value.Contains("ms", StringComparison.OrdinalIgnoreCase)) return parsed * 1_000_000;
        if (value.Contains("μs", StringComparison.OrdinalIgnoreCase) || value.Contains("us", StringComparison.OrdinalIgnoreCase)) return parsed * 1_000;
        if (value.Contains("ns", StringComparison.OrdinalIgnoreCase)) return parsed;
        return parsed;
    }

    private static char DetectSeparator(string header)
    {
        if (header.Contains(';')) return ';';
        if (header.Contains(',')) return ',';
        throw new InvalidOperationException("Benchmark CSV header has no supported delimiter.");
    }

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
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == separator && !quoted)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        if (quoted) throw new InvalidOperationException("Benchmark CSV contains an unterminated quoted field.");
        fields.Add(field.ToString());
        return fields.ToArray();
    }

    private static string FormatNumber(double value) =>
        double.IsPositiveInfinity(value) ? "∞" : double.IsNaN(value) ? "N/A" : value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string DisplayName(ComparativeEcs ecs) => ecs == ComparativeEcs.FrifloEngineECS ? "Friflo.Engine.ECS" : ecs.ToString();

    private static string Csv(string value) => value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static string Markdown(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}

/// <summary>Only unified classes are included here; no Legacy class is reachable from the new routes.</summary>
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
        foreach (var workload in new[] { "Iteration.Dense", "Iteration.Movement2Components", "Iteration.Movement4Components", "Iteration.WideArchetypeNarrowQuery", "Iteration.SparseWorldCachedQuery", "Iteration.SparseWorldColdQuery" })
        {
            if (ComparativeCapabilityManifest.Rows.Count(row => row.Workload == workload) != ecsCount)
                throw new InvalidOperationException($"Capability manifest is incomplete for {workload}.");
        }

        var unsupported = ComparativeReportBuilder.BuildManifestRows().Where(row => !row.Supported).ToArray();
        if (unsupported.Any(row => !double.IsPositiveInfinity(row.Mean) || !double.IsPositiveInfinity(row.RatioToDelta)))
            throw new InvalidOperationException("Unsupported capability rows must render as infinity.");

        var benchmarkAttribute = typeof(BenchmarkDotNet.Attributes.BenchmarkAttribute);
        var measuredCapabilities = new HashSet<(string Workload, ComparativeEcs Ecs)>();
        foreach (var type in FullComparison)
        {
            var methods = type.GetMethods().Where(method => method.GetCustomAttributes(benchmarkAttribute, inherit: true).Length != 0).ToArray();
            if (methods.Length == 0) throw new InvalidOperationException($"Comparative class {type.Name} has no benchmark methods.");
            var baselines = methods.Where(method => ((BenchmarkDotNet.Attributes.BenchmarkAttribute)method.GetCustomAttributes(benchmarkAttribute, true).Single()).Baseline).ToArray();
            if (baselines.Length == 0 || baselines.Any(method => !method.Name.StartsWith("DeltaECS_", StringComparison.Ordinal)))
                throw new InvalidOperationException($"Comparative class {type.Name} must expose a DeltaECS baseline.");
            foreach (var method in methods)
            {
                if (!ComparativeReportBuilder.TryMapMethod(method.Name, out var workload, out var ecs))
                    throw new InvalidOperationException($"Comparative method {type.Name}.{method.Name} is not mapped to a workload and ECS.");
                if (!measuredCapabilities.Add((workload, ecs)))
                    throw new InvalidOperationException($"Duplicate comparative method for {workload} and {ecs}.");
            }
        }

        foreach (var capability in ComparativeCapabilityManifest.Rows.Where(row => row.Supported))
            if (!measuredCapabilities.Contains((capability.Workload, capability.Ecs)))
                throw new InvalidOperationException($"Missing benchmark method for supported capability {capability.Workload} and {capability.Ecs}.");
    }
}
