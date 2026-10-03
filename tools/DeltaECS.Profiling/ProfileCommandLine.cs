using System.Globalization;

namespace DeltaECS.Profiling;

internal enum ProfileOutputDestination : byte
{
    Automatic,
    Console,
    File,
    Both
}

internal sealed class ProfileCommandLine
{
    internal int Depth { get; private set; } = 16;

    internal int Launches { get; private set; } = 1;

    internal int Warmups { get; private set; }

    internal int SampleCapacity { get; private set; } = 1_048_576;

    internal ProfileReportOptions Report { get; private set; } = ProfileReportOptions.Default;

    internal ProfileOutputDestination Destination { get; private set; } = ProfileOutputDestination.Automatic;

    internal string? Output { get; private set; }

    internal bool Help { get; private set; }

    internal static ProfileCommandLine Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var result = new ProfileCommandLine();
        int index = 0;
        for (; index < arguments.Length; index++)
        {
            string argument = arguments[index];
            switch (argument)
            {
                case ProfileArgumentNames.Smoke:
                    break;
                case ProfileArgumentNames.Depth:
                    result.Depth = ParsePositive(NextValue(ProfileArgumentNames.Depth));
                    break;
                case ProfileArgumentNames.Warmups:
                    result.Warmups = ParseNonNegative(NextValue(ProfileArgumentNames.Warmups));
                    break;
                case ProfileArgumentNames.SampleCapacity:
                    result.SampleCapacity = ParsePositive(NextValue(ProfileArgumentNames.SampleCapacity));
                    break;
                case ProfileArgumentNames.Sections:
                    result.Report = result.Report with
                    {
                        Sections = ParseSections(NextValue(ProfileArgumentNames.Sections))
                    };
                    break;
                case ProfileArgumentNames.Format:
                    result.Report = result.Report with
                    {
                        Format = ParseFormat(NextValue(ProfileArgumentNames.Format))
                    };
                    break;
                case ProfileArgumentNames.Sort:
                    result.Report = result.Report with
                    {
                        Sort = ParseSort(NextValue(ProfileArgumentNames.Sort))
                    };
                    break;
                case ProfileArgumentNames.Destination:
                    result.Destination = ParseDestination(NextValue(ProfileArgumentNames.Destination));
                    break;
                case ProfileArgumentNames.Output:
                    result.Output = NextValue(ProfileArgumentNames.Output);
                    break;
                case ProfileArgumentNames.Help:
                    result.Help = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{argument}'.");
            }
        }

        if (!result.Help)
        {
            result.Validate();
        }

        return result;

        string NextValue(string name)
        {
            if (++index >= arguments.Length)
            {
                throw new ArgumentException($"Missing value for {name}.");
            }

            return arguments[index];
        }
    }

    internal static void PrintUsage(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine("Usage: profile-hotpath.sh [--smoke] [options]");
        writer.WriteLine(
            $"Measurement: {ProfileArgumentNames.Depth} N {ProfileArgumentNames.Warmups} N "
            + $"{ProfileArgumentNames.SampleCapacity} N");
        writer.WriteLine(
            $"Report: {ProfileArgumentNames.Sections} summary,table,tree "
            + $"{ProfileArgumentNames.Format} markdown|text {ProfileArgumentNames.Sort} raw|self|calls");
        writer.WriteLine(
            $"Output: {ProfileArgumentNames.Destination} console|file|both {ProfileArgumentNames.Output} FILE");
    }

    private void Validate()
    {
        if (Report.Sections == ProfileReportSections.None)
        {
            throw new ArgumentException($"{ProfileArgumentNames.Sections} must include at least one section.");
        }

        ProfileOutputDestination effectiveDestination = Destination == ProfileOutputDestination.Automatic
            ? Output is null ? ProfileOutputDestination.Console : ProfileOutputDestination.File
            : Destination;
        if (effectiveDestination is ProfileOutputDestination.File or ProfileOutputDestination.Both
            && string.IsNullOrWhiteSpace(Output))
        {
            throw new ArgumentException(
                $"{ProfileArgumentNames.Output} is required for destination '{effectiveDestination}'.");
        }

        Destination = effectiveDestination;
    }

    private static int ParsePositive(string value)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result) && result > 0
            ? result
            : throw new ArgumentException($"'{value}' must be a positive integer.");

    private static int ParseNonNegative(string value)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result) && result >= 0
            ? result
            : throw new ArgumentException($"'{value}' must be a non-negative integer.");

    private static ProfileReportSections ParseSections(string value)
    {
        ProfileReportSections sections = ProfileReportSections.None;
        foreach (string section in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            sections |= section.ToUpperInvariant() switch
            {
                "SUMMARY" => ProfileReportSections.Summary,
                "TABLE" => ProfileReportSections.Table,
                "TREE" => ProfileReportSections.Tree,
                "ALL" => ProfileReportSections.All,
                _ => throw new ArgumentException($"Unknown report section '{section}'.")
            };
        }

        return sections;
    }

    private static ProfileReportFormat ParseFormat(string value)
        => value.ToUpperInvariant() switch
        {
            "MARKDOWN" => ProfileReportFormat.Markdown,
            "TEXT" => ProfileReportFormat.Text,
            _ => throw new ArgumentException($"Unknown report format '{value}'.")
        };

    private static ProfileReportSort ParseSort(string value)
        => value.ToUpperInvariant() switch
        {
            "RAW" => ProfileReportSort.Raw,
            "SELF" => ProfileReportSort.Self,
            "CALLS" => ProfileReportSort.Calls,
            _ => throw new ArgumentException($"Unknown report sort '{value}'.")
        };

    private static ProfileOutputDestination ParseDestination(string value)
        => value.ToUpperInvariant() switch
        {
            "CONSOLE" => ProfileOutputDestination.Console,
            "FILE" => ProfileOutputDestination.File,
            "BOTH" => ProfileOutputDestination.Both,
            _ => throw new ArgumentException($"Unknown output destination '{value}'.")
        };
}
