using System.Diagnostics;
using BenchmarkDotNet.Running;

namespace Delta.ECS.Benchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "contract-smoke", StringComparison.OrdinalIgnoreCase))
        {
            ComparativeBenchmarkCatalog.Validate();
            ComparativeBenchmarkExecutionSmoke.RunAmount100();
            Console.WriteLine($"Iteration contract smoke passed: {ComparativeBenchmarkCatalog.Iteration.Length} classes, Amount=100.");
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "combined-report", StringComparison.OrdinalIgnoreCase))
        {
            var directory = args.Length > 1 ? args[1] : "artifacts/comparative";
            ComparativeBenchmarkCatalog.Validate();
            ComparativeReportBuilder.WriteManifest(directory);
            Console.WriteLine($"Wrote iteration comparative report to {directory}.");
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "many-components", StringComparison.OrdinalIgnoreCase))
        {
            var manyComponentArgs = args.Length > 0 ? args[1..] : Array.Empty<string>();
            RunTimed("many-components", () => BenchmarkSwitcher.FromTypes(ComparativeBenchmarkCatalog.ManyComponents).Run(manyComponentArgs));
            return;
        }

        if (args.Length > 0 && !string.Equals(args[0], "iteration", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Unknown benchmark route '{args[0]}'. Only 'iteration' and 'many-components' are supported.", nameof(args));

        var benchmarkArgs = args.Length > 0 ? args[1..] : Array.Empty<string>();
        RunTimed("iteration", () => BenchmarkSwitcher.FromTypes(ComparativeBenchmarkCatalog.Iteration).Run(benchmarkArgs));
    }

    private static void RunTimed(string name, Action action)
    {
        var startedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"[{startedAt:yyyy-MM-dd HH:mm:ss zzz}] Benchmark started: {name}");

        using var heartbeat = new Timer(
            _ => Console.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] Benchmark still running: {name}, elapsed {stopwatch.Elapsed:hh\\:mm\\:ss}"),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
        try { action(); }
        finally
        {
            stopwatch.Stop();
            Console.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] Benchmark finished: {name}, elapsed {stopwatch.Elapsed:hh\\:mm\\:ss}");
        }
    }
}
