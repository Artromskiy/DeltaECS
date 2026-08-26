using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace DeltaECS.Profiling;

/// <summary>Collects numeric method-entry samples emitted by profiling instrumentation.</summary>
public sealed class CallProfiler
{
    private const int DefaultSampleCapacity = 1_048_576;

    private readonly Sample[] _samples;
    private readonly Frame[] _frames;
    private readonly int[] _rootMethodIds;
    private readonly int _maxDepth;
    private int _sampleCount;
    private long _droppedSamples;
    private int _depth;
    private int _suppressedDepth;
    private int _ownerThreadId;
    private int _nextInvocationId;
    private bool _capturingRoot;

    public CallProfiler(
        int maxDepth = 32,
        int sampleCapacity = DefaultSampleCapacity,
        ReadOnlySpan<int> rootMethodIds = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleCapacity);
        _maxDepth = maxDepth;
        _frames = new Frame[maxDepth];
        _samples = new Sample[sampleCapacity];
        _rootMethodIds = rootMethodIds.ToArray();
        _capturingRoot = _rootMethodIds.Length == 0;
    }

    /// <summary>Gets the number of samples captured since the last reset.</summary>
    public int SampleCount => _sampleCount;

    /// <summary>Gets the number of samples rejected because the buffer was full.</summary>
    public long DroppedSamples => _droppedSamples;

    /// <summary>Clears measurements while retaining the preallocated sample and frame buffers.</summary>
    public void ResetMeasurements()
    {
        if (_depth != 0 || _suppressedDepth != 0)
        {
            ThrowResetInsideScope();
        }

        _sampleCount = 0;
        _droppedSamples = 0;
        _nextInvocationId = 0;
        _capturingRoot = _rootMethodIds.Length == 0;
    }

    /// <summary>Returns a stable snapshot sorted by inclusive time descending.</summary>
    public IReadOnlyList<ProfileMethod> Snapshot(
        IReadOnlyDictionary<int, string>? methodNames = null,
        ProfilerOverheadCalibration? calibration = null,
        ProfileReportSort sort = ProfileReportSort.Raw)
    {
        Dictionary<int, Aggregate> aggregates = BuildAggregates();

        var result = new ProfileMethod[aggregates.Count];
        int index = 0;
        foreach ((int methodId, Aggregate aggregate) in aggregates)
        {
            double overheadTicks = calibration?.TimestampTicksPerProbe * aggregate.DescendantCalls ?? 0;
            double selfOverheadTicks = calibration?.TimestampTicksPerProbe * aggregate.DirectChildCalls ?? 0;

            result[index++] = new ProfileMethod(
                ResolveMethodName(methodId, methodNames),
                methodId,
                aggregate.Calls,
                ToTimeSpan(aggregate.InclusiveTicks),
                ToTimeSpan(aggregate.SelfTicks),
                ToTimeSpan(overheadTicks),
                ToTimeSpan(selfOverheadTicks));
        }

        Array.Sort(result, (left, right) => CompareMethods(left, right, sort));
        return result;
    }

    /// <summary>Writes a configurable profiling report.</summary>
    public void WriteReport(
        TextWriter writer,
        IReadOnlyDictionary<int, string>? methodNames,
        ProfilerOverheadCalibration? calibration,
        ProfileReportOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (calibration is not null && _droppedSamples != 0)
        {
            throw new InvalidOperationException(
                "Adjusted profiling metrics require a run without dropped samples.");
        }

        bool markdown = options.Format == ProfileReportFormat.Markdown;
        if ((options.Sections & ProfileReportSections.Summary) != 0)
        {
            writer.WriteLine(markdown ? "# Call profile" : "Call profile");
            writer.WriteLine();
            if (markdown)
            {
                writer.WriteLine("| Metric | Value |");
                writer.WriteLine("|:--|--:|");
            }

            WriteSummaryValue("Max depth", _maxDepth.ToString(CultureInfo.InvariantCulture));
            WriteSummaryValue("Measured thread", _ownerThreadId.ToString(CultureInfo.InvariantCulture));
            WriteSummaryValue("Samples", _sampleCount.ToString(CultureInfo.InvariantCulture));
            WriteSummaryValue("Dropped samples", _droppedSamples.ToString(CultureInfo.InvariantCulture));
            if (calibration is { } calibrated)
            {
                WriteSummaryValue(
                    "Probe overhead",
                    calibrated.NanosecondsPerProbe.ToString("F2", CultureInfo.InvariantCulture) + " ns/sample");
                WriteSummaryValue(
                    "Estimated collector overhead",
                    Format(ToTimeSpan(calibrated.TimestampTicksPerProbe * _sampleCount)));
                WriteSummaryValue(
                    "Calibration depths",
                    "0.." + calibrated.MaximumDepth.ToString(CultureInfo.InvariantCulture));
                WriteSummaryValue(
                    "Calibration warmups",
                    calibrated.WarmupRuns.ToString(CultureInfo.InvariantCulture));
                WriteSummaryValue(
                    "Calibration runs",
                    calibrated.MeasurementRuns.ToString(CultureInfo.InvariantCulture));
                WriteSummaryValue(
                    "Calibration path iterations",
                    calibrated.PathIterations.ToString(CultureInfo.InvariantCulture));
                WriteSummaryValue(
                    "Calibration R²",
                    calibrated.RSquared.ToString("F4", CultureInfo.InvariantCulture));
                if (calibrated.RSquared < calibrated.MinimumRSquared)
                {
                    writer.WriteLine();
                    writer.WriteLine(
                        (markdown ? "> **Calibration warning:** " : "Calibration warning: ")
                        + $"R² < {calibrated.MinimumRSquared.ToString("F4", CultureInfo.InvariantCulture)}; "
                        + $"increase {ProfileArgumentNames.CalibrationIterations} "
                        + $"or {ProfileArgumentNames.CalibrationRuns}.");
                }
            }

            writer.WriteLine();
        }

        if (_sampleCount != 0 && (options.Sections & ProfileReportSections.Tree) != 0)
        {
            WriteCallTree(writer, methodNames, calibration, options.Format, options.Sort);
        }

        if ((options.Sections & ProfileReportSections.Table) != 0)
        {
            const int detailCallsColumn = 7;
            const int detailMetricColumn = 10;
            IReadOnlyList<ProfileMethod> methods = Snapshot(methodNames, calibration, options.Sort);
            int detailMethodColumn = Math.Max(
                "METHOD".Length,
                methods.Count == 0 ? 0 : methods.Max(method => CompactMethodName(method.Name).Length));
            writer.WriteLine(markdown ? "## Detailed methods" : "Detailed methods");
            writer.WriteLine();
            if (markdown)
            {
                writer.WriteLine("```text");
            }

            string callsHeader = options.Sort == ProfileReportSort.Calls ? "CALLS↓" : "CALLS";
            string rawHeader = options.Sort == ProfileReportSort.Raw ? "RAW↓" : "RAW";
            string adjustedHeader = options.Sort == ProfileReportSort.Adjusted ? "ADJ↓" : "ADJ";
            string selfHeader = options.Sort == ProfileReportSort.Self ? "ADJSELF↓" : "ADJSELF";
            writer.Write("METHOD".PadRight(detailMethodColumn));
            writer.Write(" | ");
            writer.Write(callsHeader.PadLeft(detailCallsColumn));
            foreach (string header in new[]
            {
                rawHeader,
                "OVERHEAD",
                adjustedHeader,
                "RAWSELF",
                selfHeader,
                "ADJINNER",
                "AVG"
            })
            {
                writer.Write(" | ");
                writer.Write(header.PadLeft(detailMetricColumn));
            }

            writer.WriteLine();
            writer.WriteLine(new string(
                '-',
                detailMethodColumn + detailCallsColumn + (detailMetricColumn * 7) + 24));

            foreach (ProfileMethod method in methods)
            {
                TimeSpan average = method.Calls == 0
                    ? TimeSpan.Zero
                    : TimeSpan.FromTicks(method.AdjustedInclusive.Ticks / method.Calls);
                string name = CompactMethodName(method.Name);
                writer.Write(name.PadRight(detailMethodColumn));
                writer.Write(" | ");
                writer.Write(FormatCount(method.Calls).PadLeft(detailCallsColumn));
                foreach (TimeSpan timing in new[]
                {
                    method.RawInclusive,
                    method.Overhead,
                    method.AdjustedInclusive,
                    method.RawSelf,
                    method.AdjustedSelf,
                    method.AdjustedInner,
                    average
                }
                )
                {
                    writer.Write(" | ");
                    writer.Write(FormatCompact(timing).PadLeft(detailMetricColumn));
                }

                writer.WriteLine();
            }

            if (markdown)
            {
                writer.WriteLine("```");
            }
        }

        return;

        void WriteSummaryValue(string name, string value)
            => writer.WriteLine(markdown ? $"| {name} | {value} |" : $"{name}: {value}");
    }

    private void WriteCallTree(
        TextWriter writer,
        IReadOnlyDictionary<int, string>? methodNames,
        ProfilerOverheadCalibration? calibration,
        ProfileReportFormat format,
        ProfileReportSort sort)
    {
        const int callsColumn = 7;
        const int metricColumn = 17;
        Dictionary<CallEdge, EdgeAggregate> edges = BuildCallEdges();
        PrepareAdjustedMetrics(NoParent);
        int methodColumn = MeasureMethodColumn();
        writer.WriteLine();
        writer.WriteLine(format == ProfileReportFormat.Markdown ? "## Call tree" : "Call tree");
        writer.WriteLine();
        if (format == ProfileReportFormat.Markdown)
        {
            writer.WriteLine("```text");
        }

        string[] headers = Headers(sort);
        writer.Write("METHOD".PadRight(methodColumn));
        writer.Write(" | ");
        writer.Write((sort == ProfileReportSort.Calls ? "CALLS↓" : "CALLS").PadLeft(callsColumn));
        foreach (string header in headers)
        {
            writer.Write(" | ");
            writer.Write(header.PadLeft(metricColumn));
        }

        writer.WriteLine();
        writer.WriteLine(new string('-', methodColumn + callsColumn + (metricColumn * headers.Length) + 18));
        writer.WriteLine();
        WriteChildren(
            NoParent,
            depth: 0,
            prefix: string.Empty,
            rootCorrectedTicks: 0,
            parentRawTicks: 0);
        if (format == ProfileReportFormat.Markdown)
        {
            writer.WriteLine("```");
        }

        writer.WriteLine();

        int MeasureMethodColumn()
        {
            int maximum = "METHOD".Length;
            MeasureChildren(NoParent, depth: 0, prefix: string.Empty);
            return maximum;

            void MeasureChildren(int parentNodeId, int depth, string prefix)
            {
                CallEdge[] children = OrderedChildren(parentNodeId);
                for (int childIndex = 0; childIndex < children.Length; childIndex++)
                {
                    CallEdge edge = children[childIndex];
                    bool isRoot = depth == 0;
                    bool isLast = childIndex == children.Length - 1;
                    string nodePrefix = isRoot ? string.Empty : prefix + (isLast ? "└─ " : "├─ ");
                    string label = nodePrefix + CompactMethodName(ResolveMethodName(edge.MethodId, methodNames));
                    maximum = Math.Max(maximum, label.Length);

                    string continuationPrefix = isRoot
                        ? string.Empty
                        : prefix + (isLast ? "   " : "│  ");
                    MeasureChildren(edge.NodeId, depth + 1, continuationPrefix);
                }
            }
        }

        CallEdge[] OrderedChildren(int parentNodeId)
            => edges.Keys
                .Where(edge => edge.ParentNodeId == parentNodeId)
                .OrderByDescending(edge => SortTicks(edges[edge], sort))
                .ThenByDescending(edge => edges[edge].RawInclusiveTicks)
                .ThenBy(edge => ResolveMethodName(edge.MethodId, methodNames), StringComparer.Ordinal)
                .ToArray();

        void WriteChildren(
            int parentNodeId,
            int depth,
            string prefix,
            double rootCorrectedTicks,
            double parentRawTicks)
        {
            CallEdge[] children = OrderedChildren(parentNodeId);
            for (int childIndex = 0; childIndex < children.Length; childIndex++)
            {
                CallEdge edge = children[childIndex];
                EdgeAggregate aggregate = edges[edge];
                double overheadTicks = aggregate.OverheadTicks;
                double correctedTicks = aggregate.AdjustedInclusiveTicks;
                double correctedSelfTicks = aggregate.AdjustedSelfTicks;
                double correctedInnerTicks = aggregate.AdjustedInnerTicks;
                double effectiveRootTicks = depth == 0 ? correctedTicks : rootCorrectedTicks;
                double effectiveParentRawTicks = depth == 0
                    ? aggregate.RawInclusiveTicks
                    : parentRawTicks;
                bool isRoot = depth == 0;
                bool isLast = childIndex == children.Length - 1;
                string continuationPrefix = isRoot
                    ? string.Empty
                    : prefix + (isLast ? "   " : "│  ");
                WriteAsciiNode(
                    edge,
                    aggregate,
                    isRoot ? string.Empty : prefix + (isLast ? "└─ " : "├─ "),
                    effectiveRootTicks,
                    effectiveParentRawTicks,
                    overheadTicks,
                    correctedTicks,
                    correctedSelfTicks,
                    correctedInnerTicks);

                WriteChildren(
                    edge.NodeId,
                    depth + 1,
                    continuationPrefix,
                    effectiveRootTicks,
                    aggregate.RawInclusiveTicks);
            }
        }

        double PrepareAdjustedMetrics(int parentNodeId)
        {
            double total = 0;
            foreach (CallEdge edge in OrderedChildren(parentNodeId))
            {
                EdgeAggregate aggregate = edges[edge];
                double adjustedInnerTicks = PrepareAdjustedMetrics(edge.NodeId);
                double selfOverheadTicks = calibration?.TimestampTicksPerProbe
                    * aggregate.DirectChildCalls ?? 0;
                double adjustedSelfTicks = Math.Max(0, aggregate.RawSelfTicks - selfOverheadTicks);
                aggregate.AdjustedSelfTicks = adjustedSelfTicks;
                aggregate.AdjustedInnerTicks = adjustedInnerTicks;
                aggregate.AdjustedInclusiveTicks = adjustedSelfTicks + adjustedInnerTicks;
                aggregate.OverheadTicks = Math.Max(
                    0,
                    aggregate.RawInclusiveTicks - aggregate.AdjustedInclusiveTicks);
                edges[edge] = aggregate;
                total += aggregate.AdjustedInclusiveTicks;
            }

            return total;
        }

        void WriteAsciiNode(
            CallEdge edge,
            EdgeAggregate aggregate,
            string nodePrefix,
            double rootTicks,
            double parentTicks,
            double overheadTicks,
            double correctedTicks,
            double selfTicks,
            double innerTicks)
        {
            string method = CompactMethodName(ResolveMethodName(edge.MethodId, methodNames));
            string label = nodePrefix + method;

            string corrected = Metric(correctedTicks, correctedTicks, rootTicks);
            string raw = Metric(aggregate.RawInclusiveTicks, aggregate.RawInclusiveTicks, parentTicks);
            string self = Metric(selfTicks, selfTicks, correctedTicks);
            string inner = Metric(innerTicks, innerTicks, correctedTicks);
            string overhead = Metric(overheadTicks, overheadTicks, aggregate.RawInclusiveTicks);
            string[] metrics = OrderedMetrics(sort, corrected, raw, self, inner, overhead);
            writer.Write(label.PadRight(methodColumn));
            writer.Write(" | ");
            writer.Write(FormatCount(aggregate.Calls).PadLeft(callsColumn));
            foreach (string metric in metrics)
            {
                writer.Write(" | ");
                writer.Write(metric.PadLeft(metricColumn));
            }

            writer.WriteLine();
        }

        static string[] Headers(ProfileReportSort requestedSort)
            => requestedSort switch
            {
                ProfileReportSort.Adjusted =>
                    ["ADJ↓ ROOT%", "RAW PARENT%", "SELF ADJ%", "INNER ADJ%", "OVH RAW%"],
                ProfileReportSort.Self =>
                    ["SELF↓ ADJ%", "ADJ ROOT%", "RAW PARENT%", "INNER ADJ%", "OVH RAW%"],
                ProfileReportSort.Calls =>
                    ["ADJ ROOT%", "RAW PARENT%", "SELF ADJ%", "INNER ADJ%", "OVH RAW%"],
                _ => ["RAW↓ PARENT%", "ADJ ROOT%", "SELF ADJ%", "INNER ADJ%", "OVH RAW%"]
            };

        static string[] OrderedMetrics(
            ProfileReportSort requestedSort,
            string corrected,
            string raw,
            string self,
            string inner,
            string overhead)
            => requestedSort switch
            {
                ProfileReportSort.Adjusted => [corrected, raw, self, inner, overhead],
                ProfileReportSort.Self => [self, corrected, raw, inner, overhead],
                ProfileReportSort.Calls => [corrected, raw, self, inner, overhead],
                _ => [raw, corrected, self, inner, overhead]
            };

        static string Metric(double timestampTicks, double percentagePart, double percentageTotal)
            => FormatCompact(ToTimeSpan(timestampTicks)).PadLeft(9)
                + " "
                + Percent(percentagePart, percentageTotal).PadLeft(6);


        static string Percent(double part, double total)
        {
            if (total <= 0)
            {
                return "0.0%";
            }

            double percentage = part * 100 / total;
            return percentage >= 999.95
                ? ">999%"
                : percentage.ToString("F1", CultureInfo.InvariantCulture) + "%";
        }

        double SortTicks(EdgeAggregate aggregate, ProfileReportSort requestedSort)
            => requestedSort switch
            {
                ProfileReportSort.Adjusted => aggregate.AdjustedInclusiveTicks,
                ProfileReportSort.Self => aggregate.AdjustedSelfTicks,
                ProfileReportSort.Calls => aggregate.Calls,
                _ => aggregate.RawInclusiveTicks
            };
    }

    private Dictionary<CallEdge, EdgeAggregate> BuildCallEdges()
    {
        var edges = new Dictionary<CallEdge, EdgeAggregate>();
        int[] samplesByInvocation = CreateInvocationMap();
        var nodesByPath = new Dictionary<CallPath, int>();
        var nodesByInvocation = new int[_nextInvocationId + 1];
        Array.Fill(nodesByInvocation, NoParent);

        for (int sampleIndex = 0; sampleIndex < _sampleCount; sampleIndex++)
        {
            Sample sample = _samples[sampleIndex];
            int nodeId = ResolveNode(sampleIndex);
            int parentNodeId = sample.ParentInvocationId == NoInvocation
                ? NoParent
                : nodesByInvocation[sample.ParentInvocationId];
            var edge = new CallEdge(nodeId, parentNodeId, sample.MethodId);
            edges.TryGetValue(edge, out EdgeAggregate aggregate);
            aggregate.Calls++;
            aggregate.RawInclusiveTicks += sample.EndTicks - sample.StartTicks;
            aggregate.RawSelfTicks += sample.SelfTicks;
            edges[edge] = aggregate;
        }

        for (int sampleIndex = 0; sampleIndex < _sampleCount; sampleIndex++)
        {
            Sample sample = _samples[sampleIndex];
            int ancestorInvocationId = sample.ParentInvocationId;
            while (ancestorInvocationId != NoInvocation)
            {
                int ancestorIndex = samplesByInvocation[ancestorInvocationId];
                if (ancestorIndex < 0)
                {
                    break;
                }

                Sample ancestor = _samples[ancestorIndex];
                int ancestorNodeId = nodesByInvocation[ancestorInvocationId];
                int ancestorParentNodeId = ancestor.ParentInvocationId == NoInvocation
                    ? NoParent
                    : nodesByInvocation[ancestor.ParentInvocationId];
                var ancestorEdge = new CallEdge(
                    ancestorNodeId,
                    ancestorParentNodeId,
                    ancestor.MethodId);
                EdgeAggregate ancestorAggregate = edges[ancestorEdge];
                if (ancestorInvocationId == sample.ParentInvocationId)
                {
                    ancestorAggregate.DirectChildCalls++;
                }

                edges[ancestorEdge] = ancestorAggregate;
                ancestorInvocationId = ancestor.ParentInvocationId;
            }
        }

        return edges;

        int ResolveNode(int sampleIndex)
        {
            Sample sample = _samples[sampleIndex];
            int existingNodeId = nodesByInvocation[sample.InvocationId];
            if (existingNodeId != NoParent)
            {
                return existingNodeId;
            }

            int parentNodeId = NoParent;
            if (sample.ParentInvocationId != NoInvocation)
            {
                int parentSampleIndex = samplesByInvocation[sample.ParentInvocationId];
                if (parentSampleIndex >= 0)
                {
                    parentNodeId = ResolveNode(parentSampleIndex);
                }
            }

            var path = new CallPath(parentNodeId, sample.MethodId);
            if (!nodesByPath.TryGetValue(path, out int nodeId))
            {
                nodeId = nodesByPath.Count;
                nodesByPath.Add(path, nodeId);
            }

            nodesByInvocation[sample.InvocationId] = nodeId;
            return nodeId;
        }
    }

    private Dictionary<int, Aggregate> BuildAggregates()
    {
        var aggregates = new Dictionary<int, Aggregate>(Math.Min(_sampleCount, 256));
        for (int index = 0; index < _sampleCount; index++)
        {
            Sample sample = _samples[index];
            aggregates.TryGetValue(sample.MethodId, out Aggregate aggregate);
            aggregate.Calls++;
            aggregate.InclusiveTicks += sample.EndTicks - sample.StartTicks;
            aggregate.SelfTicks += sample.SelfTicks;
            aggregates[sample.MethodId] = aggregate;
        }

        int[] samplesByInvocation = CreateInvocationMap();
        for (int sampleIndex = 0; sampleIndex < _sampleCount; sampleIndex++)
        {
            Sample sample = _samples[sampleIndex];
            bool directChild = true;
            int ancestorInvocationId = sample.ParentInvocationId;
            while (ancestorInvocationId != NoInvocation)
            {
                int ancestorIndex = samplesByInvocation[ancestorInvocationId];
                if (ancestorIndex < 0)
                {
                    break;
                }

                Sample ancestor = _samples[ancestorIndex];
                Aggregate ancestorAggregate = aggregates[ancestor.MethodId];
                ancestorAggregate.DescendantCalls++;
                if (directChild)
                {
                    ancestorAggregate.DirectChildCalls++;
                    directChild = false;
                }

                aggregates[ancestor.MethodId] = ancestorAggregate;
                ancestorInvocationId = ancestor.ParentInvocationId;
            }
        }

        return aggregates;
    }

    private int[] CreateInvocationMap()
    {
        var samplesByInvocation = new int[_nextInvocationId + 1];
        Array.Fill(samplesByInvocation, -1);
        for (int sampleIndex = 0; sampleIndex < _sampleCount; sampleIndex++)
        {
            samplesByInvocation[_samples[sampleIndex].InvocationId] = sampleIndex;
        }

        return samplesByInvocation;
    }

    private static string ResolveMethodName(
        int methodId,
        IReadOnlyDictionary<int, string>? methodNames)
        => methodNames is not null && methodNames.TryGetValue(methodId, out string? name)
            ? name
            : $"Method#{methodId}";

    private static int CompareMethods(
        ProfileMethod left,
        ProfileMethod right,
        ProfileReportSort sort)
        => sort switch
        {
            ProfileReportSort.Adjusted => right.AdjustedInclusive.CompareTo(left.AdjustedInclusive),
            ProfileReportSort.Self => right.AdjustedSelf.CompareTo(left.AdjustedSelf),
            ProfileReportSort.Calls => right.Calls.CompareTo(left.Calls),
            _ => right.RawInclusive.CompareTo(left.RawInclusive)
        };

    private void ExitCurrentMethod()
    {
        ref Frame frame = ref _frames[_depth - 1];
        long inclusiveTicks = Stopwatch.GetTimestamp() - frame.StartTimestamp;
        long selfTicks = inclusiveTicks - frame.ChildTicks;

        if (_sampleCount < _samples.Length)
        {
            _samples[_sampleCount++] = new Sample(
                frame.MethodId,
                frame.InvocationId,
                frame.ParentInvocationId,
                frame.StartTimestamp,
                frame.StartTimestamp + inclusiveTicks,
                selfTicks);
        }
        else
        {
            _droppedSamples++;
        }

        _depth--;
        if (_depth != 0)
        {
            _frames[_depth - 1].ChildTicks += inclusiveTicks;
        }
    }

    internal void EnterMethod(int methodId)
    {
        EnsureThread();
        if (!_capturingRoot)
        {
            if (!IsRootMethod(methodId))
            {
                return;
            }

            _capturingRoot = true;
        }

        if (_depth >= _maxDepth)
        {
            _suppressedDepth++;
            return;
        }

        int parentInvocationId = _depth == 0
            ? NoInvocation
            : _frames[_depth - 1].InvocationId;
        int invocationId = ++_nextInvocationId;
        _frames[_depth] = new Frame(
            Stopwatch.GetTimestamp(),
            methodId,
            invocationId,
            parentInvocationId);
        _depth++;
    }

    internal void ExitMethod(int methodId)
    {
        EnsureThread();
        if (!_capturingRoot)
        {
            return;
        }

        if (_suppressedDepth != 0)
        {
            _suppressedDepth--;
            return;
        }

        if (_depth == 0 || _frames[_depth - 1].MethodId != methodId)
        {
            ThrowInvalidInstrumentedExit();
        }

        ExitCurrentMethod();
        if (_depth == 0 && _rootMethodIds.Length != 0)
        {
            _capturingRoot = false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsRootMethod(int methodId)
    {
        for (int index = 0; index < _rootMethodIds.Length; index++)
        {
            if (_rootMethodIds[index] == methodId)
            {
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureThread()
    {
        int threadId = Environment.CurrentManagedThreadId;
        if (_ownerThreadId == 0)
        {
            InitializeOwnerThread(threadId);
        }
        else if (_ownerThreadId != threadId)
        {
            ThrowWrongThread();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitializeOwnerThread(int threadId)
    {
        _ownerThreadId = threadId;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidInstrumentedExit()
    {
        throw new InvalidOperationException("Instrumented method exits must be nested in LIFO order.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowWrongThread()
    {
        throw new InvalidOperationException("CallProfiler is single-threaded; use one profiler per thread.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowResetInsideScope()
    {
        throw new InvalidOperationException("Measurements cannot be reset while a profile scope is active.");
    }

    private static TimeSpan ToTimeSpan(long timestampTicks)
        => TimeSpan.FromSeconds((double)timestampTicks / Stopwatch.Frequency);

    private static TimeSpan ToTimeSpan(double timestampTicks)
        => TimeSpan.FromSeconds(timestampTicks / Stopwatch.Frequency);

    private static string Format(TimeSpan value)
        => value.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + " ms";

    private static string CompactMethodName(string method)
    {
        int openParenthesis = method.IndexOf('(', StringComparison.Ordinal);
        if (openParenthesis < 0
            || (openParenthesis + 1 < method.Length && method[openParenthesis + 1] == ')'))
        {
            return method;
        }

        return method[..(openParenthesis + 1)] + "...)";
    }

    private static string FormatCompact(TimeSpan value)
    {
        double milliseconds = value.TotalMilliseconds;
        if (milliseconds >= 1_000)
        {
            double seconds = value.TotalSeconds;
            if (seconds < 1_000)
            {
                return seconds.ToString("F3", CultureInfo.InvariantCulture) + "s";
            }

            double minutes = value.TotalMinutes;
            if (minutes < 1_000)
            {
                return minutes.ToString("F2", CultureInfo.InvariantCulture) + "m";
            }

            double hours = value.TotalHours;
            return hours < 10_000
                ? hours.ToString("F1", CultureInfo.InvariantCulture) + "h"
                : ">9999h";
        }

        if (milliseconds >= 0.001)
        {
            return milliseconds.ToString("F3", CultureInfo.InvariantCulture) + "ms";
        }

        double microseconds = milliseconds * 1_000;
        return microseconds >= 0.001
            ? microseconds.ToString("F3", CultureInfo.InvariantCulture) + "us"
            : (microseconds * 1_000).ToString("F1", CultureInfo.InvariantCulture) + "ns";
    }

    private static string FormatCount(int count)
    {
        if (count < 10_000_000)
        {
            return count.ToString(CultureInfo.InvariantCulture);
        }

        return count < 1_000_000_000
            ? (count / 1_000_000d).ToString("F1", CultureInfo.InvariantCulture) + "M"
            : (count / 1_000_000_000d).ToString("F1", CultureInfo.InvariantCulture) + "B";
    }

    private struct Frame
    {
        internal Frame(
            long startTimestamp,
            int methodId,
            int invocationId,
            int parentInvocationId)
        {
            StartTimestamp = startTimestamp;
            MethodId = methodId;
            InvocationId = invocationId;
            ParentInvocationId = parentInvocationId;
            ChildTicks = 0;
        }

        internal readonly long StartTimestamp;
        internal readonly int MethodId;
        internal readonly int InvocationId;
        internal readonly int ParentInvocationId;
        internal long ChildTicks;
    }

    private readonly struct Sample
    {
        internal readonly int MethodId;
        internal readonly int InvocationId;
        internal readonly int ParentInvocationId;
        internal readonly long StartTicks;
        internal readonly long EndTicks;
        internal readonly long SelfTicks;

        internal Sample(
            int methodId,
            int invocationId,
            int parentInvocationId,
            long startTicks,
            long endTicks,
            long selfTicks)
        {
            MethodId = methodId;
            InvocationId = invocationId;
            ParentInvocationId = parentInvocationId;
            StartTicks = startTicks;
            EndTicks = endTicks;
            SelfTicks = selfTicks;
        }
    }

    private struct Aggregate
    {
        internal int Calls;
        internal long InclusiveTicks;
        internal long SelfTicks;
        internal long DirectChildCalls;
        internal long DescendantCalls;
    }

    private const int NoParent = int.MinValue;
    private const int NoInvocation = 0;

    private readonly record struct CallPath(int ParentNodeId, int MethodId);

    private readonly record struct CallEdge(int NodeId, int ParentNodeId, int MethodId);

    private struct EdgeAggregate
    {
        internal int Calls;
        internal long RawInclusiveTicks;
        internal long RawSelfTicks;
        internal long DirectChildCalls;
        internal double AdjustedInclusiveTicks;
        internal double AdjustedSelfTicks;
        internal double AdjustedInnerTicks;
        internal double OverheadTicks;
    }
}
