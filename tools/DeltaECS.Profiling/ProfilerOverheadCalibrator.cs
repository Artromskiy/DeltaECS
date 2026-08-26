using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DeltaECS.Profiling;

/// <summary>Measures active-probe overhead from warmed paths collected at increasing depths.</summary>
internal static class ProfilerOverheadCalibrator
{
    private const int CalibrationMethodBase = 1_500_000_000;
    private static int s_sink;

    internal static ProfilerOverheadCalibration Calibrate(
        int maximumDepth,
        int warmupRuns = 2,
        int measurementRuns = 7,
        int pathIterations = 65_536,
        double minimumRSquared = 0.8)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupRuns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(measurementRuns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pathIterations);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumRSquared, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimumRSquared, 1);
        _ = ProfilerRuntime.Detach();

        var points = new CalibrationPoint[maximumDepth + 1];
        points[0] = new CalibrationPoint(0, 0);
        int sampleCapacity = checked((maximumDepth * pathIterations) + maximumDepth + 1);
        var allSlopes = new double[maximumDepth * measurementRuns];
        int slopeIndex = 0;
        for (int collectedDepth = 1; collectedDepth <= maximumDepth; collectedDepth++)
        {
            CallProfiler profiler = ProfilerRuntime.Start(collectedDepth, sampleCapacity);

            for (int warmup = 0; warmup < warmupRuns; warmup++)
            {
                s_sink ^= RunPath(maximumDepth, pathIterations);
                profiler.ResetMeasurements();
            }

            _ = ProfilerRuntime.Detach();
            s_sink ^= RunPath(maximumDepth, pathIterations);
            var overhead = new long[measurementRuns];
            long samples = 0;
            for (int run = 0; run < measurementRuns; run++)
            {
                profiler.ResetMeasurements();
                long inactiveTicks;
                long activeTicks;
                if ((run & 1) == 0)
                {
                    inactiveTicks = MeasurePath(maximumDepth, pathIterations);
                    ProfilerRuntime.Attach(profiler);
                    activeTicks = MeasurePath(maximumDepth, pathIterations);
                    _ = ProfilerRuntime.Detach();
                }
                else
                {
                    ProfilerRuntime.Attach(profiler);
                    activeTicks = MeasurePath(maximumDepth, pathIterations);
                    _ = ProfilerRuntime.Detach();
                    inactiveTicks = MeasurePath(maximumDepth, pathIterations);
                }

                samples = profiler.SampleCount;
                overhead[run] = activeTicks - inactiveTicks;
                allSlopes[slopeIndex++] = overhead[run] / (double)samples;
            }

            Array.Sort(overhead);
            points[collectedDepth] = new CalibrationPoint(samples, Median(overhead));
        }

        Array.Sort(allSlopes, 0, slopeIndex);
        double slope = Median(allSlopes.AsSpan(0, slopeIndex));
        double rSquared = CalculateRSquared(points, slope);
        return new ProfilerOverheadCalibration(
            Math.Max(0, slope),
            maximumDepth,
            warmupRuns,
            measurementRuns,
            pathIterations,
            rSquared,
            minimumRSquared);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunPath(int depth, int iterations)
    {
        int result = 0;
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            result ^= RunLevel(0, depth, iteration);
        }

        return result;
    }

    private static long MeasurePath(int depth, int iterations)
    {
        long start = Stopwatch.GetTimestamp();
        s_sink ^= RunPath(depth, iterations);
        return Stopwatch.GetTimestamp() - start;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunLevel(int level, int maximumDepth, int value)
    {
        int methodId = CalibrationMethodBase + level;
        ProfilerRuntime.Enter(methodId);
        try
        {
            return level + 1 < maximumDepth
                ? RunLevel(level + 1, maximumDepth, value + level)
                : (value * 17) ^ level;
        }
        finally
        {
            ProfilerRuntime.Leave(methodId);
        }
    }

    private static double CalculateRSquared(
        ReadOnlySpan<CalibrationPoint> points,
        double slope)
    {
        double mean = 0;
        foreach (CalibrationPoint point in points)
        {
            mean += point.OverheadTicks;
        }

        mean /= points.Length;
        double residual = 0;
        double total = 0;
        foreach (CalibrationPoint point in points)
        {
            double predicted = slope * point.Samples;
            double residualDelta = point.OverheadTicks - predicted;
            double totalDelta = point.OverheadTicks - mean;
            residual += residualDelta * residualDelta;
            total += totalDelta * totalDelta;
        }

        return total == 0 ? 1 : Math.Clamp(1 - (residual / total), 0, 1);
    }

    private static long Median(Span<long> values)
    {
        int middle = values.Length / 2;
        return values.Length % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2
            : values[middle];
    }

    private static double Median(Span<double> values)
    {
        int middle = values.Length / 2;
        return values.Length % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2
            : values[middle];
    }

    private readonly record struct CalibrationPoint(long Samples, long OverheadTicks);
}
