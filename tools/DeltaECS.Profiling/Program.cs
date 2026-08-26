using DeltaECS.Profiling;

ProfileCommandLine options = ProfileCommandLine.Parse(args);
if (options.Help)
{
    ProfileCommandLine.PrintUsage(Console.Out);
    return;
}

using var report = new StringWriter();
if (options.Probe == ProfileProbe.Movement4)
{
    PrepareMovement4(options);
}

WriteRunConfiguration(options, report);
long checksum = options.Probe switch
{
    ProfileProbe.Movement4 => RunMovement4(options, report),
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

static long RunMovement4(ProfileCommandLine options, TextWriter report)
{
    ProfilerOverheadCalibration? calibration = options.Correction == ProfileCorrectionMode.Off
        ? null
        : ProfilerOverheadCalibrator.Calibrate(
            options.Depth,
            options.CalibrationWarmups,
            options.CalibrationRuns,
            options.CalibrationIterations,
            options.CorrectionMinimumRSquared);

    CallProfiler profiler = ProfilerRuntime.Start(options.Depth, options.SampleCapacity);
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

    Dictionary<int, string> methodNames = ProfilerRuntime.LoadMethodNames(
        typeof(Delta.ECS.World).Assembly);
    MergeMethodNames(
        methodNames,
        ProfilerRuntime.LoadMethodNames(typeof(Movement4DelegateProfile).Assembly));
    methodNames[0] = "DeltaECS.Profiling.Movement4DelegateProfile::Run";
    profiler.WriteReport(report, methodNames, calibration, options.Report);
    return checksum;
}

static void PrepareMovement4(ProfileCommandLine options)
{
    for (int warmup = 0; warmup < options.Warmups; warmup++)
    {
        _ = Movement4DelegateProfile.Run();
    }

    CallProfiler pilot = ProfilerRuntime.Start(options.Depth, options.SampleCapacity);
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
    var profiler = new CallProfiler(options.Depth, options.SampleCapacity);
    int runWorkload = profiler.RegisterMethod(nameof(RunWorkload));
    int workItem = profiler.RegisterMethod(nameof(WorkItem));
    int leaf = profiler.RegisterMethod(nameof(Leaf));
    for (int warmup = 0; warmup < options.Warmups; warmup++)
    {
        _ = RunWorkload(
            profiler,
            runWorkload,
            workItem,
            leaf);
        profiler.ResetMeasurements();
    }

    long checksum = 0;
    for (int launch = 0; launch < options.Launches; launch++)
    {
        checksum = unchecked(checksum + RunWorkload(
            profiler,
            runWorkload,
            workItem,
            leaf));
    }

    profiler.WriteReport(report, methodNames: null, calibration: null, options.Report);
    return checksum;
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

static int RunWorkload(CallProfiler profiler, int methodId, int workItemId, int leafId)
{
    using var scope = profiler.Enter(methodId);
    return WorkItem(profiler, 0, workItemId, leafId);
}

static int WorkItem(CallProfiler profiler, int value, int methodId, int leafId)
{
    using var scope = profiler.Enter(methodId);
    return Leaf(profiler, value, leafId) + Leaf(profiler, value + 1, leafId);
}

static int Leaf(CallProfiler profiler, int value, int methodId)
{
    using var scope = profiler.Enter(methodId);
    return (value * 17) ^ (value >> 3);
}
