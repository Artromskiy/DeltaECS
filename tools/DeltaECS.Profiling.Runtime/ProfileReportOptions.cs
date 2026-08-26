namespace DeltaECS.Profiling;

/// <summary>Sections included in a profiling report.</summary>
[Flags]
public enum ProfileReportSections : byte
{
    None = 0,
    Summary = 1,
    Table = 2,
    Tree = 4,
    All = Summary | Table | Tree
}

/// <summary>Text encoding used for a profiling report.</summary>
public enum ProfileReportFormat : byte
{
    Markdown,
    Text
}

/// <summary>Metric used to order flat report rows.</summary>
public enum ProfileReportSort : byte
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
