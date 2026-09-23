using System.Globalization;
using BenchmarkDotNet.Running;

namespace Delta.ECS.ArrayRefBenchmarks;

internal static class Program
{
    private const string AmountEnvironmentVariable = "DELTAECS_ARRAY_REF_AMOUNT";
    private const string ChunkCapacityEnvironmentVariable = "DELTAECS_ARRAY_REF_CHUNK_CAPACITY";
    private const int DefaultAmount = 200_000;
    private const int DefaultChunkCapacity = 1_024;

    internal static int Amount { get; private set; } = ReadEnvironmentValue(AmountEnvironmentVariable, DefaultAmount);
    internal static int ChunkCapacity { get; private set; } = ReadEnvironmentValue(
        ChunkCapacityEnvironmentVariable,
        DefaultChunkCapacity);
    internal static int ChunkCount => ((Amount - 1) / ChunkCapacity) + 1;

    private static void Main(string[] args)
    {
        string[] benchmarkArgs = ExtractWorkloadArguments(
            args,
            out int? amount,
            out int? chunkCapacity);
        Amount = amount ?? DefaultAmount;
        ChunkCapacity = chunkCapacity ?? DefaultChunkCapacity;
        Environment.SetEnvironmentVariable(AmountEnvironmentVariable, Amount.ToString(CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable(
            ChunkCapacityEnvironmentVariable,
            ChunkCapacity.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine(
            $"Chunk iteration workload: {Amount:N0} elements, capacity {ChunkCapacity:N0}, {ChunkCount:N0} chunks.");
        BenchmarkSwitcher.FromTypes(new[]
        {
            typeof(ForEachArrayReferenceBenchmarks),
            typeof(ChunkIterationBenchmarks)
        }).Run(benchmarkArgs);
    }

    private static string[] ExtractWorkloadArguments(
        string[] args,
        out int? amount,
        out int? chunkCapacity)
    {
        var benchmarkArgs = new List<string>(args.Length);
        amount = null;
        chunkCapacity = null;

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
                    case "--chunk-capacity":
                        chunkCapacity = ParsePositiveInt(option, value);
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
            || option.Equals("--chunk-capacity", StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadEnvironmentValue(string name, int defaultValue) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            && value > 0
            ? value
            : defaultValue;

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
