using DeltaECS.Profiling;

ProfileCommandLine options = ProfileCommandLine.Parse(args);
if (options.Help)
{
    ProfileCommandLine.PrintUsage(Console.Out);
    return;
}

using var report = new StringWriter();
Dictionary<int, string>? movementMethodNames = null;
int[] rootMethodIds = [];
if (options.Probe == ProfileProbe.Movement4)
{
    movementMethodNames = LoadMovementMethodNames();
    rootMethodIds = ResolveRootMethodIds(options.Root, movementMethodNames);
    PrepareMovement4(options, rootMethodIds);
}

WriteRunConfiguration(options, report);
long checksum = options.Probe switch
{
    ProfileProbe.Movement4 => RunMovement4(
        options,
        report,
        movementMethodNames ?? throw new InvalidOperationException("Movement4 metadata was not prepared."),
        rootMethodIds),
    _ => RunSmoke(options, report)
};

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

    Write("Probe", options.Probe.ToString());
    Write("Depth", options.Depth);
    Write("Launches", options.Launches);
    Write("Warmups", options.Warmups);
    Write("Root", options.Root ?? "All instrumented methods");
    if (options.Probe == ProfileProbe.Movement4)
    {
        Write("Fixed entities", Movement4DelegateProfile.EntityCount);
    }
    Write("Sample capacity", options.SampleCapacity);
    Write("Correction", options.Correction.ToString());
    Write("Correction minimum R²", options.CorrectionMinimumRSquared.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
    Write("Report sections", options.Report.Sections.ToString());
    Write("Report format", options.Report.Format.ToString());
    Write("Report sort", options.Report.Sort.ToString());
    report.WriteLine();

    void Write(string name, object value)
        => report.WriteLine(markdown ? $"| {name} | {value} |" : $"{name}: {value}");
}

static long RunMovement4(
    ProfileCommandLine options,
    TextWriter report,
    IReadOnlyDictionary<int, string> methodNames,
    ReadOnlySpan<int> rootMethodIds)
{
    ProfilerOverheadCalibration? calibration = options.Correction == ProfileCorrectionMode.Off
        ? null
        : ProfilerOverheadCalibrator.Calibrate(
            options.Depth,
            options.CalibrationWarmups,
            options.CalibrationRuns,
            options.CalibrationIterations,
            options.CorrectionMinimumRSquared);

    CallProfiler profiler = ProfilerRuntime.Start(options.Depth, options.SampleCapacity, rootMethodIds);
    long checksum = 0;
    try
    {
        for (int launch = 0; launch < options.Launches; launch++)
        {
            checksum = unchecked(checksum + Movement4DelegateProfile.Run());
        }
    }
    finally
    {
        _ = ProfilerRuntime.Detach();
    }

    if (options.Correction == ProfileCorrectionMode.Required)
    {
        if (calibration is null || calibration.Value.RSquared < options.CorrectionMinimumRSquared)
        {
            string minimum = options.CorrectionMinimumRSquared.ToString(
                "F4",
                System.Globalization.CultureInfo.InvariantCulture);
            string measured = calibration?.RSquared.ToString(
                "F4",
                System.Globalization.CultureInfo.InvariantCulture) ?? "unavailable";
            throw new InvalidOperationException(
                $"Correction requires R² >= {minimum}; measured {measured}.");
        }

        if (profiler.DroppedSamples != 0)
        {
            throw new InvalidOperationException(
                $"Correction requires zero dropped samples; measured {profiler.DroppedSamples}.");
        }
    }
    else if (profiler.DroppedSamples != 0)
    {
        calibration = null;
    }

    profiler.WriteReport(report, methodNames, calibration, options.Report);
    return checksum;
}

static void PrepareMovement4(ProfileCommandLine options, ReadOnlySpan<int> rootMethodIds)
{
    for (int warmup = 0; warmup < options.Warmups; warmup++)
    {
        _ = Movement4DelegateProfile.Run();
    }

    CallProfiler pilot = ProfilerRuntime.Start(options.Depth, options.SampleCapacity, rootMethodIds);
    try
    {
        _ = Movement4DelegateProfile.Run();
    }
    finally
    {
        _ = ProfilerRuntime.Detach();
    }

    if (pilot.DroppedSamples != 0 || pilot.SampleCount == 0)
    {
        throw new InvalidOperationException(
            "The sample buffer must fit at least one complete Movement4 launch.");
    }

    int sampleBudget = checked((int)(options.SampleCapacity * 0.9));
    int launches = Math.Max(1, sampleBudget / pilot.SampleCount);
    options.SetCalibratedLaunches(launches);
}

static Dictionary<int, string> LoadMovementMethodNames()
{
    Dictionary<int, string> methodNames = ProfilerRuntime.LoadMethodNames(
        typeof(Delta.ECS.World).Assembly);
    MergeMethodNames(
        methodNames,
        ProfilerRuntime.LoadMethodNames(typeof(Movement4DelegateProfile).Assembly));
    methodNames[0] = "DeltaECS.Profiling.Movement4DelegateProfile::Run";
    return methodNames;
}

static int[] ResolveRootMethodIds(
    string? selector,
    IReadOnlyDictionary<int, string> methodNames)
{
    if (selector is null)
    {
        return [];
    }

    bool worldForEach = string.Equals(selector, "World.ForEach", StringComparison.OrdinalIgnoreCase);
    int[] matches = methodNames
        .Where(pair => worldForEach
            ? pair.Value.StartsWith("DemandForEachExtensions_", StringComparison.Ordinal)
                && pair.Value.EndsWith(".ForEach", StringComparison.Ordinal)
            : pair.Value.Contains(selector, StringComparison.OrdinalIgnoreCase))
        .Select(static pair => pair.Key)
        .Distinct()
        .ToArray();
    if (matches.Length == 0)
    {
        throw new ArgumentException(
            $"{ProfileArgumentNames.Root} '{selector}' did not match an instrumented method.");
    }

    return matches;
}

static void MergeMethodNames(Dictionary<int, string> target, IReadOnlyDictionary<int, string> source)
{
    foreach ((int methodId, string methodName) in source)
    {
        if (target.TryGetValue(methodId, out string? existingName)
            && !string.Equals(existingName, methodName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Profile method ID collision between '{existingName}' and '{methodName}'.");
        }

        target[methodId] = methodName;
    }
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
            checksum = unchecked(checksum + RunWorkload(runWorkload, workItem, leaf));
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
