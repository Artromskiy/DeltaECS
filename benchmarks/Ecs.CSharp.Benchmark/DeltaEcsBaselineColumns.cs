using System;
using System.Globalization;
using System.Linq;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Ecs.CSharp.Benchmark
{
    internal static class DeltaEcsBaselineSelector
    {
        internal static BenchmarkReport? Find(Summary summary)
            => summary.Reports
                .Where(report => IsDeltaEcsBenchmark(report.BenchmarkCase)
                    && report.ResultStatistics is not null)
                .OrderBy(report => report.ResultStatistics!.Mean)
                .FirstOrDefault();

        internal static BenchmarkReport? Find(Summary summary, BenchmarkCase benchmarkCase)
            => summary.Reports.FirstOrDefault(report => report.BenchmarkCase == benchmarkCase);

        private static bool IsDeltaEcsBenchmark(BenchmarkCase benchmarkCase)
            => benchmarkCase.Descriptor.Categories.Contains(Categories.DeltaECS)
                || benchmarkCase.Descriptor.Categories.Contains(Categories.DeltaECSBatch);
    }

    internal sealed class FastestDeltaEcsRatioColumn : IColumn
    {
        public string Id => nameof(FastestDeltaEcsRatioColumn);

        public string ColumnName => "Ratio";

        public string Legend => "Ratio to the fastest DeltaECS benchmark; Delta marks that benchmark.";

        public bool AlwaysShow => true;

        public ColumnCategory Category => ColumnCategory.Statistics;

        public int PriorityInCategory => 1;

        public bool IsNumeric => false;

        public UnitType UnitType => UnitType.Dimensionless;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        {
            BenchmarkReport? baseline = DeltaEcsBaselineSelector.Find(summary);
            BenchmarkReport? report = DeltaEcsBaselineSelector.Find(summary, benchmarkCase);
            if (baseline == report)
            {
                return "Delta";
            }

            if (baseline?.ResultStatistics is not { } baselineStatistics
                || report?.ResultStatistics is not { } reportStatistics)
            {
                return "NA";
            }

            double ratio = reportStatistics.Mean / baselineStatistics.Mean;
            return ratio.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
            => GetValue(summary, benchmarkCase);

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

        public bool IsAvailable(Summary summary) => DeltaEcsBaselineSelector.Find(summary) is not null;
    }
}
