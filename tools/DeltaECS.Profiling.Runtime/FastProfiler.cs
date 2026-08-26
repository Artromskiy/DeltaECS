using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace DeltaECS.Profiling;

/// <summary>Records primitive timing samples without strings or allocations in the hot path.</summary>
public sealed class FastProfiler
{
    private readonly Sample[] _samples;
    private readonly Frame[] _frames;
    private readonly int _maxDepth;
    private int _sampleCount;
    private int _depth;
    private long _droppedSamples;

    public FastProfiler(int maxDepth = 32, int sampleCapacity = 1_048_576)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleCapacity);
        _maxDepth = maxDepth;
        _samples = new Sample[sampleCapacity];
        _frames = new Frame[maxDepth];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Begin(int methodId)
    {
        long start = Stopwatch.GetTimestamp();
        int depth = _depth++;
        if (depth < _maxDepth)
        {
            _frames[depth] = new Frame(methodId, start);
        }

        return start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void End(int methodId, long start)
    {
        long end = Stopwatch.GetTimestamp();
        int depth = --_depth;
        if (depth < 0)
        {
            ThrowUnbalancedEnd();
            return;
        }

        if (depth >= _maxDepth)
        {
            return;
        }

        ref Frame frame = ref _frames[depth];
        long duration = end - start;
        long self = duration - frame.ChildTicks;
        if (_sampleCount < _samples.Length)
        {
            _samples[_sampleCount++] = new Sample(methodId, depth, start, end, self);
        }
        else
        {
            _droppedSamples++;
        }

        if (depth != 0)
        {
            _frames[depth - 1].ChildTicks += duration;
        }
    }

    /// <summary>Writes the report and resolves method IDs only after collection.</summary>
    public void WriteMarkdown(TextWriter writer, IReadOnlyDictionary<int, string> methodNames)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(methodNames);

        var aggregates = new Dictionary<int, Aggregate>(_sampleCount);
        for (int index = 0; index < _sampleCount; index++)
        {
            Sample sample = _samples[index];
            if (!aggregates.TryGetValue(sample.MethodId, out Aggregate aggregate))
            {
                aggregate = new Aggregate();
            }

            aggregate.Calls++;
            aggregate.InclusiveTicks += sample.EndTicks - sample.StartTicks;
            aggregate.SelfTicks += sample.SelfTicks;
            aggregates[sample.MethodId] = aggregate;
        }

        var rows = new List<FastProfileMethod>(aggregates.Count);
        foreach ((int methodId, Aggregate aggregate) in aggregates)
        {
            string name = methodNames.TryGetValue(methodId, out string? resolvedName)
                ? resolvedName
                : $"Method#{methodId}";
            rows.Add(new FastProfileMethod(
                name,
                aggregate.Calls,
                ToTimeSpan(aggregate.InclusiveTicks),
                ToTimeSpan(aggregate.SelfTicks)));
        }

        rows.Sort(static (left, right) => right.Inclusive.CompareTo(left.Inclusive));
        writer.WriteLine("# Fast call profile");
        writer.WriteLine();
        writer.WriteLine($"Max depth: `{_maxDepth}`");
        writer.WriteLine($"Samples: `{_sampleCount}`");
        writer.WriteLine($"Dropped samples: `{_droppedSamples}`");
        writer.WriteLine();
        writer.WriteLine("| Method | Calls | Inclusive | Self | Inner | Avg/call |");
        writer.WriteLine("|:--|--:|--:|--:|--:|--:|");
        foreach (FastProfileMethod row in rows)
        {
            TimeSpan inner = row.Inclusive - row.Self;
            TimeSpan average = row.Calls == 0
                ? TimeSpan.Zero
                : TimeSpan.FromTicks(row.Inclusive.Ticks / row.Calls);
            writer.WriteLine(
                $"| `{row.Name}` | {row.Calls} | {Format(row.Inclusive)} | "
                + $"{Format(row.Self)} | {Format(inner)} | {Format(average)} |");
        }
    }

    private static TimeSpan ToTimeSpan(long timestampTicks)
        => TimeSpan.FromSeconds((double)timestampTicks / Stopwatch.Frequency);

    private static string Format(TimeSpan value)
        => value.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + " ms";

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnbalancedEnd()
    {
        throw new InvalidOperationException("Fast profiler scopes are unbalanced.");
    }

    private struct Frame
    {
        internal Frame(int methodId, long startTicks)
        {
            MethodId = methodId;
            StartTicks = startTicks;
            ChildTicks = 0;
        }

        internal readonly int MethodId;
        internal readonly long StartTicks;
        internal long ChildTicks;
    }

    private readonly record struct Sample(
        int MethodId,
        int Depth,
        long StartTicks,
        long EndTicks,
        long SelfTicks);

    private struct Aggregate
    {
        internal int Calls;
        internal long InclusiveTicks;
        internal long SelfTicks;
    }
}

public readonly record struct FastProfileMethod(
    string Name,
    int Calls,
    TimeSpan Inclusive,
    TimeSpan Self);
