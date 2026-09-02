using System.Diagnostics;
using System.Globalization;
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
            var manyComponentArgs = BenchmarkConfiguration.SelectAmounts(
                args[1..],
                BenchmarkConfiguration.DefaultAmounts,
                out int[] manyComponentAmounts);
            RunTimed("many-components", () => RunForAmounts(
                ComparativeBenchmarkCatalog.ManyComponents,
                manyComponentArgs,
                manyComponentAmounts));
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "parallel", StringComparison.OrdinalIgnoreCase))
        {
            var parallelArgs = ParallelBenchmarkArguments.Extract(args[1..]);
            if (parallelArgs.Any(static arg => string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)))
            {
                ParallelBenchmarkArguments.PrintUsage();
            }

            RunTimed("parallel", () =>
            {
                foreach (int amount in ParallelBenchmarkConfiguration.Amounts)
                {
                    foreach (int workerCount in ParallelBenchmarkConfiguration.WorkerCounts)
                    {
                        ParallelBenchmarkConfiguration.Amount = amount;
                        ParallelBenchmarkConfiguration.WorkerCount = workerCount;
                        BenchmarkSwitcher.FromTypes([typeof(ParallelMovement4IterationBenchmarks)]).Run(parallelArgs);
                    }
                }
            });
            return;
        }

        if (args.Length > 0 && !string.Equals(args[0], "iteration", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Unknown benchmark route '{args[0]}'. Only 'iteration', 'many-components' and 'parallel' are supported.",
                nameof(args));

        var benchmarkArgs = BenchmarkConfiguration.SelectAmounts(
            args.Length > 0 ? args[1..] : Array.Empty<string>(),
            BenchmarkConfiguration.DefaultAmounts,
            out int[] amounts);
        RunTimed("iteration", () => RunForAmounts(ComparativeBenchmarkCatalog.Iteration, benchmarkArgs, amounts));
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

    private static void RunForAmounts(Type[] benchmarkTypes, string[] benchmarkArgs, int[] amounts)
    {
        foreach (int amount in amounts)
        {
            BenchmarkConfiguration.Amount = amount;
            BenchmarkSwitcher.FromTypes(benchmarkTypes).Run(benchmarkArgs);
        }
    }
}

internal static class ParallelBenchmarkArguments
{
    internal static string[] Extract(string[] args)
    {
        var remaining = new List<string>(args.Length);
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (TryGetInlineValue(argument, "--amount", out string inlineAmount)
                || TryGetInlineValue(argument, "--amounts", out inlineAmount))
            {
                ParallelBenchmarkConfiguration.Amounts = ParseValues(inlineAmount, "amount");
                continue;
            }

            if (TryGetInlineValue(argument, "--workers", out string inlineWorkers)
                || TryGetInlineValue(argument, "--worker-counts", out inlineWorkers))
            {
                ParallelBenchmarkConfiguration.WorkerCounts = ParseValues(inlineWorkers, "workers");
                continue;
            }

            if (string.Equals(argument, "--amount", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--amounts", StringComparison.OrdinalIgnoreCase))
            {
                ParallelBenchmarkConfiguration.Amounts = ParseValues(ReadValue(args, ref index, argument), "amount");
                continue;
            }

            if (string.Equals(argument, "--workers", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--worker-counts", StringComparison.OrdinalIgnoreCase))
            {
                ParallelBenchmarkConfiguration.WorkerCounts = ParseValues(ReadValue(args, ref index, argument), "workers");
                continue;
            }

            remaining.Add(argument);
        }

        return remaining.ToArray();
    }

    internal static void PrintUsage()
    {
        Console.WriteLine("Parallel benchmark options:");
        Console.WriteLine("  --amount N             Run one entity amount");
        Console.WriteLine("  --amounts N,N,...      Run selected entity amounts");
        Console.WriteLine("  --workers N            Run with one worker count");
        Console.WriteLine("  --worker-counts N,N,... Run selected worker counts");
        Console.WriteLine("These options are consumed by the DeltaECS runner; remaining options go to BenchmarkDotNet.");
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
        {
            throw new ArgumentException($"Missing value for {option}.", nameof(args));
        }

        return args[index];
    }

    private static bool TryGetInlineValue(string argument, string option, out string value)
    {
        string prefix = option + "=";
        if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = argument[prefix.Length..];
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static int[] ParseValues(string value, string name)
    {
        string[] tokens = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            throw new ArgumentException($"{name} must contain at least one value.", nameof(value));
        }

        var values = new int[tokens.Length];
        for (int index = 0; index < tokens.Length; index++)
        {
            if (!int.TryParse(tokens[index], NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
                || parsed <= 0)
            {
                throw new ArgumentException($"{name} must contain positive integers: '{tokens[index]}'.", nameof(value));
            }

            values[index] = parsed;
        }

        return values;
    }
}
