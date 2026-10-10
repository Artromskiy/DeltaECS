using DeltaECS.Profiling;

ProfileCommandLine options = ProfileCommandLine.Parse(args);
if (options.Help)
{
    ProfileCommandLine.PrintUsage(Console.Out);
    return;
}

using var report = new StringWriter();
WriteRunConfiguration(options, report);
long checksum = RunSmoke(options, report);

if ((options.Report.Sections & ProfileReportSections.Summary) != 0)
{
    report.WriteLine();
    report.WriteLine(options.Report.Format == ProfileReportFormat.Markdown
        ? $"Checksum: `{checksum}`"
        : $"Checksum: {checksum}");
}

WriteOutput(options, report.ToString(), checksum);

static void WriteRunConfiguration(ProfileCommandLine options, TextWriter report)
{
    if ((options.Report.Sections & ProfileReportSections.Summary) == 0)
    {
        return;
    }

    bool markdown = options.Report.Format == ProfileReportFormat.Markdown;
    report.WriteLine(markdown ? "# Profile run" : "Profile run");
    report.WriteLine();
    if (markdown)
    {
        report.WriteLine("| Parameter | Value |");
        report.WriteLine("|:--|--:|");
    }

    Write("Probe", "Smoke");
    Write("Depth", options.Depth);
    Write("Sample capacity", options.SampleCapacity);
    Write("Report sections", options.Report.Sections.ToString());
    Write("Report format", options.Report.Format.ToString());
    Write("Report sort", options.Report.Sort.ToString());
    report.WriteLine();

    void Write(string name, object value)
        => report.WriteLine(markdown ? $"| {name} | {value} |" : $"{name}: {value}");
}

static long RunSmoke(ProfileCommandLine options, TextWriter report)
{
    const int runWorkload = 1;
    const int workItem = 2;
    const int leaf = 3;
    var methodNames = new Dictionary<int, string>
    {
        [runWorkload] = nameof(RunWorkload),
        [workItem] = nameof(WorkItem),
        [leaf] = nameof(Leaf)
    };
    CallProfiler profiler = ProfilerRuntime.Start(options.Depth, options.SampleCapacity);
    try
    {
        for (int warmup = 0; warmup < options.Warmups; warmup++)
        {
            _ = RunWorkload(runWorkload, workItem, leaf);
            profiler.ResetMeasurements();
        }

        long checksum = 0;
        for (int launch = 0; launch < options.Launches; launch++)
        {
            checksum += RunWorkload(runWorkload, workItem, leaf);
        }

        profiler.WriteReport(report, methodNames, calibration: null, options.Report);
        return checksum;
    }
    finally
    {
        _ = ProfilerRuntime.Detach();
    }
}

static void WriteOutput(ProfileCommandLine options, string report, long checksum)
{
    if (options.Destination is ProfileOutputDestination.Console or ProfileOutputDestination.Both)
    {
        Console.Write(report);
    }

    if (options.Destination is not (ProfileOutputDestination.File or ProfileOutputDestination.Both))
    {
        return;
    }

    string output = options.Output
        ?? throw new InvalidOperationException("File output requires a validated output path.");
    string fullPath = Path.GetFullPath(output);
    string? directory = Path.GetDirectoryName(fullPath);
    if (directory is not null)
    {
        Directory.CreateDirectory(directory);
    }

    File.WriteAllText(fullPath, report);
    if (options.Destination == ProfileOutputDestination.File)
    {
        Console.WriteLine($"Profile written to {fullPath}");
        Console.WriteLine($"Checksum: {checksum}");
    }
}

static int RunWorkload(int methodId, int workItemId, int leafId)
{
    ProfilerRuntime.Enter(methodId);
    try
    {
        return WorkItem(0, workItemId, leafId);
    }
    finally
    {
        ProfilerRuntime.Leave(methodId);
    }
}

static int WorkItem(int value, int methodId, int leafId)
{
    ProfilerRuntime.Enter(methodId);
    try
    {
        return Leaf(value, leafId) + Leaf(value + 1, leafId);
    }
    finally
    {
        ProfilerRuntime.Leave(methodId);
    }
}

static int Leaf(int value, int methodId)
{
    ProfilerRuntime.Enter(methodId);
    try
    {
        return (value * 17) ^ (value >> 3);
    }
    finally
    {
        ProfilerRuntime.Leave(methodId);
    }
}
