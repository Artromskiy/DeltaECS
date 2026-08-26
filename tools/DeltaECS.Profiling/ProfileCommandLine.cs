using System.Globalization;

namespace DeltaECS.Profiling;

internal enum ProfileProbe : byte
{
    Smoke,
    Movement4
}

internal enum ProfileCorrectionMode : byte
{
    Off,
    Optional,
    Required
}

internal enum ProfileOutputDestination : byte
{
    Automatic,
    Console,
    File,
    Both
}

internal sealed class ProfileCommandLine
{
    internal ProfileProbe Probe { get; private set; } = ProfileProbe.Smoke;

    internal int Depth { get; private set; } = 16;

    internal int Launches { get; private set; } = 1;

    internal int Warmups { get; private set; }

    internal int SampleCapacity { get; private set; } = 1_048_576;

    internal ProfileCorrectionMode Correction { get; private set; }

    internal double CorrectionMinimumRSquared { get; private set; } = 0.8;

    internal int CalibrationWarmups { get; private set; } = 2;

    internal int CalibrationRuns { get; private set; } = 7;

    internal int CalibrationIterations { get; private set; } = 65_536;

    internal ProfileReportOptions Report { get; private set; } = ProfileReportOptions.Default;

    internal ProfileOutputDestination Destination { get; private set; } = ProfileOutputDestination.Automatic;

    internal string? Output { get; private set; }

    internal bool Help { get; private set; }

    internal static ProfileCommandLine Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var result = new ProfileCommandLine();
        bool probeSpecified = false;
        bool correctionSpecified = false;
        int index = 0;
        for (; index < arguments.Length; index++)
        {
            string argument = arguments[index];
            switch (argument)
            {
                case ProfileArgumentNames.Movement4:
                    SetProbe(ProfileProbe.Movement4);
                    break;
                case ProfileArgumentNames.Smoke:
                    SetProbe(ProfileProbe.Smoke);
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
                case ProfileArgumentNames.Correction:
                    result.Correction = ParseCorrection(NextValue(ProfileArgumentNames.Correction));
                    correctionSpecified = true;
                    break;
                case ProfileArgumentNames.CorrectionMinimumRSquared:
                    result.CorrectionMinimumRSquared = ParseProbability(
                        NextValue(ProfileArgumentNames.CorrectionMinimumRSquared));
                    break;
                case ProfileArgumentNames.CalibrationWarmups:
                    result.CalibrationWarmups = ParseNonNegative(
                        NextValue(ProfileArgumentNames.CalibrationWarmups));
                    break;
                case ProfileArgumentNames.CalibrationRuns:
                    result.CalibrationRuns = ParsePositive(NextValue(ProfileArgumentNames.CalibrationRuns));
                    break;
                case ProfileArgumentNames.CalibrationIterations:
                    result.CalibrationIterations = ParsePositive(
                        NextValue(ProfileArgumentNames.CalibrationIterations));
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

        if (!correctionSpecified && result.Probe == ProfileProbe.Movement4)
        {
            result.Correction = ProfileCorrectionMode.Optional;
        }

        if (!result.Help)
        {
            result.Validate();
        }

        return result;

        void SetProbe(ProfileProbe probe)
        {
            if (probeSpecified && result.Probe != probe)
            {
                throw new ArgumentException(
                    $"{ProfileArgumentNames.Movement4} and {ProfileArgumentNames.Smoke} cannot be combined.");
            }

            result.Probe = probe;
            probeSpecified = true;
        }

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
        writer.WriteLine("Usage: profile-hotpath.sh [probe] [measurement] [correction] [report]");
        writer.WriteLine();
        writer.WriteLine($"Probe: {ProfileArgumentNames.Movement4} | {ProfileArgumentNames.Smoke}");
        writer.WriteLine(
            $"Measurement: {ProfileArgumentNames.Depth} N {ProfileArgumentNames.Warmups} N "
            + $"{ProfileArgumentNames.SampleCapacity} N");
        writer.WriteLine(
            $"Correction: {ProfileArgumentNames.Correction} off|optional|required "
            + $"{ProfileArgumentNames.CorrectionMinimumRSquared} 0..1");
        writer.WriteLine(
            $"Calibration: {ProfileArgumentNames.CalibrationWarmups} N "
            + $"{ProfileArgumentNames.CalibrationRuns} N {ProfileArgumentNames.CalibrationIterations} N");
        writer.WriteLine(
            $"Report: {ProfileArgumentNames.Sections} summary,table,tree "
            + $"{ProfileArgumentNames.Format} markdown|text {ProfileArgumentNames.Sort} raw|adjusted|self|calls");
        writer.WriteLine(
            $"Output: {ProfileArgumentNames.Destination} console|file|both {ProfileArgumentNames.Output} FILE");
    }

    internal void SetCalibratedLaunches(int launches)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(launches);
        Launches = launches;
    }

    private void Validate()
    {
        if (Probe == ProfileProbe.Smoke && Correction != ProfileCorrectionMode.Off)
        {
            throw new ArgumentException(
                $"{ProfileArgumentNames.Correction} is available only with {ProfileArgumentNames.Movement4}.");
        }

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

    private static double ParseProbability(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            && result is >= 0 and <= 1
                ? result
                : throw new ArgumentException($"'{value}' must be between 0 and 1.");

    private static ProfileCorrectionMode ParseCorrection(string value)
        => value.ToUpperInvariant() switch
        {
            "OFF" => ProfileCorrectionMode.Off,
            "OPTIONAL" => ProfileCorrectionMode.Optional,
            "REQUIRED" => ProfileCorrectionMode.Required,
            _ => throw new ArgumentException($"Unknown correction mode '{value}'.")
        };

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
            "ADJUSTED" => ProfileReportSort.Adjusted,
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
