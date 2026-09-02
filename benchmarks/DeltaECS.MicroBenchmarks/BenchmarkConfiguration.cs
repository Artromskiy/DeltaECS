using System.Globalization;

namespace Delta.ECS.MicroBenchmarks;

internal static class MicroBenchmarkConfiguration
{
    internal static int CurrentAmount { get; set; }

    internal static int[] DefaultAmounts(Type benchmarkType) => benchmarkType.Name switch
    {
        nameof(DenseIterationMicroBenchmarks) => [100, 1_000, 10_000, 100_000, 1_000_000],
        nameof(Movement4OrderMicroBenchmarks) => [100_000, 1_000_000],
        nameof(GeneratedFunctorMovement4MicroBenchmarks) => [1_000_000],
        nameof(Movement4ApiComparisonMicroBenchmarks) => [100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000],
        _ => throw new ArgumentOutOfRangeException(nameof(benchmarkType), benchmarkType, "Unknown microbenchmark type.")
    };

    internal static string[] Extract(string[] args, out int[]? requestedAmounts)
    {
        var remaining = new List<string>(args.Length);
        requestedAmounts = null;
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (TryGetValue(argument, "--amount", out string inline)
                || TryGetValue(argument, "--amounts", out inline))
            {
                requestedAmounts = Parse(inline);
                continue;
            }

            if (string.Equals(argument, "--amount", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--amounts", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {argument}.", nameof(args));
                }
                requestedAmounts = Parse(args[index]);
                continue;
            }

            remaining.Add(argument);
        }

        return remaining.ToArray();
    }

    private static bool TryGetValue(string argument, string option, out string value)
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

    private static int[] Parse(string value)
    {
        string[] tokens = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            throw new ArgumentException("amounts must contain at least one value.", nameof(value));
        }

        var result = new int[tokens.Length];
        for (int index = 0; index < tokens.Length; index++)
        {
            if (!int.TryParse(tokens[index], NumberStyles.None, CultureInfo.InvariantCulture, out int amount)
                || amount <= 0)
            {
                throw new ArgumentException($"amounts must contain positive integers: '{tokens[index]}'.", nameof(value));
            }
            result[index] = amount;
        }

        return result;
    }
}
