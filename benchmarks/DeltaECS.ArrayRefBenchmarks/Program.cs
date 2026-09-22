using System.Globalization;
using BenchmarkDotNet.Running;

namespace Delta.ECS.ArrayRefBenchmarks;

internal static class Program
{
    private const int DefaultAmount = 200_000;
    private const int DefaultChunkSize = 4_096;
    private const int DefaultChunkCount = 8;

    internal static int Amount { get; private set; } = DefaultAmount;
    internal static int ChunkSize { get; private set; } = DefaultChunkSize;
    internal static int ChunkCount { get; private set; } = DefaultChunkCount;

    private static void Main(string[] args)
    {
        string[] benchmarkArgs = ExtractWorkloadArguments(
            args,
            out int? amount,
            out int? chunkSize,
            out int? chunkCount);
        Amount = amount ?? DefaultAmount;
        ChunkSize = chunkSize ?? DefaultChunkSize;
        ChunkCount = chunkCount ?? DefaultChunkCount;
        BenchmarkSwitcher.FromTypes(new[]
        {
            typeof(ForEachArrayReferenceBenchmarks),
            typeof(ChunkIterationBenchmarks)
        }).Run(benchmarkArgs);
    }

    private static string[] ExtractWorkloadArguments(
        string[] args,
        out int? amount,
        out int? chunkSize,
        out int? chunkCount)
    {
        var benchmarkArgs = new List<string>(args.Length);
        amount = null;
        chunkSize = null;
        chunkCount = null;

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (TryGetWorkloadOption(argument, out string option, out string? inlineValue))
            {
                string value;
                if (inlineValue is null)
                {
                    if (++index >= args.Length)
                    {
                        throw new ArgumentException($"Missing value for {option}.", nameof(args));
                    }

                    value = args[index];
                }
                else
                {
                    value = inlineValue;
                }

                switch (option)
                {
                    case "--amount":
                        amount = ParsePositiveInt(option, value);
                        break;
                    case "--chunk-size":
                        chunkSize = ParsePositiveInt(option, value);
                        break;
                    case "--chunk-count":
                        chunkCount = ParsePositiveInt(option, value);
                        break;
                }

                continue;
            }

            benchmarkArgs.Add(argument);
        }

        return benchmarkArgs.ToArray();
    }

    private static bool TryGetWorkloadOption(string argument, out string option, out string? value)
    {
        int separator = argument.IndexOf('=');
        option = (separator < 0 ? argument : argument[..separator]).ToLowerInvariant();
        value = separator < 0 ? null : argument[(separator + 1)..];
        return option.Equals("--amount", StringComparison.OrdinalIgnoreCase)
            || option.Equals("--chunk-size", StringComparison.OrdinalIgnoreCase)
            || option.Equals("--chunk-count", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParsePositiveInt(string option, string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int amount)
            || amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"{option} must be a positive integer.");
        }

        return amount;
    }
}
