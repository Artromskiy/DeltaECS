using System.Diagnostics;

namespace DeltaECS.Profiling;

/// <summary>Sections included in a profiling report.</summary>
[Flags]
public enum ProfileReportSections
{
    None = 0,
    Summary = 1,
    Table = 2,
    Tree = 4,
    All = Summary | Table | Tree
}

/// <summary>Text encoding used for a profiling report.</summary>
public enum ProfileReportFormat
{
    Markdown,
    Text
}

/// <summary>Metric used to order flat report rows.</summary>
public enum ProfileReportSort
{
    Raw,
    Adjusted,
    Self,
    Calls
}

/// <summary>Controls report sections, formatting and ordering.</summary>
public readonly record struct ProfileReportOptions(
    ProfileReportSections Sections,
    ProfileReportFormat Format,
    ProfileReportSort Sort)
{
    public static ProfileReportOptions Default { get; } = new(
        ProfileReportSections.All,
        ProfileReportFormat.Text,
        ProfileReportSort.Adjusted);
}

/// <summary>Immutable aggregate for one instrumented method.</summary>
public readonly record struct ProfileMethod(
    string Name,
    int MethodId,
    int Calls,
    TimeSpan RawInclusive,
    TimeSpan RawSelf,
    TimeSpan Overhead,
    TimeSpan SelfOverhead)
{
    public TimeSpan AdjustedInclusive => Clamp(RawInclusive - Overhead);

    public TimeSpan AdjustedSelf => Clamp(RawSelf - SelfOverhead);

    public TimeSpan AdjustedInner => Clamp(AdjustedInclusive - AdjustedSelf);

    private static TimeSpan Clamp(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
}

/// <summary>Measured cost model for one active profiler sample.</summary>
public readonly record struct ProfilerOverheadCalibration(
    double TimestampTicksPerProbe,
    int MaximumDepth,
    int WarmupRuns,
    int MeasurementRuns,
    int PathIterations,
    double RSquared,
    double MinimumRSquared)
{
    public double NanosecondsPerProbe
        => TimestampTicksPerProbe * 1_000_000_000d / Stopwatch.Frequency;
}
