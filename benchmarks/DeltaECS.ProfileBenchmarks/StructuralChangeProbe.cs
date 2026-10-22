extern alias baselineAdapter;
extern alias candidateAdapter;

namespace DeltaECS.ProfileBenchmarks;

using System.Diagnostics;
using System.Runtime.InteropServices;

internal static class StructuralChangeProbe
{
    internal static void Run()
    {
        Console.WriteLine($"runtime={RuntimeInformation.FrameworkDescription}; arch={RuntimeInformation.ProcessArchitecture}; samples=7; min_window_ms=250");
        foreach (bool partial in new[] { false, true })
        {
            foreach (int subscribers in new[] { 0, 64 })
            {
                using var baseline = new baselineAdapter::DeltaECS.ProfileAdapter.StructuralChangeScenario(ProfileOptions.Amount, subscribers, partial);
                using var candidate = new candidateAdapter::DeltaECS.ProfileAdapter.StructuralChangeScenario(ProfileOptions.Amount, subscribers, partial);
                Func<int> previous = baseline.RoundTrip;
                Func<int> current = candidate.RoundTrip;
                long start = GC.GetAllocatedBytesForCurrentThread();
                int expected = previous();
                long baselineCold = GC.GetAllocatedBytesForCurrentThread() - start;
                start = GC.GetAllocatedBytesForCurrentThread();
                int selectedCount = partial ? (ProfileOptions.Amount + 1) / 2 : ProfileOptions.Amount;
                if (expected != selectedCount * 8 || current() != expected)
                {
                    throw new InvalidOperationException("Structural operation counts differ.");
                }

                long candidateCold = GC.GetAllocatedBytesForCurrentThread() - start;
                for (int warmup = 0; warmup < 3; warmup++)
                {
                    previous();
                    current();
                }

                _ = Measure(previous);
                _ = Measure(current);
                var before = new double[7];
                var after = new double[7];
                long beforeBytes = 0;
                long afterBytes = 0;
                for (int sample = 0; sample < before.Length; sample++)
                {
                    if ((sample & 1) == 0)
                    {
                        (before[sample], beforeBytes) = Measure(previous);
                        (after[sample], afterBytes) = Measure(current);
                    }
                    else
                    {
                        (after[sample], afterBytes) = Measure(current);
                        (before[sample], beforeBytes) = Measure(previous);
                    }
                }

                baseline.Validate();
                candidate.Validate();
                double previousMean = before.Average();
                double currentMean = after.Average();
                Console.WriteLine($"amount={ProfileOptions.Amount}; partial={partial}; subscribers={subscribers}; baseline_us={previousMean:F3}; baseline_sd={Deviation(before):F3}; candidate_us={currentMean:F3}; candidate_sd={Deviation(after):F3}; delta={100 * (currentMean / previousMean - 1):F2}%; cold_bytes={baselineCold}/{candidateCold}; warm_bytes={beforeBytes}/{afterBytes}");
            }
        }
    }

    private static (double Microseconds, long Bytes) Measure(Func<int> action)
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        int count = 0;
        do
        {
            _ = action();
            count++;
        }
        while (Stopwatch.GetElapsedTime(started).TotalMilliseconds < 250);
        double microseconds = Stopwatch.GetElapsedTime(started).TotalMicroseconds / count;
        return (microseconds, (GC.GetAllocatedBytesForCurrentThread() - allocated) / count);
    }

    private static double Deviation(double[] values)
    {
        double mean = values.Average();
        return Math.Sqrt(values.Sum(value => (value - mean) * (value - mean)) / (values.Length - 1));
    }
}
